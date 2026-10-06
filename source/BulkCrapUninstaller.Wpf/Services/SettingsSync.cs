/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Linq;
using BulkCrapUninstaller.Properties;
using Klocman.Extensions;
using UninstallTools;

namespace BulkCrapUninstaller.Wpf.Services
{
    // Settings that the scanner reads from UninstallToolsGlobalConfig, kept in sync like MainWindow.SetupBasicSettingBindings does
    internal static class SettingsSync
    {
        private static readonly object GroupTag = new();

        public static void Start()
        {
            var s = Settings.Default.SettingBinder;

            s.Subscribe((x, y) =>
            {
                var paths = y.NewValue.SplitNewlines(StringSplitOptions.RemoveEmptyEntries);
                UninstallToolsGlobalConfig.CustomProgramFiles = paths.Select(path => path.Trim().Trim('"').Trim()).ToArray();
            }, x => x.FoldersCustomProgramDirs, GroupTag);

            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanSteam = y.NewValue, x => x.ScanSteam, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanStoreApps = y.NewValue, x => x.ScanStoreApps, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanWinFeatures = y.NewValue, x => x.ScanWinFeatures, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanWinUpdates = y.NewValue, x => x.ScanWinUpdates, GroupTag);

            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanDrives = y.NewValue, x => x.ScanDrives, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanRegistry = y.NewValue, x => x.ScanRegistry, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanPreDefined = y.NewValue, x => x.ScanPreDefined, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanChocolatey = y.NewValue, x => x.ScanChocolatey, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanOculus = y.NewValue, x => x.ScanOculus, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.ScanScoop = y.NewValue, x => x.ScanScoop, GroupTag);

            s.Subscribe((x, y) => UninstallToolsGlobalConfig.AutoDetectCustomProgramFiles = y.NewValue, x => x.FoldersAutoDetect, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.AutoDetectScanRemovable = y.NewValue, x => x.FoldersScanRemovable, GroupTag);

            s.Subscribe((x, y) => UninstallToolsGlobalConfig.QuietAutomatization = y.NewValue, x => x.QuietAutomatization, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.QuietAutomatizationKillStuck = y.NewValue, x => x.QuietAutomatizationKillStuck, GroupTag);
            s.Subscribe((x, y) => UninstallToolsGlobalConfig.UseQuietUninstallDaemon = y.NewValue, x => x.QuietUseDaemon, GroupTag);

            s.Subscribe((x, y) => UninstallToolsGlobalConfig.EnableAppInfoCache = y.NewValue, x => x.CacheAppInfo, GroupTag);

            s.SendUpdates(GroupTag);
        }
    }
}
