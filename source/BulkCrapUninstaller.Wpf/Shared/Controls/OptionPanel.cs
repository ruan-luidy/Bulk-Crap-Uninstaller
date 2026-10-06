/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BulkCrapUninstaller.Wpf.Shared.Controls
{
    // Card with an icon, a small upper case header and the option rows
    public class OptionPanel : HeaderedContentControl
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(Geometry), typeof(OptionPanel));

        public static readonly DependencyProperty AsideProperty =
            DependencyProperty.Register(nameof(Aside), typeof(object), typeof(OptionPanel));

        public Geometry Icon
        {
            get => (Geometry)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        // Extra content right of the header (counter, small button)
        public object Aside
        {
            get => GetValue(AsideProperty);
            set => SetValue(AsideProperty, value);
        }
    }
}
