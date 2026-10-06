/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BulkCrapUninstaller.Forms;
using BulkCrapUninstaller.Properties;
using BulkCrapUninstaller.Wpf.Services;
using BulkCrapUninstaller.Wpf.Shared;

namespace BulkCrapUninstaller.Wpf.Features.Apps
{
    public partial class AppsView
    {
        // Below this the details pane folds away and the list gets the whole width
        private const double DetailsBelow = 900;

        private AppsViewModel _viewModel;
        private ContextMenu _rowMenu;
        private readonly (string Key, DataGridColumn Column, string Header)[] _columns;

        public AppsView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => Attach(DataContext as AppsViewModel);
            SizeChanged += (_, e) => DetailsColumn.Width = new GridLength(e.NewSize.Width < DetailsBelow ? 0 : 340);
            UpdateCheckColumn();

            // Everything but the name can be switched off from the header menu
            _columns = new (string, DataGridColumn, string)[]
            {
                ("Publisher", ColPublisher, "Column_Publisher"), ("Version", ColVersion, "Column_Version"),
                ("Size", ColSize, "Column_Size"), ("Installed", ColInstalled, "Column_Installed"), ("Type", ColType, "Column_Type"),
                ("Architecture", ColArch, "Column_Arch"), ("Startup", ColStartup, "Column_Startup"),
                ("Certificate", ColCertificate, "Column_Certificate"), ("Location", ColLocation, "Column_Location"),
                ("UninstallString", ColUninstall, "Column_UninstallString"), ("QuietString", ColQuiet, "Column_QuietString"),
                ("Registry", ColRegistry, "Column_Registry"), ("Website", ColWebsite, "Column_Website"),
                ("Guid", ColGuid, "Column_Guid"), ("Note", ColNote, "Column_Note"),
            };
            ApplyColumns();
            List.PreviewMouseRightButtonUp += OnHeaderRightClick;

            List.MouseDoubleClick += OnRowDoubleClick;
            List.PreviewKeyDown += OnListKeyDown;
            List.PreviewTextInput += OnListTextInput;
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    e.Handled = true;
                }
            };
            SearchBox.PreviewKeyDown += (_, e) =>
            {
                // Down from the search box goes to the list, like the WinForms search did
                if (e.Key == Key.Down && List.Items.Count > 0)
                {
                    List.SelectedIndex = Math.Max(0, List.SelectedIndex);
                    FocusSelectedRow();
                    e.Handled = true;
                }
            };

            Settings.Default.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Settings.UninstallerListUseCheckboxes))
                    UpdateCheckColumn();
            };
        }

        private void Attach(AppsViewModel viewModel)
        {
            if (_viewModel != null)
                _viewModel.SelectionRequested -= Select;

            _viewModel = viewModel;
            if (viewModel == null) return;

            viewModel.SelectionRequested += Select;

            _rowMenu = (ContextMenu)Resources["RowMenu"];
            _rowMenu.DataContext = viewModel;
            _rowMenu.Opened += (_, _) => FillDynamicMenus();
            List.ContextMenu = _rowMenu;
        }

        private void ApplyColumns()
        {
            var visible = ShellPrefs.Current.Columns;
            foreach (var (key, column, _) in _columns)
                column.Visibility = visible.Contains(key) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnHeaderRightClick(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<DataGridColumnHeader>((DependencyObject)e.OriginalSource) == null)
                return;

            var menu = new ContextMenu { PlacementTarget = List, Placement = PlacementMode.MousePoint };
            menu.Items.Add(new MenuItem { Header = Tr.Get("Column_Menu"), IsEnabled = false });
            foreach (var (key, column, header) in _columns)
            {
                var item = new MenuItem
                {
                    Header = Capitalize(Tr.Get(header)),
                    IsCheckable = true,
                    IsChecked = column.Visibility == Visibility.Visible,
                    StaysOpenOnClick = true
                };
                item.Click += (_, _) =>
                {
                    ShellPrefs.Current.SetColumn(key, item.IsChecked);
                    ApplyColumns();
                };
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
            e.Handled = true;
        }

        private static string Capitalize(string header) =>
            string.IsNullOrEmpty(header) ? header : header[0] + header[1..].ToLower(System.Globalization.CultureInfo.CurrentCulture);

        private static T FindAncestor<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null && element is not T)
                element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                    : LogicalTreeHelper.GetParent(element);
            return element as T;
        }

        private void UpdateCheckColumn() =>
            CheckColumn.Visibility = Settings.Default.UninstallerListUseCheckboxes ? Visibility.Visible : Visibility.Collapsed;

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => _viewModel?.SetSelection(List.SelectedItems);

        private void Select(IReadOnlyList<AppItem> items)
        {
            List.SelectedItems.Clear();
            foreach (var item in items)
                List.SelectedItems.Add(item);
            if (items.Count > 0)
                List.ScrollIntoView(items[0]);
            List.Focus();
        }

        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            if (_rowMenu == null) return;
            _rowMenu.PlacementTarget = (UIElement)sender;
            _rowMenu.Placement = PlacementMode.Bottom;
            _rowMenu.IsOpen = true;
        }

        // Run and take ownership list the executables and folders of what is selected
        private void FillDynamicMenus()
        {
            var run = FindItem("RunMenu");
            if (run != null)
            {
                run.Items.Clear();
                foreach (var exe in _viewModel.CurrentExecutables)
                    run.Items.Add(new MenuItem { Header = exe, Command = _viewModel.RunExecutableCommand, CommandParameter = exe });
                run.IsEnabled = run.Items.Count > 0;
            }

            var ownership = FindItem("OwnershipMenu");
            if (ownership != null)
            {
                ownership.Items.Clear();
                foreach (var dir in _viewModel.SelectedFolders)
                    ownership.Items.Add(new MenuItem { Header = dir, Command = _viewModel.TakeOwnershipCommand, CommandParameter = dir });
                ownership.IsEnabled = ownership.Items.Count > 0;
            }
        }

        private MenuItem FindItem(string name) => _rowMenu.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == name);

        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null || ItemsControl.ContainerFromElement(List, (DependencyObject)e.OriginalSource) is not DataGridRow)
                return;

            switch (Settings.Default.UninstallerListDoubleClickAction)
            {
                case UninstallerListDoubleClickAction.OpenProperties:
                    _viewModel.PropertiesCommand.Execute(null);
                    break;
                case UninstallerListDoubleClickAction.Uninstall:
                    _viewModel.UninstallCommand.Execute(null);
                    break;
            }
        }

        // Same shortcuts as the WinForms list
        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null) return;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var mods = Keyboard.Modifiers;
            ICommand command = (key, mods) switch
            {
                (Key.Delete, ModifierKeys.None) => _viewModel.UninstallCommand,
                (Key.Delete, ModifierKeys.Shift) => _viewModel.QuietUninstallCommand,
                (Key.Delete, ModifierKeys.Control | ModifierKeys.Shift) => _viewModel.ManualUninstallCommand,
                (Key.Enter, ModifierKeys.Alt) => _viewModel.PropertiesCommand,
                (Key.C, ModifierKeys.Control) => _viewModel.CopyFullInformationCommand,
                (Key.B, ModifierKeys.Control) => _viewModel.CreateRegistryBackupCommand,
                (Key.R, ModifierKeys.Control) => _viewModel.OpenInRegeditCommand,
                (Key.Apps, _) => null,
                _ => null
            };

            if (key == Key.Apps && _rowMenu != null && List.SelectedItems.Count > 0)
            {
                _rowMenu.PlacementTarget = List;
                _rowMenu.Placement = PlacementMode.Center;
                _rowMenu.IsOpen = true;
                e.Handled = true;
                return;
            }

            if (command == null) return;
            if (command.CanExecute(null)) command.Execute(null);
            e.Handled = true;
        }

        // Typing a letter jumps to the first entry starting with it
        private void OnListTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_viewModel == null || string.IsNullOrWhiteSpace(e.Text) || char.IsControl(e.Text[0])) return;

            var target = _viewModel.Visible.FirstOrDefault(x => x.Name?.StartsWith(e.Text, StringComparison.CurrentCultureIgnoreCase) == true);
            if (target == null) return;

            List.SelectedItem = target;
            List.ScrollIntoView(target);
            FocusSelectedRow();
            e.Handled = true;
        }

        private void FocusSelectedRow()
        {
            List.UpdateLayout();
            if (List.ItemContainerGenerator.ContainerFromIndex(List.SelectedIndex) is DataGridRow row)
                row.Focus();
            else
                List.Focus();
        }
    }
}
