using System.Linq;
using UnityEngine;
using static TLDRevamp.Net.PlayerActs;

namespace TLDRevamp.Net
{
    internal static class PlayerActsTools
    {
        /// Test: the hum / pee key held (inputscript.i.hum / .piss, set where the keyboard's input lands).
        public static bool TestHum = default, TestPiss = default, TestSleep = default;

        [HarmonyLib.HarmonyPatch(typeof(inputscript), "Update")]
        internal static class TestKeys
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(inputscript __instance)
            {
                if (TestHum) __instance.hum = true;
                if (TestPiss) __instance.piss = true;
                if (TestSleep) __instance.sleep = true;
            }
        }

        /// Bridge `mp acts`: what the local player does, and what every remote model shows.
        public static string Probe(string cmd = "")
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            // test: something to pee (a fresh world's bladder can be empty)
            if (cmd == "fill" && pl != null && pl.piss != null && pl.piss.Tank != null) pl.piss.Tank.F.ChangeOne(pl.piss.Tank.F.maxC * 0.8f, mainscript.fluidenum.water);
            string local = "null";
            if (pl != null)
            {
                var pc = pl.piss;
                local = "{\"humPlaying\":" + (pl.humSource != null && pl.humSource.isPlaying ? "true" : "false") +
                        ",\"humVol\":" + (pl.humSource != null ? pl.humSource.volume.ToString("F2", ic) : "0") +
                        ",\"pissEmit\":" + (pc != null && pc.ps != null && pc.ps.emission.enabled ? "true" : "false") +
                        ",\"pissParticles\":" + (pc != null && pc.ps != null ? pc.ps.particleCount : 0) +
                        ",\"pissSound\":" + (pc != null && pc.SSource != null && pc.SSource.isPlaying ? "true" : "false") +
                        ",\"bladder\":" + (pc != null && pc.Tank != null && pc.Tank.F != null ? pc.Tank.F.GetAmount().ToString("F2", ic) : "-1") + "}";
            }
            var rows = new System.Collections.Generic.List<string>();
            foreach (var s in Object.FindObjectsOfType<Show>())
                rows.Add("{\"model\":" + Json.Str(s.name) + ",\"fresh\":" + (s.Last != null && Time.time - s.At < 0.5f ? "true" : "false") +
                         ",\"humPlaying\":" + (s.HumSrc != null && s.HumSrc.isPlaying ? "true" : "false") + ",\"humVol\":" + (s.HumSrc != null ? s.HumSrc.volume.ToString("F2", ic) : "0") +
                         ",\"humClip\":" + Json.Str(s.HumSrc != null && s.HumSrc.clip != null ? s.HumSrc.clip.name : "") +
                         ",\"pissEmit\":" + (s.Ps != null && s.Ps.emission.enabled ? "true" : "false") + ",\"pissParticles\":" + (s.Ps != null ? s.Ps.particleCount : 0) +
                         ",\"pourPlaying\":" + (s.PourSrc != null && s.PourSrc.isPlaying ? "true" : "false") + "}");
            return "{\"sent\":" + Sent + ",\"applied\":" + Applied + ",\"local\":" + local + ",\"models\":[" + string.Join(",", rows) + "]}";
        }
    }
}
