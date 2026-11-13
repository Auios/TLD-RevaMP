using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntitySprayTools
    {
        /// The local player holds the left mouse button for `secs` with the can in hand: the game's own call per frame
        /// (fpscontroller: input.lmb → pP.spray.Fire(Th)).
        public static string SprayHold(float secs)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var sp = HeldSpray(pl);
            if (sp == null) return "{\"error\":\"no spray can in hand\"}";
            ObservedHoldUntil = Time.time + secs;
            return "{\"holding\":" + secs.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"paint\":" + (sp.hasPaint ? "true" : "false") + "}";
        }

        /// Test: the local player looks at shared object <net> (body yaw + head pitch, as FillLab aims) — for screenshots.
        public static string LookAt(uint net)
        {
            var p = mainscript.s != null ? mainscript.s.player : null;
            if (p == null || !ObservedEntities.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no player / entity\"}";
            var d = e.Root.transform.position - p.mainCam.transform.position;
            var flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude > 1e-4f) p.BodyRot.rotation = Quaternion.LookRotation(flat);
            p.FyRot = -Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg;
            if (!mainscript.IsVR()) p.Th.localEulerAngles = new Vector3(p.FyRot, 0f, 0f);
            return "{\"dist\":" + d.magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "}";
        }

        /// Test: turn an item's switch the game's way (`mp turn <net>`: its headlight's usable, else its first turnable
        /// usable — usablescript.Turn, the sync point a player's click goes through) and `mp lamp <net>`: its light
        /// state here (the switch, headlightscript states, Lights shining).
        public static string TurnItem(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            var hl = e.Root.GetComponentInChildren<headlightscript>(true);
            var u = hl != null && hl.usableSet != null ? hl.usableSet : null;
            if (u == null) foreach (var x in e.Root.GetComponentsInChildren<usablescript>(true)) if (x.turnable) { u = x; break; }
            if (u == null) return "{\"error\":\"no switch\"}";
            u.Turn();
            return LampState(net);
        }
        internal static void SprayHoldTick()
        {
            if (_holdUntil <= 0f) return;
            if (Time.time > _holdUntil) { _holdUntil = 0f; return; }
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var sp = HeldSpray(pl);
            if (sp != null) sp.Fire(pl.Th);
        }

        // ---- test tooling (tools/spraytest.py)
        internal static float _holdUntil;
        internal static float ObservedHoldUntil { get => _holdUntil; set => _holdUntil = value; }
    }
}
