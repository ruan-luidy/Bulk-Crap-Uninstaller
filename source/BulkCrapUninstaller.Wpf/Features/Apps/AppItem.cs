/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using BulkCrapUninstaller.Functions.ApplicationList;
using BulkCrapUninstaller.Properties;
using BulkCrapUninstaller.Wpf.Shared;
using Klocman.Extensions;
using Klocman.Localising;
using Klocman.Tools;
using UninstallTools;

namespace BulkCrapUninstaller.Wpf.Features.Apps
{
    public enum AppStatus
    {
        None,
        Verified,
        Unverified,
        Invalid,
        Orphan,
        Feature,
        Store
    }

    // One row of the list: the BCU entry plus the text and flags the templates bind to
    public sealed class AppItem : INotifyPropertyChanged
    {
        private ImageSource _icon;

        public AppItem(ApplicationUninstallerEntry entry)
        {
            Entry = entry;
        }

        public ApplicationUninstallerEntry Entry { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name => Entry.DisplayName;
        public string Publisher => Entry.PublisherTrimmed;
        public string Version => Entry.DisplayVersion;
        public string Comment => Entry.Comment;
        public string Note => Entry.CustomNote;
        public bool HasNote => !string.IsNullOrWhiteSpace(Entry.CustomNote);

        public long SizeKb => Entry.EstimatedSize.GetKbSize();
        public string SizeText => Entry.EstimatedSize.GetKbSize() > 0 ? Entry.EstimatedSize.ToString() : string.Empty;

        public DateTime? InstallDate => Entry.InstallDate.IsDefault() ? null : Entry.InstallDate.Date;
        public string InstallDateText => InstallDate?.ToShortDateString() ?? string.Empty;

        public string Kind => Entry.UninstallerKind.GetLocalisedName();
        public string Architecture => Entry.Is64Bit switch
        {
            MachineType.X64 => "64-bit",
            MachineType.X86 => "32-bit",
            MachineType.Unknown => string.Empty,
            _ => Entry.Is64Bit.GetLocalisedName()
        };

        public bool HasStartup => Entry.HasStartups;
        public string StartupText => Entry.HasStartups ? Tr.Get("Common_Yes") : string.Empty;
        public bool IsProtected => Entry.IsProtected;
        public bool IsSystemComponent => Entry.SystemComponent;
        public bool IsUpdate => Entry.IsUpdate;
        public bool IsStoreApp => Entry.UninstallerKind == UninstallerType.StoreApp;
        public bool IsFeature => Entry.UninstallerKind == UninstallerType.WindowsFeature;
        public bool QuietPossible => Entry.QuietUninstallPossible;
        public bool CanModify => !string.IsNullOrEmpty(Entry.ModifyPath);

        public string InstallLocation => Entry.InstallLocation;
        public string UninstallString => Entry.UninstallString;
        public string QuietUninstallString => Entry.QuietUninstallString;
        public string RegistryPath => Entry.RegistryPath;
        public string AboutUrl => Entry.AboutUrl;
        public string Guid => Entry.BundleProviderKey == System.Guid.Empty ? string.Empty : Entry.BundleProviderKey.ToString("B");

        public string Certificate => ApplicationListConstants.GetApplicationCertificateText(Entry);
        public string Integrity => ApplicationListConstants.GetApplicationIntegrityText(Entry) is string[] arr
            ? string.Join(", ", arr)
            : ApplicationListConstants.GetApplicationIntegrityText(Entry)?.ToString();

        public ImageSource Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            }
        }

        // Same priority as ApplicationListConstants.GetApplicationBackColor
        public AppStatus Status
        {
            get
            {
                var s = Settings.Default;
                if (s.AdvancedHighlightSpecial)
                {
                    if (Entry.UninstallerKind == UninstallerType.WindowsFeature) return AppStatus.Feature;
                    if (Entry.UninstallerKind == UninstallerType.StoreApp) return AppStatus.Store;
                    if (Entry.IsOrphaned) return AppStatus.Orphan;
                }

                if (!Entry.IsValid && s.AdvancedTestInvalid) return AppStatus.Invalid;

                if (s.AdvancedTestCertificates)
                {
                    var result = Entry.IsCertificateValid(true);
                    if (result.HasValue) return result.Value ? AppStatus.Verified : AppStatus.Unverified;
                }

                return AppStatus.None;
            }
        }

        public string StatusText => Status switch
        {
            AppStatus.Verified => Tr.Get("Status_Verified"),
            AppStatus.Unverified => Tr.Get("Status_Unverified"),
            AppStatus.Invalid => Tr.Get("Status_Invalid"),
            AppStatus.Orphan => Tr.Get("Status_Orphan"),
            AppStatus.Feature => Tr.Get("Status_Feature"),
            AppStatus.Store => Tr.Get("Status_Store"),
            _ => string.Empty
        };

        // Small tags in the details pane
        public IEnumerable<string> Tags
        {
            get
            {
                if (!string.IsNullOrEmpty(Architecture)) yield return Architecture;
                if (IsStoreApp) yield return Tr.Get("Tag_Store");
                if (IsFeature) yield return Tr.Get("Tag_Feature");
                if (IsUpdate) yield return Tr.Get("Tag_Update");
                if (IsSystemComponent) yield return Tr.Get("Tag_System");
                if (IsProtected) yield return Tr.Get("Tag_Protected");
                if (HasStartup) yield return Tr.Get("Tag_Startup");
                if (QuietPossible) yield return Tr.Get("Tag_Quiet");
                if (Entry.IsOrphaned) yield return Tr.Get("Tag_Orphan");
                if (!Entry.IsValid) yield return Tr.Get("Tag_Invalid");
            }
        }

        public bool Matches(Func<ApplicationUninstallerEntry, bool> predicate) => predicate(Entry);

        // After the certificate check, a rename or a note: everything may have changed
        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

        public override string ToString() => Name;
    }

    internal static class AppItemExtensions
    {
        public static ApplicationUninstallerEntry[] Entries(this IEnumerable<AppItem> items) =>
            items.Select(x => x.Entry).ToArray();
    }
}
