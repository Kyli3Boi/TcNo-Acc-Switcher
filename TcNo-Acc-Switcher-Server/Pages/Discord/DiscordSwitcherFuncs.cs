﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Win32;
using Newtonsoft.Json;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Data;
using TcNo_Acc_Switcher_Server.Pages.General;
using TcNo_Acc_Switcher_Server.Pages.General.Classes;
using Task = System.Threading.Tasks.Task;

namespace TcNo_Acc_Switcher_Server.Pages.Discord
{
    public class DiscordSwitcherFuncs
    {
        private static readonly Lang Lang = Lang.Instance;

        private static readonly Data.Settings.Discord Discord = Data.Settings.Discord.Instance;
        private static readonly string DiscordRoaming = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord");
        private static readonly string DiscordCacheFolder = Path.Join(DiscordRoaming, "Cache");
        private static readonly string DiscordLocalStorage = Path.Join(DiscordRoaming, "Local Storage");
        private static readonly string DiscordSessionStorage = Path.Join(DiscordRoaming, "Session Storage");
        private static readonly string DiscordBlobStorage = Path.Join(DiscordRoaming, "blob_storage");

        /// <summary>
        /// Main function for Discord Account Switcher. Run on load.
        /// Collects accounts from cache folder
        /// Prepares HTML Elements string for insertion into the account switcher GUI.
        /// </summary>
        /// <returns>Whether account loading is successful, or a path reset is needed (invalid dir saved)</returns>
        public static void LoadProfiles()
        {
            // Normal:
            Globals.DebugWriteLine(@"[Func:Discord\DiscordSwitcherFuncs.LoadProfiles] Loading Discord profiles");
            Data.Settings.Discord.Instance.LoadFromFile();
            _ = GenericFunctions.GenericLoadAccounts("Discord", true);
        }

        /// <summary>
        /// Used in JS. Gets whether forget account is enabled (Whether to NOT show prompt, or show it).
        /// </summary>
        /// <returns></returns>
        [JSInvokable]
        public static Task<bool> GetDiscordForgetAcc() => Task.FromResult(Discord.ForgetAccountEnabled);

        private static string GetHashedDiscordToken()
        {
            // Loop through log/ldb files:
            var token = "";
            if (!Directory.Exists(Path.Join(DiscordLocalStorage, "leveldb"))) return "";
            foreach (var file in new DirectoryInfo(Path.Join(DiscordLocalStorage, "leveldb")).GetFiles("*"))
            {
                if (!file.Name.EndsWith(".ldb") && !file.Name.EndsWith(".log")) continue;
                var text = GeneralFuncs.ReadOnlyReadAllText(file.FullName);
                var test1 = new Regex(@"[\w-]{24}\.[\w-]{6}\.[\w-]{27}").Match(text).Value;
                var test2 = new Regex(@"mfa\.[\w-]{84}").Match(text).Value;
                token = test1 != "" ? test1 : test2 != "" ? test2 : token;
            }

            if (token == "")
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_Discord_NoToken"], "Error", "toastarea");
            else
            {
                token = Globals.GetSha256HashString(token);
            }

            return token;
        }

        [SupportedOSPlatform("windows")]
        public static void SwapDiscordAccounts() => SwapDiscordAccounts("", "");
        /// <summary>
        /// Restart Discord with a new account selected. Leave accName empty to log into a new account.
        /// </summary>
        /// <param name="accName">(Optional) User's login username</param>
        /// <param name="args">Starting arguments</param>
        [SupportedOSPlatform("windows")]
        public static void SwapDiscordAccounts(string accName, string args = "")
        {
            Globals.DebugWriteLine(@"[Func:Discord\DiscordSwitcherFuncs.SwapDiscordAccounts] Swapping to: hidden.");

            _ = AppData.InvokeVoidAsync("updateStatus", Lang["Status_ClosingPlatform", new { platform = "Discord" }]);
            if (!GeneralFuncs.CloseProcesses(Data.Settings.Discord.Processes, Data.Settings.Discord.Instance.AltClose))
            {
                _ = AppData.InvokeVoidAsync("updateStatus", Lang["Status_ClosingPlatformFailed", new { platform = "Discord" }]);
                return;
            };

            ClearCurrentLoginDiscord();
            if (accName != "")
            {
                if (!DiscordCopyInAccount(accName)) return;
                Globals.AddTrayUser("Discord", "+d:" + accName, accName, Discord.TrayAccNumber); // Add to Tray list
            }

            _ = AppData.InvokeVoidAsync("updateStatus", Lang["Status_StartingPlatform", new { platform = "Discord" }]);

            GeneralFuncs.StartProgram(Discord.Exe(), Discord.Admin, args);

            Globals.RefreshTrayArea();
            _ = AppData.InvokeVoidAsync("updateStatus", Lang["Done"]);
        }

        [SupportedOSPlatform("windows")]
        private static void ClearCurrentLoginDiscord()
        {
            Globals.DebugWriteLine(@"[Func:Discord\DiscordSwitcherFuncs.ClearCurrentLoginDiscord]");
            // Save current
            var hash = GetHashedDiscordToken();
            var allIds = GeneralFuncs.ReadAllIds_Generic("Discord");
            if (allIds.ContainsKey(hash))
                DiscordAddCurrent(allIds[hash]);

            // Remove existing folders:
            if (Directory.Exists(DiscordLocalStorage)) Globals.RecursiveDelete(new DirectoryInfo(DiscordLocalStorage), false);
            if (Directory.Exists(DiscordSessionStorage)) Globals.RecursiveDelete(new DirectoryInfo(DiscordSessionStorage), false);
            if (Directory.Exists(DiscordBlobStorage)) Globals.RecursiveDelete(new DirectoryInfo(DiscordBlobStorage), false);

            // Remove existing files:
            var fileList = new List<string>() { "Cookies", "Network Persistent State", "Preferences", "TransportSecurity" };
            foreach (var f in fileList.Where(f => File.Exists(Path.Join(DiscordRoaming, f))))
                File.Delete(Path.Join(DiscordRoaming, f));

            // Loop through Cache files:
            foreach (var file in new DirectoryInfo(DiscordCacheFolder).GetFiles("*"))
                if (file.Name.StartsWith("data_") || file.Name == "index")
                    File.Delete(file.FullName);
        }

        [SupportedOSPlatform("windows")]
        private static bool DiscordCopyInAccount(string accName)
        {
            Globals.DebugWriteLine(@"[Func:Discord\DiscordSwitcherFuncs.DiscordCopyInAccount]");
            _ = GeneralInvocableFuncs.ShowToast("info", Lang["Toast_Discord_SaveHint"],
                "Discord note", "toastarea");

            var accFolder = $"LoginCache\\Discord\\{accName}\\";
            if (!Directory.Exists(accFolder))
            {
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["CouldNotFindX", new { x = accFolder }], Lang["DirectoryNotFound"], "toastarea");
                return false;
            }

            // Clear files first, as some extra files from different accounts can have different names.
            ClearCurrentLoginDiscord();

            // Copy files in base folder:
            var fileList = new List<string>() { "Cookies", "Network Persistent State", "Preferences", "TransportSecurity" };
            foreach (var f in fileList.Where(f => File.Exists(Path.Join(accFolder, f))))
                File.Copy(Path.Join(accFolder, f), Path.Join(DiscordRoaming, f), true);

            // Copy folders:
            if (Directory.Exists(Path.Join(accFolder, "Local Storage"))) Globals.CopyFilesRecursive(Path.Join(accFolder, "Local Storage"), DiscordLocalStorage, true);
            if (Directory.Exists(Path.Join(accFolder, "Session Storage"))) Globals.CopyFilesRecursive(Path.Join(accFolder, "Session Storage"), DiscordSessionStorage, true);
            if (Directory.Exists(Path.Join(accFolder, "blob_storage"))) Globals.CopyFilesRecursive(Path.Join(accFolder, "blob_storage"), DiscordBlobStorage, true);

            // Decrypt important files in folders:
            foreach (var f in Directory.GetFiles(DiscordSessionStorage))
                if (f.EndsWith(".log") || f.EndsWith(".ldb"))
                    _ = Cryptography.StringCipher.DecryptFile(f, Discord.Password);
            foreach (var f in Directory.GetFiles(Path.Join(DiscordLocalStorage, "leveldb")))
                if (f.EndsWith(".log") || f.EndsWith(".ldb"))
                    _ = Cryptography.StringCipher.DecryptFile(f, Discord.Password);

            // Loop through Cache files:
            foreach (var file in new DirectoryInfo(Path.Join(accFolder, "Cache")).GetFiles("*"))
                if (file.Name.StartsWith("data_") || file.Name == "index")
                    File.Copy(file.FullName, Path.Join(DiscordCacheFolder, file.Name), true);

            return true;
        }

        public static void DiscordAddCurrent(string accName)
        {
            Globals.DebugWriteLine(@"[Func:Discord\DiscordSwitcherFuncs.DiscordAddCurrent]");

            // Reject if name is invalid length. Discord names limited to between 2-32 characters.
            if (accName.Length > 33) return;
            // Check if this is the copy/paste code to get account info, and reject if it is.
            if (accName.Contains("// Find ", StringComparison.InvariantCultureIgnoreCase) ||
                accName.Contains("copied information", StringComparison.InvariantCultureIgnoreCase)) return;

            _ = GeneralInvocableFuncs.ShowToast("info", Lang["Toast_Discord_SignOutHint"],
                "Discord note", "toastarea");

            // See if user used automated collection tool:
            var imgUrl = "";
            if (accName.StartsWith("TCNO:"))
            {
                var parts = accName.Split("|");
                imgUrl = parts[0][5..].Split("?")[0];
                accName = parts[1];
            }

            var closedBySwitcher = false;
            var accFolder = $"LoginCache\\Discord\\{accName}\\";
            _ = Directory.CreateDirectory(accFolder);
            var hash = "";
            var attempts = 0;
            while (attempts < 5)
            {
                try
                {
                    hash = GetHashedDiscordToken();
                    break;
                }
                catch (IOException e)
                {
                    if (attempts > 2 && e.HResult == -2147024864) // File is in use - but wait 2 seconds to see...
                    {
                        _ = GeneralFuncs.CloseProcesses(Data.Settings.Discord.Processes, Data.Settings.Discord.Instance.AltClose);
                        closedBySwitcher = true;

                    }
                    else if (attempts < 4)
                    {
                        _ = AppData.InvokeVoidAsync("updateStatus", $"Files in use... Timeout: ({attempts}/4 seconds)");
                        System.Threading.Thread.Sleep(1000);
                    }
                    else throw;
                }

                attempts++;
            }

            if (string.IsNullOrEmpty(hash)) return;

            var allIds = GeneralFuncs.ReadAllIds_Generic("Discord");
            allIds[hash] = accName;
            File.WriteAllText("LoginCache\\Discord\\ids.json", JsonConvert.SerializeObject(allIds));

            // Copy files in base folder:
            var fileList = new List<string>() { "Cookies", "Network Persistent State", "Preferences", "TransportSecurity" };
            foreach (var f in fileList.Where(f => File.Exists(Path.Join(DiscordRoaming, f))))
                File.Copy(Path.Join(DiscordRoaming, f), Path.Join(accFolder, f), true);

            // Remove existing folders (as to not create thousands of extra files over lots of copies):
            if (Directory.Exists(Path.Join(accFolder, "Local Storage"))) Globals.RecursiveDelete(new DirectoryInfo(Path.Join(accFolder, "Local Storage")), false);
            if (Directory.Exists(Path.Join(accFolder, "Session Storage"))) Globals.RecursiveDelete(new DirectoryInfo(Path.Join(accFolder, "Session Storage")), false);
            if (Directory.Exists(Path.Join(accFolder, "blob_storage"))) Globals.RecursiveDelete(new DirectoryInfo(Path.Join(accFolder, "blob_storage")), false);
            // Copy folders:
            if (Directory.Exists(DiscordLocalStorage)) Globals.CopyFilesRecursive(DiscordLocalStorage, Path.Join(accFolder, "Local Storage"), true);
            if (Directory.Exists(DiscordSessionStorage)) Globals.CopyFilesRecursive(DiscordSessionStorage, Path.Join(accFolder, "Session Storage"), true);
            if (Directory.Exists(DiscordSessionStorage)) Globals.CopyFilesRecursive(DiscordBlobStorage, Path.Join(accFolder, "blob_storage"), true);

            // Encrypt important files in folders:
            foreach (var f in Directory.GetFiles(Path.Join(accFolder, "Session Storage")))
                if (f.EndsWith(".log") || f.EndsWith(".ldb"))
                    _ = Cryptography.StringCipher.EncryptFile(f, Discord.Password);
            foreach (var f in Directory.GetFiles(Path.Join(accFolder, "Local Storage\\leveldb")))
                if (f.EndsWith(".log") || f.EndsWith(".ldb"))
                    _ = Cryptography.StringCipher.EncryptFile(f, Discord.Password);

            // Loop through Cache files:
            if (Directory.Exists(Path.Join(accFolder, "Cache"))) Globals.RecursiveDelete(new DirectoryInfo(Path.Join(accFolder, "Cache")), false);
            _ = Directory.CreateDirectory(Path.Join(accFolder, "Cache"));
            foreach (var file in new DirectoryInfo(DiscordCacheFolder).GetFiles("*"))
                if (file.Name.StartsWith("data_") || file.Name == "index")
                    File.Copy(file.FullName, Path.Join(accFolder, "Cache", file.Name), true);

            // Handle profile image:
            _ = Directory.CreateDirectory(Path.Join(GeneralFuncs.WwwRoot(), "\\img\\profiles\\discord"));
            var profileImg = Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\discord\\{Globals.GetCleanFilePath(accName).Replace("#", "-")}.jpg");

            // Check to see if profile image download required:
            if (!string.IsNullOrEmpty(imgUrl))
            {
                try
                {
                    using var client = new WebClient();
                    client.DownloadFile(new Uri(imgUrl), profileImg);
                }
                catch (WebException)
                {
                    if (!File.Exists(profileImg)) File.Copy(Path.Join(GeneralFuncs.WwwRoot(), "\\img\\DiscordDefault.png"), profileImg, true);
                }
            }
            else
            {
                // Copy in profile image from default
                if (!File.Exists(profileImg)) File.Copy(Path.Join(GeneralFuncs.WwwRoot(), "\\img\\DiscordDefault.png"), profileImg, true);
            }

            if (closedBySwitcher) GeneralFuncs.StartProgram(Discord.Exe(), Discord.Admin);

            try
            {
                AppData.ActiveNavMan?.NavigateTo("/Discord/?cacheReload&toast_type=success&toast_title=Success&toast_message=" + Uri.EscapeDataString(Lang["Toast_SavedItem", new {item = accName}]), true);
            }
            catch (Microsoft.AspNetCore.Components.NavigationException) // Page was reloaded.
            {
                //
            }
        }

        [JSInvokable]
        public static bool SkipGetUsername()
        {
            // Save current
            var hash = GetHashedDiscordToken();
            var allIds = GeneralFuncs.ReadAllIds_Generic("Discord");
            if (!allIds.ContainsKey(hash)) return false;
            // Else list already contains the token, so just save with the same username.
            DiscordAddCurrent(allIds[hash]);
            return true;
        }

        public static void ChangeUsername(string oldName, string newName, bool reload = true)
        {
            // See if user used automated collection tool:
            var imgUrl = "";
            if (newName.StartsWith("TCNO:"))
            {
                var parts = newName.Split("|");
                imgUrl = parts[0][5..].Split("?")[0];
                newName = parts[1];
            }

            var allIds = GeneralFuncs.ReadAllIds_Generic("Discord");
            try
            {
                allIds[allIds.Single(x => x.Value == oldName).Key] = newName;
                File.WriteAllText("LoginCache\\Discord\\ids.json", JsonConvert.SerializeObject(allIds));
            }
            catch (Exception)
            {
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_CantChangeUsername"], Lang["Error"], "toastarea");
                return;
            }

            // Check to see if profile image download required:
            if (!string.IsNullOrEmpty(imgUrl))
            {
                var path = Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\discord\\{Globals.GetCleanFilePath(newName).Replace("#", "-")}.jpg");
                // Download new image
                try
                {
                    using var client = new WebClient();
                    client.DownloadFile(new Uri(imgUrl), path);
                }
                catch (WebException)
                {
                    // Move existing image
                    File.Move(Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\discord\\{Globals.GetCleanFilePath(oldName).Replace("#", "-")}.jpg"),
                        Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\discord\\{Globals.GetCleanFilePath(newName).Replace("#", "-")}.jpg")); // Rename image
                }
            }
            else
            {
                // Move existing image
                File.Move(Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\discord\\{Globals.GetCleanFilePath(oldName).Replace("#", "-")}.jpg"),
                    Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\discord\\{Globals.GetCleanFilePath(newName).Replace("#", "-")}.jpg")); // Rename image
            }

            if ($"LoginCache\\Discord\\{oldName}\\" != $"LoginCache\\Discord\\{newName}\\")
                Directory.Move($"LoginCache\\Discord\\{oldName}\\", $"LoginCache\\Discord\\{newName}\\"); // Rename login cache folder

            if (reload) AppData.ActiveNavMan?.NavigateTo("/Discord/?cacheReload&toast_type=success&toast_title=Success&toast_message=" + Uri.EscapeDataString(Lang["Toast_ChangedUsername"]), true);
        }

        #region DISCORD_MANAGEMENT
        /// <summary>
        /// Kills Discord processes when run via cmd.exe
        /// </summary>
        public static void ClearDiscordCache()
        {
            int totalFiles = 0, failedFiles = 0;
            var totalFileSize = 0.0;

            // Loop through Cache files:
            var fileInfoArr = new DirectoryInfo(DiscordCacheFolder).GetFiles();
            foreach (var file in fileInfoArr)
            {
                if (!file.Name.StartsWith("f_")) continue; // Ignore files that aren't cache files
                try
                {
                    totalFiles++;
                    File.Delete(file.FullName);
                    totalFileSize += file.Length;
                }
                catch (Exception)
                {
                    failedFiles++;
                }
            }

            _ = GeneralInvocableFuncs.ShowToast("success", Lang["Toast_DeletedTotal", new { files = totalFiles - failedFiles, totalFiles, totalSize = GeneralFuncs.FileSizeString(totalFileSize) }],
                Lang["Toast_DeletedTotalTitle"], "toastarea", 10000);
        }
        #endregion
    }
}
