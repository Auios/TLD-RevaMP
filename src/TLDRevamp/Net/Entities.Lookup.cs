using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal static class EntityLookup
    {
        internal static List<tankscript> TanksOf(List<tosaveitemscript> items)
        {
            var l = new List<tankscript>();
            foreach (var it in items) if (it != null && it.tanks != null) foreach (var t in it.tanks) l.Add(t);
            return l;
        }

        internal static tosaveitemscript NearestCar()
        {
            if (savedatascript.s == null || mainscript.s == null || mainscript.s.player == null) return null;
            var p = mainscript.s.player.transform.position;
            tosaveitemscript best = null; float bd = float.MaxValue;
            foreach (var it in savedatascript.s.items.Values)
                if (it != null && it.car != null && it.transform.parent == null)
                {
                    float d = (it.transform.position - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = it; }
                }
            return best;
        }
    }

    public static partial class Entities
    {
        /// The shared object whose group holds this item (diagnostics).
        internal static Ent SharedGroupOf(tosaveitemscript it)
        {
            return EditEntityOf(it);
        }

        /// Test labs outside Net (Diagnostics.AimLab): an entity's root object here.
        internal static bool TryRoot(uint net, out GameObject root)
        {
            root = ByNet.TryGetValue(net, out var e) && Resolve(e) && e.Root != null ? e.Root.gameObject : null;
            return root != null;
        }

        /// The entity's tanks in EntState order (its items in order, each item's tanks): amount per fluid, caps open.
        internal static List<tankscript> TanksOf(Ent e) => EntityLookup.TanksOf(e.Items);

        /// For logs: which shared object a root is — net, owner, copy or own — or "local" if it isn't shared.
        internal static string DescribeRoot(Transform root)
        {
            foreach (var e in ByNet.Values)
                if (e.Root != null && e.Root.transform.root == root) return "net " + e.NetId + " owner " + e.OwnerId + (e.Proxy ? " copy" : " own");
            return "local";
        }

        internal static Transform RootOf(uint net) => ByNet.TryGetValue(net, out var e) && Resolve(e) && e.Root != null ? e.Root.transform.root : null;

        internal static pickupable PickupableOf(uint net) => ByNet.TryGetValue(net, out var e) && Resolve(e) && e.Root != null ? e.Root.P : null;

        internal static tankscript TankOf(uint net, int ti)
        {
            if (!ByNet.TryGetValue(net, out var e) || !Resolve(e)) return null;
            var l = TanksOf(e);
            return ti >= 0 && ti < l.Count ? l[ti] : null;
        }

        /// Nearest car to the local player (for scenarios).
        internal static tosaveitemscript RootOfNet(uint net) => ByNet.TryGetValue(net, out var e) && Resolve(e) ? e.Root : null;

    }
}
