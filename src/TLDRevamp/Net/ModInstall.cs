using System;
using System.Collections.Generic;
using System.IO;
using TLDRevamp.Multiplayer;

namespace TLDRevamp.Net
{
    /// Verified catalog files are installed only in the managed mod folder.
    public static class ModInstall
    {
        public static int MaxBytes = 16 << 20;
        public static long Installed;
        public static string InstallDir => Path.Combine(BepInEx.Paths.PluginPath, "TLDRevampMods");
        public static string Stats() => "{\"installed\":" + Installed + ",\"installDir\":" + Json.Str(InstallDir) + "}";
        internal static string Sha256(byte[] data) => ModCatalog.Sha256(data);
        internal static string Size(long n) => n >= 1 << 20 ? (n / 1048576.0).ToString("F1") + " MB" : Math.Max(1, n / 1024) + " KB";
        private sealed class Package { internal string Id, Name, Version, Hash; internal byte[] Data; }

        /// Write a verified file into BepInEx/plugins/TLDRevampMods and set aside an older copy of the same mod (it
        /// would load next to the new one). Returns null, or what went wrong.
        private static string Install(Package o)
        {
            try
            {
                string id = ModActivation.Resolve(InstallDir, o.Id);
                foreach (var m in MpApi.Mods)
                    if (m.Id == o.Id && ModFiles.Managed(InstallDir, m.File))
                        foreach (var p in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                            if (ModFiles.PathEq(p.Location, m.File)) id = p.Metadata.GUID;
                var old = ManagedFiles(id);
                foreach (var m in MpApi.Mods)
                    if (m.Id == o.Id && ModFiles.Managed(InstallDir, m.File) && !old.Contains(m.File)) old.Add(m.File);
                string error = ModFiles.Install(InstallDir, id, o.Version, o.Data, old);
                if (error != null) return o.Name + " " + o.Version + ": " + error;
                Installed++;
                Plugin.Log.LogInfo($"mods: installed {o.Name} {o.Version} (sha256 {MpApi.Short(o.Hash)})");
                return null;
            }
            catch (Exception e) { return $"{o.Name} could not be saved: {e.Message}"; }
        }

        internal static string Install(string id, string name, string version, string hash, byte[] bytes) =>
            Install(new Package { Id = id, Name = name, Version = version, Hash = hash, Data = bytes });

        internal static List<string> ManagedFiles(string id)
        {
            if (ModActivation.Core(id)) return new List<string>();
            return ModActivation.Files(InstallDir, id);
        }

        internal static void Remove(string file) => ModFiles.Remove(InstallDir, file);
    }
}
