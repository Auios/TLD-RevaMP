using System;
using System.Collections.Generic;
using System.IO;
using TLDRevamp.Multiplayer;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class ModsScreen
    {
        private static ModsNeeded.List _needed;
        private static readonly List<ModsNeeded.Row> NeededRows = new List<ModsNeeded.Row>();
        private static readonly Queue<int> _installQueue = new Queue<int>();
        internal static ModsNeeded.List RequiredList => _needed;
        private static bool _checking;
        private static ModCatalog.Catalog JoinCatalog => ModCatalog.CatalogUnavailable ? null : ModCatalog.Current;
        private static long _installedAtOpen;

        internal static bool OpenNeeded(ModsNeeded.List list)
        {
            if (DataFromMenuScript.s != null && !DataFromMenuScript.s.mainmenu && mainscript.s != null && !mainscript.s.pauseMenuOpen)
                mainscript.s.OpenMenuWithEscape();
            OpenTapes();
            if (!IsOpen) return false;
            try
            {
                _needed = list; _installQueue.Clear(); _installedAtOpen = ModInstall.Installed;
                // Refresh even a cached list: a host may already use a release just added to the live catalog.
                ModCatalog.Refresh(); _checking = ModCatalog.Busy;
                Refresh();
                return true;
            }
            catch (Exception e) { Plugin.Log.LogError("ModsScreen: " + e); Close(); return false; }   // the join screen shows the refusal instead
        }

        internal static ModsNeeded.Row NeededRow(ModsNeeded.Entry need)
        {
            try
            {
                var catalogMod = ModsNeeded.CatalogMod(need, JoinCatalog);
                string id = ModActivation.Resolve(ModInstall.InstallDir, catalogMod?.Id ?? need.Id);
                string installed = Installed(new ModCatalog.Mod { Id = id }, out bool manual);
                foreach (var local in MpApi.Mods)
                    if (local.Id == need.Id)
                    {
                        installed = local.Version;
                        if (File.Exists(local.File)) manual |= !ModFiles.Managed(ModInstall.InstallDir, local.File);
                    }
                bool disabled = ModActivation.Desired(ModInstall.InstallDir, id) == "disabled", identical = false;
                foreach (string file in ModInstall.ManagedFiles(id))
                    if (FileHash(file) == need.Hash) identical = true;
                return ModsNeeded.Decide(need, JoinCatalog, installed, disabled, manual, identical, ModInstall.MaxBytes, MpApi.ValidId);
            }
            catch (Exception e) { return new ModsNeeded.Row { Text = need.Name + " " + need.Version + " - could not check local copy: " + e.Message }; }
        }

        private static readonly Dictionary<string, (long Length, DateTime Written, string Hash)> _fileHashes =
            new Dictionary<string, (long, DateTime, string)>(StringComparer.OrdinalIgnoreCase);

        /// The SHA-256 of a local mod file, read again only when its size or write time changed.
        private static string FileHash(string file)
        {
            var info = new FileInfo(file);
            if (_fileHashes.TryGetValue(file, out var c) && c.Length == info.Length && c.Written == info.LastWriteTimeUtc) return c.Hash;
            string hash = ModInstall.Sha256(File.ReadAllBytes(file));
            _fileHashes[file] = (info.Length, info.LastWriteTimeUtc, hash);
            return hash;
        }

        private static void RefreshNeeded()
        {
            _refreshAt = Time.unscaledTime + .25f;
            if (_checking && !ModCatalog.Busy) _checking = false;
            NeededRows.Clear();
            foreach (var need in _needed.Mods) NeededRows.Add(NeededRow(need));
            foreach (var problem in _needed.Problems) NeededRows.Add(ModsNeeded.Problem(problem));
            int count = NeededRows.Count, pages = Math.Max(1, (count + Tiles.Count - 1) / Math.Max(1, Tiles.Count));
            _page = Math.Max(0, Math.Min(_page, pages - 1));
            _title.text = "You need these mods to join " + Esc(_needed.Host) + ":";
            _title.enableAutoSizing = true; _title.fontSizeMin = 18;
            _count.text = (_page + 1) + "/" + pages;
            for (int i = 0; i < Tiles.Count; i++)
            {
                int at = _page * Tiles.Count + i; var tile = Tiles[i]; tile.Go.SetActive(at < count);
                if (at >= count) continue;
                var row = NeededRows[at];
                tile.Name.text = at < _needed.Mods.Count ? Esc(_needed.Mods[at].Name) : "Problem";
                tile.Name.color = Color.white;
                tile.Required.SetActive(true); tile.RequiredText.text = row.Text; tile.RequiredText.color = Color.white;
                tile.Name.enableAutoSizing = true; tile.Name.fontSizeMin = 16;
                tile.Version.text = at < _needed.Mods.Count ? Esc(_needed.Mods[at].Version) : "";
                tile.Action.gameObject.SetActive(row.Do != ModsNeeded.Action.None);
                Label(tile.Action, row.Do == ModsNeeded.Action.Enable ? "Enable" : "Install");
                tile.Action.interactable = !ModCatalog.Busy && _installQueue.Count == 0;
                tile.Enable.gameObject.SetActive(row.CanEnable && row.Do != ModsNeeded.Action.Enable);
                tile.Enable.interactable = tile.Action.interactable;
            }
            _up.gameObject.SetActive(_page > 0); _down.gameObject.SetActive(_page < pages - 1);
            _source.gameObject.SetActive(true); Label(_source, "Copy list"); _source.interactable = true;
            _refresh.gameObject.SetActive(true); Label(_refresh, "Install all");
            _refresh.interactable = !ModCatalog.Busy && NeededRows.Exists(r => r.Do == ModsNeeded.Action.Install);
            _name.text = "Required mods"; _version.text = "";
            foreach (var line in Lines) line.text = "";
            if (_selected >= 0 && _selected < count)
            {
                var row = NeededRows[_selected];
                Lines[0].text = row.Text; Lines[1].text = row.Source ?? "";
            }
            _message.text = NeededStatus(); _message.gameObject.SetActive(true);
            NextNeededInstall();
        }

        /// The status line under the required-mods list (also what the debug bridge reports).
        internal static string NeededStatus()
        {
            string status = _checking ? "Checking the mod list..." : JoinCatalog == null ?
                "The mod list could not be reached. " + ModCatalog.Message : ModCatalog.Progress;
            if (ModInstall.Installed > _installedAtOpen || ModCatalog.RestartRequired) status += "\nRestart the game, then join again.";
            return status;
        }

        internal static void NeededAction(int index)
        {
            if (_needed == null || ModCatalog.Busy || index < 0 || index >= _needed.Mods.Count) return;
            var row = NeededRow(_needed.Mods[index]);
            if (row.Do == ModsNeeded.Action.Install) ModCatalog.Download(row.Mod, row.Release);
            else if (row.Do == ModsNeeded.Action.Enable) NeededEnable(index);
        }

        internal static void NeededEnable(int index)
        {
            if (_needed == null || ModCatalog.Busy || index < 0 || index >= _needed.Mods.Count) return;
            var row = NeededRow(_needed.Mods[index]);
            if (!row.CanEnable) return;
            try
            {
                ModBrowser.Action(ModActivation.Resolve(ModInstall.InstallDir, row.Mod?.Id ?? _needed.Mods[index].Id), "enabled");
                ModCatalog.Message = "Enabled at next start. Restart the game, then join again.";
            }
            catch (Exception e) { ModCatalog.Message = "Could not enable: " + e.Message; }
        }

        internal static void InstallNeededAll()
        {
            if (_needed == null || ModCatalog.Busy) return;
            _installQueue.Clear();
            for (int i = 0; i < _needed.Mods.Count; i++)
                if (NeededRow(_needed.Mods[i]).Do == ModsNeeded.Action.Install) _installQueue.Enqueue(i);
            NextNeededInstall();
        }
        private static void NextNeededInstall()
        {
            while (!ModCatalog.Busy && _installQueue.Count > 0) NeededAction(_installQueue.Dequeue());
        }
        internal static void CopyNeeded()
        {
            if (_needed == null) return;
            GUIUtility.systemCopyBuffer = ModsNeeded.Copy(_needed, ModCatalog.Current);
            ModCatalog.Message = "Copied mod list.";
        }
    }
}
