/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Form = System.Windows.Forms.Form;

namespace BulkCrapUninstaller.Wpf.Services
{
    /// <summary>
    /// The existing BCU dialogs (wizards, progress, junk, message boxes) want a WinForms Form as their owner,
    /// both to center on and to marshal calls with SafeInvoke. This one is never shown: it sits under the WPF
    /// window with the same bounds and is owned by it, so the dialogs stay on top and center over the shell.
    /// </summary>
    internal sealed class WinFormsOwner : Form
    {
        private readonly Window _window;

        public WinFormsOwner(Window window)
        {
            _window = window;
            ShowInTaskbar = false;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            Text = window.Title;
            Icon = BulkCrapUninstaller.Properties.Resources.Icon_Logo;

            // Creates the handle without showing anything, SafeInvoke needs it
            _ = Handle;

            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            SetWindowLongPtr(Handle, GWLP_HWNDPARENT, hwnd);

            window.LocationChanged += (_, _) => SyncBounds();
            window.SizeChanged += (_, _) => SyncBounds();
            window.StateChanged += (_, _) => SyncBounds();
            SyncBounds();
        }

        private void SyncBounds()
        {
            if (IsDisposed) return;

            var source = PresentationSource.FromVisual(_window);
            var scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var bounds = _window.WindowState == System.Windows.WindowState.Maximized
                ? System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(_window).Handle).WorkingArea
                : new System.Drawing.Rectangle((int)(_window.Left * scale), (int)(_window.Top * scale),
                    (int)(_window.ActualWidth * scale), (int)(_window.ActualHeight * scale));
            Bounds = bounds;
        }

        private const int GWLP_HWNDPARENT = -8;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
