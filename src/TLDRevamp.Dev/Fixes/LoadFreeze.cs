using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Fixes.LoadFreeze;

namespace TLDRevamp.Fixes
{
    internal static class LoadFreezeTools
    {
        private static readonly RaycastHit[] ProbeHits = new RaycastHit[16];

        /// Bridge `stillfrozen [radius]` (tests): every kinematic body near the player, how far above what is under it, and
        /// what could be keeping it (a save item's body the streamer asks about, held by the ground rule, locked away, ...).
        public static string StillFrozen(float radius)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            var items = new Dictionary<Rigidbody, tosaveitemscript>();
            if (savedatascript.s != null) foreach (var it in savedatascript.s.items.Values) if (it != null && it.RB != null) items[it.RB] = it;
            var own = pl.transform.root; var p = pl.transform.position;
            var sb = new System.Text.StringBuilder("{\"bodies\":[");
            int n = 0;
            foreach (var rb in Object.FindObjectsOfType<Rigidbody>())
            {
                if (rb == null || !rb.isKinematic || rb.transform.root == own || (rb.position - p).sqrMagnitude > radius * radius) continue;
                float above = -1f;
                int k = Physics.RaycastNonAlloc(rb.position + Vector3.up * 0.1f, Vector3.down, ProbeHits, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int i = 0; i < k; i++) if (ProbeHits[i].collider != null && ProbeHits[i].collider.transform.root != rb.transform.root && ProbeHits[i].distance < best) best = ProbeHits[i].distance;
                if (best < float.MaxValue) above = best - 0.1f;
                items.TryGetValue(rb, out var item);
                var pk = rb.GetComponent<pickupable>();
                if (n++ > 0) sb.Append(',');
                sb.Append("{\"root\":").Append(Json.Str(rb.transform.root.name)).Append(",\"body\":").Append(Json.Str(rb.name))
                  .Append(",\"above\":").Append(above.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"item\":").Append(item != null ? "true" : "false")
                  .Append(",\"groundHold\":").Append(item != null && UnfreezeOnGround.Holding(item) ? "true" : "false")
                  .Append(",\"buried\":").Append(item != null && item.buried ? "true" : "false")
                  .Append(",\"locked\":").Append(pk != null && pk.physlock != null ? "true" : "false")
                  .Append(",\"held\":").Append(pk != null && (pk.pickedUp || pk.inRightHand) ? "true" : "false")
                  .Append(",\"parent\":").Append(Json.Str(rb.transform.parent != null ? rb.transform.parent.name : ""))
                  .Append(",\"dist\":").Append((rb.position - p).magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append('}');
            }
            return sb.Append("]}").ToString();
        }
    }
}
