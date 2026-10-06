/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BulkCrapUninstaller.Forms;
using BulkCrapUninstaller.Functions.ApplicationList;
using BulkCrapUninstaller.Properties;
using Klocman.Tools;
using UninstallTools;
using UninstallTools.Factory;

namespace BulkCrapUninstaller.Wpf.Features.Apps
{
    public sealed class ScanProgress
    {
        public ScanProgress(double value, string message, string detail)
        {
            Value = value;
            Message = message;
            Detail = detail;
        }

        // 0..1, or NaN when the total is unknown
        public double Value { get; }
        public string Message { get; }
        public string Detail { get; }
    }

    /// <summary>
    /// Scans for installed applications (same factory as the WinForms list), loads their icons in the
    /// background and runs the certificate post-processing that colors the rows.
    /// </summary>
    internal sealed class AppCatalog : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly UninstallerListPostProcesser _postProcesser;
        private Dictionary<ApplicationUninstallerEntry, AppItem> _byEntry = new();
        private CancellationTokenSource _iconLoad;

        public AppCatalog(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            if (Settings.Default.CacheCertificates)
                MainWindow.CertificateCache.LoadCertificateCache();

            _postProcesser = new UninstallerListPostProcesser(OnEntriesPostProcessed, MainWindow.CertificateCache);
        }

        public object UninstallLock
        {
            set => _postProcesser.UninstallerFileLock = value;
        }

        public bool IsScanning { get; private set; }

        public async Task<IReadOnlyList<AppItem>> ScanAsync(IProgress<ScanProgress> progress, CancellationToken token)
        {
            IsScanning = true;
            _postProcesser.StopProcessingThread();
            try
            {
                var entries = await Task.Run(() => Scan(progress, token), token);
                var items = entries.Select(x => new AppItem(x)).ToList();
                _byEntry = items.ToDictionary(x => x.Entry);
                LoadIcons(items);
                return items;
            }
            finally
            {
                IsScanning = false;
            }
        }

        private static IList<ApplicationUninstallerEntry> Scan(IProgress<ScanProgress> progress, CancellationToken token)
        {
            var entries = ApplicationUninstallerFactory.GetUninstallerEntries(x =>
            {
                token.ThrowIfCancellationRequested();
                var value = x.TotalCount > 0 ? (double)x.CurrentCount / (x.TotalCount + 1) : double.NaN;
                progress.Report(new ScanProgress(value, x.Message, x.Inner?.Message ?? string.Empty));
            });

            // Don't list ourselves
            if (!string.IsNullOrEmpty(Program.InstalledRegistryKeyName))
            {
                var mine = entries.Where(x => PathTools.PathsEqual(x.RegistryKeyName, Program.InstalledRegistryKeyName)).ToList();
                foreach (var entry in mine)
                    entries.Remove(entry);
            }

            return entries;
        }

        // Certificates of what is on screen get checked first, same as the WinForms list
        public void PostProcess(IEnumerable<AppItem> visible) => _postProcesser.StartProcessingThread(visible.Select(x => x.Entry).ToList());

        public void StopPostProcessing() => _postProcesser.StopProcessingThread();

        private void OnEntriesPostProcessed(IList entries)
        {
            var items = entries.OfType<ApplicationUninstallerEntry>().ToList();
            _dispatcher.BeginInvoke(() =>
            {
                foreach (var entry in items)
                {
                    if (_byEntry.TryGetValue(entry, out var item))
                        item.Refresh();
                }
            }, DispatcherPriority.Background);
        }

        private void LoadIcons(IReadOnlyList<AppItem> items)
        {
            _iconLoad?.Cancel();
            _iconLoad = new CancellationTokenSource();
            var token = _iconLoad.Token;

            Task.Run(() =>
            {
                var fallback = new FallbackIcons();
                var byName = new Dictionary<string, IconPair>();
                foreach (var item in items)
                {
                    if (token.IsCancellationRequested) return;

                    var pair = GetIcon(item.Entry, byName) ?? fallback.For(item.Entry);
                    _dispatcher.BeginInvoke(() => item.SetIcons(pair.Small, pair.Large), DispatcherPriority.Background);
                }
            }, token);
        }

        internal sealed record IconPair(ImageSource Small, ImageSource Large)
        {
            public static IconPair From(Icon icon)
            {
                var large = ToImageSource(icon, 32);
                return large == null ? null : new IconPair(ToImageSource(icon, 16) ?? large, large);
            }
        }

        private static IconPair GetIcon(ApplicationUninstallerEntry entry, Dictionary<string, IconPair> byName)
        {
            if (!string.IsNullOrEmpty(entry.DisplayName) && byName.TryGetValue(entry.DisplayName, out var known))
                return known;

            try
            {
                var icon = entry.GetIcon();
                var pair = icon == null ? null : IconPair.From(icon);
                if (pair != null && !string.IsNullOrEmpty(entry.DisplayName))
                    byName[entry.DisplayName] = pair;
                return pair;
            }
            catch
            {
                return null;
            }
        }

        // Picks the frame closest to the size and, if the icon has no such frame, scales it once here instead of on every render
        internal static ImageSource ToImageSource(Icon icon, int size)
        {
            try
            {
                using var sized = new Icon(icon, size, size);
                var source = Imaging.CreateBitmapSourceFromHIcon(sized.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(size, size));
                source.Freeze();
                return source;
            }
            catch
            {
                return null;
            }
        }

        // Same fallbacks as UninstallerIconGetter
        private sealed class FallbackIcons
        {
            private readonly IconPair _app = IconPair.From(SystemIcons.Application);
            private readonly IconPair _invalid = IconPair.From(SystemIcons.Exclamation);
            private readonly IconPair _shield = IconPair.From(SystemIcons.Shield);
            private readonly IconPair _msi;

            public FallbackIcons()
            {
                var msiexec = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
                var msiIcon = File.Exists(msiexec) ? Klocman.Tools.DrawingTools.ExtractAssociatedIcon(msiexec) : null;
                _msi = msiIcon != null ? IconPair.From(msiIcon) ?? _app : _app;
            }

            public IconPair For(ApplicationUninstallerEntry entry)
            {
                if (entry.ParentKeyName == "OperatingSystem" || entry.IsUpdate || entry.UninstallerKind == UninstallerType.WindowsFeature)
                    return _shield;
                if (entry.UninstallerKind is UninstallerType.Msiexec or UninstallerType.SdbInst)
                    return _msi;
                return entry.IsValid ? _app : _invalid;
            }
        }

        public void Dispose()
        {
            _iconLoad?.Cancel();
            _postProcesser.Dispose();

            try
            {
                if (Settings.Default.CacheCertificates)
                    MainWindow.CertificateCache.SaveCertificateCache();
                else
                    MainWindow.CertificateCache.ClearChache();

                if (!Settings.Default.CacheAppInfo)
                    File.Delete(UninstallToolsGlobalConfig.AppInfoCachePath);
            }
            catch (SystemException e)
            {
                Console.WriteLine(@"Failed to delete cache: " + e);
            }
        }
    }
}
