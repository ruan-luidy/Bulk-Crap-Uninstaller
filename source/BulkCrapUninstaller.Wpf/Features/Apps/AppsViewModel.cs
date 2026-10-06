/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using BulkCrapUninstaller.Properties;
using BulkCrapUninstaller.Wpf.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klocman.IO;
using UninstallTools;
using UninstallTools.Lists;
using UninstallTools.Uninstaller;

namespace BulkCrapUninstaller.Wpf.Features.Apps
{
    public sealed partial class AppsViewModel : ObservableObject, IDisposable
    {
        private readonly AppCatalog _catalog;
        private readonly DispatcherTimer _searchDelay;
        private readonly FilterCondition _searchCondition = new() { FilterText = string.Empty };
        private CancellationTokenSource _scan;
        private IReadOnlyList<AppItem> _selection = Array.Empty<AppItem>();
        private ListFlags _flags;
        private bool _applyingView;

        internal AppsViewModel(AppCatalog catalog)
        {
            _catalog = catalog;
            View = CollectionViewSource.GetDefaultView(Items);
            View.Filter = o => o is AppItem item && Filter(item);
            View.SortDescriptions.Add(new SortDescription(nameof(AppItem.Name), ListSortDirection.Ascending));

            QuickViews = QuickView.CreateAll();
            Scopes = QuickViews.Where(x => x.IsScope).ToList();
            Filters = QuickViews.Where(x => !x.IsScope).ToList();
            _flags = ListFlags.FromSettings();
            MarkActiveView();

            _searchDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            _searchDelay.Tick += (_, _) =>
            {
                _searchDelay.Stop();
                _searchCondition.FilterText = Search?.Trim() ?? string.Empty;
                ApplyFilter();
            };

            Settings.Default.PropertyChanged += OnSettingChanged;
        }

        private static readonly HashSet<string> FlagSettings = new()
        {
            nameof(Settings.FilterHideMicrosoft), nameof(Settings.FilterShowStoreApps), nameof(Settings.FilterShowWinFeatures),
            nameof(Settings.AdvancedDisplayOrphans), nameof(Settings.FilterShowProtected), nameof(Settings.FilterShowSystemComponents),
            nameof(Settings.FilterShowUpdates), nameof(Settings.FilterShowTweaks)
        };

        private static readonly HashSet<string> MarkSettings = new()
        {
            nameof(Settings.AdvancedHighlightSpecial), nameof(Settings.AdvancedTestInvalid),
            nameof(Settings.AdvancedTestCertificates), nameof(Settings.MiscColorblind)
        };

        // Anything that changes what is listed or how it's marked
        private void OnSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (FlagSettings.Contains(e.PropertyName) && !_applyingView)
            {
                _flags = ListFlags.FromSettings();
                SpecialView = null;
                MarkActiveView();
                ApplyFilter();
            }
            else if (MarkSettings.Contains(e.PropertyName))
            {
                foreach (var item in Items) item.Refresh();
                if (e.PropertyName == nameof(Settings.AdvancedTestCertificates) && Settings.Default.AdvancedTestCertificates)
                    _catalog.PostProcess(Visible);
            }
        }

        public ObservableCollection<AppItem> Items { get; } = new();

        public ICollectionView View { get; }

        public IReadOnlyList<QuickView> QuickViews { get; }
        public IReadOnlyList<QuickView> Scopes { get; }
        public IReadOnlyList<QuickView> Filters { get; }

        // Set by the shell: lock and refresh go through it
        internal AppActions Actions { get; set; }

        public IEnumerable<AppItem> Visible => View.Cast<AppItem>();

        // ===== Search and views =====

        [ObservableProperty]
        private string _search = string.Empty;

        partial void OnSearchChanged(string value)
        {
            _searchDelay.Stop();
            _searchDelay.Start();
        }

        // A view with a condition ("only browsers"), shown as a chip over the list
        [ObservableProperty]
        private QuickView _specialView;

        [RelayCommand]
        private void ApplyView(QuickView view)
        {
            if (view == null) return;

            _applyingView = true;
            try
            {
                view.Flags.ToSettings();
            }
            finally
            {
                _applyingView = false;
            }

            _flags = view.Flags;
            // Picking a scope keeps the search but drops the "only X" filter
            SpecialView = view.IsScope ? null : view;
            MarkActiveView();
            ApplyFilter();
        }

        [RelayCommand]
        private void ClearSpecialView()
        {
            SpecialView = null;
            MarkActiveView();
            ApplyFilter();
        }

        [RelayCommand]
        private void ClearSearch() => Search = string.Empty;

        private void MarkActiveView()
        {
            // The scope segment follows the flags even while a filter narrows the list
            foreach (var view in QuickViews)
                view.IsActive = view.IsScope ? view.Flags == _flags : view == SpecialView;
        }

        private bool Filter(AppItem item)
        {
            var entry = item.Entry;
            if (!_flags.Allows(entry)) return false;
            if (SpecialView?.Condition != null && SpecialView.Condition.TestEntry(entry) != true) return false;
            if (string.IsNullOrEmpty(_searchCondition.FilterText)) return true;
            return _searchCondition.TestEntry(entry) == true;
        }

        private void ApplyFilter()
        {
            View.Refresh();
            UpdateStats();
            if (Settings.Default.AdvancedTestCertificates && !IsScanning)
                _catalog.PostProcess(Visible);
        }

        // ===== Stats =====

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShownCountText))]
        private int _shownCount;

        public string ShownCountText => Tr.Format("Apps_Shown", ShownCount);

        [ObservableProperty]
        private string _titleTag = string.Empty;

        [ObservableProperty]
        private string _shownSize = string.Empty;

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private string _selectedSize = string.Empty;

        [ObservableProperty]
        private AppItem _current;

        public bool HasSelection => SelectedCount > 0;
        public bool HasMultipleSelected => SelectedCount > 1;
        public bool HasItems => Items.Count > 0;
        public bool IsEmptyResult => !IsScanning && Items.Count > 0 && ShownCount == 0;

        public IReadOnlyList<AppItem> Selection => _selection;

        public string SelectionSummary => Tr.Format("Selection_Summary", SelectedCount, SelectedSize);

        private void UpdateStats()
        {
            var shown = Visible.ToList();
            ShownCount = shown.Count;
            ShownSize = FileSize.SumFileSizes(shown.Select(x => x.Entry.EstimatedSize)).ToString();
            TitleTag = ShownCountText + " · " + ShownSize;
            foreach (var view in QuickViews)
                view.Count = Items.Count(x => view.Matches(x.Entry));
            OnPropertyChanged(nameof(IsEmptyResult));
            OnPropertyChanged(nameof(HasItems));
        }

        public void SetSelection(IList selected)
        {
            _selection = selected.OfType<AppItem>().ToList();
            SelectedCount = _selection.Count;
            SelectedSize = FileSize.SumFileSizes(_selection.Select(x => x.Entry.EstimatedSize)).ToString();
            Current = _selection.Count == 1 ? _selection[0] : null;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasMultipleSelected));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(Selection));
            OnSelectionCommandsChanged();
        }

        // Raised for the view so it can select rows (select all, invert)
        public event Action<IReadOnlyList<AppItem>> SelectionRequested;

        [RelayCommand]
        private void SelectAll() => SelectionRequested?.Invoke(Visible.ToList());

        [RelayCommand]
        private void SelectNone() => SelectionRequested?.Invoke(Array.Empty<AppItem>());

        [RelayCommand]
        private void InvertSelection() => SelectionRequested?.Invoke(Visible.Except(_selection).ToList());

        // ===== Scan =====

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmptyResult))]
        private bool _isScanning;

        [ObservableProperty]
        private double _scanValue;

        [ObservableProperty]
        private bool _scanIndeterminate = true;

        [ObservableProperty]
        private string _scanMessage = string.Empty;

        [ObservableProperty]
        private string _scanDetail = string.Empty;

        public bool FirstScanDone { get; private set; }

        public event EventHandler ScanFinished;

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (IsScanning) return;

            _scan = new CancellationTokenSource();
            IsScanning = true;
            ScanMessage = Localisable.LoadingDialogTitlePopulatingList;
            ScanDetail = string.Empty;
            ScanIndeterminate = true;

            var progress = new Progress<ScanProgress>(p =>
            {
                ScanIndeterminate = double.IsNaN(p.Value);
                if (!ScanIndeterminate) ScanValue = p.Value;
                if (!string.IsNullOrEmpty(p.Message)) ScanMessage = p.Message;
                ScanDetail = p.Detail ?? string.Empty;
            });

            try
            {
                var items = await _catalog.ScanAsync(progress, _scan.Token);
                Load(items);
                FirstScanDone = true;
                ScanFinished?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                // Keep whatever was listed before
            }
            finally
            {
                IsScanning = false;
                _scan = null;
            }

            if (Settings.Default.AdvancedTestCertificates)
                _catalog.PostProcess(Visible);
        }

        [RelayCommand]
        private void CancelScan() => _scan?.Cancel();

        // Also used by the harness, which never scans the real system
        public void Load(IEnumerable<AppItem> items)
        {
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
            ApplyFilter();
        }

        public void RefreshItems(IEnumerable<ApplicationUninstallerEntry> entries)
        {
            var set = entries.ToHashSet();
            foreach (var item in Items.Where(x => set.Contains(x.Entry)))
                item.Refresh();
            ApplyFilter();
        }

        // ===== Actions on the selection =====

        private IReadOnlyList<ApplicationUninstallerEntry> Selected => _selection.Select(x => x.Entry).ToList();

        private bool AnySelected() => _selection.Count > 0;
        private bool OneSelected() => _selection.Count == 1;
        private bool CanModifySelected() => _selection.Count == 1 && _selection[0].CanModify;
        private bool CanQuietSelected() => _selection.Any(x => x.QuietPossible);

        private void OnSelectionCommandsChanged()
        {
            foreach (var command in new IRelayCommand[]
                     {
                         UninstallCommand, QuietUninstallCommand, ManualUninstallCommand, ModifyCommand, PropertiesCommand,
                         OpenInstallLocationCommand, OpenUninstallerLocationCommand, OpenInstallSourceCommand, OpenWebPageCommand,
                         OpenInRegeditCommand, RenameCommand, EditNoteCommand, DisableAutostartCommand, DeleteRegistryEntriesCommand,
                         CreateRegistryBackupCommand, CopyNamesCommand, CopyGuidsCommand, CopyUninstallStringsCommand,
                         CopyRegistryPathsCommand, CopyFullInformationCommand, AdvancedCopyCommand, SearchOnlineCommand,
                         ExportSelectedCommand, ExportBatchScriptCommand, ExportStoreAppsScriptCommand, UninstallUsingMsiCommand
                     })
                command.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void Uninstall() => Actions.Uninstall(Selected, false);

        [RelayCommand(CanExecute = nameof(CanQuietSelected))]
        private void QuietUninstall() => Actions.Uninstall(Selected, true);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void ManualUninstall() => Actions.ManualUninstall(Selected);

        [RelayCommand(CanExecute = nameof(CanModifySelected))]
        private void Modify() => Actions.Modify(Selected);

        [RelayCommand(CanExecute = nameof(OneSelected))]
        private void UninstallUsingMsi(string mode)
        {
            if (Enum.TryParse<MsiUninstallModes>(mode, out var parsed))
                Actions.UninstallUsingMsi(Selected, parsed);
        }

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void Properties() => Actions.Properties(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void OpenInstallLocation() => Actions.OpenInstallLocation(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void OpenUninstallerLocation() => Actions.OpenUninstallerLocation(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void OpenInstallSource() => Actions.OpenInstallSource(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void OpenWebPage() => Actions.OpenWebPage(Selected);

        [RelayCommand(CanExecute = nameof(OneSelected))]
        private void OpenInRegedit() => Actions.OpenInRegedit(Selected);

        [RelayCommand(CanExecute = nameof(OneSelected))]
        private void Rename() => Actions.Rename(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void EditNote() => Actions.EditNote(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void DisableAutostart() => Actions.DisableAutostart(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void DeleteRegistryEntries() => Actions.DeleteRegistryEntries(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void CreateRegistryBackup() => Actions.CreateRegistryBackup(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void CopyNames() => Actions.CopyNames(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void CopyGuids() => Actions.CopyGuids(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void CopyUninstallStrings() => Actions.CopyUninstallStrings(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void CopyRegistryPaths() => Actions.CopyRegistryPaths(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void CopyFullInformation() => Actions.CopyFullInformation(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void AdvancedCopy() => Actions.AdvancedCopy(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void SearchOnline(string site)
        {
            switch (site)
            {
                case "AlternativeTo": Actions.SearchAlternativeTo(Selected); break;
                case "Slant": Actions.SearchSlant(Selected); break;
                case "GitHub": Actions.SearchGithub(Selected); break;
                case "SourceForge": Actions.SearchSourceForge(Selected); break;
                case "FossHub": Actions.SearchFossHub(Selected); break;
                case "FileHippo": Actions.SearchFileHippo(Selected); break;
                default: Actions.SearchGoogle(Selected); break;
            }
        }

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void ExportSelected() => Actions.ExportSelected(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void ExportBatchScript() => Actions.ExportBatchScript(Selected);

        [RelayCommand(CanExecute = nameof(AnySelected))]
        private void ExportStoreAppsScript() =>
            Actions.ExportStoreAppsScript(Selected.Where(x => x.UninstallerKind == UninstallerType.StoreApp).ToList());

        [RelayCommand]
        private void RunExecutable(string path) => AppActions.Run(path);

        [RelayCommand]
        private void TakeOwnership(string path) => Actions.TakeOwnership(path);

        // Executables of the focused entry for the "Run" submenu
        public IEnumerable<string> CurrentExecutables => Current?.Entry.GetSortedExecutables() ?? Enumerable.Empty<string>();

        // Folders of the selection for "Take ownership"
        public IEnumerable<string> SelectedFolders => _selection
            .SelectMany(x => new[] { x.Entry.InstallLocation, x.Entry.UninstallerLocation })
            .Where(x => !string.IsNullOrEmpty(x))
            .DistinctBy(x => x.ToLowerInvariant())
            .OrderBy(x => x);

        public void Dispose()
        {
            Settings.Default.PropertyChanged -= OnSettingChanged;
            _scan?.Cancel();
            _catalog.Dispose();
        }
    }
}
