﻿using Assistant.NINAPlugin.Database;
using Assistant.NINAPlugin.Database.Schema;
using Assistant.NINAPlugin.Util;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyFlatDevice;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Plugin.Assistant.Shared.Utility;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Camera;
using NINA.Sequencer.SequenceItem.FilterWheel;
using NINA.Sequencer.SequenceItem.FlatDevice;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Sequencer.SequenceItem.Rotator;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assistant.NINAPlugin.Sequencer {

    [ExportMetadata("Name", "Target Scheduler Flats")]
    [ExportMetadata("Description", "Flats automation for Target Scheduler")]
    [ExportMetadata("Icon", "Scheduler.SchedulerSVG")]
    [ExportMetadata("Category", "Target Scheduler")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TargetSchedulerFlats : SequenceItem, IValidatable {

        private IProfileService profileService;
        private ICameraMediator cameraMediator;
        private IImagingMediator imagingMediator;
        private IImageSaveMediator imageSaveMediator;
        private IImageHistoryVM imageHistoryVM;
        private IFilterWheelMediator filterWheelMediator;
        private IRotatorMediator rotatorMediator;
        private IFlatDeviceMediator flatDeviceMediator;

        SchedulerDatabaseInteraction database;

        [ImportingConstructor]
        public TargetSchedulerFlats(IProfileService profileService, ICameraMediator cameraMediator, IImagingMediator imagingMediator, IImageSaveMediator imageSaveMediator, IImageHistoryVM imageHistoryVM, IFilterWheelMediator filterWheelMediator, IRotatorMediator rotatorMediator, IFlatDeviceMediator flatDeviceMediator) {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
            this.filterWheelMediator = filterWheelMediator;
            this.rotatorMediator = rotatorMediator;
            this.flatDeviceMediator = flatDeviceMediator;
        }

        public TargetSchedulerFlats(TargetSchedulerFlats cloneMe) : this(
            cloneMe.profileService,
            cloneMe.cameraMediator,
            cloneMe.imagingMediator,
            cloneMe.imageSaveMediator,
            cloneMe.imageHistoryVM,
            cloneMe.filterWheelMediator,
            cloneMe.rotatorMediator,
            cloneMe.flatDeviceMediator) {
            CopyMetaData(cloneMe);
        }

        public override void Initialize() {
            database = new SchedulerDatabaseInteraction();
        }

        public override object Clone() {
            return new TargetSchedulerFlats(this);
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            try {
                DisplayText = "Determining needed flats";
                List<LightSession> neededFlats = GetNeededFlats();
                if (neededFlats == null) {
                    DisplayText = "";
                    return;
                }

                LogTrainedFlatDetails();

                // Prep the flat device
                DisplayText = "Preparing flat device";
                await CloseCover(progress, token);
                await ToggleLight(true, progress, token);

                List<FlatSpec> takenFlats = new List<FlatSpec>();
                foreach (LightSession neededFlat in neededFlats) {
                    bool success = true;
                    if (!takenFlats.Contains(neededFlat.FlatSpec)) {
                        success = await TakeFlatSet(neededFlat.FlatSpec, progress, token);
                        if (success) {
                            takenFlats.Add(neededFlat.FlatSpec);
                        }
                    }
                    else {
                        TSLogger.Info($"TS Flats: flat already taken, skipping: {neededFlat}");
                    }

                    // Write the flat history record
                    if (success) {
                        TSLogger.Info($"TS Flats: writing flat history: {neededFlat}");
                        using (var context = database.GetContext()) {
                            context.FlatHistorySet.Add(GetFlatHistoryRecord(neededFlat));
                            context.SaveChanges();
                        }
                    }
                }

                DisplayText = "";
                Iterations = 0;
                CompletedIterations = 0;

                await ToggleLight(false, progress, token);
            }
            catch (Exception ex) {
                DisplayText = "";

                if (Utils.IsCancelException(ex)) {
                    TSLogger.Warning("TS Flats: sequence was canceled/interrupted");
                    Status = SequenceEntityStatus.CREATED;
                    token.ThrowIfCancellationRequested();
                }
                else {
                    TSLogger.Error($"Exception taking flats: {ex.Message}:\n{ex.StackTrace}");
                }

                if (ex is SequenceEntityFailedException) {
                    throw;
                }

                throw new SequenceEntityFailedException($"exception taking flats: {ex.Message}", ex);
            }

            return;
        }

        private string displayText = "";
        public string DisplayText {
            get => displayText;
            set {
                displayText = value;
                RaisePropertyChanged(nameof(DisplayText));
            }
        }

        private int iterations = 0;
        public int Iterations {
            get => iterations;
            set {
                iterations = value;
                RaisePropertyChanged(nameof(Iterations));
            }
        }

        private int completedIterations = 0;
        public int CompletedIterations {
            get => completedIterations;
            set {
                completedIterations = value;
                RaisePropertyChanged(nameof(CompletedIterations));
            }
        }

        private IList<string> issues = new List<string>();
        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public bool Validate() {
            var i = new List<string>();

            CameraInfo cameraInfo = this.cameraMediator.GetInfo();
            if (!cameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            }
            else {
                if (!cameraInfo.CanSetGain) {
                    i.Add("camera can't set gain, unusable for Target Scheduler Flats");
                }
                if (!cameraInfo.CanSetOffset) {
                    i.Add("camera can't set offset, unusable for Target Scheduler Flats");
                }
            }

            FlatDeviceInfo flatDeviceInfo = flatDeviceMediator.GetInfo();
            if (!flatDeviceInfo.Connected) {
                i.Add(Loc.Instance["LblFlatDeviceNotConnected"]);
            }
            else {
                if (!flatDeviceInfo.SupportsOnOff) {
                    i.Add(Loc.Instance["LblFlatDeviceCannotControlBrightness"]);
                }
            }

            Issues = i;
            return i.Count == 0;
        }

        private async Task<bool> TakeFlatSet(FlatSpec flatSpec, IProgress<ApplicationStatus> progress, CancellationToken token) {

            try {
                TrainedFlatExposureSetting setting = GetTrainedFlatExposureSetting(flatSpec);
                if (setting == null) {
                    TSLogger.Warning($"TS Flats: failed to find trained settings for {flatSpec}");
                    return false;
                }

                int count = profileService.ActiveProfile.FlatWizardSettings.FlatCount;
                DisplayText = $"Flat set: {flatSpec.FilterName} {setting.Time}s ({GetFlatSpecDisplay(flatSpec)})";
                Iterations = count;
                CompletedIterations = 0;

                // Set rotation angle, if applicable
                if (flatSpec.Rotation != ImageMetadata.NO_ROTATOR_ANGLE && rotatorMediator.GetInfo().Connected) {
                    TSLogger.Info($"TS Flats: setting rotation angle: {flatSpec.Rotation}");
                    MoveRotatorMechanical rotate = new MoveRotatorMechanical(rotatorMediator) { MechanicalPosition = (float)flatSpec.Rotation };
                    await rotate.Execute(progress, token);
                }

                // Set the camera readout mode
                TSLogger.Info($"TS Flats: setting readout mode: {flatSpec.ReadoutMode}");
                SetReadoutMode setReadoutMode = new SetReadoutMode(cameraMediator) { Mode = (short)flatSpec.ReadoutMode };
                await setReadoutMode.Execute(progress, token);

                // Switch filters
                TSLogger.Info($"TS Flats: switching filter: {flatSpec.FilterName}");
                SwitchFilter switchFilter = new SwitchFilter(profileService, filterWheelMediator) { Filter = Utils.LookupFilter(profileService, flatSpec.FilterName) };
                await switchFilter.Execute(progress, token);

                // Set the panel brightness
                TSLogger.Info($"TS Flats: setting panel brightness: {setting.Brightness}");
                SetBrightness setBrightness = new SetBrightness(flatDeviceMediator) { Brightness = setting.Brightness };
                await setBrightness.Execute(progress, token);

                // Take the exposures
                TakeSubframeExposure takeExposure = new TakeSubframeExposure(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM) {
                    ImageType = CaptureSequence.ImageTypes.FLAT,
                    ExposureCount = 0,
                    Gain = flatSpec.Gain,
                    Offset = flatSpec.Offset,
                    Binning = flatSpec.BinningMode,
                    ExposureTime = setting.Time,
                    ROI = flatSpec.ROI
                };

                TSLogger.Info($"TS Flats: taking {count} flats: exp:{setting.Time}, brightness: {setting.Brightness}, for {flatSpec}");

                for (int i = 0; i < count; i++) {
                    await takeExposure.Execute(progress, token);
                    CompletedIterations++;
                }

                return true;
            }
            catch (Exception ex) {
                TSLogger.Error($"Exception taking automated flat: {ex.Message}\n{ex}");
                return false;
            }
        }

        private string GetFlatSpecDisplay(FlatSpec flatSpec) {
            string rot = flatSpec.Rotation != ImageMetadata.NO_ROTATOR_ANGLE ? flatSpec.Rotation.ToString() : "n/a";
            return $"Filter: {flatSpec.FilterName} Gain: {flatSpec.Gain} Offset: {flatSpec.Offset} Binning: {flatSpec.BinningMode} Rotation: {rot} ROI: {flatSpec.ROI}";
        }

        private async Task CloseCover(IProgress<ApplicationStatus> progress, CancellationToken token) {

            if (!flatDeviceMediator.GetInfo().SupportsOpenClose) {
                return;
            }

            CoverState coverState = flatDeviceMediator.GetInfo().CoverState;

            // Last chance to skip if flat device doesn't support open/close
            if (coverState == CoverState.Unknown || coverState == CoverState.NeitherOpenNorClosed) {
                return;
            }

            if (coverState == CoverState.Closed) {
                return;
            }

            TSLogger.Info("TS Flats: closing flat device");
            await flatDeviceMediator.CloseCover(progress, token);

            coverState = flatDeviceMediator.GetInfo().CoverState;
            if (coverState != CoverState.Closed) {
                throw new SequenceEntityFailedException($"Failed to close flat cover");
            }
        }

        private async Task ToggleLight(bool onOff, IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (flatDeviceMediator.GetInfo().LightOn == onOff) {
                return;
            }

            TSLogger.Info($"TS Flats: toggling flat device light: {onOff}");
            await flatDeviceMediator.ToggleLight(onOff, progress, token);

            if (flatDeviceMediator.GetInfo().LightOn != onOff) {
                throw new SequenceEntityFailedException($"Failed to toggle flat panel light");
            }
        }

        private List<LightSession> GetNeededFlats() {
            List<LightSession> neededFlats = new List<LightSession>();
            FlatsExpert flatsExpert = new FlatsExpert();
            DateTime cutoff = DateTime.Now.Date.AddDays(FlatsExpert.ACQUIRED_IMAGES_CUTOFF_DAYS);
            string profileId = profileService.ActiveProfile.Id.ToString();

            using (var context = database.GetContext()) {
                List<Project> activeProjects = context.GetActiveProjects(profileId);
                List<AcquiredImage> acquiredImages = context.GetAcquiredImages(profileId, cutoff);

                // Handle flats taken periodically
                List<Target> targets = flatsExpert.GetTargetsForPeriodicFlats(activeProjects);
                if (targets.Count > 0) {
                    List<LightSession> lightSessions = flatsExpert.GetLightSessions(targets, acquiredImages);
                    if (lightSessions.Count > 0) {
                        List<FlatHistory> takenFlats = context.GetFlatsHistory(targets);
                        neededFlats.AddRange(flatsExpert.GetNeededPeriodicFlats(DateTime.Now, targets, lightSessions, takenFlats));
                    }
                    else {
                        TSLogger.Info("TS Flats: no light sessions for targets active for periodic flats");
                    }
                }
                else {
                    TSLogger.Info("TS Flats: no targets active for periodic flats");
                }

                // Add any flats needed for target completion targets
                targets = flatsExpert.GetCompletedTargetsForFlats(activeProjects);
                if (targets.Count > 0) {
                    List<LightSession> lightSessions = flatsExpert.GetLightSessions(targets, acquiredImages);
                    if (lightSessions.Count > 0) {
                        List<FlatHistory> takenFlats = context.GetFlatsHistory(targets);
                        neededFlats.AddRange(flatsExpert.GetNeededTargetCompletionFlats(targets, lightSessions, takenFlats));
                    }
                    else {
                        TSLogger.Info("TS Flats: no light sessions for targets active for target completed flats");
                    }
                }
                else {
                    TSLogger.Info("TS Flats: no targets active for target completed flats");
                }

                if (neededFlats.Count == 0) {
                    TSLogger.Info("TS Flats: no flats needed");
                    return null;
                }

                // Sort in increasing rotation angle order to minimize rotator movements
                neededFlats.Sort(delegate (LightSession x, LightSession y) {
                    return x.FlatSpec.Rotation.CompareTo(y.FlatSpec.Rotation);
                });

                return neededFlats;
            }
        }

        private TrainedFlatExposureSetting GetTrainedFlatExposureSetting(FlatSpec flatSpec) {

            int filterPosition = GetFilterPosition(flatSpec.FilterName);
            if (filterPosition == -1) { return null; }

            Collection<TrainedFlatExposureSetting> settings = profileService.ActiveProfile.FlatDeviceSettings.TrainedFlatExposureSettings;
            return settings.FirstOrDefault(
                setting => setting.Filter == filterPosition
                && setting.Binning.X == flatSpec.BinningMode.X
                && setting.Binning.Y == flatSpec.BinningMode.Y
                && setting.Gain == flatSpec.Gain
                && setting.Offset == flatSpec.Offset);
        }

        private short GetFilterPosition(string filterName) {
            FilterInfo info = Utils.LookupFilter(profileService, filterName);
            if (info != null) {
                return info.Position;
            }

            TSLogger.Error($"No configured filter in filter wheel for filter '{filterName}'");
            return -1;
        }

        private void LogTrainedFlatDetails() {
            Collection<TrainedFlatExposureSetting> settings = profileService.ActiveProfile.FlatDeviceSettings?.TrainedFlatExposureSettings;

            /* Write training flats for testing.
            BinningMode binning = new BinningMode(1, 1);
            settings.Add(new TrainedFlatExposureSetting() { Filter = 0, Gain = 139, Offset = 21, Binning = binning, Time = 0.78125, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 1, Gain = 139, Offset = 21, Binning = binning, Time = 4.0625, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 2, Gain = 139, Offset = 21, Binning = binning, Time = 2.875, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 3, Gain = 139, Offset = 21, Binning = binning, Time = 2.28125, Brightness = 21 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 4, Gain = 139, Offset = 21, Binning = binning, Time = 8.8125, Brightness = 30 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 5, Gain = 139, Offset = 21, Binning = binning, Time = 9.125, Brightness = 40 });
            settings.Add(new TrainedFlatExposureSetting() { Filter = 6, Gain = 139, Offset = 21, Binning = binning, Time = 6.25, Brightness = 30 });
            */

            if (settings == null || settings.Count == 0) {
                TSLogger.Debug("TS Flats: no trained flat exposure details found");
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (TrainedFlatExposureSetting trainedFlat in settings) {
                sb.AppendLine($"    filter pos: {trainedFlat.Filter} gain: {trainedFlat.Gain} offset: {trainedFlat.Offset} binning: {trainedFlat.Binning} exposure: {trainedFlat.Time} brightness: {trainedFlat.Brightness}");
            }

            TSLogger.Debug($"TS Flats: trained flat exposure details:\n{sb}");
        }

        private FlatHistory GetFlatHistoryRecord(LightSession neededFlat) {
            return new FlatHistory(neededFlat.TargetId,
                neededFlat.SessionDate,
                DateTime.Now,
                profileService.ActiveProfile.Id.ToString(),
                FlatHistory.FLAT_TYPE_PANEL,
                neededFlat.FlatSpec.FilterName,
                neededFlat.FlatSpec.Gain,
                neededFlat.FlatSpec.Offset,
                neededFlat.FlatSpec.BinningMode,
                neededFlat.FlatSpec.ReadoutMode,
                neededFlat.FlatSpec.Rotation,
                neededFlat.FlatSpec.ROI);
        }
    }
}
