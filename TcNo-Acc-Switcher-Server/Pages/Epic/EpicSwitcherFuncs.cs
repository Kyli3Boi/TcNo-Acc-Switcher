﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Win32;
using Newtonsoft.Json;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Data;
using TcNo_Acc_Switcher_Server.Pages.General;

namespace TcNo_Acc_Switcher_Server.Pages.Epic
{
    public class EpicSwitcherFuncs
    {
        private static readonly Lang Lang = Lang.Instance;

        private static readonly Data.Settings.Epic Epic = Data.Settings.Epic.Instance;
        /*
                private static string _epicRoaming = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Epic");
        */
        private static readonly string EpicLocalAppData = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher");
        private static readonly string EpicSavedConfigWin = Path.Join(EpicLocalAppData, "Saved\\Config\\Windows");
        private static readonly string EpicGameUserSettings = Path.Join(EpicSavedConfigWin, "GameUserSettings.ini");
        /// <summary>
        /// Main function for Epic Account Switcher. Run on load.
        /// Collects accounts from cache folder
        /// Prepares HTML Elements string for insertion into the account switcher GUI.
        /// </summary>
        /// <returns>Whether account loading is successful, or a path reset is needed (invalid dir saved)</returns>
        public static void LoadProfiles()
        {
            // Normal:
            Globals.DebugWriteLine(@"[Func:Epic\EpicSwitcherFuncs.LoadProfiles] Loading Epic profiles");
            Data.Settings.Epic.Instance.LoadFromFile();
            _ = GenericFunctions.GenericLoadAccounts("Epic", true);
        }

        /// <summary>
        /// Used in JS. Gets whether forget account is enabled (Whether to NOT show prompt, or show it).
        /// </summary>
        /// <returns></returns>
        [JSInvokable]
        public static Task<bool> GetEpicForgetAcc() => Task.FromResult(Epic.ForgetAccountEnabled);

        /// <summary>
        /// Restart Epic with a new account selected. Leave args empty to log into a new account.
        /// </summary>
        /// <param name="accName">(Optional) User's login username</param>
        /// <param name="args">Starting arguments</param>
        [SupportedOSPlatform("windows")]
        public static void SwapEpicAccounts(string accName = "", string args = "")
        {
            Globals.DebugWriteLine(@"[Func:Epic\EpicSwitcherFuncs.SwapEpicAccounts] Swapping to: hidden.");

            _ = AppData.InvokeVoidAsync("updateStatus", Lang["Status_ClosingPlatform", new { platform = "Epic" }]);
            if (!GeneralFuncs.CloseProcesses(Data.Settings.Epic.Processes, Data.Settings.Epic.Instance.AltClose))
            {
                _ = AppData.InvokeVoidAsync("updateStatus", Lang["Status_ClosingPlatformFailed", new { platform = "Epic" }]);
                return;
            };

            ClearCurrentLoginEpic();
            if (accName != "")
            {
                if (!EpicCopyInAccount(accName)) return;
                Globals.AddTrayUser("Epic", "+e:" + accName, accName, Epic.TrayAccNumber); // Add to Tray list
            }

            _ = AppData.InvokeVoidAsync("updateStatus", Lang["Status_StartingPlatform", new { platform = "Epic" }]);
            GeneralFuncs.StartProgram(Epic.Exe(), Epic.Admin, args);

            Globals.RefreshTrayArea();
            _ = AppData.InvokeVoidAsync("updateStatus", Lang["Done"]);
        }

        [SupportedOSPlatform("windows")]
        private static void ClearCurrentLoginEpic()
        {
            Globals.DebugWriteLine(@"[Func:Epic\EpicSwitcherFuncs.ClearCurrentLoginEpic]");
            // Get current information for logged in user, and save into files:
            var currentAccountId = (string)Registry.CurrentUser.OpenSubKey(@"Software\Epic Games\Unreal Engine\Identifiers")?.GetValue("AccountId");

            var allIds = GeneralFuncs.ReadAllIds_Generic("Epic");
            if (currentAccountId != null && allIds.ContainsKey(currentAccountId))
                EpicAddCurrent(allIds[currentAccountId]);

            if (File.Exists(EpicGameUserSettings)) File.Delete(EpicGameUserSettings); // Delete GameUserSettings.ini file
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Epic Games\Unreal Engine\Identifiers");
            key?.SetValue("AccountId", ""); // Clear logged in account in registry, but leave MachineId
        }

        [SupportedOSPlatform("windows")]
        private static bool EpicCopyInAccount(string accName)
        {
            Globals.DebugWriteLine(@"[Func:Epic\EpicSwitcherFuncs.EpicCopyInAccount]");
            var localCachePath = $"LoginCache\\Epic\\{accName}\\";
            if (!Directory.Exists(localCachePath))
            {
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["CouldNotFindX", new { x = localCachePath }], Lang["DirectoryNotFound"], "toastarea");
                return false;
            }

            File.Copy(Path.Join(localCachePath, "GameUserSettings.ini"), EpicGameUserSettings);

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Epic Games\Unreal Engine\Identifiers");

            var allIds = GeneralFuncs.ReadAllIds_Generic("Epic");
            try
            {
                key?.SetValue("AccountId", allIds.Single(x => x.Value == accName).Key);
            }
            catch (InvalidOperationException)
            {
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_DuplicateNames"], Lang["Error"], "toastarea");
            }

            return true;
        }

        [SupportedOSPlatform("windows")]
        public static void EpicAddCurrent(string accName)
        {
            Globals.DebugWriteLine(@"[Func:Epic\EpicSwitcherFuncs.EpicAddCurrent]");
            var localCachePath = $"LoginCache\\Epic\\{accName}\\";
            _ = Directory.CreateDirectory(localCachePath);

            if (!File.Exists(EpicGameUserSettings))
            {
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_CouldNotLocate"], Lang["Error"], "toastarea");
                return;
            }
            // Save files
            File.Copy(EpicGameUserSettings, Path.Join(localCachePath, "GameUserSettings.ini"), true);
            // Save registry key
            var currentAccountId = (string)Registry.CurrentUser.OpenSubKey(@"Software\Epic Games\Unreal Engine\Identifiers")?.GetValue("AccountId");
            if (currentAccountId == null)
            {
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_AccountIdReg"], Lang["Error"], "toastarea");
                return;
            }

            var allIds = GeneralFuncs.ReadAllIds_Generic("Epic");
            allIds[currentAccountId] = accName;
            File.WriteAllText("LoginCache\\Epic\\ids.json", JsonConvert.SerializeObject(allIds));

            // Copy in profile image from default
            _ = Directory.CreateDirectory(Path.Join(GeneralFuncs.WwwRoot(), "\\img\\profiles\\epic"));
            var profileImg = Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\epic\\{Globals.GetCleanFilePath(accName)}.jpg");
            if (!File.Exists(profileImg)) File.Copy(Path.Join(GeneralFuncs.WwwRoot(), "\\img\\EpicDefault.png"), profileImg, true);

            AppData.ActiveNavMan?.NavigateTo("/Epic/?cacheReload&toast_type=success&toast_title=Success&toast_message=" + Uri.EscapeDataString(Lang["Toast_SavedItem", new { item = accName }]), true);
        }

        public static void ChangeUsername(string oldName, string newName, bool reload = true)
        {
            var allIds = GeneralFuncs.ReadAllIds_Generic("Epic");
            try
            {
                allIds[allIds.Single(x => x.Value == oldName).Key] = newName;
                File.WriteAllText("LoginCache\\Epic\\ids.json", JsonConvert.SerializeObject(allIds));
            }
            catch (Exception)
            {
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_CantChangeUsername"], Lang["Error"], "toastarea");
                return;
            }

            File.Move(Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\epic\\{Globals.GetCleanFilePath(oldName)}.jpg"),
                Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\epic\\{Globals.GetCleanFilePath(newName)}.jpg")); // Rename image
            Directory.Move($"LoginCache\\Epic\\{oldName}\\", $"LoginCache\\Epic\\{newName}\\"); // Rename login cache folder

            if (reload) AppData.ActiveNavMan?.NavigateTo("/Epic/?cacheReload&toast_type=success&toast_title=Success&toast_message=" + Uri.EscapeDataString(Lang["Toast_ChangedUsername"]), true);
        }
    }
}
