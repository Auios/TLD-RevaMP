using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPushGripTools
    {

        public static string GripTest(string[] a)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            if (a[1] == "stop") { pl.StopPushGripping(); ObservedGripWalkUntil = 0f; return "{\"stopped\":true}"; }
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            uint net = uint.Parse(a[1]);
            float secs = a.Length > 2 ? float.Parse(a[2], ic) : 0f;
            ObservedGripSteer = a.Length > 3 ? float.Parse(a[3], ic) : 0f;
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null || e.Root.car == null) return "{\"error\":\"no such vehicle\"}";
            var grips = e.Root.car.pushgrip;
            if (grips == null || grips.Length == 0) return "{\"error\":\"no grip\"}";
            pushgripable best = null; float bd = float.MaxValue;
            foreach (var gp in grips) if (gp != null) { float d = (gp.transform.position - pl.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = gp; } }
            if (best == null || (best.inuse && pl.pushgrip != best)) return "{\"error\":\"grip in use\"}";
            if (pl.pushgrip != best)
            {
                // where a player taking it stands: at the grip, beside the vehicle (griptest run 1: 1.9 m away, past the
                // game's pushGripDistance - let go the next frame)
                var side = best.transform.position - e.Root.transform.position; side.y = 0f;
                var at = best.transform.position + (side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right) * 0.5f;
                at.y = pl.transform.position.y;
                pl.Teleport(at);
                bd = (best.transform.position - at).sqrMagnitude;
                var f = e.Root.transform.forward; f.y = 0f;   // a player taking the grip faces along the vehicle
                if (f.sqrMagnitude > 1e-4f) pl.BodyRot.rotation = Quaternion.LookRotation(f);
                pl.pushgripping = true; GripNow(pl) = true;
                pl.pushgrip = best; best.GetRot(); best.inuse = true;
                var j = pl.gameObject.AddComponent<ConfigurableJoint>();
                j.xMotion = ConfigurableJointMotion.Locked; j.yMotion = ConfigurableJointMotion.Free; j.zMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = ConfigurableJointMotion.Free; j.angularYMotion = ConfigurableJointMotion.Free; j.angularZMotion = ConfigurableJointMotion.Free;
                j.connectedBody = best.transform.root.GetComponent<Rigidbody>();
                GripJoint(pl) = j;
            }
            ObservedGripWalkUntil = Time.time + secs;
            return "{\"gripping\":" + Json.Str(best.name) + ",\"dist\":" + Mathf.Sqrt(bd).ToString("F2", ic) + ",\"maxDist\":" + Mathf.Sqrt(pl.pushGripDistance).ToString("F2", ic) + ",\"proxy\":" + (e.Proxy ? "true" : "false") + "}";
        }

        // ---- test hooks (bridge `mp grip net [secs] [steer]`, `mp grip stop`): take the vehicle's grip the way the
        // game does (fpscontroller's push-key branch on a pushgripable) and walk with the forward key held
        internal static readonly AccessTools.FieldRef<fpscontroller, ConfigurableJoint> GripJoint = AccessTools.FieldRefAccess<fpscontroller, ConfigurableJoint>("pushGripJoint");
        internal static readonly AccessTools.FieldRef<fpscontroller, bool> GripNow = AccessTools.FieldRefAccess<fpscontroller, bool>("pushgripnow");
        private static float _gripWalkUntil, _gripSteer;
        internal static float ObservedGripSteer { get => _gripSteer; set => _gripSteer = value; }
        internal static float ObservedGripWalkUntil { get => _gripWalkUntil; set => _gripWalkUntil = value; }

        [HarmonyPatch(typeof(inputscript), "Update")]
        private static class GripWalk
        {
            [HarmonyPostfix]
            private static void Postfix(inputscript __instance)
            {
                if (_gripWalkUntil <= 0f) return;
                if (Time.time > _gripWalkUntil) { _gripWalkUntil = 0f; return; }
                var pl = mainscript.s != null ? mainscript.s.player : null;
                if (pl == null || !(pl.pushgripping || (_drawbarWalk && pl.pickedUp != null))) return;
                __instance.forward = 1f; __instance.steer = _gripSteer;
            }
        }

        private static bool _drawbarWalk;

        /// Bridge `mount drawbar <net> [secs] [steer]`: the local player picks up trailer `net`'s drawbar
        /// (attachgripscript.Grip, as the pickup key does: fpscontroller.Pickup) and walks forward with it for secs;
        /// `mount drawbar stop` lets go (fpscontroller.Drop); `mount drawbar state` what the player holds. For trailercreep.py's walk.
        public static string DrawbarTest(string[] a)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            if (a[1] == "state") return "{\"pickedUp\":" + Json.Str(pl.pickedUp != null ? pl.pickedUp.name : "") + ",\"walking\":" + (_drawbarWalk && Time.time < _gripWalkUntil ? "true" : "false") + "}";
            if (a[1] == "stop") { _drawbarWalk = false; _gripWalkUntil = 0f; if (pl.pickedUp != null) pl.Drop(); return "{\"dropped\":true}"; }
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            if (!ObservedEntities.TryGetValue(uint.Parse(a[1]), out var e) || e.Root == null) return "{\"error\":\"no such item\"}";
            var g = e.Root.GetComponentInChildren<attachgripscript>(true);
            var grip = g != null && g.Grip != null ? g.Grip.GetComponent<pickupable>() : null;
            if (grip == null) return "{\"error\":\"no drawbar\"}";
            if (pl.pickedUp != grip.GetComponent<pickupable>())
            {
                // standing a step in front of the drawbar's end, facing it
                var away = grip.transform.position - e.Root.transform.position; away.y = 0f;
                var stand = grip.transform.position + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward) * 0.3f;
                stand.y = grip.transform.position.y + 0.1f;
                pl.Teleport(stand);
                // facing away from the trailer, to pull it along
                if (away.sqrMagnitude > 1e-4f) pl.BodyRot.rotation = Quaternion.LookRotation(away.normalized);
                pl.Pickup(grip, grip.transform.position);
            }
            _gripSteer = a.Length > 3 ? float.Parse(a[3], ic) : 0f;
            _drawbarWalk = true;
            _gripWalkUntil = Time.time + (a.Length > 2 ? float.Parse(a[2], ic) : 0f);
            return "{\"holding\":" + (pl.pickedUp == grip ? "true" : "false") + ",\"grip\":" + Json.Str(grip.name) + "}";
        }
    }
}
