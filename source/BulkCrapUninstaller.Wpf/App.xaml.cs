/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BulkCrapUninstaller.Functions;
using BulkCrapUninstaller.Functions.Tools;
using BulkCrapUninstaller.Properties;
using BulkCrapUninstaller.Wpf.Features.Apps;
using BulkCrapUninstaller.Wpf.Features.Options;
using BulkCrapUninstaller.Wpf.Services;
using BulkCrapUninstaller.Wpf.Shell;
using Klocman.Forms;
using Klocman.Forms.Tools;

namespace BulkCrapUninstaller.Wpf
{
    public partial class App : Application
    {
        private MainViewModel _main;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;

#if DEBUG
            Settings.Default.Debug = true;
#endif
            if (Program.EnableDebug)
                Settings.Default.AdvancedSimulate = true;

            ShellPrefs.Current.Load();
            SettingsSync.Start();
            Settings.Default.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Settings.MiscColorblind))
                    ShellPrefs.Current.ApplyStatusColors();
            };

            var catalog = new AppCatalog(Dispatcher);
            var apps = new AppsViewModel(catalog);
            _main = new MainViewModel(apps, new OptionsViewModel());

            var window = new MainWindow(_main);
            MainWindow = window;

            // The old dialogs center on and marshal through this one
            var owner = new WinFormsOwner(window);
            MessageBoxes.DefaultOwner = owner;
            LoadingDialog.DefaultOwner = owner;
            PremadeDialogs.DefaultOwner = owner;
            PremadeDialogs.SendErrorAction = NBug.Exceptions.Report;

            var uninstaller = new AppUninstaller(
                () => Dispatcher.BeginInvoke(() => _ = apps.RefreshAsync()),
                locked => Dispatcher.BeginInvoke(() => _main.IsLocked = locked),
                visible => Dispatcher.BeginInvoke(() => window.SetVisible(visible)));
            catalog.UninstallLock = uninstaller.PublicUninstallLock;

            var actions = new AppActions(owner, uninstaller, () => apps.Items.Entries(), () => _ = apps.RefreshAsync(), apps.RefreshItems);
            apps.Actions = actions;
            _main.Actions = actions;
            _main.SetWindowVisible = window.SetVisible;

            UpdateGrabber.Setup();
            apps.ScanFinished += OnFirstScan;

            window.Show();
            _ = apps.RefreshAsync();
        }

        private void OnFirstScan(object sender, EventArgs e)
        {
            _main.Apps.ScanFinished -= OnFirstScan;
            Dispatcher.BeginInvoke(_main.OnFirstScanFinished, DispatcherPriority.ApplicationIdle);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _main?.Dispose();
                SystemRestore.CancelSysRestore();

                if (OptionsViewModel.ResetRequested)
                    File.Delete(Program.ConfigFileFullname);
                else
                    Settings.Default.Save();
            }
            catch (Exception ex)
            {
                // Let the app close in peace
                Console.WriteLine(ex);
            }

            base.OnExit(e);
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            PremadeDialogs.GenericError(e.Exception);
            e.Handled = true;
        }
    }
}
