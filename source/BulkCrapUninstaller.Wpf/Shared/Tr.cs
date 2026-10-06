/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace BulkCrapUninstaller.Wpf.Shared
{
    // Strings of the WPF shell: Properties/Strings.resx plus one .resx per language, same culture BCU already picked
    public static class Tr
    {
        private static readonly ResourceManager Resources =
            new("BulkCrapUninstaller.Wpf.Properties.Strings", typeof(Tr).Assembly);

        public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public static string Format(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    // Text="{loc:Tr Uninstall}"
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class TrExtension : MarkupExtension
    {
        public TrExtension()
        {
        }

        public TrExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Tr.Get(Key);
    }
}
