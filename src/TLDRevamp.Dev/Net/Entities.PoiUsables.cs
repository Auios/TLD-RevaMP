using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPoiUsablesTools
    {

        public static string PoiDrag(string[] a) => TLDRevamp.Net.EntityLiveUsablesTools.StartUsableDrag(EntityUsables.PoiArg(a, 1), float.Parse(a[6], System.Globalization.CultureInfo.InvariantCulture),
            a.Length > 7 ? float.Parse(a[7], System.Globalization.CultureInfo.InvariantCulture) : 60f);
        public static string PoiProbe(string[] a) => a.Length > 1 ? TLDRevamp.Net.EntityLiveUsablesTools.StartUsableProbe(EntityUsables.PoiArg(a, 1), float.Parse(a[6], System.Globalization.CultureInfo.InvariantCulture)) : TLDRevamp.Net.EntityLiveUsablesTools.UsableProbeJson();

        /// Operate it as a player does (the game's own input calls, synced like a player's): `mp poiuse gen x y z u
        /// rot dx dy | slide v | turn`.
        public static string PoiUse(string[] a)
        {
            var u = EntityUsables.PoiArg(a, 1);
            if (u == null) return "{\"error\":\"not spawned here\"}";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            switch (a[6])
            {
                case "rot": u.Rot(float.Parse(a[7], ci), float.Parse(a[8], ci), true); break;
                case "slide": { float v = float.Parse(a[7], ci); u.Slide(v, v, v, true); break; }
                case "turn": u.Turn(); break;
                default: return "{\"error\":\"rot dx dy | slide v | turn\"}";
            }
            return "{" + TLDRevamp.Net.EntityUsables.PoiStateJson(u) + "}";
        }
    }
}
