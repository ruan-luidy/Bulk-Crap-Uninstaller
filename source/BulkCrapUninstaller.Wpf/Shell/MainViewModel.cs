/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BulkCrapUninstaller.Forms;
using BulkCrapUninstaller.Functions;
using BulkCrapUninstaller.Functions.Tools;
using BulkCrapUninstaller.Properties;
using BulkCrapUninstaller.Wpf.Features.Apps;
using BulkCrapUninstaller.Wpf.Features.Options;
using BulkCrapUninstaller.Wpf.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klocman.Forms.Tools;
using Klocman.Native;
using Klocman.Tools;
using UninstallTools;
using UninstallTools.Dialogs;
using UninstallTools.Lists;

namespace BulkCrapUninstaller.Wpf.Shell
{
    public enum Page
    {
        Apps,
        Options
    }

    public sealed partial class MainViewModel : ObservableObject, IDisposable
    {
        public MainViewModel(AppsViewModel apps, OptionsViewModel options)
        {
            Apps = apps;
            Options = options;
            _current = apps;
            Apps.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(AppsViewModel.ShownCount) or nameof(AppsViewModel.IsScanning))
                    OnPropertyChanged(nameof(LibrarySummary));
            };
        }

        public AppsViewModel Apps { get; }
        public OptionsViewModel Options { get; }

        [ObservableProperty]
        private object _current;

        [ObservableProperty]
        private Page _page = Page.Apps;

        // While the uninstaller works the whole window is locked, like LockApplication did
        [ObservableProperty]
        private bool _isLocked;

        public string Title { get; } = "Bulk Crap Uninstaller";

        public string VersionText
        {
            get
            {
                var version = Program.AssemblyVersion;
                var text = "v" + version.ToString(version.Build != 0 ? 3 : 2);
                if (!Program.IsInstalled) text += " · " + Localisable.StrIsPortable;
                if (Program.EnableDebug) text += " · " + Localisable.StrDebug;
                return text;
            }
        }

        public string LibrarySummary => Apps.IsScanning
            ? Tr.Get("Library_Scanning")
            : Tr.Format("Library_Summary", Apps.Items.Count, FileSizeOf(Apps.Items));

        private static string FileSizeOf(System.Collections.Generic.IEnumerable<AppItem> items) =>
            Klocman.IO.FileSize.SumFileSizes(items.Select(x => x.Entry.EstimatedSize)).ToString();

        internal AppActions Actions { get; set; }

        // Set by the window: hides itself while the uninstall progress window is up
        public Action<bool> SetWindowVisible { get; set; } = _ => { };

        [RelayCommand]
        private void ShowApps()
        {
            Page = Page.Apps;
            Current = Apps;
        }

        [RelayCommand]
        private void ShowOptions()
        {
            Page = Page.Options;
            Current = Options;
        }

        [RelayCommand]
        private void ShowView(QuickView view)
        {
            ShowApps();
            Apps.ApplyViewCommand.Execute(view);
        }

        // ===== Tools =====

        [RelayCommand]
        private void StartupManager()
        {
            var results = Actions.OpenStartupManager();
            Apps.ScanMessage = Localisable.MainWindow_Statusbar_RefreshingStartup;
            UninstallTools.Factory.ApplicationUninstallerFactory.AttachStartupEntries(Apps.Items.Select(x => x.Entry), results);
            Apps.RefreshItems(Apps.Items.Select(x => x.Entry));
        }

        [RelayCommand]
        private void UninstallFromDirectory() => Actions.UninstallFromDirectory();

        [RelayCommand]
        private void UninstallFromWindow() => Actions.UninstallFromTargetWindow(SetWindowVisible);

        [RelayCommand]
        private void CleanProgramFiles() => Actions.CleanProgramFiles();

        [RelayCommand]
        private void CreateRestorePoint()
        {
            IsLocked = true;
            try
            {
                Actions.CreateRestorePoint();
            }
            finally
            {
                IsLocked = false;
            }
        }

        [RelayCommand]
        private static void ProgramsAndFeatures() => AppActions.OpenControlPanel(ControlPanelCanonicalNames.ProgramsAndFeatures);

        [RelayCommand]
        private static void SystemRestore() => AppActions.OpenControlPanel(ControlPanelCanonicalNames.Recovery);

        [RelayCommand]
        private static void DiskCleanup() => AppActions.StartDiskCleanup();

        [RelayCommand]
        private static void InstallNet35() => AppActions.InstallNet35();

        [RelayCommand]
        private static void Troubleshooter() => AppActions.Troubleshooter();

        [RelayCommand]
        private void SetupWizard() => Options.SetupWizardCommand.Execute(null);

        // ===== Help =====

        [RelayCommand]
        private static void OpenHelp() => MessageBoxes.DisplayHelp();

        [RelayCommand]
        private static void CheckForUpdates()
        {
            if (WindowsTools.IsNetworkAvailable())
                UpdateGrabber.LookForUpdates();
            else
                MessageBoxes.NoNetworkConnected();
        }

        [RelayCommand]
        private static void About()
        {
            using var box = new AboutBox();
            box.ShowDialog(MessageBoxes.DefaultOwner);
        }

        [RelayCommand]
        private static void Feedback() => FeedbackBox.ShowFeedbackBox(MessageBoxes.DefaultOwner, false);

        [RelayCommand]
        private static void Donate() => BulkCrapUninstaller.Forms.MainWindow.OpenUrls(new[] { new Uri(Resources.DonateLink) });

        [RelayCommand]
        private void UninstallBcu() => Actions.Uninstaller.AskToSelfUninstall();

        public bool IsInstalled => Program.IsInstalled;

        // ===== Startup =====

        // What MainWindow did after the first list refresh: wizard on the first run, news after an upgrade, update check
        public void OnFirstScanFinished()
        {
            var settings = Settings.Default;
            var isFirstRun = settings.MiscFirstRun;
            if (isFirstRun)
                SetupWizard();

            if (Program.IsAfterUpgrade || isFirstRun)
                NewsPopup.ShowPopup(MessageBoxes.DefaultOwner);

            UpdateGrabber.AutoUpdate(() => Apps.FirstScanDone,
                v => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => UpdateGrabber.AskAndBeginUpdate(v)));
        }

        public void Dispose() => Apps.Dispose();
    }
}
