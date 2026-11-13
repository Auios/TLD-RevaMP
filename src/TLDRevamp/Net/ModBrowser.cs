using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Bootstrap;
using TLDRevamp.Multiplayer;

namespace TLDRevamp.Net
{
    internal static class ModBrowser
    {
        internal static List<ModCatalog.Mod> Items()
        {
            var result = new List<ModCatalog.Mod>();
            if (ModCatalog.Current != null) result.AddRange(ModCatalog.Current.Mods);
            try
            {
                foreach (string id in ModActivation.Ids(ModTransfer.InstallDir))
                {
                    if (result.Exists(m => m.Id == id)) continue;
                    try
                    {
                        var files = ModActivation.Files(ModTransfer.InstallDir, id);
                        if (files.Count == 0 && ModActivation.Desired(ModTransfer.InstallDir, id) != "disabled") continue;
                        string v = files.Count == 0 ? "unknown" : Version(files[0], id);
                        result.Add(Local(id, id, v));
                    }
                    catch (Exception e) { Unreadable(id, e); }
                }
            }
            catch (Exception e) { Unreadable("local mod folder", e); }
            foreach (var p in Chainloader.PluginInfos.Values)
                if (!result.Exists(m => m.Id == p.Metadata.GUID)) result.Add(Local(p.Metadata.GUID, p.Metadata.Name, p.Metadata.Version.ToString()));
            // Check catalog and loaded entries too: one bad receipt must not break the whole screen.
            result.RemoveAll(m =>
            {
                if (ModActivation.Core(m.Id)) return false;
                try { ModActivation.Desired(ModTransfer.InstallDir, m.Id); ModActivation.Files(ModTransfer.InstallDir, m.Id); return false; }
                catch (Exception e) { Unreadable(m.Id, e); return true; }
            });
            return result;
        }
        private static void Unreadable(string id, Exception e)
        {
            ModCatalog.Message = "Could not read mod '" + id + "': " + e.Message;
            Plugin.Log.LogWarning(ModCatalog.Message);
        }
        private static ModCatalog.Mod Local(string id, string name, string version) => new ModCatalog.Mod {
            Id = id, Name = name, Description = "Local installation.", Versions = new List<ModCatalog.Release> { new ModCatalog.Release { Version = version } }
        };
        internal static string Version(string file, string id) => Path.GetFileNameWithoutExtension(file).Substring(id.Length + 1);
        private static bool Loaded(string id)
        {
            foreach (var p in Chainloader.PluginInfos.Values) if (p.Metadata.GUID == id) return true;
            return false;
        }
        internal static bool Pending(string id) => ModActivation.Pending(ModTransfer.InstallDir, id, Loaded(id));
        internal static string State(string id) => ModActivation.Status(ModTransfer.InstallDir, id, Loaded(id));
        internal static bool HasDisabled()
        {
            // This is only an optional hint on an existing refusal; preserve that refusal if scanning fails.
            try
            {
                foreach (string id in ModActivation.Ids(ModTransfer.InstallDir))
                    try { if (ModActivation.Desired(ModTransfer.InstallDir, id) == "disabled") return true; }
                    catch (Exception e) { Unreadable(id, e); }
            }
            catch (Exception e) { Unreadable("local mod folder", e); }
            return false;
        }
        internal static string RequiredError(string id, string hash)
        {
            // A failed local check must end the join with a reason, never escape the offer handler.
            try
            {
                return DisabledRequired(id, hash) ? "The host requires mods that are not loaded. You have disabled mods installed: enable them in the mod browser, restart the game, then join again." : null;
            }
            catch (Exception e) { return "Could not check required mod '" + id + "': " + e.Message; }
        }
        internal static bool DisabledRequired(string id, string hash)
        {
            if (!ModActivation.Allowed(id)) return false;
            string managed = ModActivation.Resolve(ModTransfer.InstallDir, id);
            if (ModActivation.Desired(ModTransfer.InstallDir, managed) == "disabled") return true;
            foreach (string candidate in ModActivation.Ids(ModTransfer.InstallDir))
            {
                if (ModActivation.Core(candidate) || ModActivation.Desired(ModTransfer.InstallDir, candidate) != "disabled") continue;
                foreach (string file in ModActivation.Files(ModTransfer.InstallDir, candidate))
                    if (ModTransfer.Sha256(File.ReadAllBytes(file)) == hash) return true;
            }
            return false;
        }
        internal static void Action(string id, string action)
        {
            ModActivation.Validate(id);
            // A hand-installed copy would still load even after moving a managed copy.
            foreach (var p in Chainloader.PluginInfos.Values)
                if (p.Metadata.GUID == id && !ModFiles.Managed(ModTransfer.InstallDir, p.Location, id))
                    throw new IOException("Installed by hand: manage this mod's files outside the browser.");
            var files = ModActivation.Files(ModTransfer.InstallDir, id);
            foreach (var p in MpApi.Mods)
                if (!string.IsNullOrEmpty(p.File) && files.Exists(f => ModFiles.PathEq(f, p.File))) ModActivation.Alias(ModTransfer.InstallDir, id, p.Id);
            ModActivation.Queue(ModTransfer.InstallDir, id, action);
            ModCatalog.RestartRequired = true;
        }
    }
}
