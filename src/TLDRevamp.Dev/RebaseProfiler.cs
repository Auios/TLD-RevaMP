using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace TLDRevamp
{
    /// Measurement: time each visszarako.Visszarakas(dir) (the floating-origin rebase moving one object) and group by
    /// object name, with child transform / collider / rigidbody counts. Bridge: `rebaseprof start|stop`.
    [HarmonyPatch]
    public static class RebaseLab
    {
        public static bool On;
        private sealed class G { public long Ticks, Max; public int N, Transforms, Colliders, Bodies, Moved; }
        private static readonly Dictionary<string, G> Groups = new Dictionary<string, G>();
        private static long _total;

        [HarmonyPatch(typeof(visszarako), nameof(visszarako.Visszarakas), new[] { typeof(Vector3) })]
        [HarmonyPrefix] private static void Pre(out long __state) { __state = On && PerObject ? System.Diagnostics.Stopwatch.GetTimestamp() : 0; }

        [HarmonyPatch(typeof(visszarako), nameof(visszarako.Visszarakas), new[] { typeof(Vector3) })]
        [HarmonyPostfix]
        private static void Post(visszarako __instance, long __state)
        {
            if (!On || __state == 0) return;
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - __state;
            _total += dt;
            string key = System.Text.RegularExpressions.Regex.Replace(__instance.transform.root.name, @" \(\d+\)|\(Clone\)", "");
            if (!Groups.TryGetValue(key, out var g))
            {
                Groups[key] = g = new G();
                g.Transforms = __instance.GetComponentsInChildren<Transform>(true).Length;
                g.Colliders = __instance.GetComponentsInChildren<Collider>(true).Length;
                g.Bodies = __instance.GetComponentsInChildren<Rigidbody>(true).Length;
            }
            g.N++; g.Ticks += dt; if (dt > g.Max) g.Max = dt;
            if (__instance.transform.parent == null || __instance.moveWhenParented) g.Moved++;
        }

        // Time Physics.SyncTransforms() right after the game's rebase (measurement: splits "loop" from "physics sync").
        public static bool TimeSync = true;
        public static bool PerObject = true;   // per-object timing (slow: regex per call); off → only loop + sync timing
        public static long Checks, Shifts;
        public static double LastLoopMs, LastSyncMs, LastCheckMs, MaxShiftMs;
        private static Vector3d _originBefore;
        public static int BigRebases;
        private static long _loopT0;

        public static bool PauseParticles = false; // measurement: pause playing particle systems across the rebase frame
        private static readonly List<ParticleSystem> Paused = new List<ParticleSystem>();
        private static bool _resumeNextFrame;

        [HarmonyPatch(typeof(mainscript), nameof(mainscript.VisszaRakas))]
        [HarmonyPrefix, HarmonyPriority(Priority.First)] private static void RebasePre()
        {
            _loopT0 = System.Diagnostics.Stopwatch.GetTimestamp();
            _originBefore = mainscript.s != null ? mainscript.s.visszarakva : default;
            Checks++;
            // a shift far beyond the game's normal ~4.5 km (a teleport) — logged with the player's state: farworld runs
            // ended with origins 1,700 km off after a 200 km trip home (the body held still: not a flying player)
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl != null && pl.transform.position.sqrMagnitude > 1e8f)
                Plugin.Log.LogWarning($"big rebase #{BigRebases++}: player at {pl.transform.position} parent {(pl.transform.parent != null ? pl.transform.parent.name : "none")} " +
                                      $"rb {(pl.RB != null ? pl.RB.position.ToString() + (pl.RB.isKinematic ? " kin" : "") : "none")} origin before {mainscript.s.visszarakva.x:F0},{mainscript.s.visszarakva.y:F0},{mainscript.s.visszarakva.z:F0}");
            if (PauseParticles && mainscript.s != null && mainscript.s.player != null && mainscript.s.player.transform.position.sqrMagnitude > 6250000f)
            {
                foreach (var ps in Object.FindObjectsOfType<ParticleSystem>()) if (ps.isPlaying) { ps.Pause(false); Paused.Add(ps); }
                _resumeNextFrame = true;
            }
        }

        /// From the Runner (next frame's Update): resume what we paused.
        public static void Tick()
        {
            if (!_resumeNextFrame) return;
            _resumeNextFrame = false;
            foreach (var ps in Paused) if (ps != null) ps.Play(false);
            Paused.Clear();
        }

        [HarmonyPatch(typeof(mainscript), nameof(mainscript.VisszaRakas))]
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void RebasePost()
        {
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            LastCheckMs = (t1 - _loopT0) * ms;
            var after = mainscript.s != null ? mainscript.s.visszarakva : default;
            if (after.x == _originBefore.x && after.y == _originBefore.y && after.z == _originBefore.z) return;
            Shifts++;
            LastLoopMs = LastCheckMs;
            if (LastLoopMs > MaxShiftMs) MaxShiftMs = LastLoopMs;
            LastSyncMs = 0;
            if (On && TimeSync)
            {
                Physics.SyncTransforms();
                LastSyncMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t1) * ms;
            }
        }

        public static string Start() { Groups.Clear(); _total = 0; On = true; return "{\"on\":true}"; }

        public static string Stop()
        {
            On = false;
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            var sb = new StringBuilder("{\"checks\":").Append(Checks).Append(",\"shifts\":").Append(Shifts).Append(",\"maxShiftMs\":").Append(MaxShiftMs.ToString("F2")).Append(",\"totalMs\":").Append((_total * ms).ToString("F2")).Append(",\"lastLoopMs\":").Append(LastLoopMs.ToString("F2"))
                .Append(",\"lastSyncMs\":").Append(LastSyncMs.ToString("F2")).Append(",\"groups\":[");
            bool first = true;
            foreach (var kv in Groups.OrderByDescending(kv => kv.Value.Ticks).Take(25))
            {
                if (!first) sb.Append(',');
                first = false;
                var g = kv.Value;
                sb.Append("{\"obj\":").Append(Json.Str(kv.Key)).Append(",\"calls\":").Append(g.N).Append(",\"moved\":").Append(g.Moved)
                  .Append(",\"ms\":").Append((g.Ticks * ms).ToString("F2")).Append(",\"usPerCall\":").Append((g.Ticks * ms * 1000 / g.N).ToString("F1"))
                  .Append(",\"transforms\":").Append(g.Transforms).Append(",\"colliders\":").Append(g.Colliders).Append(",\"bodies\":").Append(g.Bodies).Append('}');
            }
            return sb.Append("]}").ToString();
        }
    }
}
