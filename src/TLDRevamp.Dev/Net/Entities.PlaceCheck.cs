using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPlaceCheckTools
    {

        /// `mp nudgemember <net> <member> <m>`: test, a glued member of this copy moved here only.
        public static string TestNudgeMember(uint net, int i, float m)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !e.Proxy || !Resolve(e) || e.Root == null || i <= 0 || i >= e.Items.Count || e.Items[i] == null) return "{\"error\":\"no such member of a copy here\"}";
            var at = e.Items[i].attachable;
            if (at == null || !at.attached || at.slot != null) return "{\"error\":\"not glued\"}";
            at.transform.position += e.Root.transform.up * m;
            return "{\"moved\":" + Json.Str(e.Items[i].name) + "}";
        }
        public static uint TestDropStates;   // test: this copy doesn't hear this object's states (a lost final state)
    }
}
