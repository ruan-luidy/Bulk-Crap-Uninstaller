/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using BulkCrapUninstaller.Forms;
using BulkCrapUninstaller.Functions;
using BulkCrapUninstaller.Properties;
using BulkCrapUninstaller.Wpf.Services;
using BulkCrapUninstaller.Wpf.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klocman;

namespace BulkCrapUninstaller.Wpf.Features.Options
{
    public sealed record Choice(object Value, string Name);

    public sealed class AccentChoice : ObservableObject
    {
        public AccentChoice(string color) => Color = color;

        public string Color { get; }

        public bool IsSelected => string.Equals(ThemeService.Current.Accent, Color, StringComparison.OrdinalIgnoreCase);

        public void Changed() => OnPropertyChanged(nameof(IsSelected));
    }

    public sealed partial class OptionsViewModel : ObservableObject
    {
        // Settings scanned for the counter on the detection card
        private static readonly string[] Sources =
        {
            nameof(Settings.ScanRegistry), nameof(Settings.ScanDrives), nameof(Settings.ScanPreDefined), nameof(Settings.ScanStoreApps),
            nameof(Settings.ScanWinFeatures), nameof(Settings.ScanWinUpdates), nameof(Settings.ScanSteam), nameof(Settings.ScanChocolatey),
            nameof(Settings.ScanScoop), nameof(Settings.ScanOculus)
        };

        public OptionsViewModel()
        {
            Accents = ThemeService.Accents.Select(x => new AccentChoice(x)).ToList();
            ThemeService.Current.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(DarkTheme));
                foreach (var accent in Accents) accent.Changed();
            };

            Settings.Default.PropertyChanged += (_, e) =>
            {
                if (Sources.Contains(e.PropertyName)) OnPropertyChanged(nameof(SourcesSummary));
                if (e.PropertyName == nameof(Settings.Language)) OnPropertyChanged(nameof(LanguageChanged));
            };

            YesNoAskChoices = new[]
            {
                new Choice(YesNoAsk.Ask, Tr.Get("Choice_Ask")),
                new Choice(YesNoAsk.Yes, Tr.Get("Choice_Always")),
                new Choice(YesNoAsk.No, Tr.Get("Choice_Never")),
            };

            DoubleClickChoices = new[]
            {
                new Choice(UninstallerListDoubleClickAction.OpenProperties, Tr.Get("Choice_OpenProperties")),
                new Choice(UninstallerListDoubleClickAction.Uninstall, Tr.Get("Choice_Uninstall")),
                new Choice(UninstallerListDoubleClickAction.DoNothing, Tr.Get("Choice_DoNothing")),
            };

            Languages = new[] { new Choice(string.Empty, Tr.Get("Choice_SystemLanguage")) }
                .Concat(CultureConfigurator.SupportedLanguages
                    .OrderBy(x => x.NativeName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new Choice(x.Name, Capitalize(x.NativeName))))
                .ToList();
            _startLanguage = Settings.Default.Language;
        }

        private readonly string _startLanguage;

        public SettingsAccessor S { get; } = new();

        public IReadOnlyList<AccentChoice> Accents { get; }
        public IReadOnlyList<Choice> YesNoAskChoices { get; }
        public IReadOnlyList<Choice> DoubleClickChoices { get; }
        public IReadOnlyList<Choice> Languages { get; }

        public bool DarkTheme
        {
            get => ThemeService.Current.Dark;
            set => ThemeService.Current.Dark = value;
        }

        public string SourcesSummary => Tr.Format("Options_SourcesSummary",
            Sources.Count(x => Settings.Default[x] is true), Sources.Length);

        public bool LanguageChanged => Settings.Default.Language != _startLanguage;

        public string Version => "Bulk Crap Uninstaller " + Program.AssemblyVersion.ToString(Program.AssemblyVersion.Build != 0 ? 3 : 2)
                                 + (Program.IsInstalled ? string.Empty : " · " + Localisable.StrIsPortable);

        public string SettingsFolder => Path.GetDirectoryName(Program.ConfigFileFullname) ?? Program.AssemblyLocation.FullName;

        [RelayCommand]
        private void PickAccent(string color) => ThemeService.Current.Accent = color;

        [RelayCommand]
        private static void BrowseBackupFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = Settings.Default.BackupLeftoversDirectory };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Settings.Default.BackupLeftoversDirectory = dialog.SelectedPath;
        }

        [RelayCommand]
        private void OpenSettingsFolder() => Process.Start(new ProcessStartInfo(SettingsFolder) { UseShellExecute = true });

        [RelayCommand]
        private static void ClearCaches() => Program.ClearCaches(true);

        [RelayCommand]
        private static void ResetSettings()
        {
            if (MessageBoxes.ResetSettingsConfirmation() != MessageBoxes.PressedButton.Yes) return;

            // Same as SettingTools: the file goes away on the way out and the app comes back with defaults
            ResetRequested = true;
            EntryPoint.Restart();
        }

        [RelayCommand]
        private static void RestartNow() => EntryPoint.Restart();

        [RelayCommand]
        private static void SetupWizard()
        {
            using var wizard = new FirstStartBox(true) { StartPosition = System.Windows.Forms.FormStartPosition.CenterParent };
            wizard.ShowDialog(MessageBoxes.DefaultOwner);
        }

        public static bool ResetRequested { get; private set; }

        private static string Capitalize(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
    }
}
