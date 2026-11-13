using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// Walking a vehicle by its handlebars (pushgripable: the push key on a grip). The game joins the player to the
    /// vehicle's body (a ConfigurableJoint on the player) and steers it with the player's steering keys (carscript reads
    /// pushgripable.steerValue). On a vehicle another machine owns that body is a kinematic copy: the player was held
    /// by something that does not move, and the steering went nowhere.
    /// Now taking the grip takes the vehicle, as picking up an item does (provisional at once, the server confirms):
    /// this machine moves it, the others see it walked. Not from its driver: a vehicle someone rides is let go at once
    /// (the server refuses that claim too); and if the vehicle goes to another machine while held, it is let go.
    public static partial class Entities
    {
        public static long GripClaims, GripRefused, GripLost;
        private static uint _gripNet;

        private static void PushGripTick()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var g = pl != null && pl.pushgripping ? pl.pushgrip : null;
            if (g == null) { _gripNet = 0; return; }
            var root = g.transform.root;
            Ent e = null;
            e = PlacedOfTransform(root); if (e != null && e.PartIndex >= 0) e = null;   // the car itself, not a can stored in it
            if (e == null) return;
            if (!e.Proxy) { _gripNet = e.NetId; return; }
            if (_gripNet == e.NetId) { pl.StopPushGripping(); GripLost++; _gripNet = 0; return; }   // gone to another machine: not ours to hold
            if (e.Driven) { pl.StopPushGripping(); GripRefused++; return; }                        // someone rides it
            SetProxy(e, false);
            e.OwnerId = MyId;
            W.Reset(); WriteClaim(W, e.NetId, ClaimReason.PushGrip); ToServer(W, true);
            ClaimsSent++; GripClaims++;
            _gripNet = e.NetId;
        }

        public static string GripStatus()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            return "{\"gripping\":" + (pl != null && pl.pushgripping ? "true" : "false") + ",\"grip\":" + Json.Str(pl != null && pl.pushgrip != null ? pl.pushgrip.name : "") +
                   ",\"yaw\":" + (pl != null && pl.pushgrip != null && pl.pushgrip.car != null ? pl.pushgrip.car.transform.eulerAngles.y : 0f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                   ",\"steer\":" + (pl != null && pl.pushgrip != null && pl.pushgrip.car != null ? pl.pushgrip.car.steer : 0f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                   ",\"net\":" + _gripNet + ",\"claims\":" + GripClaims + ",\"refused\":" + GripRefused + ",\"lost\":" + GripLost + "}";
        }

        /// Diagnostics (bridge `mp grips`): the item prefabs with grips: [index, name, grips].
        public static string GripPrefabs()
        {
            var rows = new List<string>();
            var db = itemdatabase.s != null ? itemdatabase.s.items : null;
            if (db == null) return "{\"error\":\"no itemdatabase\"}";
            for (int i = 0; i < db.Length; i++)
            {
                var c = db[i] != null ? db[i].GetComponent<carscript>() : null;
                if (c != null && c.pushgrip != null && c.pushgrip.Length > 0) rows.Add("[" + i + "," + Json.Str(db[i].name) + "," + c.pushgrip.Length + "]");
            }
            return "{\"grips\":[" + string.Join(",", rows) + "]}";
        }
    }
}
