using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityWornTools
    {
        internal static readonly NetWriter W = new NetWriter();

        // ------------------------------------------------------------------ tests (bridge)

        /// `mp wear <net>`: the local player puts on shared item `net` (taken first if another machine has it, as the
        /// pick-up before the wear key does).
        public static string WearNet(uint net)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || !ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null || e.Root.P == null) return "{\"error\":\"no such item here\"}";
            var p = e.Root.P;
            if (p.wear == null) return "{\"error\":\"not wearable\"}";
            int i = (int)p.wear.tipus;
            if (pl.weared[i] != null) return "{\"error\":\"slot " + p.wear.tipus + " taken by " + pl.weared[i].name + "\"}";
            if (e.Proxy && e.Root.car == null)
            {
                SetProxy(e, false); e.OwnerId = MyId;
                W.Reset(); WriteClaim(W, e.NetId, ClaimReason.Wear); ToServer(W, true); ClaimsSent++;
            }
            if (pl.pickedUp == p) pl.Drop();
            pl.Wear(p);
            return "{\"worn\":" + Json.Str(e.Root.name) + ",\"slot\":" + Json.Str(p.wear.tipus.ToString()) + "}";
        }

        /// `mp takeoff <net> [drop]`: off the local player's head — into the hands (the wear key), or `drop`: where it is.
        public static string TakeOffNet(uint net, bool drop)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || !ObservedEntities.TryGetValue(net, out var e) || e.Root == null || LocalWornSlot(e.Root) < 0) return "{\"error\":\"not worn here\"}";
            pl.TakeOff(e.Root.P, drop);
            return "{\"off\":" + Json.Str(e.Root.name) + ",\"held\":" + (pl.pickedUp == e.Root.P ? "true" : "false") + "}";
        }

        /// `mp wearnear [name]`: the local player puts on the nearest wearable within 10 m whose name contains `name` (in a
        /// session or not: a host that wore it before hosting).
        public static string WearNear(string name)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            pickupable best = null; float bd = 100f;
            foreach (var p in Object.FindObjectsOfType<pickupable>())
            {
                if (p.wear == null || p.wear.wearing || (name != "" && p.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;
                int i = (int)p.wear.tipus;
                if (i < 0 || i >= pl.weared.Length || pl.weared[i] != null || IsProxy(p)) continue;
                float d = (p.transform.position - pl.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best == null) return "{\"error\":\"no free wearable within 10 m\"}";
            pl.Wear(best);
            return "{\"worn\":" + Json.Str(best.name) + ",\"slot\":" + Json.Str(best.wear.tipus.ToString()) + "}";
        }
    }
}
