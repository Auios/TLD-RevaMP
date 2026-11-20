using System;
using System.Collections.Generic;

namespace TLDRevamp.Net
{
    /// A refusal can carry identities and advice, never executable bytes or download URLs.
    public static class ModsNeeded
    {
        public const int MaxEntries = 256, MaxProblems = 4096, MaxText = 256;
        public sealed class Entry { public string Id, Name, Version, Hash; }
        public sealed class List
        {
            public string Host;
            public readonly List<Entry> Mods = new List<Entry>();
            public readonly List<string> Problems = new List<string>();
        }

        public static void Write(NetWriter w, string host, IList<Entry> mods, IList<string> problems)
        {
            w.Str(host); w.VarU32((uint)mods.Count);
            foreach (var m in mods) { w.Str(m.Id); w.Str(m.Name); w.Str(m.Version); w.Str(m.Hash); }
            w.VarU32((uint)problems.Count);
            foreach (var p in problems) w.Str(p);
        }

        private static string Text(NetReader r, int max)
        {
            // Bound the encoded length before allocating the string. NetWriter adds one for non-null strings.
            int start = r.Pos; uint n = r.VarU32();
            if (r.Bad || n == 0 || n - 1 > max || n - 1 > r.Remaining) { r.Bad = true; return ""; }
            r.Pos = start;
            string value = r.Str();
            foreach (char c in value) if (char.IsControl(c)) { r.Bad = true; break; }
            return value;
        }

        public static List Read(NetReader r)
        {
            var list = new List { Host = Text(r, MaxText) };
            uint n = r.VarU32();
            if (r.Bad || n > MaxEntries) { r.Bad = true; return null; }
            for (uint i = 0; i < n && !r.Bad; i++)
                list.Mods.Add(new Entry { Id = Text(r, 64), Name = Text(r, MaxText), Version = Text(r, 64), Hash = Text(r, 64).ToLowerInvariant() });
            uint problems = r.VarU32();
            if (r.Bad || problems > MaxProblems) { r.Bad = true; return null; }
            for (uint i = 0; i < problems && !r.Bad; i++) list.Problems.Add(Text(r, MaxText));
            if (r.Bad || r.Remaining != 0) { r.Bad = true; return null; }
            return list;
        }

        public enum Action { None, Install, Enable }
        public sealed class Row
        {
            public string Text, Source;
            public Action Do;
            public bool CanEnable;
            public ModCatalog.Mod Mod;
            public ModCatalog.Release Release;
        }

        // API registration IDs can differ from plugin GUIDs. Match the actual DLL hash across the catalog.
        public static ModCatalog.Mod CatalogMod(Entry need, ModCatalog.Catalog catalog) =>
            catalog?.Mods.Find(m => m.Versions.Exists(v => string.Equals(v.Hash, need.Hash, StringComparison.OrdinalIgnoreCase))) ??
            catalog?.Mods.Find(m => m.Id == need.Id);

        /// Local state decides the action; only a catalog release with the identical DLL hash can be installed.
        public static Row Decide(Entry need, ModCatalog.Catalog catalog, string installed, bool disabled, bool manual,
            bool identical, int maxBytes, Func<string, bool> validId)
        {
            var row = new Row { Text = need.Name + " " + need.Version };
            row.Mod = CatalogMod(need, catalog);
            row.Source = row.Mod?.Source;
            row.Release = row.Mod?.Versions.Find(v => string.Equals(v.Hash, need.Hash, StringComparison.OrdinalIgnoreCase) &&
                ModCatalog.ApprovalReason(row.Mod, v, maxBytes, validId) == null);
            if (installed != null && installed != need.Version) row.Text += " (you have " + installed + ")";
            row.CanEnable = disabled && !manual;
            if (disabled && !manual) row.Text += " - installed but disabled";
            if (manual) row.Text += " - replace your copy of " + need.Name + " by hand";
            else if (disabled && identical) row.Do = Action.Enable;
            else if (identical) row.Text += " - installed; restart the game, then join again";
            else if (catalog == null) row.Text += " - mod list unavailable";
            else if (row.Release == null) row.Text += " - not in the mod list: get it from its author";
            else row.Do = Action.Install;
            return row;
        }

        public static Row Problem(string text) => new Row { Text = text.StartsWith("remove ", StringComparison.Ordinal) ? "Remove: " + text.Substring(7) : text };
        public static string Copy(List list, ModCatalog.Catalog catalog)
        {
            var lines = new List<string>();
            foreach (var m in list.Mods)
            {
                string source = CatalogMod(m, catalog)?.Source;
                lines.Add(m.Name + " " + m.Version + (string.IsNullOrEmpty(source) ? "" : " " + source));
            }
            foreach (var p in list.Problems) lines.Add(Problem(p).Text);
            return string.Join("\n", lines);
        }
    }
}
