using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Fixes.FallRescue;

namespace TLDRevamp.Fixes
{
    internal static class FallRescueTools
    {

        /// Bridge `fallrescue test` (tests): the nearest loose item to the player, not frozen, put 30 m under the ground where it
        /// is, falling at 20 m/s.
        public static string Test()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || savedatascript.s == null) return "{\"error\":\"no world\"}";
            tosaveitemscript best = null; float bd = float.MaxValue;
            foreach (var it in savedatascript.s.items.Values)
            {
                if (it == null || it.RB == null || it.RB.isKinematic || it.transform.parent != null || it.car != null || (it.P != null && it.P.physlock != null)) continue;
                float d = (it.transform.position - pl.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = it; }
            }
            if (best == null) return "{\"error\":\"no loose item near\"}";
            ObservedTest = best;
            var p = best.transform.position + Vector3.down * 30f;
            best.RB.position = p; best.transform.position = p; best.RB.velocity = Vector3.down * 20f;
            return "{\"dropped\":" + Json.Str(best.name) + ",\"y\":" + Json.Num(p.y) + "}";
        }

        private static tosaveitemscript _test;
        internal static tosaveitemscript ObservedTest { get => _test; set => _test = value; }
        public static string Stats => "{\"enabled\":" + (Enabled ? "true" : "false") + ",\"rescued\":" + Rescued + ",\"checked\":" + Checked + ",\"last\":" + Json.Str(Last) +
                                      ",\"testUnder\":" + (_test != null && ObservedGroundMask > 0 && Physics.Raycast(_test.transform.position + Vector3.up * 1000f, Vector3.down, out var g, 2000f, ObservedGroundMask, QueryTriggerInteraction.Ignore) ? Json.Num(g.point.y - _test.transform.position.y) : "null") + "}";
    }
}
