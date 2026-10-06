/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Windows;
using System.Windows.Controls.Primitives;

namespace BulkCrapUninstaller.Wpf.Shared.Controls
{
    // Dropdown Gap px away from the field, below or above; ShadowRoom = shadow margin around the card
    public static class DropDownPlacement
    {
        public static readonly DependencyProperty GapProperty =
            DependencyProperty.RegisterAttached("Gap", typeof(double), typeof(DropDownPlacement),
                new PropertyMetadata(double.NaN, OnGapChanged));

        public static readonly DependencyProperty ShadowRoomProperty =
            DependencyProperty.RegisterAttached("ShadowRoom", typeof(Thickness), typeof(DropDownPlacement), new PropertyMetadata(default(Thickness)));

        public static double GetGap(DependencyObject popup) => (double)popup.GetValue(GapProperty);

        public static void SetGap(DependencyObject popup, double value) => popup.SetValue(GapProperty, value);

        public static Thickness GetShadowRoom(DependencyObject popup) => (Thickness)popup.GetValue(ShadowRoomProperty);

        public static void SetShadowRoom(DependencyObject popup, Thickness value) => popup.SetValue(ShadowRoomProperty, value);

        private static void OnGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Popup popup)
                return;

            popup.Placement = PlacementMode.Custom;
            popup.CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
            {
                // Sizes come in pixels, gap and margin are in DIPs
                var target = popup.PlacementTarget as FrameworkElement;
                var scale = target is { ActualHeight: > 0 } ? targetSize.Height / target.ActualHeight : 1.0;
                var gap = GetGap(popup) * scale;
                var room = GetShadowRoom(popup);
                var (left, top, bottom) = (room.Left * scale, room.Top * scale, room.Bottom * scale);

                var below = new CustomPopupPlacement(new Point(-left, targetSize.Height + gap - top), PopupPrimaryAxis.None);
                var above = new CustomPopupPlacement(new Point(-left, -popupSize.Height - gap - top), PopupPrimaryAxis.None);
                if (target == null || PresentationSource.FromVisual(target) == null)
                    return new[] { below, above };

                // Left alone WPF slides the popup up over the field, so the side is picked here from the work area
                var origin = target.PointToScreen(new Point(0, 0));
                var work = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)origin.X, (int)origin.Y)).WorkingArea;
                var fitsBelow = origin.Y + targetSize.Height + gap + popupSize.Height + bottom <= work.Bottom;
                var roomAbove = origin.Y - work.Y;
                var roomBelow = work.Bottom - (origin.Y + targetSize.Height);
                return new[] { fitsBelow || roomBelow >= roomAbove ? below : above };
            };
        }
    }
}
