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

using System.Collections.Generic;
using System.IO;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Pages.General;
using TcNo_Acc_Switcher_Server.Pages.General.Classes;

namespace TcNo_Acc_Switcher_Server.Data.Settings
{
    public class Steam
    {
        private static readonly Lang Lang = Lang.Instance;
        private static Steam _instance = new();

        private static readonly object LockObj = new();
        public static Steam Instance
        {
            get
            {
                lock (LockObj)
                {
                    return _instance ??= new Steam();
                }
            }
            set => _instance = value;
        }

        // Variables
        private bool _forgetAccountEnabled;
        [JsonProperty("ForgetAccountEnabled", Order = 0)] public bool ForgetAccountEnabled { get => _instance._forgetAccountEnabled; set => _instance._forgetAccountEnabled = value; }
        private string _folderPath = "C:\\Program Files (x86)\\Steam\\";
        [JsonProperty("FolderPath", Order = 1)] public string FolderPath { get => _instance._folderPath; set => _instance._folderPath = value; }
        private bool _admin;
        [JsonProperty("Steam_Admin", Order = 3)] public bool Admin { get => _instance._admin; set => _instance._admin = value; }
        private bool _showSteamId;
        [JsonProperty("Steam_ShowSteamID", Order = 4)] public bool ShowSteamId { get => _instance._showSteamId; set => _instance._showSteamId = value; }
        private bool _showVac = true;
        [JsonProperty("Steam_ShowVAC", Order = 5)] public bool ShowVac { get => _instance._showVac; set => _instance._showVac = value; }
        private bool _showLimited = true;
        [JsonProperty("Steam_ShowLimited", Order = 6)] public bool ShowLimited { get => _instance._showLimited; set => _instance._showLimited = value; }
        private bool _showAccUsername = true;
        [JsonProperty("Steam_ShowAccUsername", Order = 7)] public bool ShowAccUsername { get => _instance._showAccUsername; set => _instance._showAccUsername = value; }
        private bool _trayAccName;
        [JsonProperty("Steam_TrayAccountName", Order = 8)] public bool TrayAccName { get => _instance._trayAccName; set => _instance._trayAccName = value; }
        private int _imageExpiryTime = 7;
        [JsonProperty("Steam_ImageExpiryTime", Order = 9)] public int ImageExpiryTime { get => _instance._imageExpiryTime; set => _instance._imageExpiryTime = value; }
        private int _trayAccNumber = 3;
        [JsonProperty("Steam_TrayAccNumber", Order = 10)] public int TrayAccNumber { get => _instance._trayAccNumber; set => _instance._trayAccNumber = value; }
        private int _overrideState = -1;
        [JsonProperty("Steam_OverrideState", Order = 11)] public int OverrideState { get => _instance._overrideState; set => _instance._overrideState = value; }
        private bool _altClose;
        [JsonProperty("AltClose", Order = 13)] public bool AltClose { get => _instance._altClose; set => _instance._altClose = value; }

        private bool _desktopShortcut;
        [JsonIgnore] public bool DesktopShortcut { get => _instance._desktopShortcut; set => _instance._desktopShortcut = value; }

        // Constants
        [JsonIgnore] public static readonly List<string> Processes = new() { "steam.exe", "steamservice.exe", "steamwebhelper.exe", "GameOverlayUI.exe" };
        [JsonIgnore] public readonly string VacCacheFile = Path.Join(Globals.UserDataFolder, "LoginCache\\Steam\\VACCache\\SteamVACCache.json");
        [JsonIgnore] public static readonly string SettingsFile = "SteamSettings.json";
        [JsonIgnore] public readonly string ForgottenFile = "SteamForgotten.json";
        [JsonIgnore] public readonly string SteamImagePath = "wwwroot/img/profiles/steam/";
        [JsonIgnore] public readonly string SteamImagePathHtml = "img/profiles/steam/";
        [JsonIgnore] public readonly string ContextMenuJson = $@"[
				{{""{Lang["Context_SwapTo"]}"": ""swapTo(-1, event)""}},
				{{""{Lang["Context_LoginAsSubmenu"]}"": [
					{{""{Lang["Invisible"]}"": ""swapTo(7, event)""}},
					{{""{Lang["Offline"]}"": ""swapTo(0, event)""}},
					{{""{Lang["Online"]}"": ""swapTo(1, event)""}},
					{{""{Lang["Busy"]}"": ""swapTo(2, event)""}},
					{{""{Lang["Away"]}"": ""swapTo(3, event)""}},
					{{""{Lang["Snooze"]}"": ""swapTo(4, event)""}},
					{{""{Lang["LookingToTrade"]}"": ""swapTo(5, event)""}},
					{{""{Lang["LookingToPlay"]}"": ""swapTo(6, event)""}}
				]}},
				{{""{Lang["Context_CopySubmenu"]}"": [
				  {{""{Lang["Context_CopyProfileSubmenu"]}"": [
				    {{""{Lang["Context_CommunityUrl"]}"": ""copy('URL', event)""}},
				    {{""{Lang["Context_CommunityUsername"]}"": ""copy('Line2', event)""}},
				    {{""{Lang["Context_LoginUsername"]}"": ""copy('Username', event)""}}
				  ]}},
				  {{""{Lang["Context_CopySteamIdSubmenu"]}"": [
				    {{""{Lang["Context_Steam_Id"]}"": ""copy('SteamId', event)""}},
				    {{""{Lang["Context_Steam_Id3"]}"": ""copy('SteamId3', event)""}},
				    {{""{Lang["Context_Steam_Id32"]}"": ""copy('SteamId32', event)""}},
				    {{""{Lang["Context_Steam_Id64"]}"": ""copy('id', event)""}}
				  ]}},
				  {{""{Lang["Context_CopyOtherSubmenu"]}"": [
					{{""SteamRep"": ""copy('SteamRep', event)""}},
					{{""SteamID.uk"": ""copy('SteamID.uk', event)""}},
					{{""SteamID.io"": ""copy('SteamID.io', event)""}},
					{{""SteamIDFinder.com"": ""copy('SteamIDFinder.com', event)""}}
				  ]}},
				]}},
				{{""{Lang["Context_CreateShortcutSubmenu"]}"": [
					{{"""": ""createShortcut()""}},
					{{""{Lang["OnlineDefault"]}"": ""createShortcut()""}},
					{{""{Lang["Invisible"]}"": ""createShortcut(':7')""}},
					{{""{Lang["Offline"]}"": ""createShortcut(':0')""}},
					{{""{Lang["Busy"]}"": ""createShortcut(':2')""}},
					{{""{Lang["Away"]}"": ""createShortcut(':3')""}},
					{{""{Lang["Snooze"]}"": ""createShortcut(':4')""}},
					{{""{Lang["LookingToTrade"]}"": ""createShortcut(':5')""}},
					{{""{Lang["LookingToPlay"]}"": ""createShortcut(':6')""}}
				]}},
				{{""{Lang["Context_ChangeImage"]}"": ""changeImage(event)""}},
				{{""{Lang["Context_Steam_OpenUserdata"]}"": ""openUserdata(event)""}},
				{{""{Lang["Forget"]}"": ""forget(event)""}}
            ]";

        public static string StateToString(int state)
        {
            return state switch
            {
                -1 => Lang["NoDefault"],
                0 => Lang["Offline"],
                1 => Lang["Online"],
                2 => Lang["Busy"],
                3 => Lang["Away"],
                4 => Lang["Snooze"],
                5 => Lang["LookingToTrade"],
                6 => Lang["LookingToPlay"],
                7 => Lang["Invisible"],
                _ => ""
            };
        }

        /// <summary>
        /// Default settings for SteamSettings.json
        /// </summary>
        public void ResetSettings()
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\Steam.ResetSettings]");
            _instance.ForgetAccountEnabled = false;
            _instance.FolderPath = "C:\\Program Files (x86)\\Steam\\";
            _instance.Admin = false;
            _instance.ShowSteamId = false;
            _instance.ShowVac = true;
            _instance.ShowLimited = true;
            _instance.TrayAccName = false;
            _instance.ImageExpiryTime = 7;
            _instance.TrayAccNumber = 3;
            _instance._desktopShortcut = Shortcut.CheckShortcuts("Steam");
            _ = Shortcut.StartWithWindows_Enabled();
            _instance._altClose = false;

            SaveSettings();
        }

        /// <summary>
        /// Get path of loginusers.vdf, resets & returns "RESET_PATH" if invalid.
        /// </summary>
        /// <returns>(Steam's path)\config\loginusers.vdf</returns>
        public string LoginUsersVdf()
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\Steam.LoginUsersVdf]");
            var path = Path.Join(FolderPath, "config\\loginusers.vdf");
            if (File.Exists(path)) return path;

            FolderPath = "";
            SaveSettings();
            return "RESET_PATH";
        }

        /// <summary>
        /// Get Steam.exe path from SteamSettings.json
        /// </summary>
        /// <returns>Steam.exe's path string</returns>
        public string Exe() => Path.Join(FolderPath, "Steam.exe");

        /// <summary>
        /// Updates the ForgetAccountEnabled bool in Steam settings file
        /// </summary>
        /// <param name="enabled">Whether will NOT prompt user if they're sure or not</param>
        public void SetForgetAcc(bool enabled)
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\Steam.SetForgetAcc]");
            if (ForgetAccountEnabled == enabled) return; // Ignore if already set
            ForgetAccountEnabled = enabled;
            SaveSettings();
        }

        /// <summary>
        /// Returns a block of CSS text to be used on the page. Used to hide or show certain things in certain ways, in components that aren't being added through Blazor.
        /// </summary>
        public string GetSteamIdCssBlock() => ".steamId { display: " + (_instance._showSteamId ? "block" : "none") + " }";

        #region SETTINGS
        public void SetFromJObject(JObject j)
        {
            Globals.DebugWriteLine(@"[Func:Data\Settings\Steam.SetFromJObject]");
            var curSettings = j.ToObject<Steam>();
            if (curSettings == null) return;
            _instance.ForgetAccountEnabled = curSettings.ForgetAccountEnabled;
            _instance.FolderPath = curSettings.FolderPath;
            _instance.Admin = curSettings.Admin;
            _instance.ShowSteamId = curSettings.ShowSteamId;
            _instance.ShowVac = curSettings.ShowVac;
            _instance.ShowLimited = curSettings.ShowLimited;
            _instance.TrayAccName = curSettings.TrayAccName;
            _instance.ImageExpiryTime = curSettings.ImageExpiryTime;
            _instance.TrayAccNumber = curSettings.TrayAccNumber;
            _instance._desktopShortcut = Shortcut.CheckShortcuts("Steam");
            _instance._altClose = curSettings.AltClose;
            _ = Shortcut.StartWithWindows_Enabled();
        }
        public void LoadFromFile() => SetFromJObject(GeneralFuncs.LoadSettings(SettingsFile, GetJObject()));

        public JObject GetJObject() => JObject.FromObject(this);

        [JSInvokable]
        public void SaveSettings(bool mergeNewIntoOld = false) => GeneralFuncs.SaveSettings(SettingsFile, GetJObject(), mergeNewIntoOld);
        #endregion
    }
}
