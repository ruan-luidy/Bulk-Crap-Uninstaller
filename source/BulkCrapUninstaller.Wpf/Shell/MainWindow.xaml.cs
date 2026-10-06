/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using BulkCrapUninstaller.Properties;
using FormWindowState = System.Windows.Forms.FormWindowState;

namespace BulkCrapUninstaller.Wpf.Shell
{
    public partial class MainWindow
    {
        // Below this the sidebar becomes an icon rail
        private const double CompactBelow = 1040;

        // Inherited down the content (templates included), so the rail styles can just read it from themselves
        public static readonly DependencyProperty IsCompactProperty =
            DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(MainWindow),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetIsCompact(DependencyObject element) => (bool)element.GetValue(IsCompactProperty);

        public static void SetIsCompact(DependencyObject element, bool value) => element.SetValue(IsCompactProperty, value);

        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width);

            InputBindings.Add(new KeyBinding(viewModel.Apps.RefreshCommand, Key.F5, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(viewModel.ShowOptionsCommand, Key.P, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(viewModel.OpenHelpCommand, Key.F1, ModifierKeys.None));

            RestorePlacement();
        }

        public void ApplyLayout(double width)
        {
            var compact = width < CompactBelow;
            SetIsCompact(Body, compact);
            Sidebar.Width = compact ? 56 : 236;
            LibraryCard.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                Show();
                Activate();
            }
            else
            {
                Hide();
            }
        }

        private void OnMenuButtonClick(object sender, RoutedEventArgs e)
        {
            var menu = MenuButton.ContextMenu!;
            menu.PlacementTarget = MenuButton;
            menu.DataContext = DataContext;
            menu.IsOpen = true;
        }

        // Same settings the WinForms window used, so both remember the last spot
        private void RestorePlacement()
        {
            var s = Settings.Default;
            if (s.MiscFirstRun || s.WindowSize.Width < MinWidth || s.WindowSize.Height < MinHeight || s.WindowPosition.IsEmpty)
                return;

            var rect = new System.Drawing.Rectangle(s.WindowPosition, s.WindowSize);
            if (!System.Windows.Forms.Screen.AllScreens.Any(x => x.WorkingArea.IntersectsWith(rect)))
                return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s.WindowPosition.X;
            Top = s.WindowPosition.Y;
            Width = s.WindowSize.Width;
            Height = s.WindowSize.Height;
            if (s.WindowState == FormWindowState.Maximized)
                WindowState = WindowState.Maximized;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var s = Settings.Default;
            s.WindowState = WindowState == WindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (!bounds.IsEmpty)
            {
                s.WindowPosition = new System.Drawing.Point((int)bounds.Left, (int)bounds.Top);
                s.WindowSize = new System.Drawing.Size((int)bounds.Width, (int)bounds.Height);
            }

            base.OnClosing(e);
        }
    }

    // IsChecked of the nav items: Page == ConverterParameter
    public sealed class PageIs : IValueConverter
    {
        public static PageIs Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value?.ToString() == parameter?.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
