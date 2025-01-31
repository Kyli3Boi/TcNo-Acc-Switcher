﻿// TcNo Account Switcher - A Super fast account switcher
// Copyright (C) 2019-2022 TechNobo (Wesley Pyburn)
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Pages.General;
using TcNo_Acc_Switcher_Server.Pages.General.Classes;

namespace TcNo_Acc_Switcher_Server.Data.Settings
{
    public class Ubisoft
    {
        private static readonly Lang Lang = Lang.Instance;
        private static Ubisoft _instance = new();

        private static readonly object LockObj = new();
        public static Ubisoft Instance
        {
            get
            {
                lock (LockObj)
                {
                    return _instance ??= new Ubisoft();
                }
            }
            set => _instance = value;
        }

        // Variables
        private string _folderPath = "C:\\Program Files (x86)\\Ubisoft\\Ubisoft Game launcher\\";
        [JsonProperty("FolderPath", Order = 1)] public string FolderPath { get => _instance._folderPath; set => _instance._folderPath = value; }
        private bool _admin;
        [JsonProperty("Ubisoft_Admin", Order = 2)] public bool Admin { get => _instance._admin; set => _instance._admin = value; }
        private int _trayAccNumber = 3;
        [JsonProperty("Ubisoft_TrayAccNumber", Order = 3)] public int TrayAccNumber { get => _instance._trayAccNumber; set => _instance._trayAccNumber = value; }
        private bool _forgetAccountEnabled;
        [JsonProperty("ForgetAccountEnabled", Order = 4)] public bool ForgetAccountEnabled { get => _instance._forgetAccountEnabled; set => _instance._forgetAccountEnabled = value; }
        private int _imageExpiryTime = 7;
        [JsonProperty("ImageExpiryTime", Order = 5)] public int ImageExpiryTime { get => _instance._imageExpiryTime; set => _instance._imageExpiryTime = value; }
        private bool _altClose;
        [JsonProperty("AltClose", Order = 6)] public bool AltClose { get => _instance._altClose; set => _instance._altClose = value; }

        private bool _desktopShortcut;
        [JsonIgnore] public bool DesktopShortcut { get => _instance._desktopShortcut; set => _instance._desktopShortcut = value; }

        // Constants
        [JsonIgnore] public static readonly string SettingsFile = "UbisoftSettings.json";
        /*
            [JsonIgnore] public string UbisoftImagePath = "wwwroot/img/profiles/ubi/";
            [JsonIgnore] public string UbisoftImagePathHtml = "img/profiles/ubi/";
        */
        [JsonIgnore] public static readonly string Processes = "upc";
        [JsonIgnore] public readonly string ContextMenuJson = $@"[
				{{""{Lang["Context_SwapTo"]}"": ""swapTo(-1, event)""}},
				{{""{Lang["Context_LoginAsSubmenu"]}"": [
					{{""{Lang["Online"]}"": ""swapTo(0, event)""}},
					{{""{Lang["Offline"]}"": ""swapTo(10, event)""}},
				]}},
				{{""{Lang["Context_ChangeName"]}"": ""showModal('changeUsername')""}},
				{{""{Lang["Context_CreateShortcut"]}"": ""createShortcut()""}},
				{{""{Lang["Context_ChangeImage"]}"": ""changeImage(event)""}},
				{{""{Lang["Forget"]}"": ""forget(event)""}}
            ]";

        /// <summary>
        /// Updates the ForgetAccountEnabled bool in settings file
        /// </summary>
        /// <param name="enabled">Whether will NOT prompt user if they're sure or not</param>
        public void SetForgetAcc(bool enabled)
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\Ubisoft.SetForgetAcc]");
            if (ForgetAccountEnabled == enabled) return; // Ignore if already set
            ForgetAccountEnabled = enabled;
            SaveSettings();
        }

        /// <summary>
        /// Get Ubisoft.exe path from UbisoftSettings.json
        /// </summary>
        /// <returns>Ubisoft.exe's path string</returns>
        public string Exe() => Path.Join(FolderPath, "upc.exe");

        #region SETTINGS
        /// <summary>
        /// Default settings for UbisoftSettings.json
        /// </summary>
        public void ResetSettings()
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\Ubisoft.ResetSettings]");
            _instance.FolderPath = "C:\\Program Files (x86)\\Ubisoft\\Ubisoft Game Launcher\\";
            _instance.Admin = false;
            _instance.TrayAccNumber = 3;
            _instance._desktopShortcut = Shortcut.CheckShortcuts("Ubisoft");
            _instance._altClose = false;

            SaveSettings();
        }
        public void SetFromJObject(JObject j)
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\Ubisoft.SetFromJObject]");
            var curSettings = j.ToObject<Ubisoft>();
            if (curSettings == null) return;
            _instance.FolderPath = curSettings.FolderPath;
            _instance.Admin = curSettings.Admin;
            _instance.TrayAccNumber = curSettings.TrayAccNumber;
            _instance._desktopShortcut = Shortcut.CheckShortcuts("Ubisoft");
            _instance._altClose = curSettings.AltClose;
        }
        public void LoadFromFile() => SetFromJObject(GeneralFuncs.LoadSettings(SettingsFile, GetJObject()));
        public JObject GetJObject() => JObject.FromObject(this);
        [JSInvokable]
        public void SaveSettings(bool mergeNewIntoOld = false) => GeneralFuncs.SaveSettings(SettingsFile, GetJObject(), mergeNewIntoOld);
        #endregion
    }
}
