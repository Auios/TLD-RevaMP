using System.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityHealthWatchTools
    {

        /// `mp healthwatch`: the watch's state. `mp healthwatch ground`: the ground under the player (which layer).
        /// `mp healthwatch break <net> collisions|heldlayer|pickup|fling|sink|fix`: break an object here on purpose, to test.
        public static string HealthWatchCommand(string[] a)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (a.Length >= 2 && a[1] == "ground")
            {
                if (pl == null) return "{\"error\":\"no player\"}";
                var p = pl.transform.position;
                var hits = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                var hs = new List<string>();
                foreach (var h in hits) hs.Add("{\"name\":" + Json.Str(h.collider.name) + ",\"root\":" + Json.Str(h.collider.transform.root.name) + ",\"layer\":" + Json.Str(LayerMask.LayerToName(h.collider.gameObject.layer)) + ",\"m\":" + Json.Num(h.distance) + "}");
                return "{\"hits\":[" + string.Join(",", hs) + "],\"groundOver\":" + Json.Num((float)GroundOver(p)) + ",\"playerY\":" + Json.Num(p.y) + "}";
            }
            if (a.Length >= 4 && a[1] == "break")
            {
                if (!ObservedEntities.TryGetValue(uint.Parse(a[2], CultureInfo.InvariantCulture), out var e) || !Resolve(e) || e.Root == null) return "{\"error\":\"no such object here\"}";
                var rb = e.Root.GetComponent<Rigidbody>(); string mode = a[3];
                switch (mode)
                {
                    case "collisions": if (rb != null) rb.detectCollisions = false; break;
                    case "heldlayer": foreach (var c in e.Root.GetComponentsInChildren<Collider>(true)) c.gameObject.layer = mainscript.pickedUpLayer; break;
                    case "pickup": foreach (var c in e.Root.GetComponentsInChildren<Collider>(true)) c.gameObject.layer = LayerMask.NameToLayer("UI"); break;
                    case "fling": if (rb != null && !rb.isKinematic) rb.velocity = Vector3.up * 400f; break;
                    case "sink": if (rb != null) rb.isKinematic = true; e.Root.transform.position += Vector3.down * 30f; break;
                    case "slot":
                    {
                        // the first fitted part made to name another slot of the root (fix puts it back)
                        partslotscript from = null, to = null;
                        foreach (var ps in e.Root.partslotscripts) { if (ps == null) continue; if (from == null && ps.hasPart()) from = ps; else if (to == null && ps != from) to = ps; }
                        if (from == null || to == null) return "{\"error\":\"no fitted part and another slot\"}";
                        _brokenSlot = (from.part(), from); from.part().slot = to; break;
                    }
                    case "fix":
                        if (rb != null) rb.detectCollisions = true;
                        if (e.Root.P != null) e.Root.P.resetLayer();
                        if (_brokenSlot.part != null) { _brokenSlot.part.slot = _brokenSlot.slot; _brokenSlot = default; }
                        break;
                    default: return "{\"error\":\"collisions|heldlayer|pickup|fling|sink|slot|fix\"}";
                }
                return "{\"broken\":" + Json.Str(e.Root.name) + ",\"mode\":" + Json.Str(mode) + "}";
            }
            if (a.Length >= 2 && a[1] == "reset") { ResetHealthObservations(); return "{\"reset\":true}"; }
            var rows = new List<string>();
            foreach (var kv in TLDRevamp.Net.Entities.ObservedHwActive) rows.Add("{\"net\":" + kv.Key + ",\"line\":" + Json.Str(kv.Value) + "}");
            return "{\"enabled\":" + (HealthWatch ? "true" : "false") + ",\"near\":" + TLDRevamp.Net.Entities.ObservedHwCycle.Count + ",\"far\":" + TLDRevamp.Net.Entities.ObservedHwFar.Count + ",\"flags\":" + HealthWatchFlags + ",\"marks\":" + AutoMarksSaved
                   + ",\"items\":" + (DevHooks.ItemFlags?.Invoke() ?? 0) + ",\"last\":" + Json.Str(HealthWatchLast) + ",\"itemLast\":" + Json.Str(DevHooks.ItemLast?.Invoke() ?? "") + ",\"active\":[" + string.Join(",", rows) + "]}";
        }
        internal static (attachablescript part, partslotscript slot) _brokenSlot;   // `break slot`, for fix
    }
}
