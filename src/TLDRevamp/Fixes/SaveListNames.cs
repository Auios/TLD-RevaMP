using System;
using System.Collections.Generic;

namespace TLDRevamp.Fixes
{
    /// Which files in the Saves folder are saves, by the game's own naming (pathscript.saveDataPreName + name +
    /// saveDataFormat: TLDsaveData_<name>.tlds). No Unity here, so tools/unit/SaveListNamesTest runs it as it is.
    ///
    /// The game's load screen takes every file whose path merely CONTAINS "TLDsaveData_" and ".tlds", so the files RevaMP
    /// keeps next to a save (TLDsaveData_<name>.tlds.leases / .players / .tune, Entities.Leases.SideFiles) showed up as
    /// extra saves called "<name>.tlds" with no picture. A save is a file whose last extension is exactly ".tlds".
    internal static class SaveListNames
    {
        internal const string Prefix = "TLDsaveData_", Format = ".tlds";
        private static readonly char[] Separators = { '/', '\\' };

        /// The save's name for a file in the Saves folder, or null when the file is no save (a side file, a thumbnail, a
        /// properties file). Matches the game's own checks (ordinal Contains), plus the extension rule.
        internal static string SaveNameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string file = path.Substring(path.LastIndexOfAny(Separators) + 1);   // both kinds, whatever the OS says
            if (!file.StartsWith(Prefix, StringComparison.Ordinal) || !file.EndsWith(Format, StringComparison.Ordinal)) return null;
            if (file.Length == Prefix.Length + Format.Length) return null;   // "TLDsaveData_.tlds": no name
            return file.Substring(Prefix.Length, file.Length - Prefix.Length - Format.Length);
        }

        /// The names of the saves among `paths`.
        internal static HashSet<string> SaveNames(IEnumerable<string> paths)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in paths) { var n = SaveNameOf(p); if (n != null) names.Add(n); }
            return names;
        }

        /// The indexes in `listed` (names, in the game's order) to drop: names with no save of their own, and a name
        /// listed a second time (a side file of save "x" is named "x.tlds", the same as a save someone called "x.tlds").
        internal static List<int> Drop(IList<string> listed, HashSet<string> saves)
        {
            var drop = new List<int>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < listed.Count; i++)
                if (listed[i] == null || !saves.Contains(listed[i]) || !seen.Add(listed[i])) drop.Add(i);
            return drop;
        }
    }
}
