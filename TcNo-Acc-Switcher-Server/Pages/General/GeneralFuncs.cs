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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Data;
using TcNo_Acc_Switcher_Server.Pages.General.Classes;

namespace TcNo_Acc_Switcher_Server.Pages.General
{
    public class GeneralFuncs
    {
        private static readonly Lang Lang = Lang.Instance;

        #region PROCESS_OPERATIONS
        public static bool IsAdministrator => OperatingSystem.IsWindows() && new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        public static void StartProgram(string path, bool elevated) => StartProgram(path, elevated, "");
        /// <summary>
        /// Starts a process with or without Admin
        /// </summary>
        /// <param name="path">Path of process to start</param>
        /// <param name="elevated">Whether the process should start elevated or not</param>
        /// <param name="args">Arguments to pass into the program</param>
        public static void StartProgram(string path, bool elevated, string args)
        {

            if (!elevated && IsAdministrator)
                ProcessHandler.RunAsDesktopUser(path, args);
            else
                ProcessHandler.StartProgram(path, elevated, args);
        }

        public static bool CanKillProcess(List<string> procNames) => procNames.Aggregate(true, (current, s) => current & CanKillProcess(s));

        public static bool CanKillProcess(string processName, bool showModal = true)
        {
            // Checks whether program is running as Admin or not
            var currentlyElevated = false;
            if (OperatingSystem.IsWindows())
            {
                var securityIdentifier = WindowsIdentity.GetCurrent().Owner;
                if (securityIdentifier is not null) currentlyElevated = securityIdentifier.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
            }

            bool canKill;
            if (currentlyElevated)
                canKill = true; // Elevated process can kill most other processes.
            else
            {
                if (OperatingSystem.IsWindows())
                    canKill = !ProcessHelper.IsProcessAdmin(processName); // If other process is admin, this process can't kill it (because it's not admin)
                else
                    canKill = false;
            }

            // Restart self as admin.
            if (!canKill && showModal) _ = GeneralInvocableFuncs.ShowModal("notice:RestartAsAdmin");

            return canKill;
        }

        public static bool CloseProcesses(string procName, bool altMethod = false)
        {
            if (!OperatingSystem.IsWindows()) return false;
            Globals.DebugWriteLine(@"Closing: " + procName);
            if (!GeneralFuncs.CanKillProcess(procName)) return false;
            Globals.KillProcess(procName, altMethod);

            return GeneralFuncs.WaitForClose(procName);
        }
        public static bool CloseProcesses(List<string> procNames, bool altMethod = false)
        {
            if (!OperatingSystem.IsWindows()) return false;
            Globals.DebugWriteLine(@"Closing: " + string.Join(", ", procNames));
            if (!GeneralFuncs.CanKillProcess(procNames)) return false;
            Globals.KillProcess(procNames, altMethod);

            return GeneralFuncs.WaitForClose(procNames);
        }

        /// <summary>
        /// Waits for a program to close, and returns true if not running anymore.
        /// </summary>
        /// <param name="procName">Name of process to lookup</param>
        /// <returns>Whether it was closed before this function returns or not.</returns>
        public static bool WaitForClose(string procName)
        {
            if (!OperatingSystem.IsWindows()) return false;
            var timeout = 0;
            while (ProcessHelper.IsProcessRunning(procName) && timeout < 10)
            {
                timeout++;
                _ = AppData.InvokeVoidAsync("updateStatus", $"Waiting for {procName} to close ({timeout}/10 seconds)");
                System.Threading.Thread.Sleep(1000);
            }

            if (timeout == 10)
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["CouldNotCloseX", new { x = procName }], Lang["Error"], "toastarea");

            return timeout != 10; // Returns true if timeout wasn't reached.
        }
        public static bool WaitForClose(List<string> procNames)
        {
            if (!OperatingSystem.IsWindows()) return false;
            var timeout = 0;
            while (procNames.All(ProcessHelper.IsProcessRunning) && timeout < 10)
            {
                timeout++;
                _ = AppData.InvokeVoidAsync("updateStatus", $"Waiting for {procNames[0]} & {procNames.Count - 1} others to close ({timeout}/10 seconds)");
                System.Threading.Thread.Sleep(1000);
            }

            if (timeout == 10)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                var leftOvers = procNames.Where(x => !ProcessHelper.IsProcessRunning(x));
#pragma warning restore CA1416 // Validate platform compatibility
                _ = GeneralInvocableFuncs.ShowToast("error", Lang["CouldNotCloseX", new { x = string.Join(", ", leftOvers.ToArray()) }], Lang["Error"], "toastarea");
            }

            return timeout != 10; // Returns true if timeout wasn't reached.
        }
        #endregion

        #region FILE_OPERATIONS

        /// <summary>
        /// Remove requested account from account switcher (Generic file/ids.json operation)
        /// </summary>
        /// <param name="accName">Basic account name</param>
        /// <param name="platform">Platform string (file safe)</param>
        /// <param name="accNameIsId">Whether the accName is the unique ID in ids.json (false by default)</param>
        public static bool ForgetAccount_Generic(string accName, string platform, bool accNameIsId = false)
        {
            Globals.DebugWriteLine(@"[Func:General\GeneralSwitcherFuncs.ForgetAccount_Generic] Forgetting account: hidden, Platform: " + platform);

            // Remove ID from list of ids
            var idsFile = $"LoginCache\\{platform}\\ids.json";
            if (File.Exists(idsFile))
            {
                var allIds = ReadAllIds_Generic(idsFile);
                if (accNameIsId)
                {
                    var accId = accName;
                    accName = allIds[accName];
                    _ = allIds.Remove(accId);
                }
                else
                    _ = allIds.Remove(allIds.Single(x => x.Value == accName).Key);
                File.WriteAllText(idsFile, JsonConvert.SerializeObject(allIds));
            }

            // Remove cached files
            GeneralFuncs.RecursiveDelete(new DirectoryInfo($"LoginCache\\{platform}\\{accName}"), false);

            // Remove image
            var img = Path.Join(GeneralFuncs.WwwRoot(), $"\\img\\profiles\\{platform}\\{Globals.GetCleanFilePath(accName)}.jpg");
            if (File.Exists(img)) File.Delete(img);

            // Remove from Tray
            Globals.RemoveTrayUser(platform, accName); // Add to Tray list
            return true;
        }

        /// <summary>
        /// Read all ids from requested platform file
        /// </summary>
        /// <param name="idsFile">Full ids.json file path (file safe)</param>
        public static Dictionary<string, string> ReadAllIds_Generic(string idsFile)
        {
            Globals.DebugWriteLine(@"[Func:General\GeneralSwitcherFuncs.ReadAllIds_Generic]");
            var s = JsonConvert.SerializeObject(new Dictionary<string, string>());
            if (!File.Exists(idsFile)) return JsonConvert.DeserializeObject<Dictionary<string, string>>(s);
            try
            {
                s = Globals.ReadAllText(idsFile);
            }
            catch (Exception)
            {
                //
            }

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(s);
        }


        public static string WwwRoot()
        {
            return Path.Join(Globals.UserDataFolder, "\\wwwroot");
        }

        // Overload for below
        public static bool DeletedOutdatedFile(string filename) => DeletedOutdatedFile(filename, 0);

        /// <summary>
        /// Checks if input file is older than 7 days, then deletes if it is
        /// </summary>
        /// <param name="filename">File path to be checked, and possibly deleted</param>
        /// <param name="daysOld">How many days old the file needs to be to be deleted</param>
        /// <returns>Whether file was deleted or not (Outdated or not)</returns>
        public static bool DeletedOutdatedFile(string filename, int daysOld)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.DeletedOutdatedFile] filename={filename.Substring(filename.Length - 8, 8)}, daysOld={daysOld}");
            if (!File.Exists(filename)) return true;
            if (DateTime.Now.Subtract(File.GetLastWriteTime(filename)).Days <= daysOld) return false;
            File.Delete(filename);
            return true;
        }

        /// <summary>
        /// Checks if images is a valid GDI+ image, deleted if not.
        /// </summary>
        /// <param name="filename">File path of image to be checked</param>
        /// <returns>Whether file was deleted, or file was not deleted and was valid</returns>
        public static bool DeletedInvalidImage(string filename)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.DeletedInvalidImage] filename={filename.Substring(filename.Length - 8, 8)}");
            try
            {
                if (File.Exists(filename) && OperatingSystem.IsWindows() && !IsValidGdiPlusImage(filename)) // Delete image if is not as valid, working image.
                {
                    File.Delete(filename);
                    return true;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.Delete(filename);
                    return true;
                }
                catch (Exception)
                {
                    Globals.WriteToLog("Empty profile image detected (0 bytes). Can't delete to re-download.\nInfo: \n" + ex);
                    throw;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if image is a valid GDI+ image
        /// </summary>
        /// <param name="filename">File path of image to be checked</param>
        /// <returns>Whether image is a valid file or not</returns>
        [SupportedOSPlatform("windows")]
        private static bool IsValidGdiPlusImage(string filename)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.IsValidGdiPlusImage] filename={filename.Substring(filename.Length - 8, 8)}");
            //From https://stackoverflow.com/questions/8846654/read-image-and-determine-if-its-corrupt-c-sharp
            try
            {
                using var bmp = new System.Drawing.Bitmap(filename);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void JsDestNewline(string jsDest)
        {
            if (string.IsNullOrEmpty(jsDest)) return;
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.JsDestNewline] jsDest={jsDest}");
            _ = AppData.InvokeVoidAsync(jsDest, "<br />"); //Newline
        }

        // Overload for below
        public static void DeleteFile(string file, string jsDest) => DeleteFile(new FileInfo(file), jsDest);

        /// <summary>
        /// Deletes a single file
        /// </summary>
        /// <param name="f">(Optional) FileInfo of file to delete</param>
        /// <param name="jsDest">Place to send responses (if any)</param>
        public static void DeleteFile(FileInfo f, string jsDest)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.DeleteFile] file={f?.FullName ?? ""}{(jsDest != "" ? ", jsDest=" + jsDest : "")}");
            try
            {
                if (f is { Exists: false } && !string.IsNullOrEmpty(jsDest)) _ = AppData.InvokeVoidAsync(jsDest, "File not found: " + f.FullName);
                else
                {
                    if (f == null) return;
                    f.IsReadOnly = false;
                    f.Delete();
                    if (!string.IsNullOrEmpty(jsDest))
                        _ = AppData.InvokeVoidAsync(jsDest, "Deleted: " + f.FullName);
                }
            }
            catch (Exception e)
            {
                if (string.IsNullOrEmpty(jsDest)) return;
                if (f != null) _ = AppData.InvokeVoidAsync(jsDest, Lang["CouldntDeleteX", new { x = f.FullName }]);
                else _ = AppData.InvokeVoidAsync(jsDest, Lang["CouldntDeleteUndefined"]);
                _ = AppData.InvokeVoidAsync(jsDest, e.ToString());
                JsDestNewline(jsDest);
            }
        }

        // Overload for below
        public static void ClearFolder(string folder) => ClearFolder(folder, "");

        /// <summary>
        /// Shorter RecursiveDelete (Sets keep folders to true)
        /// </summary>
        public static void ClearFolder(string folder, string jsDest)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.ClearFolder] folder={folder}, jsDest={jsDest}");
            RecursiveDelete(new DirectoryInfo(folder), true, jsDest);
        }

        // Overload for below
        public static void RecursiveDelete(DirectoryInfo baseDir, bool keepFolders) => RecursiveDelete(baseDir, keepFolders, "");

        /// <summary>
        /// Recursively delete files in folders (Choose to keep or delete folders too)
        /// </summary>
        /// <param name="baseDir">Folder to start working inwards from (as DirectoryInfo)</param>
        /// <param name="keepFolders">Set to False to delete folders as well as files</param>
        /// <param name="jsDest">Place to send responses (if any)</param>
        public static void RecursiveDelete(DirectoryInfo baseDir, bool keepFolders, string jsDest)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.RecursiveDelete] baseDir={baseDir.Name}, jsDest={jsDest}");
            if (!baseDir.Exists)
                return;

            foreach (var dir in baseDir.EnumerateDirectories())
            {
                RecursiveDelete(dir, keepFolders, jsDest);
            }
            var files = baseDir.GetFiles();
            foreach (var file in files)
            {
                DeleteFile(file, jsDest);
            }

            if (keepFolders) return;
            baseDir.Delete();
            if (!string.IsNullOrEmpty(jsDest)) _ = AppData.InvokeVoidAsync(jsDest, Lang["DeletingFolder"] + baseDir.FullName);
            JsDestNewline(jsDest);
        }

        /// <summary>
        /// Deletes registry keys
        /// </summary>
        /// <param name="subKey">Subkey to delete</param>
        /// <param name="val">Value to delete</param>
        /// <param name="jsDest">Place to send responses (if any)</param>
        [SupportedOSPlatform("windows")]
        public static void DeleteRegKey(string subKey, string val, string jsDest)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.DeleteRegKey] subKey={subKey}, val={val}, jsDest={jsDest}");
            using var key = Registry.CurrentUser.OpenSubKey(subKey, true);
            if (key == null)
                _ = AppData.InvokeVoidAsync(jsDest, Lang["Reg_DoesntExist", new { subKey }]);
            else if (key.GetValue(val) == null)
                _ = AppData.InvokeVoidAsync(jsDest, Lang["Reg_DoesntContain", new { subKey, val }]);
            else
            {
                _ = AppData.InvokeVoidAsync(jsDest, Lang["Reg_Removing", new { subKey, val }]);
                key.DeleteValue(val);
            }
            JsDestNewline(jsDest);
        }

        /// <summary>
        /// Returns a string array of files in a folder, based on a SearchOption.
        /// </summary>
        /// <param name="sourceFolder">Folder to search for files in</param>
        /// <param name="filter">Filter for files in folder</param>
        /// <param name="searchOption">Option: ie: Sub-folders, TopLevel only etc.</param>
        private static IEnumerable<string> GetFiles(string sourceFolder, string filter, SearchOption searchOption)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.GetFiles] sourceFolder={sourceFolder}, filter={filter}");
            var alFiles = new ArrayList();
            var multipleFilters = filter.Split('|');
            foreach (var fileFilter in multipleFilters)
                alFiles.AddRange(Directory.GetFiles(sourceFolder, fileFilter, searchOption));

            return (string[])alFiles.ToArray(typeof(string));
        }

        /// <summary>
        /// Deletes all files of a specific type in a directory.
        /// </summary>
        /// <param name="folder">Folder to search for files in</param>
        /// <param name="extensions">Extensions of files to delete</param>
        /// <param name="so">SearchOption of where to look for files</param>
        /// <param name="jsDest">Place to send responses (if any)</param>
        public static void ClearFilesOfType(string folder, string extensions, SearchOption so, string jsDest)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.ClearFilesOfType] folder={folder}, extensions={extensions}, jsDest={jsDest}");
            if (!Directory.Exists(folder))
            {
                _ = AppData.InvokeVoidAsync(jsDest, Lang["DirectoryNotFound", new { folder }]);
                JsDestNewline(jsDest);
                return;
            }
            foreach (var file in GetFiles(folder, extensions, so))
            {
                _ = AppData.InvokeVoidAsync(jsDest, Lang["DeletingFile", new { file }]);
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _ = AppData.InvokeVoidAsync(jsDest, Lang["ErrorDetails", new { ex }]);
                }
            }
            JsDestNewline(jsDest);
        }

        /// <summary>
        /// Gets a file's MD5 Hash
        /// </summary>
        /// <param name="filePath">Path to file to get hash of</param>
        /// <returns></returns>
        public static string GetFileMd5(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            return stream.Length != 0 ? BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() : "0";
        }

        public static string ReadOnlyReadAllText(string f)
        {
            var text = "";
            using var stream = File.Open(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                text += reader.ReadLine() + Environment.NewLine;
            }

            return text;
        }


        /// <summary>
        /// Converts file length to easily read string.
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        public static string FileSizeString(double len)
        {
            if (len < 0.001) return "0 bytes";
            string[] sizes = { "B", "KB", "MB", "GB" };
            var n2 = (int)Math.Log10(len) / 3;
            var n3 = len / Math.Pow(1e3, n2);
            return $"{n3:0.##} {sizes[n2]}";
        }
        #endregion

        #region SETTINGS
        // Overload for below
        public static void SaveSettings(string file, JObject joNewSettings) => SaveSettings(file, joNewSettings, false);
        /// <summary>
        /// Saves input JObject of settings to input file path
        /// </summary>
        /// <param name="file">File path to save JSON string to</param>
        /// <param name="joNewSettings">JObject of settings to be saved</param>
        /// <param name="mergeNewIntoOld">True merges old with new settings, false merges new with old</param>
        public static void SaveSettings(string file, JObject joNewSettings, bool mergeNewIntoOld)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.SaveSettings] file={file}, joNewSettings=hidden, mergeNewIntoOld={mergeNewIntoOld}");
            var sFilename = file.EndsWith(".json") ? file : file + ".json";

            // Create folder if it doesn't exist:
            var folder = Path.GetDirectoryName(file);
            if (folder != "") _ = Directory.CreateDirectory(folder ?? string.Empty);

            // Get existing settings
            var joSettings = new JObject();
            if (File.Exists(sFilename))
                try
                {
                    joSettings = JObject.Parse(Globals.ReadAllText(sFilename));
                }
                catch (Exception ex)
                {
                    Globals.WriteToLog(ex.ToString());
                }

            if (mergeNewIntoOld)
            {
                // Merge new settings with existing settings --> Adds missing variables etc
                joNewSettings.Merge(joSettings, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union
                });
                // Save all settings back into file
                File.WriteAllText(sFilename, joNewSettings.ToString());
            }
            else
            {
                // Merge existing settings with settings from site
                joSettings.Merge(joNewSettings, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Replace
                });
                // Save all settings back into file
                File.WriteAllText(sFilename, joSettings.ToString());
            }
        }

        /// <summary>
        /// Saves input JArray of items to input file path
        /// </summary>
        /// <param name="file">File path to save JSON string to</param>
        /// <param name="joOrder">JArray order of items on page</param>
        public static void SaveOrder(string file, JArray joOrder)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.SaveOrder] file={file}, joOrder=hidden");
            var sFilename = file.EndsWith(".json") ? file : file + ".json";

            // Create folder if it doesn't exist:
            var folder = Path.GetDirectoryName(file);
            if (folder != "") _ = Directory.CreateDirectory(folder ?? string.Empty);

            File.WriteAllText(sFilename, joOrder.ToString());
        }

        // Overload for below
        public static JObject LoadSettings(string file) => LoadSettings(file, null);

        /// <summary>
        /// Loads settings from input file (JSON string to JObject)
        /// </summary>
        /// <param name="file">JSON file to be read</param>
        /// <param name="defaultSettings">(Optional) Default JObject, for merging in missing parameters</param>
        /// <returns>JObject created from file</returns>
        public static JObject LoadSettings(string file, JObject defaultSettings)
        {
            Globals.DebugWriteLine($@"[Func:General\GeneralFuncs.LoadSettings] file={file}, defaultSettings=hidden");
            var sFilename = file.EndsWith(".json") ? file : file + ".json";
            if (!File.Exists(sFilename)) return defaultSettings ?? new JObject();

            var fileSettingsText = Globals.ReadAllLines(sFilename);
            if (fileSettingsText.Length == 0 && defaultSettings != null)
            {
                File.WriteAllText(sFilename, defaultSettings.ToString());
                return defaultSettings;
            }

            var fileSettings = new JObject();
            var tryAgain = true;
            var handledError = false;
            while (tryAgain)
                try
                {
                    fileSettings = JObject.Parse(string.Join(Environment.NewLine, fileSettingsText));
                    tryAgain = false;
                    if (handledError)
                        File.WriteAllLines(sFilename, fileSettingsText);
                }
                catch (Newtonsoft.Json.JsonReaderException e)
                {
                    if (handledError) // Only try once
                    {
                        Globals.WriteToLog(e.ToString());

                        // Reset file:
                        var errFile = sFilename.Replace(".json", "_err.json");
                        if (File.Exists(errFile)) File.Delete(errFile);
                        File.Move(sFilename, errFile);

                        File.WriteAllText("LastError.txt", "LAST CRASH DETAILS:\nThe following file appears to be corrupt:" + sFilename + "\nThe file was reset. Check the CrashLogs folder for more details.");
                        throw;
                    }

                    // Possible error: Fixes single slashes in string, where there should be double.
                    for (var i = 0; i < fileSettingsText.Length; i++)
                        if (fileSettingsText[i].Contains("FolderPath"))
                            fileSettingsText[i] = Regex.Replace(fileSettingsText[i], @"(?<=[^\\])(\\)(?=[^\\])", @"\\");
                    // Other fixes go here
                    handledError = true;
                }
                catch (Exception e)
                {
                    Globals.WriteToLog(e.ToString());
                    throw;
                }

            if (defaultSettings == null) return fileSettings;

            var addedKey = false;
            // Add missing keys from default
            foreach (var (key, value) in defaultSettings)
            {
                if (fileSettings.ContainsKey(key)) continue;
                fileSettings[key] = value;
                addedKey = true;
            }
            // Save all settings back into file
            if (addedKey) File.WriteAllText(sFilename, fileSettings.ToString());
            return fileSettings;
        }

        //public static JObject SortJObject(JObject joIn)
        //{
        //    return new JObject( joIn.Properties().OrderByDescending(p => p.Name) );
        //}

        #endregion

        #region WINDOW SETTINGS
        private static readonly AppSettings AppSettings = AppSettings.Instance;
        public static bool WindowSettingsValid()
        {
            Globals.DebugWriteLine(@"[Func:General\GeneralFuncs.WindowSettingsValid]");
            _ = AppSettings.LoadFromFile();
            return true;
        }
        #endregion

        #region OTHER
        /// <summary>
        /// Replaces last occurrence of string in string
        /// </summary>
        /// <param name="input">String to modify</param>
        /// <param name="sOld">String to find (and replace)</param>
        /// <param name="sNew">New string to input</param>
        /// <returns></returns>
        public static string ReplaceLast(string input, string sOld, string sNew)
        {
            var lastIndex = input.LastIndexOf(sOld, StringComparison.Ordinal);
            var lastIndexEnd = lastIndex + sOld.Length;
            return input[..lastIndex] + sNew + input[lastIndexEnd..];
        }

        /// <summary>
        /// Escape text to be used as text inside HTML elements, using innerHTML
        /// </summary>
        /// <param name="text">String to escape</param>
        /// <returns>HTML escaped string</returns>
        public static string EscapeText(string text)
        {
            return text.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&#34;")
                .Replace("'", "&#39;")
                .Replace("/", "&#47;");
        }
        #endregion

        #region SWITCHER_FUNCTIONS

        public static async Task HandleFirstRender(bool firstRender, string platform)
        {
            AppData.Instance.WindowTitle = Lang["Title_AccountsList", new { platform }];
            if (firstRender)
            {
                // Handle Streamer Mode notification
                if (AppSettings.Instance.StreamerModeEnabled && AppSettings.Instance.StreamerModeTriggered)
                    _ = GeneralInvocableFuncs.ShowToast("info", Lang["Toast_StreamerModeHint"], Lang["Toast_StreamerModeTitle"], "toastarea");

                // Handle loading accounts for specific platforms
                // - Init file if it doesn't exist, or isn't fully initialised (adds missing settings when true)
                switch (platform)
                {
                    case null:
                        return;

                    case "BattleNet":
                        await BattleNet.BattleNetSwitcherFuncs.LoadProfiles();
                        Data.Settings.BattleNet.Instance.SaveSettings(!File.Exists(Data.Settings.BattleNet.SettingsFile));
                        break;

                    case "Discord":
                        Discord.DiscordSwitcherFuncs.LoadProfiles();
                        Data.Settings.Discord.Instance.SaveSettings(!File.Exists(Data.Settings.Discord.SettingsFile));
                        break;

                    case "Epic Games":
                        Epic.EpicSwitcherFuncs.LoadProfiles();
                        Data.Settings.Epic.Instance.SaveSettings(!File.Exists(Data.Settings.Epic.SettingsFile));
                        break;

                    case "Origin":
                        Origin.OriginSwitcherFuncs.LoadProfiles();
                        Data.Settings.Origin.Instance.SaveSettings(!File.Exists(Data.Settings.Origin.SettingsFile));
                        break;

                    case "Riot Games":
                        Riot.RiotSwitcherFuncs.LoadProfiles();
                        Data.Settings.Riot.Instance.SaveSettings(!File.Exists(Data.Settings.Riot.SettingsFile));
                        break;

                    case "Steam":
                        await Steam.SteamSwitcherFuncs.LoadProfiles();
                        Data.Settings.Steam.Instance.SaveSettings(!File.Exists(Data.Settings.Steam.SettingsFile));
                        break;

                    case "Ubisoft":
                        await Ubisoft.UbisoftSwitcherFuncs.LoadProfiles();
                        Data.Settings.Ubisoft.Instance.SaveSettings(!File.Exists(Data.Settings.Ubisoft.SettingsFile));
                        break;

                    default:
                        Basic.BasicSwitcherFuncs.LoadProfiles();
                        Data.Settings.Basic.Instance.SaveSettings( !File.Exists(CurrentPlatform.Instance.FullName));
                        break;
                }

                // Handle queries and invoke status "Ready"
                _ = HandleQueries();
                _ = AppData.InvokeVoidAsync("updateStatus", Lang["Done"]);
            }
        }

        /// <summary>
        /// For handling queries in URI
        /// </summary>
        public static bool HandleQueries()
        {
            Globals.DebugWriteLine(@"[JSInvoke:General\GeneralFuncs.HandleQueries]");
            var uri = AppData.ActiveNavMan.ToAbsoluteUri(AppData.ActiveNavMan.Uri);
            // Clear cache reload
            var queries = QueryHelpers.ParseQuery(uri.Query);
            // cacheReload handled in JS

            //Modal
            if (queries.TryGetValue("modal", out var modalValue))
                foreach (var stringValue in modalValue) _ = GeneralInvocableFuncs.ShowModal(Uri.UnescapeDataString(stringValue));

            // Toast
            if (!queries.TryGetValue("toast_type", out var toastType) ||
                !queries.TryGetValue("toast_title", out var toastTitle) ||
                !queries.TryGetValue("toast_message", out var toastMessage)) return true;
            for (var i = 0; i < toastType.Count; i++)
            {
                try
                {
                    _ = GeneralInvocableFuncs.ShowToast(toastType[i], toastMessage[i], toastTitle[i], "toastarea");
                    _ = AppData.InvokeVoidAsync("removeUrlArgs", "toast_type,toast_title,toast_message");
                }
                catch (TaskCanceledException e)
                {
                    Globals.WriteToLog(e.ToString());
                }
            }

            return true;
        }


        #endregion
    }

    public class ProcessHandler
    {
        /// <summary>
        /// Start a program
        /// </summary>
        /// <param name="fileName">Path to file</param>
        /// <param name="elevated">Whether program should be elevated</param>
        /// <param name="args">Arguments for program</param>
        public static void StartProgram(string fileName, bool elevated, string args = "")
        {
            // This runas.exe program is a temporary workaround for processes closing when this closes.
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = Path.Join(Globals.AppDataFolder, "runas.exe"),
                    Arguments = $"\"{fileName}\" {(elevated ? "1" : "0")} {args}",
                    Verb = elevated ? "runas" : ""
                });
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                if (e.HResult != -2147467259) // Not because it was cancelled by user
                    throw;
            }
        }

        // See unmodified code source from link below:
        // https://stackoverflow.com/a/40501607/5165437
        public static void RunAsDesktopUser(string fileName, string args = "")
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(fileName));

            // Set working directory
            var tempWorkingDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(fileName) ?? Directory.GetCurrentDirectory());

            // To start process as shell user you will need to carry out these steps:
            // 1. Enable the SeIncreaseQuotaPrivilege in your current token
            // 2. Get an HWND representing the desktop shell (GetShellWindow)
            // 3. Get the Process ID(PID) of the process associated with that window(GetWindowThreadProcessId)
            // 4. Open that process(OpenProcess)
            // 5. Get the access token from that process (OpenProcessToken)
            // 6. Make a primary token with that token(DuplicateTokenEx)
            // 7. Start the new process with that primary token(CreateProcessWithTokenW)

            var hProcessToken = IntPtr.Zero;
            // Enable SeIncreaseQuotaPrivilege in this process.  (This won't work if current process is not elevated.)
            try
            {
                var process = GetCurrentProcess();
                if (!OpenProcessToken(process, 0x0020, ref hProcessToken))
                    return;

                var tkp = new TokenPrivileges
                {
                    PrivilegeCount = 1,
                    Privileges = new LuidAndAttributes[1]
                };

                if (!LookupPrivilegeValue(null, "SeIncreaseQuotaPrivilege", ref tkp.Privileges[0].Luid))
                    return;

                tkp.Privileges[0].Attributes = 0x00000002;

                if (!AdjustTokenPrivileges(hProcessToken, false, ref tkp, 0, IntPtr.Zero, IntPtr.Zero))
                    return;
            }
            finally
            {
                CloseHandle(hProcessToken);
            }

            // Get an HWND representing the desktop shell.
            // CAVEATS:  This will fail if the shell is not running (crashed or terminated), or the default shell has been
            // replaced with a custom shell.  This also won't return what you probably want if Explorer has been terminated and
            // restarted elevated.
            var hwnd = GetShellWindow();
            if (hwnd == IntPtr.Zero)
                return;

            var hShellProcess = IntPtr.Zero;
            var hShellProcessToken = IntPtr.Zero;
            var hPrimaryToken = IntPtr.Zero;
            try
            {
                // Get the PID of the desktop shell process.
                if (GetWindowThreadProcessId(hwnd, out var dwPid) == 0)
                    return;

                // Open the desktop shell process in order to query it (get the token)
                hShellProcess = OpenProcess(ProcessAccessFlags.QueryInformation, false, dwPid);
                if (hShellProcess == IntPtr.Zero)
                    return;

                // Get the process token of the desktop shell.
                if (!OpenProcessToken(hShellProcess, 0x0002, ref hShellProcessToken))
                    return;

                const uint dwTokenRights = 395U;

                // Duplicate the shell's process token to get a primary token.
                // Based on experimentation, this is the minimal set of rights required for CreateProcessWithTokenW (contrary to current documentation).
                if (!DuplicateTokenEx(hShellProcessToken, dwTokenRights, IntPtr.Zero, SecurityImpersonationLevel.SecurityImpersonation, TokenType.TokenPrimary, out hPrimaryToken))
                    return;

                // Arguments need a space just before, for some reason.
                if (args.Length > 1 && args[0] != ' ') args = ' ' + args;

                // Start the target process with the new token.
                var si = new StartupInfo();
                var pi = new ProcessInformation();
                if (!CreateProcessWithTokenW(hPrimaryToken, 0, fileName, args, 0, IntPtr.Zero, Path.GetDirectoryName(fileName), ref si, out pi))
                    return;
            }
            finally
            {
                CloseHandle(hShellProcessToken);
                CloseHandle(hPrimaryToken);
                CloseHandle(hShellProcess);
            }

            // Reset working directory
            Directory.SetCurrentDirectory(tempWorkingDir);

        }

        #region Interop

        private struct TokenPrivileges
        {
            public uint PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public LuidAndAttributes[] Privileges;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct LuidAndAttributes
        {
            public Luid Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct Luid
        {
            private readonly uint LowPart;
            private readonly int HighPart;
        }

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        private enum SecurityImpersonationLevel
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        private enum TokenType
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct ProcessInformation
        {
            private readonly IntPtr hProcess;
            private readonly IntPtr hThread;
            private readonly int dwProcessId;
            private readonly int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private readonly struct StartupInfo
        {
            private readonly int cb;
            private readonly string lpReserved;
            private readonly string lpDesktop;
            private readonly string lpTitle;
            private readonly int dwX;
            private readonly int dwY;
            private readonly int dwXSize;
            private readonly int dwYSize;
            private readonly int dwXCountChars;
            private readonly int dwYCountChars;
            private readonly int dwFillAttribute;
            private readonly int dwFlags;
            private readonly int wShowWindow;
            private readonly int cbReserved2;
            private readonly int lpReserved2;
            private readonly int hStdInput;
            private readonly int hStdOutput;
            private readonly int hStdError;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string host, string name, ref Luid pluid);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokenPrivileges newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);


        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, uint processId);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, SecurityImpersonationLevel impersonationLevel, TokenType tokenType, out IntPtr phNewToken);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithTokenW(IntPtr hToken, int dwLogonFlags, string lpApplicationName, string lpCommandLine, int dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref StartupInfo lpStartupInfo, out ProcessInformation lpProcessInformation);

        #endregion
    }
}
