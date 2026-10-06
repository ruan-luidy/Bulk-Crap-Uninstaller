/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using BulkCrapUninstaller.Properties;
using HandyControl.Themes;

namespace BulkCrapUninstaller.Wpf.Services
{
    // Theme, accent and list columns of the WPF shell. Kept next to BCUninstaller.settings so the WinForms build never sees them.
    public sealed class ShellPrefs : INotifyPropertyChanged
    {
        // Before Current: static fields initialize in order and the default theme reads this
        public static readonly string[] Accents = { "#3778bf", "#2f9e8f", "#8a63d2", "#c4537a", "#d08a2e", "#5b8f3a" };

        public static ShellPrefs Current { get; } = new();

        private ShellTheme _theme = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public bool Dark
        {
            get => _theme.Dark;
            set
            {
                if (_theme.Dark == value) return;
                _theme.Dark = value;
                Save();
            }
        }

        public string Accent
        {
            get => _theme.Accent;
            set
            {
                if (_theme.Accent == value || string.IsNullOrEmpty(value)) return;
                _theme.Accent = value;
                Save();
            }
        }

        // Columns shown on a fresh start; the rest are a right click on the header away
        public static readonly string[] DefaultColumns = { "Publisher", "Version", "Size", "Installed", "Type" };

        // List columns that are switched on, by key
        public IReadOnlyCollection<string> Columns => (IReadOnlyCollection<string>)_theme.Columns ?? DefaultColumns;

        public void SetColumn(string key, bool visible)
        {
            var set = new HashSet<string>(Columns);
            if (!(visible ? set.Add(key) : set.Remove(key))) return;
            _theme.Columns = set.OrderBy(x => x).ToList();
            Save();
        }

        public Color AccentColor =>
            ColorConverter.ConvertFromString(_theme.Accent ?? Accents[0]) is Color color ? color : Color.FromRgb(0x37, 0x78, 0xbf);

        private static string FilePath
        {
            get
            {
                var dir = string.IsNullOrEmpty(Program.ConfigFileFullname)
                    ? Program.AssemblyLocation.FullName
                    : Path.GetDirectoryName(Program.ConfigFileFullname);
                return Path.Combine(dir!, "BCUninstaller.Wpf.json");
            }
        }

        public void Load(string path = null)
        {
            try
            {
                var file = path ?? FilePath;
                if (File.Exists(file))
                    _theme = JsonSerializer.Deserialize<ShellTheme>(File.ReadAllText(file)) ?? new ShellTheme();
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"Failed to load the shell theme: " + ex);
                _theme = new ShellTheme();
            }

            Apply();
        }

        // Only the harness sets this, so its renders never touch the real file
        public bool ReadOnly { get; set; }

        private void Save()
        {
            Apply();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            if (ReadOnly) return;

            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(_theme));
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"Failed to save the shell theme: " + ex);
            }
        }

        // HandyControl swaps its own palette; brushes of ours that follow the theme and accent go here
        public void Apply()
        {
            var dark = _theme.Dark;
            var accent = AccentColor;
            ThemeManager.Current.ApplicationTheme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ThemeManager.Current.AccentColor = Frozen(accent);

            if (Application.Current is not { } app)
                return;

            var res = app.Resources;
            res["Workspace.BackgroundBrush"] = Frozen(dark ? Color.FromRgb(0x11, 0x11, 0x13) : Color.FromRgb(0xF3, 0xF3, 0xF5));
            res["Hover.RowBrush"] = Frozen(dark ? Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x0D, 0x00, 0x00, 0x00));
            res["Selection.RowBrush"] = Frozen(accent, dark ? 0.28 : 0.20);
            res["Accent.SoftBrush"] = Frozen(accent, dark ? 0.16 : 0.12);
            ApplyStatusColors();
        }

        // Same meaning as the WinForms row colors, as small marks instead of whole row backgrounds
        public void ApplyStatusColors()
        {
            if (Application.Current is not { } app)
                return;

            var colorblind = Settings.Default.MiscColorblind;
            var res = app.Resources;
            res["Status.VerifiedBrush"] = Frozen(Hex(colorblind ? "#f6382d" : "#3fb950"));
            res["Status.UnverifiedBrush"] = Frozen(Hex(colorblind ? "#fc8d59" : "#58a6ff"));
            res["Status.InvalidBrush"] = Frozen(Hex(colorblind ? "#5189d3" : "#8b949e"));
            res["Status.OrphanBrush"] = Frozen(Hex(colorblind ? "#91bfdb" : "#f0883e"));
            res["Status.FeatureBrush"] = Frozen(Hex(colorblind ? "#fee090" : "#a371f7"));
            res["Status.StoreBrush"] = Frozen(Hex(colorblind ? "#c9dade" : "#39c5cf"));
        }

        private static Color Hex(string value) => (Color)ColorConverter.ConvertFromString(value)!;

        private static SolidColorBrush Frozen(Color color, double opacity = 1)
        {
            var brush = new SolidColorBrush(color) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }

        private sealed class ShellTheme
        {
            public bool Dark { get; set; } = true;
            public string Accent { get; set; } = Accents[0];
            public List<string> Columns { get; set; }
        }
    }
}
