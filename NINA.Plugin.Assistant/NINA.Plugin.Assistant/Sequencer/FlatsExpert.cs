﻿using Assistant.NINAPlugin.Database;
using Assistant.NINAPlugin.Database.Schema;
using Assistant.NINAPlugin.Util;
using NINA.Core.Model.Equipment;
using NINA.Plugin.Assistant.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assistant.NINAPlugin.Sequencer {

    public class FlatsExpert {

        public static readonly double ACQUIRED_IMAGES_CUTOFF_DAYS = -45;

        private SchedulerDatabaseInteraction _database = null;

        public FlatsExpert() { }

        /// <summary>
        /// Get all needed flats across all targets that are either cadence type or target completed
        /// flats handling, as determined at the provided check time.
        /// </summary>
        /// <param name="activeProfile"></param>
        /// <param name="checkDateTime"></param>
        /// <returns></returns>
        public List<LightSession> GetNeededFlats(IProfile activeProfile, DateTime checkDateTime) {

            List<LightSession> neededFlats = new List<LightSession>();
            List<AcquiredImage> allAcquiredImages = GetAcquiredImages(activeProfile);

            // Get needed flats for cadence period flats
            List<Target> targets = GetTargetsForPeriodicFlats(activeProfile);
            TSLogger.Debug($"TS Flats: processing {targets.Count} targets as cadence type");

            foreach (Target target in targets) {
                List<AcquiredImage> targetAcquiredImages = allAcquiredImages.Where(ai => ai.TargetId == target.Id).ToList();
                List<LightSession> lightSessions = GetLightSessions(target, targetAcquiredImages);

                if (lightSessions.Count > 0) {
                    lightSessions = CullByCadencePeriod(target, lightSessions, checkDateTime);
                    if (lightSessions.Count > 0) {
                        List<FlatHistory> targetFlatHistories = GetFlatHistory(target);
                        LogFlatHistories($"flat history for {target.Name}", targetFlatHistories);
                        lightSessions = CullByFlatsHistory(target, lightSessions, targetFlatHistories);
                        neededFlats.AddRange(lightSessions);
                    }
                }
            }

            // Get needed flats for target completed flats
            targets = GetTargetsForCompletionFlats(activeProfile);
            TSLogger.Debug($"TS Flats: processing {targets.Count} targets as target completed type");

            foreach (Target target in targets) {
                List<AcquiredImage> targetAcquiredImages = allAcquiredImages.Where(ai => ai.TargetId == target.Id).ToList();
                List<LightSession> lightSessions = GetLightSessions(target, targetAcquiredImages);

                if (lightSessions.Count > 0) {
                    List<FlatHistory> targetFlatHistories = GetFlatHistory(target);
                    LogFlatHistories($"flat history for {target.Name}", targetFlatHistories);
                    lightSessions = CullByFlatsHistory(target, lightSessions, targetFlatHistories);
                    neededFlats.AddRange(lightSessions);
                }
            }

            LogLightSessions("raw needed flats", neededFlats);
            return neededFlats;
        }

        /// <summary>
        /// Aggregate the acquired images into a set of unique light sessions for the target.
        /// 
        /// The provided acquired images are assumed to have been prefiltered to only those for
        /// the provided target.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="targetAcquiredImages"></param>
        /// <returns></returns>
        public List<LightSession> GetLightSessions(Target target, List<AcquiredImage> targetAcquiredImages) {
            List<LightSession> lightSessions = new List<LightSession>();

            foreach (AcquiredImage light in targetAcquiredImages) {
                LightSession lightSession = new LightSession(light.TargetId,
                                                         GetLightSessionDate(light.AcquiredDate),
                                                         light.Metadata.SessionId,
                                                         new FlatSpec(light));
                if (!lightSessions.Contains(lightSession)) {
                    lightSessions.Add(lightSession);
                }
            }

            lightSessions.Sort();
            LogLightSessions($"Raw light sessions for {target.Name}", lightSessions);

            return lightSessions;
        }

        /// <summary>
        /// Remove light sessions that are in the 'current' cadence period for the target
        /// and therefore not yet ready to have flats taken.
        /// 
        /// The provided light sessions have been prefiltered to only those for the provided
        /// target.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="targetLightSessions"></param>
        /// <param name="checkDateTime"></param>
        /// <returns></returns>
        public List<LightSession> CullByCadencePeriod(Target target, List<LightSession> targetLightSessions, DateTime checkDateTime) {

            // If the cadence period is 1, nothing is culled: all light sessions are due flats
            if (target.Project.FlatsHandling == 1) {
                return targetLightSessions;
            }

            List<LightSession> remainingLightSessions = new List<LightSession>();
            int currentSessionId = GetCurrentSessionId(target.Project, checkDateTime);

            foreach (LightSession lightSession in targetLightSessions) {
                if (lightSession.SessionId < currentSessionId) {
                    remainingLightSessions.Add(lightSession);
                }
            }

            LogLightSessions($"after culled by cadence for {target.Name}, current sid: {currentSessionId}", remainingLightSessions);
            return remainingLightSessions;
        }

        /// <summary>
        /// Remove light sessions that are already covered by flats history (flats already taken).
        /// </summary>
        /// <param name="target"></param>
        /// <param name="targetLightSessions"></param>
        /// <param name="targetFlatHistory"></param>
        /// <returns></returns>
        public List<LightSession> CullByFlatsHistory(Target target, List<LightSession> targetLightSessions, List<FlatHistory> targetFlatHistory) {
            List<LightSession> remainingLightSessions = new List<LightSession>();

            foreach (LightSession lightSession in targetLightSessions) {
                remainingLightSessions.Add(lightSession);
                foreach (FlatHistory flatHistory in targetFlatHistory) {
                    if (LightSessionAndFlatHistoryEqual(lightSession, flatHistory)) {
                        remainingLightSessions.Remove(lightSession);
                        continue;
                    }
                }
            }

            LogLightSessions($"after culled by flats history for {target.Name}", remainingLightSessions);
            return remainingLightSessions;
        }

        /// <summary>
        /// Determine whether we need to take this flat set or not.
        /// 
        /// We never want to repeat a flat set for the same (current) target, so targetTakenFlats maintains the list of flats
        /// that have already been taken for the current target.
        /// </summary>
        /// <param name="alwaysRepeatFlatSet"></param>
        /// <param name="neededFlat"></param>
        /// <param name="targetTakenFlats"></param>
        /// <param name="allTakenFlats"></param>
        /// <returns></returns>
        public bool IsRequiredFlat(bool alwaysRepeatFlatSet, LightSession neededFlat, List<FlatSpec> targetTakenFlats, List<FlatSpec> allTakenFlats) {

            // Never repeat a flat set for the same target
            if (targetTakenFlats.Contains(neededFlat.FlatSpec)) {
                return false;
            }

            // If repeat is off and the flat has already been taken, skip
            if (!alwaysRepeatFlatSet && allTakenFlats.Contains(neededFlat.FlatSpec)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get all targets from the database where both project and target are active and the project
        /// is set for cadence-based flats handling.
        /// </summary>
        /// <param name="activeProfile"></param>
        /// <returns></returns>
        public virtual List<Target> GetTargetsForPeriodicFlats(IProfile activeProfile) {
            string profileId = activeProfile.Id.ToString();

            using (var context = GetDatabaseContext().GetContext()) {
                List<Project> activeProjects = context.GetActiveProjects(profileId);
                List<Target> targets = new List<Target>();
                foreach (Project project in activeProjects) {
                    if (project.FlatsHandling == Project.FLATS_HANDLING_OFF ||
                        project.FlatsHandling == Project.FLATS_HANDLING_TARGET_COMPLETION ||
                        project.FlatsHandling == Project.FLATS_HANDLING_IMMEDIATE) { continue; }

                    targets.AddRange(project.Targets.Where(t => t.Enabled == true));
                }

                return targets;
            }
        }

        /// <summary>
        /// Get all targets from the database where both project and target are active, the project is
        /// set for target completion flats handling, and the targets are 100% complete.
        /// </summary>
        /// <param name="activeProfile"></param>
        /// <returns></returns>
        public virtual List<Target> GetTargetsForCompletionFlats(IProfile activeProfile) {
            string profileId = activeProfile.Id.ToString();

            using (var context = GetDatabaseContext().GetContext()) {
                List<Project> activeProjects = context.GetActiveProjects(profileId);
                List<Target> targets = new List<Target>();
                foreach (Project project in activeProjects) {
                    if (project.FlatsHandling == Project.FLATS_HANDLING_TARGET_COMPLETION) {
                        targets.AddRange(project.Targets.Where(t => t.Enabled == true && t.PercentComplete >= 100));
                    }
                }

                return targets;
            }
        }

        /// <summary>
        /// Get all acquired image records for the current profile that are more recent than
        /// the cutoff date.  Note that we also filter out records where the session id is
        /// zero: those were saved before TS flats support and wouldn't be handled correctly.
        /// </summary>
        /// <param name="activeProfile"></param>
        /// <returns></returns>
        public virtual List<AcquiredImage> GetAcquiredImages(IProfile activeProfile) {
            string profileId = activeProfile.Id.ToString();
            DateTime cutoff = DateTime.Now.Date.AddDays(FlatsExpert.ACQUIRED_IMAGES_CUTOFF_DAYS);

            using (var context = GetDatabaseContext().GetContext()) {
                return context.GetAcquiredImages(profileId, cutoff).Where(ai => ai.Metadata.SessionId != 0).ToList();
            }
        }

        /// <summary>
        /// Get all flat history records for the target.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual List<FlatHistory> GetFlatHistory(Target target) {
            using (var context = GetDatabaseContext().GetContext()) {
                return context.GetFlatsHistory(target.Id);
            }
        }

        /// <summary>
        /// A light session date is just a date/time marker that groups exposures into a single 'session'.
        /// All instances are set to noon indicating that the associated session is the upcoming period of
        /// darkness (immediate dusk to following dawn).
        /// </summary>
        /// <param name="exposureDate"></param>
        /// <returns></returns>
        public DateTime GetLightSessionDate(DateTime exposureDate) {
            return (exposureDate.Hour >= 12 && exposureDate.Hour <= 23)
                ? exposureDate.Date.AddHours(12)
                : exposureDate.Date.AddDays(-1).AddHours(12);
        }

        /// <summary>
        /// Calculate the session identifier for the provided date/time, based on the project's flats
        /// handling configuration and creation date.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="checkDateTime"></param>
        /// <returns></returns>
        public int GetCurrentSessionId(Project project, DateTime checkDateTime) {

            if (project == null) {
                return 1;
            }

            int flatsHandling;
            if (project.FlatsHandling == Project.FLATS_HANDLING_OFF
                || project.FlatsHandling == Project.FLATS_HANDLING_TARGET_COMPLETION
                || project.FlatsHandling == Project.FLATS_HANDLING_IMMEDIATE) {
                flatsHandling = 1;
            }
            else {
                flatsHandling = project.FlatsHandling;
            }

            DateTime lightSessionDate = GetLightSessionDate(checkDateTime);
            DateTime createSessionDate = GetLightSessionDate(project.CreateDate);
            int daysSinceProjectCreate = (int)(lightSessionDate - createSessionDate).TotalDays;
            return (daysSinceProjectCreate / flatsHandling) + 1;
        }

        /// <summary>
        /// Format the session identifier.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public string FormatSessionIdentifier(int? sessionId) {
            int id = (sessionId != null) ? (int)sessionId : 0;
            return string.Format("{0:D4}", id);
        }

        public const char OVERLOAD_SEP = '@';

        /// <summary>
        /// Overload a target name with both name and session identifier.  This is used while flats are
        /// coming through the image pipeline so we can save name/sid in the image metadata where it
        /// follows the image on the same thread.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public string GetOverloadTargetName(string name, int sessionId) {
            return name != null ? $"{name}{OVERLOAD_SEP}{sessionId}" : $"{OVERLOAD_SEP}{sessionId}";
        }

        /// <summary>
        /// Undo target name overloading.
        /// </summary>
        /// <param name="overloadedName"></param>
        /// <returns></returns>
        public Tuple<string, string> DeOverloadTargetName(string overloadedName) {
            if (overloadedName == null) {
                TSLogger.Warning("TS Flats: overloaded target name is null");
                return new Tuple<string, string>("", "0");
            }

            int pos = overloadedName.LastIndexOf(OVERLOAD_SEP);
            if (pos == -1) {
                TSLogger.Warning($"TS Flats: overloaded target name is missing sep: {overloadedName}");
                return new Tuple<string, string>("", "0");
            }

            string name = overloadedName.Substring(0, pos);
            string sid = overloadedName.Substring(pos + 1);
            return new Tuple<string, string>(name, sid);
        }

        public Target GetTarget(int projectId, int targetId) {
            using (var context = GetDatabaseContext().GetContext()) {
                return context.GetTarget(projectId, targetId);
            }
        }

        public void LogLightSessions(string header, List<LightSession> list) {
            if (list.Count == 0) {
                TSLogger.Debug($"TS Flats: {header} - empty");
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (LightSession lightSession in list) {
                sb.AppendLine(lightSession.ToString());
            }

            TSLogger.Debug($"TS Flats: {header}\n{sb}\n");
        }

        private void LogFlatHistories(string header, List<FlatHistory> list) {
            if (list.Count == 0) {
                TSLogger.Debug($"TS Flats: {header} - empty");
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (FlatHistory fh in list) {
                sb.AppendLine(fh.ToString());
            }

            TSLogger.Debug($"TS Flats: {header}\n{sb}\n");
        }

        private bool LightSessionAndFlatHistoryEqual(LightSession lightSession, FlatHistory flatHistory) {
            return lightSession.SessionDate == flatHistory.LightSessionDate
                && lightSession.SessionId == flatHistory.LightSessionId
                && lightSession.FlatSpec.Equals(new FlatSpec(flatHistory));
        }

        private SchedulerDatabaseInteraction GetDatabaseContext() {
            if (_database == null) {
                _database = new SchedulerDatabaseInteraction();
            }

            return _database;
        }
    }

    public class FlatSpec : IEquatable<FlatSpec> {

        public string FilterName { get; private set; }
        public int Gain { get; private set; }
        public int Offset { get; private set; }
        public BinningMode BinningMode { get; private set; }
        public int ReadoutMode { get; private set; }
        public double Rotation { get; private set; }
        public double ROI { get; private set; }
        public string Key { get; private set; }

        public FlatSpec(string filterName, int gain, int offset, BinningMode binning, int readoutMode, double rotation, double roi) {
            FilterName = filterName;
            Gain = gain;
            Offset = offset;
            BinningMode = binning;
            ReadoutMode = readoutMode;
            Rotation = rotation;
            ROI = roi;
            Key = GetKey();
        }

        public FlatSpec(AcquiredImage exposure) {
            FilterName = exposure.FilterName;
            Gain = exposure.Metadata.Gain;
            Offset = exposure.Metadata.Offset;
            BinningMode bin;
            BinningMode.TryParse(exposure.Metadata.Binning, out bin);
            BinningMode = bin;
            ReadoutMode = exposure.Metadata.ReadoutMode;
            Rotation = exposure.Metadata.RotatorMechanicalPosition;
            ROI = exposure.Metadata.ROI;
            Key = GetKey();
        }

        public FlatSpec(FlatHistory flatHistory) {
            FilterName = flatHistory.FilterName;
            Gain = flatHistory.Gain;
            Offset = flatHistory.Offset;
            BinningMode = flatHistory.BinningMode;
            ReadoutMode = flatHistory.ReadoutMode;
            Rotation = flatHistory.Rotation;
            ROI = flatHistory.ROI;
            Key = GetKey();
        }

        private string GetKey() {
            string rotationKey = Rotation != ImageMetadata.NO_ROTATOR_ANGLE ? $"{Rotation}" : "na";
            return $"{FilterName}_{Gain}_{Offset}_{BinningMode}_{ReadoutMode}_{rotationKey}_{ROI}";
        }

        public bool Equals(FlatSpec other) {
            if (other is null) { return false; }
            if (ReferenceEquals(this, other)) { return true; }
            if (GetType() != other.GetType()) { return false; }

            return Key == other.Key;
        }

        public override string ToString() {
            string rot = Rotation != ImageMetadata.NO_ROTATOR_ANGLE ? Rotation.ToString() : "n/a";
            return $"filter:{FilterName} gain:{Gain} offset:{Offset} bin:{BinningMode} readout:{ReadoutMode} rot:{rot} roi: {ROI}";
        }
    }

    public class LightSession : IComparable, IEquatable<LightSession> {

        public int TargetId { get; private set; }
        public DateTime SessionDate { get; private set; }
        public int SessionId { get; private set; }
        public FlatSpec FlatSpec { get; private set; }

        public LightSession(int targetId, DateTime sessionDate, int sessionId, FlatSpec flatSpec) {
            TargetId = targetId;
            SessionDate = sessionDate;
            SessionId = sessionId;
            FlatSpec = flatSpec;
        }

        public override string ToString() {
            return $"{TargetId} {Utils.FormatDateTimeFull(SessionDate)} {SessionId} {FlatSpec}";
        }

        public int CompareTo(object obj) {
            LightSession lightSession = obj as LightSession;
            return (lightSession != null) ? SessionDate.CompareTo(lightSession.SessionDate) : 0;
        }

        public bool Equals(LightSession other) {
            if (other is null) { return false; }
            if (ReferenceEquals(this, other)) { return true; }
            if (GetType() != other.GetType()) { return false; }

            return TargetId == other.TargetId
                && SessionDate == other.SessionDate
                && SessionId == other.SessionId
                && FlatSpec.Equals(other.FlatSpec);
        }
    }
}