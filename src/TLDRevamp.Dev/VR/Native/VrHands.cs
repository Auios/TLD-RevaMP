using System;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;
using static TLDRevamp.VR.Native.VrHands;

namespace TLDRevamp.VR.Native
{
    internal static class VrHandsTools
    {

        /// The items around the player a hand could take, for tests: name, where, and whether the game would let go of it.
        internal static string ItemsJson(float radius)
        {
            var pl = VrNative.Player;
            if (pl == null) return "{\"error\":\"no player\"}";
            var b = new StringBuilder(1024);
            b.Append("{\"player\":").Append(VrNative.V(pl.transform.position));
            if (pl.Cam != null) b.Append(",\"head\":").Append(VrNative.V(pl.Cam.transform.position)).Append(",\"look\":").Append(VrNative.V(pl.Cam.transform.forward));
            b.Append(",\"items\":[");
            var hits = Physics.OverlapSphere(pl.transform.position, radius, pl.pickupLayer, QueryTriggerInteraction.Ignore);   // a test's query: every collider, however many
            var seen = new System.Collections.Generic.HashSet<int>();
            bool first = true;
            for (int i = 0; i < hits.Length; i++)
            {
                var p = hits[i] != null ? hits[i].GetComponentInParent<pickupable>() : null;
                if (p == null || !seen.Add(p.GetInstanceID())) continue;
                if (!first) b.Append(',');
                first = false;
                b.Append("{\"name\":").Append(Json.Str(p.name)).Append(",\"pos\":").Append(VrNative.V(p.transform.position))
                 .Append(",\"center\":").Append(VrNative.V(Center(p))).Append(",\"take\":").Append(CanTake(pl, p) ? "true" : "false")
                 .Append(",\"mass\":").Append(p.RB != null ? VrNative.F(p.RB.mass) : "null")
                 .Append(",\"loose\":").Append(Loose(p) ? "true" : "false").Append('}');
            }
            b.Append("]}");
            return b.ToString();
        }
    }
}
