/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.ComponentModel;
using System.Globalization;
using BulkCrapUninstaller.Properties;

namespace BulkCrapUninstaller.Wpf.Features.Options
{
    // {Binding S[ScanSteam]}: the BCU settings class is internal, WPF bindings only see public members
    public sealed class SettingsAccessor : INotifyPropertyChanged
    {
        public SettingsAccessor()
        {
            Settings.Default.PropertyChanged += (_, _) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public object this[string name]
        {
            get => Settings.Default[name];
            set
            {
                var type = Settings.Default.Properties[name]?.PropertyType;
                if (type == null) return;

                var converted = value == null || type.IsInstanceOfType(value)
                    ? value
                    : type.IsEnum
                        ? Enum.Parse(type, value.ToString()!)
                        : Convert.ChangeType(value, type, CultureInfo.CurrentCulture);

                if (Equals(Settings.Default[name], converted)) return;
                Settings.Default[name] = converted;
            }
        }
    }
}
