using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TLDRevamp.Fixes
{
    /// partconditionscript (the rust/paint state of every car part) has an Update that does something only while
    /// `refresh` is set or `repairValue` is not 0 (a sponge/brush stroke fading out). No game code sets `refresh` (only
    /// prefabs can); `repairValue` is written only by its own Repair(). A car park of 16 cars has ~850 of them, each called
    /// by Unity every frame for nothing.
    ///
    /// The first Update that would do nothing switches the component off (Behaviour.enabled = false) instead; Repair()
    /// switches it on again as soon as it leaves a value to fade. Once a second the sleeping ones are checked for a
    /// `refresh` or `repairValue` some other code set (counted in `missed`, and woken). The component is otherwise
    /// untouched: Refresh/Paint/Repair/collision messages work on a disabled component, nothing in the game reads its
    /// `enabled`.
    ///
    /// A copy of a sleeping part (Instantiate of a live object) is disabled too; it has `started`, no `refresh` and no
    /// `repairValue` (all copied), so it is a sleeping part as well, and Repair wakes it the same way.
    ///
    /// Car park (physbench --cars, 16 cars, 2 passes each): settled 77.8 → 80.0 fps, p99 17.75 → 17.05 ms; missed 0.
    ///
    /// Runtime switch (bridge `set`): TLDRevamp.Fixes.PartConditionIdle.Enabled (off wakes all of them);
    /// `call TLDRevamp.Fixes.PartConditionIdle.Probe` / `.ProbeAfter`: a repair stroke on a sleeping part, then its fade.
    [HarmonyPatch]
    public static class PartConditionIdle
    {
        internal static bool Sleeping(partconditionscript part) => Asleep.Contains(part);
        public static bool Enabled = true;
        public static long Sleeps, Wakes, Missed;

        private static readonly HashSet<partconditionscript> Asleep = new HashSet<partconditionscript>();
        internal static System.Collections.Generic.IReadOnlyCollection<partconditionscript> ObservedAsleep => Asleep;
        private static readonly List<partconditionscript> Tmp = new List<partconditionscript>();
        private static float _checkAt;

        [HarmonyPrefix, HarmonyPatch(typeof(partconditionscript), "Update")]
        private static bool Update(partconditionscript __instance)
        {
            if (!Enabled || __instance.refresh || __instance.repairValue != 0f) return true;
            __instance.enabled = false;
            Asleep.Add(__instance);
            Sleeps++;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(partconditionscript), nameof(partconditionscript.Repair))]
        private static void Repair(partconditionscript __instance)
        {
            // any disabled one, not just ours: a copy of a sleeping part (Instantiate of a live object) is disabled too,
            // with started already set, exactly like a sleeping one; no game code disables these otherwise
            if (__instance.repairValue == 0f || __instance.enabled) return;
            Asleep.Remove(__instance);
            Wake(__instance);
        }

        private static void Wake(partconditionscript p)
        {
            if (p != null) p.enabled = true;
            Wakes++;
        }

        public static void Tick()
        {
            if (Asleep.Count == 0) return;
            if (Enabled && Time.unscaledTime < _checkAt) return;
            _checkAt = Time.unscaledTime + 1f;
            Tmp.Clear();
            foreach (var p in Asleep)
            {
                if (p == null) { Tmp.Add(p); continue; }
                if (!Enabled) { Tmp.Add(p); continue; }
                if (p.refresh || p.repairValue != 0f) { Tmp.Add(p); Missed++; }
                else if (p.enabled) Tmp.Add(p);   // someone else switched it on: theirs now
            }
            foreach (var p in Tmp)
            {
                Asleep.Remove(p);
                if ((object)p != null && p != null && !p.enabled) Wake(p);
            }
            Tmp.Clear();
        }

        public static string Stats =>
            "{\"enabled\":" + (Enabled ? "true" : "false") + ",\"asleep\":" + Asleep.Count + ",\"sleeps\":" + Sleeps +
            ",\"wakes\":" + Wakes + ",\"missed\":" + Missed + "}";
    }
}
