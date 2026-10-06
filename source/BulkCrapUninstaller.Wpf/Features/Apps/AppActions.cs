/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using BulkCrapUninstaller.Forms;
using BulkCrapUninstaller.Functions;
using BulkCrapUninstaller.Functions.Tools;
using BulkCrapUninstaller.Properties;
using Klocman.Extensions;
using Klocman.IO;
using Klocman.Forms;
using Klocman.Forms.Tools;
using Klocman.Native;
using Klocman.Tools;
using UninstallTools;
using UninstallTools.Dialogs;
using UninstallTools.Factory;
using UninstallTools.Startup;
using UninstallTools.Uninstaller;
using Form = System.Windows.Forms.Form;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace BulkCrapUninstaller.Wpf.Features.Apps
{
    /// <summary>
    /// What the WinForms MainWindow menus did with the selected entries, minus the menus.
    /// Every dialog shown from here is one of the existing BCU dialogs.
    /// </summary>
    internal sealed class AppActions
    {
        private readonly Form _owner;
        private readonly Func<IReadOnlyList<ApplicationUninstallerEntry>> _all;
        private readonly Action _refreshList;
        private readonly Action<IEnumerable<ApplicationUninstallerEntry>> _refreshItems;
        private readonly Settings _settings = Settings.Default;

        public AppActions(Form owner, AppUninstaller uninstaller, Func<IReadOnlyList<ApplicationUninstallerEntry>> all,
            Action refreshList, Action<IEnumerable<ApplicationUninstallerEntry>> refreshItems)
        {
            _owner = owner;
            Uninstaller = uninstaller;
            _all = all;
            _refreshList = refreshList;
            _refreshItems = refreshItems;
        }

        public AppUninstaller Uninstaller { get; }

        // ===== Uninstall =====

        public void Uninstall(IReadOnlyList<ApplicationUninstallerEntry> selected, bool quiet)
        {
            if (selected.Count == 0) return;
            Uninstaller.RunUninstall(selected, _all(), quiet);
        }

        public void ManualUninstall(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            var items = WithoutProtected(selected);
            if (items == null) return;

            if (items.Length == 0)
            {
                MessageBoxes.NoUninstallersSelectedInfo();
                return;
            }

            Uninstaller.AdvancedUninstall(items, _all());
        }

        public void Modify(IReadOnlyList<ApplicationUninstallerEntry> selected) => Uninstaller.Modify(selected);

        public void UninstallUsingMsi(IReadOnlyList<ApplicationUninstallerEntry> selected, MsiUninstallModes mode) =>
            Uninstaller.UninstallUsingMsi(mode, selected);

        public void UninstallFromDirectory() => Uninstaller.UninstallFromDirectory(_all());

        public void CleanProgramFiles() => Uninstaller.SearchForAndRemoveProgramFilesJunk(_all());

        public void UninstallFromTargetWindow(Action<bool> setVisible)
        {
            var results = TargetWindow.ShowDialog(_owner, setVisible);
            if (results == null) return;

            var apps = AppUninstaller.GetApplicationsFromDirectories(_all(), results);
            if (apps.Count == 0)
            {
                MessageBoxes.UninstallFromDirectoryNothingFound();
                return;
            }

            Uninstaller.RunUninstall(apps, _all(), true);
        }

        // Null when the user cancelled at the protected items warning
        private ApplicationUninstallerEntry[] WithoutProtected(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            var items = selected.ToArray();
            var protectedItems = items.Where(x => x.IsProtected).ToArray();

            if (!_settings.AdvancedDisableProtection && protectedItems.Any())
            {
                if (MessageBoxes.ProtectedItemsWarningQuestion(protectedItems.Select(x => x.DisplayName).ToArray()) == MessageBoxes.PressedButton.Cancel)
                    return null;

                items = items.Where(x => !x.IsProtected).ToArray();
            }

            return items;
        }

        // ===== Open =====

        public void Properties(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            if (selected.Count == 0) return;
            using var window = new PropertiesWindow();
            window.ShowPropertiesDialog(selected);
        }

        public void OpenInstallLocation(IReadOnlyList<ApplicationUninstallerEntry> selected) =>
            OpenDirectories(selected.Where(x => x.InstallLocation.IsNotEmpty()).Select(x => x.InstallLocation).ToList());

        public void OpenInstallSource(IReadOnlyList<ApplicationUninstallerEntry> selected) =>
            OpenDirectories(selected.Where(x => x.InstallSource.IsNotEmpty()).Select(x => x.InstallSource).ToList());

        public void OpenUninstallerLocation(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            var targets = selected.Where(x => x.UninstallerFullFilename.IsNotEmpty()).ToList();
            if (!MessageBoxes.OpenDirectoriesMessageBox(targets.Count)) return;

            try
            {
                foreach (var x in targets)
                {
                    if (File.Exists(x.UninstallerFullFilename))
                        WindowsTools.OpenExplorerFocusedOnObject(x.UninstallerFullFilename);
                    else
                        Process.Start(new ProcessStartInfo(x.UninstallerLocation) { UseShellExecute = true });
                }
            }
            catch (Exception e)
            {
                MessageBoxes.OpenDirectoryError(e);
            }
        }

        private static void OpenDirectories(List<string> dirs)
        {
            if (!MessageBoxes.OpenDirectoriesMessageBox(dirs.Count)) return;

            try
            {
                dirs.ForEach(x => Process.Start(new ProcessStartInfo(x) { UseShellExecute = true }));
            }
            catch (Exception e)
            {
                MessageBoxes.OpenDirectoryError(e);
            }
        }

        public void OpenWebPage(IReadOnlyList<ApplicationUninstallerEntry> selected) =>
            MainWindow.OpenUrls(selected.Select(y => y.GetAboutUri()).Where(x => x != null).ToList());

        public void OpenInRegedit(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            var target = selected.FirstOrDefault(x => x.RegKeyStillExists());
            if (target == null) return;

            try
            {
                RegistryTools.OpenRegKeyInRegedit(target.RegistryPath);
            }
            catch (IOException ex)
            {
                PremadeDialogs.GenericError(ex);
            }
        }

        public static void Run(string executable) => PremadeDialogs.StartProcessSafely(executable);

        // ===== Search online =====

        public void SearchGoogle(IReadOnlyList<ApplicationUninstallerEntry> s) => OnlineSearchTools.SearchGoogle(s);
        public void SearchAlternativeTo(IReadOnlyList<ApplicationUninstallerEntry> s) => OnlineSearchTools.SearchAlternativeTo(s);
        public void SearchSlant(IReadOnlyList<ApplicationUninstallerEntry> s) => OnlineSearchTools.SearchSlantCo(s);
        public void SearchGithub(IReadOnlyList<ApplicationUninstallerEntry> s) => OnlineSearchTools.SearchGithub(s);
        public void SearchSourceForge(IReadOnlyList<ApplicationUninstallerEntry> s) => OnlineSearchTools.SearchSourceforge(s);
        public void SearchFossHub(IReadOnlyList<ApplicationUninstallerEntry> s) => OnlineSearchTools.SearchFosshub(s);
        public void SearchFileHippo(IReadOnlyList<ApplicationUninstallerEntry> s) => OnlineSearchTools.SearchFilehippo(s);

        // ===== Copy =====

        public void CopyNames(IReadOnlyList<ApplicationUninstallerEntry> s) => ImportExport.CopyNamesToClipboard(s);
        public void CopyGuids(IReadOnlyList<ApplicationUninstallerEntry> s) => ImportExport.CopyGuidsToClipboard(s);
        public void CopyUninstallStrings(IReadOnlyList<ApplicationUninstallerEntry> s) => ImportExport.CopyUninstallStringsToClipboard(s);
        public void CopyRegistryPaths(IReadOnlyList<ApplicationUninstallerEntry> s) => ImportExport.CopyRegKeysToClipboard(s);
        public void CopyFullInformation(IReadOnlyList<ApplicationUninstallerEntry> s) => ImportExport.CopyFullInformationToClipboard(s);
        public void AdvancedCopy(IReadOnlyList<ApplicationUninstallerEntry> s) => AdvancedClipboardCopyWindow.ShowDialog(_owner, s);

        // ===== Edit =====

        public void Rename(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            if (selected.Count != 1)
            {
                MessageBoxes.CanSelectOnlyOneItemInfo();
                return;
            }

            var entry = selected[0];
            if (!_settings.AdvancedDisableProtection && entry.IsProtected)
            {
                MessageBoxes.ProtectedItemError(entry.DisplayName);
                return;
            }

            if (!entry.IsRegistered)
            {
                MessageBoxes.CantRenameUninstallerKind(entry.DisplayName, entry.UninstallerKind);
                return;
            }

            if (!StringEditBox.ShowDialog(string.Format(CultureInfo.InvariantCulture, Localisable.MainWindow_Rename_Description, entry.DisplayName),
                    Localisable.MainWindow_Rename_Title, entry.DisplayName, Buttons.ButtonOk, Buttons.ButtonCancel, out var output))
                return;

            try
            {
                if (entry.Rename(output))
                    _refreshList();
                else
                    MessageBoxes.InvalidNewEntryName();
            }
            catch (Exception exception)
            {
                PremadeDialogs.GenericError(exception);
            }
        }

        public void EditNote(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            if (selected.Count == 0) return;

            string title;
            var existing = string.Empty;
            if (selected.Count == 1)
            {
                title = selected[0].DisplayName;
                existing = selected[0].CustomNote;
            }
            else
            {
                title = $"{selected.Count} items";
                var first = selected[0].CustomNote ?? string.Empty;
                if (selected.All(x => (x.CustomNote ?? string.Empty) == first))
                    existing = first;
            }

            using var dialog = new CustomNoteDialog(title, existing);
            if (dialog.ShowDialog(_owner) != DialogResult.OK) return;

            foreach (var entry in selected)
                entry.CustomNote = dialog.NoteText;
            _refreshItems(selected);
        }

        public void DisableAutostart(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            foreach (var entry in selected.Where(x => x.StartupEntries != null))
            {
                foreach (var startup in entry.StartupEntries)
                {
                    try
                    {
                        startup.Disabled = true;
                    }
                    catch (Exception ex)
                    {
                        PremadeDialogs.GenericError(ex);
                    }
                }
            }

            _refreshItems(selected);
        }

        public void DeleteRegistryEntries(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            if (selected.Count == 0) return;

            var items = WithoutProtected(selected);
            if (items == null || items.Length == 0 || !MessageBoxes.DeleteRegKeysConfirmation(items.Select(x => x.DisplayName).ToArray()))
                return;

            foreach (var item in items)
            {
                try
                {
                    if (item.IsRegistered) RegistryTools.RemoveRegistryKey(item.RegistryPath);
                }
                catch (Exception ex)
                {
                    PremadeDialogs.GenericError(ex);
                }
            }

            _refreshList();
        }

        public void TakeOwnership(string directoryPath) =>
            PremadeDialogs.StartProcessSafely("cmd.exe", $"/c takeown /f \"{directoryPath}\" && icacls \"{directoryPath}\" /grant administrators:F && pause");

        // ===== Export =====

        public void CreateRegistryBackup(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            if (selected.Count == 0) return;

            var file = AskSaveFile(".reg", "Registry files|*.reg", Tr("Menu_RegistryBackup"));
            if (file == null) return;

            try
            {
                RegistryTools.ExportRegistry(file, selected.Select(x => x.RegistryPath));
            }
            catch (Exception ex)
            {
                MessageBoxes.ExportFailed(ex.Message, _owner);
            }
        }

        public void ExportSelected(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            if (selected.Count == 0) return;

            var file = AskSaveFile(".xml", "XML files|*.xml", Tr("Menu_ExportSelected"));
            if (file != null)
                AppUninstaller.ExportUninstallers(selected, file);
        }

        public void ExportBatchScript(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            if (selected.Count == 0) return;

            var file = AskSaveFile(".bat", "Batch file|*.bat", Tr("Menu_ExportBatch"));
            if (file == null) return;

            try
            {
                File.WriteAllLines(file, new[] { "@echo off", "echo BCU Generated batch uninstall script" }
                    .Concat(selected.Select(x => x.QuietUninstallPossible ? x.QuietUninstallString : x.UninstallString)
                        .Where(x => !string.IsNullOrEmpty(x)))
                    .Concat(new[] { "pause", "exit" }).ToArray());
            }
            catch (SystemException ex)
            {
                PremadeDialogs.GenericError(ex);
            }
        }

        public void ExportStoreAppsScript(IReadOnlyList<ApplicationUninstallerEntry> selected)
        {
            var file = AskSaveFile(".ps1", "PowerShell script|*.ps1", Tr("Menu_ExportStoreScript"));
            if (file == null) return;

            try
            {
                File.WriteAllLines(file, StoreAppFactory.ToPowerShellRemoveCommands(selected).ToArray());
            }
            catch (SystemException ex)
            {
                PremadeDialogs.GenericError(ex);
            }
        }

        private static string AskSaveFile(string extension, string filter, string title)
        {
            using var dialog = new SaveFileDialog
            {
                OverwritePrompt = true,
                AddExtension = true,
                DefaultExt = extension,
                Filter = filter,
                Title = title,
                RestoreDirectory = true,
                InitialDirectory = WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_DESKTOPDIRECTORY)
            };
            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        }

        private static string Tr(string key) => Shared.Tr.Get(key);

        // ===== Tools =====

        public IEnumerable<StartupEntryBase> OpenStartupManager() => StartupManagerWindow.ShowManagerDialog(_owner);

        public void CreateRestorePoint()
        {
            try
            {
                SystemRestore.BeginSysRestore(0, false, _owner);
            }
            catch (Exception exception)
            {
                PremadeDialogs.GenericError(exception);
            }
        }

        public static void OpenControlPanel(ControlPanelCanonicalNames applet)
        {
            try
            {
                WindowsTools.OpenControlPanelApplet(applet);
            }
            catch (Exception ex)
            {
                PremadeDialogs.GenericError(ex);
            }
        }

        public static void StartDiskCleanup() => PremadeDialogs.StartProcessSafely("cleanmgr.exe");

        public static void InstallNet35() => PremadeDialogs.StartProcessSafely(DismTools.DismFullPath, "/Online /Enable-Feature /FeatureName:NetFx3 /All");

        public static bool CanInstallNet35 => DismTools.DismIsAvailable && !WindowsTools.CheckNetFramework35Installed();

        public static void Troubleshooter() =>
            PremadeDialogs.StartProcessSafely(Path.Combine(Program.AssemblyLocation.FullName, @"Resources\MicrosoftProgram_Install_and_Uninstall.meta.diagcab"));
    }
}
