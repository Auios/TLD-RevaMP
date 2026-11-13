using System;
using HarmonyLib;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static bool BusControl(tosaveitemscript it, usablescript u)
        {
            var bus = it.busdoorscript;
            if (bus == null || bus.doors == null) return false;
            foreach (var d in bus.doors)
                if (d != null && (Has(d.UBoth) || Has(d.Uopen) || Has(d.Uclose) || Has(d.Usign))) return true;
            return false;
            bool Has(usablescript[] controls) => controls != null && Array.IndexOf(controls, u) >= 0;
        }

        /// SendMulti is gated by vanilla Steam networking; observe the consumed buttons in RevaMP too.
        [HarmonyPatch(typeof(busdoorscripts), "Update")]
        private static class BusDoorChanged
        {
            [HarmonyPrefix]
            private static void Prefix(busdoorscripts __instance, out bool __state)
            { __state = InSession && !_applyingState && !__instance.noMultiSync && __instance.Electronics(); }

            [HarmonyPostfix]
            private static void Postfix(busdoorscripts __instance, bool __state)
            {
                if (__state && __instance.somethingChanged) MarkEdit(__instance.tosave, kind: "busdoor");
            }
        }
    }
}
