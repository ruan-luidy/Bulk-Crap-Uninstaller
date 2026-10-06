/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Klocman.Extensions;

namespace UninstallTools.Controls
{
    public class UninstallerIconGetter : Component
    {
        private static readonly string ApplicationIconKey = "applicationIcon";
        private static readonly string InvalidIconKey = "invalidIcon";
        private static readonly string MsiexecIconKey = "msiexecIcon";
        private static readonly string UpdateIconKey = "updateIcon";
        // The name "OperatingSystem" is important
        private static readonly string WindowsIconKey = "OperatingSystem";

        public UninstallerIconGetter()
        {
            Disposed += DisposeHandler;
        }

        public ImageList IconList { get; private set; }

        // ImageList.Images.Keys builds a new collection on every call, and the getter runs for every painted row
        private HashSet<string> _iconKeys = new();

        public object ColumnImageGetter(object rowObj)
        {
            if (rowObj is not ApplicationUninstallerEntry entry || IconList == null)
                return null;

            if (IconListContainsKey(entry.DisplayName))
                return entry.DisplayName;

            if (entry.ParentKeyName.IsNotEmpty())
            {
                if (entry.ParentKeyName.Equals(WindowsIconKey))
                    return WindowsIconKey;

                if (IconListContainsKey(entry.ParentKeyName))
                    return entry.ParentKeyName;
            }

            if (entry.IsUpdate || entry.UninstallerKind == UninstallerType.WindowsFeature)
                return UpdateIconKey;

            if (entry.UninstallerKind == UninstallerType.Msiexec || entry.UninstallerKind == UninstallerType.SdbInst)
                return MsiexecIconKey;

            if (entry.IsValid)
                return ApplicationIconKey;

            return InvalidIconKey;
        }

        /// <exception cref="PlatformNotSupportedException">The current platform is not supported.</exception>
        public void UpdateIconList(IEnumerable<ApplicationUninstallerEntry> objList)
        {
            var iconList = new ImageList();
            var keys = new HashSet<string>();
            var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            AddIcon(ApplicationIconKey, SystemIcons.Application);
            AddIcon(InvalidIconKey, SystemIcons.Exclamation);
            AddIcon(WindowsIconKey, SystemIcons.Shield); //SystemIcons.WinLogo); winlogo not working on xp?
            AddIcon(UpdateIconKey, SystemIcons.Shield);
            var msiIcon = UninstallToolsGlobalConfig.TryExtractAssociatedIcon(windowsPath + @"\msiexec.exe");
            AddIcon(MsiexecIconKey, msiIcon ?? SystemIcons.Application);

            foreach (var obj in objList)
            {
                if (string.IsNullOrEmpty(obj.DisplayName) || keys.Contains(obj.DisplayName))
                    continue;

                try
                {
                    var image = obj.GetIcon();
                    if (image != null)
                    {
                        AddIcon(obj.DisplayName, image);
                    }
                }
                catch
                {
                    // Revert to default icon
                }
            }

            // Publish both together so the getter never sees a half-filled list
            _iconKeys = keys;
            IconList = iconList;

            void AddIcon(string key, Icon icon)
            {
                iconList.Images.Add(key, icon);
                keys.Add(key);
            }
        }

        private void DisposeHandler(object x, EventArgs y)
        {
            if (IconList != null)
            {
                IconList.Dispose();
                IconList = null;
            }
        }

        private bool IconListContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (IconList == null)
                throw new InvalidOperationException("IconListContainsKey called when IconList is null");

            return _iconKeys.Contains(key);
        }
    }
}