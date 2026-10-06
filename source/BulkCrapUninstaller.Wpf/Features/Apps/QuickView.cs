/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using BulkCrapUninstaller.Properties;
using BulkCrapUninstaller.Wpf.Shared;
using BulkCrapUninstaller.Wpf.Shared.Icons;
using MahApps.Metro.IconPacks;
using UninstallTools;
using UninstallTools.Lists;

namespace BulkCrapUninstaller.Wpf.Features.Apps
{
    // Which kinds of entries are listed; the same switches as the "Show" options of the WinForms sidebar
    public readonly record struct ListFlags(
        bool HideMicrosoft, bool StoreApps, bool WinFeatures, bool Orphans,
        bool Protected, bool SystemComponents, bool Updates, bool Tweaks)
    {
        public static ListFlags FromSettings()
        {
            var s = Settings.Default;
            return new ListFlags(s.FilterHideMicrosoft, s.FilterShowStoreApps, s.FilterShowWinFeatures, s.AdvancedDisplayOrphans,
                s.FilterShowProtected, s.FilterShowSystemComponents, s.FilterShowUpdates, s.FilterShowTweaks);
        }

        public void ToSettings()
        {
            var s = Settings.Default;
            s.FilterHideMicrosoft = HideMicrosoft;
            s.FilterShowStoreApps = StoreApps;
            s.FilterShowWinFeatures = WinFeatures;
            s.AdvancedDisplayOrphans = Orphans;
            s.FilterShowProtected = Protected;
            s.FilterShowSystemComponents = SystemComponents;
            s.FilterShowUpdates = Updates;
            s.FilterShowTweaks = Tweaks;
        }

        // Same rules as UninstallerListConfigurator.ListViewFilter
        public bool Allows(ApplicationUninstallerEntry entry)
        {
            if (HideMicrosoft && !string.IsNullOrEmpty(entry.Publisher) && entry.Publisher.Contains("Microsoft")) return false;
            if (!StoreApps && entry.UninstallerKind == UninstallerType.StoreApp) return false;
            if (!WinFeatures && entry.UninstallerKind == UninstallerType.WindowsFeature) return false;
            if (!Orphans && entry.IsOrphaned) return false;
            if (!Protected && entry.IsProtected) return false;
            if (!SystemComponents && entry.SystemComponent) return false;
            if (!Updates && entry.IsUpdate) return false;
            if (!Tweaks && entry.IsScriptTweak) return false;
            return true;
        }
    }

    /// <summary>
    /// The presets of the WinForms View > Filtering menu: a set of flags plus an optional condition on one property.
    /// </summary>
    public sealed class QuickView : INotifyPropertyChanged
    {
        private int _count;
        private bool _isActive;

        public QuickView(string key, PackIconPhosphorIconsKind icon, ListFlags flags, FilterCondition condition = null)
        {
            Key = key;
            Icon = PhosphorExtension.Get(icon);
            Flags = flags;
            Condition = condition;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Key { get; }
        public string Name => Tr.Get("View_" + Key);
        public string Description => Tr.Get("View_" + Key + "_Hint");
        public Geometry Icon { get; }
        public ListFlags Flags { get; }
        public FilterCondition Condition { get; }

        public int Count
        {
            get => _count;
            set
            {
                if (_count == value) return;
                _count = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }

        // Basic / Advanced / Everything pick what kinds are listed; the rest narrow the list to one kind
        public bool IsScope => Condition == null;

        public bool Matches(ApplicationUninstallerEntry entry) => Flags.Allows(entry) && (Condition == null || Condition.TestEntry(entry) == true);

        public static IReadOnlyList<QuickView> CreateAll()
        {
            static FilterCondition Is(string property) => new(true.ToString(), ComparisonMethod.Equals, property);

            var everything = new ListFlags(false, true, true, true, true, true, true, true);
            return new[]
            {
                new QuickView("Basic", PackIconPhosphorIconsKind.AppWindowBold, new ListFlags(true, true, false, false, false, false, false, false)),
                new QuickView("Advanced", PackIconPhosphorIconsKind.StackBold, new ListFlags(true, true, false, true, false, false, false, true)),
                new QuickView("Everything", PackIconPhosphorIconsKind.ListBulletsBold, everything),
                new QuickView("SystemComponents", PackIconPhosphorIconsKind.CpuBold,
                    new ListFlags(false, true, true, false, false, true, false, true), Is(nameof(ApplicationUninstallerEntry.SystemComponent))),
                new QuickView("Startup", PackIconPhosphorIconsKind.RocketLaunchBold, everything, Is(nameof(ApplicationUninstallerEntry.HasStartups))),
                new QuickView("Browsers", PackIconPhosphorIconsKind.GlobeBold, everything, Is(nameof(ApplicationUninstallerEntry.IsWebBrowser))),
                new QuickView("StoreApps", PackIconPhosphorIconsKind.StorefrontBold, everything,
                    new FilterCondition(nameof(UninstallerType.StoreApp), ComparisonMethod.Equals, nameof(ApplicationUninstallerEntry.UninstallerKind))),
                new QuickView("Updates", PackIconPhosphorIconsKind.ArrowsClockwiseBold, everything, Is(nameof(ApplicationUninstallerEntry.IsUpdate))),
                new QuickView("Orphans", PackIconPhosphorIconsKind.LinkBreakBold, everything, Is(nameof(ApplicationUninstallerEntry.IsOrphaned))),
                new QuickView("Invalid", PackIconPhosphorIconsKind.WarningBold, everything,
                    new FilterCondition(false.ToString(), ComparisonMethod.Equals, nameof(ApplicationUninstallerEntry.IsValid))),
                new QuickView("Tweaks", PackIconPhosphorIconsKind.WrenchBold, everything, Is(nameof(ApplicationUninstallerEntry.IsScriptTweak))),
            };
        }
    }
}
