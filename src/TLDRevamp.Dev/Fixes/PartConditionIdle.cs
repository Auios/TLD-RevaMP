using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Fixes.PartConditionIdle;

namespace TLDRevamp.Fixes
{
    internal static class PartConditionIdleTools
    {

        /// Test: a sponge-sized repair stroke (Repair with gain 0.01, every type until one counts) on a sleeping part.
        public static string Probe()
        {
            foreach (var p in TLDRevamp.Fixes.PartConditionIdle.ObservedAsleep)
            {
                if (p == null || p.state <= 0) continue;
                for (int t = 0; t < 4 && p.repairValue == 0f; t++) p.Repair(t, 0.01f);
                if (p.repairValue == 0f) continue;
                ObservedProbe = p;
                return "{\"part\":" + Json.Str(p.name) + ",\"repairValue\":" + p.repairValue.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"enabled\":" + (p.enabled ? "true" : "false") + ",\"asleep\":" + (TLDRevamp.Fixes.PartConditionIdle.ObservedAsleep.Contains(p) ? "true" : "false") + "}";
            }
            return "{\"error\":\"no sleeping part that takes a repair stroke\"}";
        }

        private static partconditionscript _probe;
        internal static partconditionscript ObservedProbe { get => _probe; set => _probe = value; }

        /// Test: the probed part afterwards (its stroke faded out by its own Update, then asleep again).
        public static string ProbeAfter()
        {
            var p = _probe;
            if (p == null) return "{\"error\":\"no probe\"}";
            return "{\"part\":" + Json.Str(p.name) + ",\"repairValue\":" + p.repairValue.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                   ",\"enabled\":" + (p.enabled ? "true" : "false") + ",\"asleep\":" + (Sleeping(p) ? "true" : "false") + "}";
        }
    }
}
