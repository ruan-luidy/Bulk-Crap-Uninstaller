/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Windows;
using System.Windows.Input;

namespace BulkCrapUninstaller.Wpf.Shared.Controls
{
    // Hovering a half visible dropdown row doesn't scroll the list on its own
    public static class HoverScroll
    {
        public static readonly DependencyProperty IsSuppressedProperty =
            DependencyProperty.RegisterAttached("IsSuppressed", typeof(bool), typeof(HoverScroll),
                new PropertyMetadata(false, OnIsSuppressedChanged));

        public static bool GetIsSuppressed(DependencyObject element) => (bool)element.GetValue(IsSuppressedProperty);

        public static void SetIsSuppressed(DependencyObject element, bool value) => element.SetValue(IsSuppressedProperty, value);

        private static void OnIsSuppressedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
                return;

            element.RequestBringIntoView -= OnRequestBringIntoView;
            if ((bool)e.NewValue)
                element.RequestBringIntoView += OnRequestBringIntoView;
        }

        private static void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (sender is UIElement { IsMouseOver: true } && !IsNavigatingByKeyboard())
                e.Handled = true;
        }

        private static bool IsNavigatingByKeyboard() =>
            Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down) || Keyboard.IsKeyDown(Key.PageUp) ||
            Keyboard.IsKeyDown(Key.PageDown) || Keyboard.IsKeyDown(Key.Home) || Keyboard.IsKeyDown(Key.End);
    }
}
