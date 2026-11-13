using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Fixes.Rabbits;

namespace TLDRevamp.Fixes
{
    internal static class RabbitsTools
    {

        /// Test (bridge `rabbits spawn`): one rabbit from the player's spawner now, as its routine would (its own place
        /// rules; the chance and the limit skipped).
        public static string TestSpawn()
        {
            var sp = Spawner();
            if (sp == null) return "{\"error\":\"no spawner\"}";
            int before = sp.AIs.Count;
            for (int k = 0; k < 20 && sp.AIs.Count == before; k++)
            {
                var p = sp.CanSpawn(sp.RandomPlace());
                if (p != Vector3.zero) sp.Spawn(p);
            }
            if (sp.AIs.Count == before) return "{\"error\":\"no place found\"}";
            var go = sp.AIs[sp.AIs.Count - 1];
            var g = mainscript.GlobalFromUnityPos(go.transform.position);
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            return "{\"spawned\":" + Json.Str(go.name) + ",\"pos\":[" + g.x.ToString("F2", ic) + "," + g.y.ToString("F2", ic) + "," + g.z.ToString("F2", ic) + "]}";
        }
    }
}
