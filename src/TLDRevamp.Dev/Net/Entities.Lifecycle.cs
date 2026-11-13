using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityLifecycleTools
    {

        /// The local player picks up the nearest pickupable (the game's own Pickup — the claim hook fires for proxies).
        public static string PickupNearest(float radius)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            pickupable best = null; float bd = radius * radius;
            foreach (var pk in UnityEngine.Object.FindObjectsOfType<pickupable>())
            {
                if (pk.pickedUp || !pk.gameObject.activeInHierarchy) continue;
                var d = (pk.transform.position - pl.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = pk; }
            }
            if (best == null) return "{\"error\":\"nothing to pick up\"}";
            if (pl.transform.root.name.Contains("Player")) { }   // the pickup target may be anywhere near
            pl.Teleport(best.transform.position + Vector3.up * 0.5f);
            best.Pickup();
            LastPicked = best;
            return "{\"picked\":" + Json.Str(best.name) + "}";
        }

        /// Test/bridge hook: the E-press path on a car — the game's pickup interaction on its pickupable component
        /// (the same call a player's enter/flip press makes). With the car-claim guard this must NOT flip ownership.
        public static string PickupCar(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null) return "{\"error\":\"no such object here\"}";
            var pk = e.Root.GetComponent<pickupable>();
            if (pk == null) pk = e.Root.GetComponentInChildren<pickupable>();
            if (pk == null) return "{\"error\":\"the car has no pickupable\"}";
            pk.Pickup();
            return "{\"picked\":" + Json.Str(e.Root.name) + "}";
        }
    }
}
