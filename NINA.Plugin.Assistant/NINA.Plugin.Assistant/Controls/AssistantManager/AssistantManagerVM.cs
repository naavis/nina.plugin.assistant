﻿using Assistant.NINAPlugin.Database;
using Assistant.NINAPlugin.Database.Schema;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Assistant.NINAPlugin.Controls.AssistantManager {

    public class AssistantManagerVM : BaseVM {

        private AssistantDatabaseInteraction database;
        private TreeDataItem activeTreeDataItem;
        private bool isEditMode = false;

        public AssistantManagerVM(IProfileService profileService) : base(profileService) {
            database = new AssistantDatabaseInteraction();

            SelectedItemChangedCommand = new RelayCommand(SelectedItemChanged);
        }

        private Visibility showProjectView = Visibility.Hidden;
        public Visibility ShowProjectView {
            get => showProjectView;
            set {
                showProjectView = value;
                RaisePropertyChanged(nameof(ShowProjectView));
            }
        }

        private ProjectViewVM projectViewVM;
        public ProjectViewVM ProjectViewVM {
            get => projectViewVM;
            set {
                projectViewVM = value;
                RaisePropertyChanged(nameof(ProjectViewVM));
            }
        }

        private Visibility showTargetView = Visibility.Hidden;
        public Visibility ShowTargetView {
            get => showTargetView;
            set {
                showTargetView = value;
                RaisePropertyChanged(nameof(ShowTargetView));
            }
        }

        private TargetViewVM targetViewVM;
        public TargetViewVM TargetViewVM {
            get => targetViewVM;
            set {
                targetViewVM = value;
                RaisePropertyChanged(nameof(TargetViewVM));
            }
        }

        private Visibility showFilterPlanView = Visibility.Hidden;
        public Visibility ShowFilterPlanView {
            get => showFilterPlanView;
            set {
                showFilterPlanView = value;
                RaisePropertyChanged(nameof(ShowFilterPlanView));
            }
        }

        private FilterPlanViewVM filterPlanViewVM;
        public FilterPlanViewVM FilterPlanViewVM {
            get => filterPlanViewVM;
            set {
                filterPlanViewVM = value;
                RaisePropertyChanged(nameof(FilterPlanViewVM));
            }
        }

        public ICommand SelectedItemChangedCommand { get; private set; }
        private void SelectedItemChanged(object obj) {
            TreeDataItem item = obj as TreeDataItem;
            if (item != null) {
                switch (item.Type) {
                    case TreeDataType.Project:
                        activeTreeDataItem = item;
                        Project project = (Project)item.Data;
                        ProjectViewVM = new ProjectViewVM(this, profileService, project);
                        ShowTargetView = Visibility.Collapsed;
                        ShowFilterPlanView = Visibility.Collapsed;
                        ShowProjectView = Visibility.Visible;
                        break;

                    case TreeDataType.Target:
                        activeTreeDataItem = item;
                        Target target = (Target)item.Data;
                        TargetViewVM = new TargetViewVM(this, profileService, target);
                        ShowProjectView = Visibility.Collapsed;
                        ShowFilterPlanView = Visibility.Collapsed;
                        ShowTargetView = Visibility.Visible;
                        break;

                    case TreeDataType.FilterPlan:
                        activeTreeDataItem = item;
                        FilterPlan filterPlan = (FilterPlan)item.Data;
                        FilterPlanViewVM = new FilterPlanViewVM(this, profileService, filterPlan);
                        ShowProjectView = Visibility.Collapsed;
                        ShowTargetView = Visibility.Collapsed;
                        ShowFilterPlanView = Visibility.Visible;
                        break;

                    default:
                        activeTreeDataItem = null;
                        ShowProjectView = Visibility.Collapsed;
                        ShowTargetView = Visibility.Collapsed;
                        ShowFilterPlanView = Visibility.Collapsed;
                        break;
                }
            }
        }

        List<TreeDataItem> rootProjectsList;
        public List<TreeDataItem> RootProjectsList {
            get {
                rootProjectsList = LoadProjectsTree();
                return rootProjectsList;
            }
            set {
                rootProjectsList = value;
                RaisePropertyChanged(nameof(RootProjectsList));
            }
        }

        bool treeViewEabled = true;
        public bool TreeViewEabled {
            get => treeViewEabled;
            set {
                treeViewEabled = value;
                RaisePropertyChanged(nameof(TreeViewEabled));
            }
        }

        private List<TreeDataItem> LoadProjectsTree() {

            List<TreeDataItem> rootList = new List<TreeDataItem>();
            TreeDataItem profilesFolder = new TreeDataItem(TreeDataType.Folder, "Profiles");
            rootList.Add(profilesFolder);

            using (var context = database.GetContext()) {
                foreach (ProfileMeta profile in profileService.Profiles) {
                    TreeDataItem profileItem = new TreeDataItem(TreeDataType.Profile, profile.Name, profile);
                    profilesFolder.Items.Add(profileItem);

                    List<Project> projects = context.GetAllProjects(profile.Id.ToString());
                    foreach (Project project in projects) {
                        TreeDataItem projectItem = new TreeDataItem(TreeDataType.Project, project.Name, project);
                        profileItem.Items.Add(projectItem);

                        foreach (Target target in project.Targets) {
                            TreeDataItem targetItem = new TreeDataItem(TreeDataType.Target, target.Name, target);
                            projectItem.Items.Add(targetItem);

                            foreach (FilterPlan filterPlan in target.FilterPlans) {
                                TreeDataItem filterPlanItem = new TreeDataItem(TreeDataType.FilterPlan, filterPlan.FilterName, filterPlan);
                                targetItem.Items.Add(filterPlanItem);
                            }
                        }
                    }
                }
            }

            return rootList;
        }

        public void SetEditMode(bool editMode) {
            isEditMode = editMode;
            TreeViewEabled = !editMode;
        }

        public void SaveProject(Project project) {
            using (var context = new AssistantDatabaseInteraction().GetContext()) {
                if (context.SaveProject(project)) {
                    activeTreeDataItem.Data = project;
                    activeTreeDataItem.Header = project.Name;
                }
                else {
                    Notification.ShowError("Failed to save Assistant Project (see log for details)");
                }
            }
        }

        public void SaveTarget(Target target) {
            using (var context = new AssistantDatabaseInteraction().GetContext()) {
                if (context.SaveTarget(target)) {
                    activeTreeDataItem.Data = target;
                    activeTreeDataItem.Header = target.Name;
                }
                else {
                    Notification.ShowError("Failed to save Assistant Target (see log for details)");
                }
            }
        }

        // TODO: this should go away
        public void SaveFilterPlan(FilterPlan filterPlan) {
            using (var context = new AssistantDatabaseInteraction().GetContext()) {
                if (context.SaveFilterPlan(filterPlan)) {
                    activeTreeDataItem.Data = filterPlan;
                    activeTreeDataItem.Header = filterPlan.FilterName;
                }
                else {
                    Notification.ShowError("Failed to save Assistant Filter Plan (see log for details)");
                }
            }
        }

        public void CopyItem() {
            Logger.Info($"COPY: {activeTreeDataItem.Header}");
            Clipboard.SetItem(activeTreeDataItem);
        }

        public void DeleteItem() {
            Logger.Info($"DELETE: {activeTreeDataItem.Header}");
        }
    }

    public enum TreeDataType {
        Folder, Profile, Project, Target, FilterPlan
    }

    public class TreeDataItem : TreeViewItem {

        public TreeDataType Type { get; }
        public object Data { get; set; }

        public TreeDataItem(TreeDataType type, string name) : this(type, name, null) { }

        public TreeDataItem(TreeDataType type, string name, object data) {
            Type = type;
            Data = data;
            Header = name;

            if (Type != TreeDataType.Folder) {
                MouseRightButtonDown += TreeDataItem_MouseRightButtonDown;
            }
        }

        private static RelayCommand menuItemCommand = new RelayCommand(MenuItemCommandExecute, MenuItemCommandCanExecute);

        private static void MenuItemCommandExecute(object obj) {
            MenuItemContext context = obj as MenuItemContext;
            switch (context.Type) {
                case MenuItemType.New: break;
                case MenuItemType.Paste:
                    Logger.Info($"PASTE: {Clipboard.GetItem().Header}");
                    break;
            }
        }

        private static bool MenuItemCommandCanExecute(object obj) {
            MenuItemContext context = obj as MenuItemContext;
            if (context == null) {
                return false;
            }

            if (context.Type != MenuItemType.Paste) {
                return true;
            }

            switch (context.Item.Type) {
                case TreeDataType.Profile: return Clipboard.HasType(TreeDataType.Project);
                case TreeDataType.Project: return Clipboard.HasType(TreeDataType.Target);
                case TreeDataType.Target: return Clipboard.HasType(TreeDataType.FilterPlan);
                default: return false;
            }
        }

        private void TreeDataItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            ContextMenu = GetContextMenu();
            e.Handled = true;
        }

        private ContextMenu GetContextMenu() {
            ContextMenu contextMenu = new ContextMenu();

            switch (Type) {
                case TreeDataType.Profile:
                    contextMenu.Items.Add(GetMenuItem("New Project", MenuItemType.New));
                    contextMenu.Items.Add(GetMenuItem("Paste Project", MenuItemType.Paste));
                    break;
                case TreeDataType.Project:
                    contextMenu.Items.Add(GetMenuItem("New Target", MenuItemType.New));
                    contextMenu.Items.Add(GetMenuItem("Paste Target", MenuItemType.Paste));
                    break;
                case TreeDataType.Target:
                    contextMenu.Items.Add(GetMenuItem("New Filter Plan", MenuItemType.New));
                    contextMenu.Items.Add(GetMenuItem("Paste Filter Plan", MenuItemType.Paste));
                    break;
                case TreeDataType.FilterPlan:
                    break;
                default:
                    break;
            }

            return contextMenu;
        }

        private MenuItem GetMenuItem(string header, MenuItemType type) {
            MenuItem menuItem = new MenuItem();
            menuItem.Header = header;
            menuItem.Command = menuItemCommand;
            menuItem.CommandParameter = new MenuItemContext(type, this);
            return menuItem;
        }
    }

    public enum MenuItemType {
        New, Paste
    }

    public class MenuItemContext {
        public MenuItemType Type { get; set; }
        public TreeDataItem Item { get; set; }

        public MenuItemContext(MenuItemType type, TreeDataItem item) {
            Type = type;
            Item = item;
        }
    }

    public class Clipboard {

        private static readonly Clipboard Instance = new Clipboard();
        private TreeDataItem item { get; set; }

        public static bool HasType(TreeDataType type) {
            return Instance.item?.Type == type;
        }

        public static void SetItem(TreeDataItem item) {
            Instance.item = item;
        }

        public static TreeDataItem GetItem() {
            return Instance.item;
        }

        private Clipboard() { }
    }
}
