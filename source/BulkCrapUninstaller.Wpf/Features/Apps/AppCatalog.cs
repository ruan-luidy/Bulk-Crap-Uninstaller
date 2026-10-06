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
                var byName = new Dictionary<string, ImageSource>();
                foreach (var item in items)
                {
                    if (token.IsCancellationRequested) return;

                    var icon = GetIcon(item.Entry, byName) ?? fallback.For(item.Entry);
                    _dispatcher.BeginInvoke(() => item.Icon = icon, DispatcherPriority.Background);
                }
            }, token);
        }

        private static ImageSource GetIcon(ApplicationUninstallerEntry entry, Dictionary<string, ImageSource> byName)
        {
            if (!string.IsNullOrEmpty(entry.DisplayName) && byName.TryGetValue(entry.DisplayName, out var known))
                return known;

            try
            {
                var icon = entry.GetIcon();
                var source = icon == null ? null : ToImageSource(icon);
                if (source != null && !string.IsNullOrEmpty(entry.DisplayName))
                    byName[entry.DisplayName] = source;
                return source;
            }
            catch
            {
                return null;
            }
        }

        internal static ImageSource ToImageSource(Icon icon)
        {
            try
            {
                // The 32px frame looks right at 20px on high DPI too
                using var sized = new Icon(icon, 32, 32);
                var source = Imaging.CreateBitmapSourceFromHIcon(sized.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
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
            private readonly ImageSource _app = ToImageSource(SystemIcons.Application);
            private readonly ImageSource _invalid = ToImageSource(SystemIcons.Exclamation);
            private readonly ImageSource _shield = ToImageSource(SystemIcons.Shield);
            private readonly ImageSource _msi;

            public FallbackIcons()
            {
                var msiexec = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
                var msiIcon = File.Exists(msiexec) ? Klocman.Tools.DrawingTools.ExtractAssociatedIcon(msiexec) : null;
                _msi = msiIcon != null ? ToImageSource(msiIcon) : _app;
            }

            public ImageSource For(ApplicationUninstallerEntry entry)
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
