using System.Linq;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Fixes.WornDestroyed;

namespace TLDRevamp.Fixes
{
    internal static class WornDestroyedTools
    {

        /// Test (bridge `guardlab worn`): the nearest wearable within 10 m put on and destroyed while worn (what the
        /// playtest's hat met); the frames after it show in `errors` (makeinvisible's NullReferenceException or none).
        internal static string Test()
        {
            var pl = mainscript.s.player;
            pickupable w = null; float best = 100f;
            foreach (var p in Object.FindObjectsOfType<pickupable>())
            {
                if (p.wear == null || p.wear.wearing) continue;
                int i = (int)p.wear.tipus;
                if (i < 0 || i >= pl.weared.Length || pl.weared[i] != null) continue;
                float d = (p.transform.position - pl.transform.position).sqrMagnitude;
                if (d < best) { best = d; w = p; }
            }
            if (w == null) return "{\"error\":\"no wearable within 10 m with a free slot\"}";
            string name = w.name; int n0 = pl.wearedRenderer.R.Count;
            pl.Wear(w);
            int n1 = pl.wearedRenderer.R.Count;
            Object.Destroy(w.gameObject);
            return "{\"worn\":" + Json.Str(name) + ",\"tipus\":" + Json.Str(w.wear.tipus.ToString()) +
                   ",\"hideListBefore\":" + n0 + ",\"hideListWorn\":" + n1 + "}";
        }
    }
}
