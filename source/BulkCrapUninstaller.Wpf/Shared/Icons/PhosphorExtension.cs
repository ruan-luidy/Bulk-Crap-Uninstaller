/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Concurrent;
using System.Windows.Markup;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace BulkCrapUninstaller.Wpf.Shared.Icons
{
    // <Path Data="{icons:Phosphor GearSixBold}" Style="{StaticResource Icon}" />, each icon parsed once
    [MarkupExtensionReturnType(typeof(Geometry))]
    public sealed class PhosphorExtension : MarkupExtension
    {
        private static readonly ConcurrentDictionary<PackIconPhosphorIconsKind, Geometry> Cache = new();

        public PhosphorExtension()
        {
        }

        public PhosphorExtension(PackIconPhosphorIconsKind kind)
        {
            Kind = kind;
        }

        [ConstructorArgument("kind")]
        public PackIconPhosphorIconsKind Kind { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Get(Kind);

        public static Geometry Get(PackIconPhosphorIconsKind kind) => Cache.GetOrAdd(kind, k =>
        {
            var data = new PackIconPhosphorIcons { Kind = k }.Data;
            if (string.IsNullOrEmpty(data))
                return Geometry.Empty;

            var geometry = Geometry.Parse(data);
            geometry.Freeze();
            return geometry;
        });
    }
}
