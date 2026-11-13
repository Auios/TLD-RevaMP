using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.MarkPanel;

namespace TLDRevamp
{
    internal static class MarkPanelTools
    {
        internal static string Bridge(string arg, bool raw = false)
        {
            if (!raw && arg.StartsWith("save ", StringComparison.Ordinal)) return Submit(null, arg.Substring(5));
            var parts = arg.Split(new[] { ' ' }, 5);
            if (parts.Length != 5 || parts[0] != "rename" || !double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double st) ||
                !int.TryParse(parts[3], out int origin)) return "{\"error\":\"use save <note> or rename <sid> <st> <origin> <note>\"}";
            string key = MarkFiles.Identity(parts[1], st, origin);
            var mark = MarkFiles.Snapshot.FirstOrDefault(e => e.Key == key);
            if (mark == null || mark.Sid != SessionLog.Sid) return "{\"error\":\"unknown session mark\"}";
            if (!raw) return Submit(mark, parts[4]);
            Net.Reports.RenameMark(mark.Sid, mark.St, mark.Origin, parts[4]); return "{\"ok\":true}";
        }
    }
}
