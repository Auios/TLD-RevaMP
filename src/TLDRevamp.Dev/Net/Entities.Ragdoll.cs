using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityRagdollTools
    {

        /// Test hooks (bridge `mp ragdoll net kill|grab|hold`): the creature dies the game's way (newAiScript / aiscript.Die, as a
        /// shot does); a limb is picked up by the local player the game's way (pickupable.Pickup, as the E press does).
        public static string RagdollTest(uint net, string what)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no such entity\"}";
            if (what == "kill")
            {
                var ai = e.Root.GetComponentInChildren<newAiScript>(true);
                if (ai != null) { ai.Die(_playSound: true); return "{\"died\":\"newAi\"}"; }
                var rab = e.Root.GetComponentInChildren<aiscript>(true);
                if (rab != null) { rab.Die(_playSound: true); return "{\"died\":\"ai\"}"; }
                return "{\"error\":\"no creature\"}";
            }
            var ls = Limbs(e);
            var rp = e.Root.ragdollPos;
            if (ls == null || rp.ps == null || rp.ps.Length == 0) return "{\"error\":\"no limbs\"}";
            var pl = mainscript.s.player;
            pickupable best = null; float bd = float.MaxValue;
            foreach (var p in rp.ps)
                if (p != null) { float d = (p.transform.position - pl.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = p; } }
            if (best == null) return "{\"error\":\"no limb\"}";
            // grab: the limb's own pickup (the claim alone); hold: the player's own pickup, the E press, so the limb
            // is in the hand and follows it
            if (what == "hold") pl.Pickup(best); else best.Pickup();
            return "{\"picked\":" + Json.Str(best.name) + ",\"dist\":" + Mathf.Sqrt(bd).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                   ",\"proxyNow\":" + (e.Proxy ? "true" : "false") + "}";
        }

        /// Diagnostics (bridge `mp ragdoll net`): the body's limbs here, world positions (global), and what was received.
        public static string RagdollStatus(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no such entity\"}";
            var ls = Limbs(e);
            if (ls == null) return "{\"error\":\"no limbs\"}";
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>(); var rest = new List<string>();
            var frame = RagdollFrame(e);
            foreach (var l in ls)
            {
                if (l == null) { rows.Add("null"); continue; }
                var g = mainscript.GlobalFromUnityPos(l.position);
                if (frame == e.Root.transform || l != e.Root.transform)
                {
                    var q = l.rotation;
                    rest.Add("[" + g.x.ToString("F3", ic) + "," + g.y.ToString("F3", ic) + "," + g.z.ToString("F3", ic) + "," +
                             q.x.ToString("F6", ic) + "," + q.y.ToString("F6", ic) + "," + q.z.ToString("F6", ic) + "," + q.w.ToString("F6", ic) + "]");
                }
                var rb = l.GetComponent<Rigidbody>();
                int par = -1; for (var pt = l.parent; pt != null && par < 0; pt = pt.parent) par = System.Array.IndexOf(ls, pt);   // nearest limb above it, if any
                rows.Add("[" + g.x.ToString("F3", ic) + "," + g.y.ToString("F3", ic) + "," + g.z.ToString("F3", ic) + "," + (rb != null && rb.isKinematic ? "true" : "false") + "," + Json.Str(l.name) + "," + par + "," + (l == e.Root.transform ? "true" : "false") + "]");
            }
            var root = mainscript.GlobalFromUnityPos(e.Root.transform.position);
            return "{\"name\":" + Json.Str(e.Root.name) + ",\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"limp\":" + (Limp(e) ? "true" : "false") +
                   ",\"root\":[" + root.x.ToString("F3", ic) + "," + root.y.ToString("F3", ic) + "," + root.z.ToString("F3", ic) + "]" +
                   ",\"following\":" + (TLDRevamp.Net.Entities.ObservedRagdollCopies.Contains(e) ? "true" : "false") + ",\"sent\":" + RagdollStatesSent + ",\"applied\":" + RagdollStatesApplied +
                   ",\"keys\":" + RagdollKeysSent + ",\"deltas\":" + RagdollDeltasSent + ",\"frames\":" + RagdollFramesSent + ",\"limbsSent\":" + RagdollLimbsSent +
                   ",\"blockBytes\":" + RagdollBlockBytes + ",\"keysIn\":" + RagdollKeysIn + ",\"deltasIn\":" + RagdollDeltasIn + ",\"deltasNoKey\":" + RagdollDeltasNoKey +
                   ",\"hashMismatch\":" + RagdollHashMismatch + ",\"lateKeys\":" + RagdollLateKeys + ",\"releasedOnNone\":" + RagdollReleasedOnNone +
                   ",\"limbs\":[" + string.Join(",", rows) + "],\"restPose\":[" + string.Join(",", rest) + "]}";
        }
    }
}
