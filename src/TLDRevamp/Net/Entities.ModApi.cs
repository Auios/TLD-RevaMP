using UnityEngine;

namespace TLDRevamp.Net
{
    /// The game's shared objects as the modding API sees them (MpApi.WorldObjectId / FindWorldObject / ShareWorldObject):
    /// a mod points at a car or an item by its network id, which is the same on every machine.
    internal static class EntityModApi
    {
        /// The network id of a shared group root on or above go (a car, an item), or 0.
        internal static uint NetIdOf(GameObject go)
        {
            if (go == null) return 0;
            for (var t = go.transform; t != null; t = t.parent)
            {
                var s = t.GetComponent<tosaveitemscript>();
                if (s == null) continue;
                if (Entities.TryModRootNet(s, out uint net)) return net;
            }
            return 0;
        }

        internal static GameObject GameObjectOf(uint net) =>
            Entities.ModObject(net);

        /// Share the topmost saveable object on or above go, with its group. True if it is shared or on its way.
        internal static bool ShareGameObject(GameObject go)
        {
            if (go == null || !Entities.InSession) return false;
            tosaveitemscript top = null;
            for (var t = go.transform; t != null; t = t.parent) { var s = t.GetComponent<tosaveitemscript>(); if (s != null) top = s; }
            if (top == null) return false;
            string res = Entities.ModShareRoot(top);
            return !res.Contains("\"error\"");
        }
    }

    public static partial class Entities
    {
    }
}
