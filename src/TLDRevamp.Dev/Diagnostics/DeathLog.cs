using System;
using HarmonyLib;

namespace TLDRevamp.Diagnostics
{
    /// Logs every player death with its cause (the caller: fall damage, survival hp, explosion, car crash, a weapon) and
    /// where the player was — farworld run 5 lost the laptop to a death mid-teleport with no trace of why.
    [HarmonyPatch(typeof(fpscontroller), nameof(fpscontroller.Death))]
    public static class DeathLog
    {
        public static long Deaths;

        [HarmonyPrefix]
        private static void Prefix(fpscontroller __instance)
        {
            if (__instance == null || __instance.died || __instance.godModeTillGrounded) return;   // the game ignores it then
            try { if (kaposztaleves.s.settings.god || kaposztaleves.s.settings.fly) return; } catch { /* best effort: settings may be gone; death logging still proceeds */ }
            Deaths++;
            string where = "?";
            try
            {
                var g = mainscript.GlobalFromUnityPos(__instance.transform.position);
                var rb = __instance.RB;
                where = $"global {g.x:F0},{g.y:F0},{g.z:F0} vel {(rb != null ? rb.velocity.magnitude : 0f):F1} m/s godTillGrounded {__instance.godModeTillGrounded}";
            }
            catch { /* best effort: destroyed player objects leave the existing unknown-location fallback */ }
            Plugin.Log.LogWarning("player death #" + Deaths + " at " + where + "\n" + Environment.StackTrace);
        }
    }
}
