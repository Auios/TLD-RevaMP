using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.RebaseLab;

namespace TLDRevamp
{
    internal static class RebaseLabTools
    {
        public static string PourTest(string step)
        {
            if (step == "start")
            {
                var p = mainscript.s.player.transform.position;
                float best = float.MaxValue; _pourT = null;
                foreach (var t in Object.FindObjectsOfType<tankcapscript>())
                {
                    if (!t.started || t.Tank == null || t.usable == null || !t.usable.turnable || t.noPour || t.tube || t.Tank.F.GetAmount() <= 0f) continue;
                    float d = (t.transform.position - p).sqrMagnitude;
                    if (d < best) { best = d; _pourT = t; }
                }
                if (_pourT == null) return "{\"error\":\"no container with fluid\"}";
                var rb = _pourT.GetComponentInParent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                var root = _pourT.transform.root;
                root.position = p + mainscript.s.player.transform.forward * 2f + Vector3.up * 1.5f;
                root.rotation = Quaternion.Euler(180f, 0f, 0f);
                _pourT.usable.currentTurnState = 1;
                return "{\"container\":" + Json.Str(root.name) + ",\"amount\":" + _pourT.Tank.F.GetAmount().ToString("F2") + "}";
            }
            if (_pourT == null) return "{\"error\":\"no test\"}";
            var ps = _pourT.ps;
            return "{\"emission\":" + (_pourT.em.enabled ? "true" : "false") + ",\"playing\":" + (ps.isPlaying ? "true" : "false") +
                   ",\"paused\":" + (ps.isPaused ? "true" : "false") + ",\"particles\":" + ps.particleCount +
                   ",\"angle\":" + _pourT.angle.ToString("F0") + ",\"valve\":" + _pourT.valve.ToString("F2") + ",\"amount\":" + _pourT.Tank.F.GetAmount().ToString("F2") + "}";
        }

        // Functional test for IdlePourPause: open + flip the nearest container that has fluid and a turnable cap.
        private static tankcapscript _pourT;
    }
}
