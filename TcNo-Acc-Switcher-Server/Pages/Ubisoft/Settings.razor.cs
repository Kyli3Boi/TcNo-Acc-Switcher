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

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Data;
using TcNo_Acc_Switcher_Server.Pages.General;

namespace TcNo_Acc_Switcher_Server.Pages.Ubisoft
{
    public partial class Settings
    {
        private static readonly Lang Lang = Lang.Instance;

        protected override void OnInitialized()
        {
            AppData.Instance.WindowTitle = Lang["Title_Ubisoft_Settings"];
            Globals.DebugWriteLine(@"[Auto:Ubisoft\Settings.razor.cs.OnInitializedAsync]");
        }

        #region SETTINGS_GENERAL
        // BUTTON: Pick folder
        public void PickFolder()
        {
            Globals.DebugWriteLine(@"[ButtonClicked:Ubisoft\Settings.razor.cs.PickFolder]");
            _ = GeneralInvocableFuncs.ShowModal("find:Ubisoft:upc.exe:UbisoftSettings");
        }

        // BUTTON: Reset settings
        public static void ClearSettings()
        {
            Globals.DebugWriteLine(@"[ButtonClicked:Ubisoft\Settings.razor.cs.ClearSettings]");
            new Data.Settings.Ubisoft().ResetSettings();
            AppData.ActiveNavMan.NavigateTo("/Ubisoft?toast_type=success&toast_title=Success&toast_message=" + Uri.EscapeDataString(Lang["Toast_ClearedPlatformSettings", new { platform = "Ubisoft" }]));
        }
        #endregion

        #region SETTINGS_TOOLS
        // BUTTON: Open Folder
        public static void OpenFolder()
        {
            Globals.DebugWriteLine(@"[ButtonClicked:Ubisoft\Settings.razor.cs.OpenUbisoftFolder]");
            _ = Process.Start("explorer.exe", new Data.Settings.Ubisoft().FolderPath);
        }

        // BUTTON: RefreshImages
        public static void RefreshImages()
        {
            var allIds = GeneralFuncs.ReadAllIds_Generic("Ubisoft");
            foreach (var (userId, _) in allIds)
            {
                UbisoftSwitcherFuncs.ImportAvatar(userId);
            }
            _ = GeneralInvocableFuncs.ShowToast("success", Lang["Toast_RefreshedImages"], Lang["Done"], "toastarea");
        }


        // BUTTON: Advanced Cleaning...
        // Might add later

        #endregion

    }
}
