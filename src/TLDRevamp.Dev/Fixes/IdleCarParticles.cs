using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Fixes.IdleCarParticles;

namespace TLDRevamp.Fixes
{
    internal static class IdleCarParticlesTools
    {

        /// Test helper: of the systems we hold paused, how many the game's IsAlive() calls dead (they would be replayed).
        public static string Probe()
        {
            int n = 0, alive = 0, paused = 0, stopped = 0;
            foreach (var ps in TLDRevamp.Fixes.IdleCarParticles.ObservedPausedByUs)
            {
                if (ps == null) continue;
                n++;
                if (ps.IsAlive()) alive++;
                if (ps.isPaused) paused++;
                if (ps.isStopped) stopped++;
            }
            return "{\"held\":" + n + ",\"isAlive\":" + alive + ",\"isPaused\":" + paused + ",\"isStopped\":" + stopped + "}";
        }

        /// Test helper: emission settings of the wheels' dust systems that are playing without particles.
        public static string DustProbe()
        {
            var sb = new System.Text.StringBuilder("[");
            int n = 0;
            foreach (var t in Object.FindObjectsOfType<talajnezo>())
            {
                var ps = t.ps;
                if (ps == null || n >= 6) continue;
                var e = ps.emission;
                if (n++ > 0) sb.Append(',');
                sb.Append("{\"en\":").Append(e.enabled ? 1 : 0).Append(",\"rot\":").Append(e.rateOverTime.constantMax).Append(",\"rotMode\":\"").Append(e.rateOverTime.mode)
                  .Append("\",\"rod\":").Append(e.rateOverDistance.constantMax).Append(",\"bursts\":").Append(e.burstCount).Append(",\"count\":").Append(ps.particleCount)
                  .Append(",\"playing\":").Append(ps.isPlaying ? 1 : 0).Append(",\"rateM\":").Append(t.rateM).Append('}');
            }
            return sb.Append(']').ToString();
        }

        /// Test helper: the player's car's smoke, steam and wheel dust right now.
        public static string CarProbe()
        {
            var car = mainscript.s != null && mainscript.s.player != null ? mainscript.s.player.Car : null;
            if (car == null) return "{\"error\":\"not in a car\"}";
            var sb = new System.Text.StringBuilder("{\"rpm\":").Append(car.Engine != null ? car.Engine.rpm : -1f).Append(",\"systems\":[");
            var list = new List<ParticleSystem>();
            if (car.psSmoke != null) list.AddRange(car.psSmoke);
            if (car.CoolantSteam != null) list.Add(car.CoolantSteam);
            foreach (var t in car.GetComponentsInChildren<talajnezo>()) if (t.ps != null) list.Add(t.ps);
            for (int i = 0; i < list.Count; i++)
            {
                var ps = list[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"n\":").Append(Json.Str(ps.name)).Append(",\"paused\":").Append(ps.isPaused ? 1 : 0).Append(",\"count\":").Append(ps.particleCount)
                  .Append(",\"en\":").Append(ps.emission.enabled ? 1 : 0).Append(",\"rate\":").Append(ps.emission.rateOverTime.constant).Append('}');
            }
            return sb.Append("]}").ToString();
        }
    }
}
