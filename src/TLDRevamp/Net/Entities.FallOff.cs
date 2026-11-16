namespace TLDRevamp.Net
{
    internal static partial class EntityLifetimeStreaming
    {
        [HarmonyLib.HarmonyPatch(typeof(attachablescript), nameof(attachablescript.FallOFf))]
        private static class NoProxyFallOff
        {
            [HarmonyLib.HarmonyPrefix]
            private static bool Prefix(attachablescript __instance) { if (!Entities.LifetimePartIsProxy(__instance)) return true; Entities.RecordLifetimeFallOff(); return false; }
        }
    }
}
