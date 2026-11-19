using static TLDRevamp.Net.EntityPartWatch;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPartWatchTools
    {
        private static readonly List<Collider> Scratch = new List<Collider>();

        /// Debugging V2's tab line for an object: its colliders switched on (of all), its body (kinematic, a physical copy,
        /// dynamic; collisions off; part colliders off for distance) and its fitted parts the player can't reach
        /// (PartWatchRules) — the 10:57 hood would have read "1 part unreachable: Car08Hood". Worked out within
        /// OverlayCollidersM, at most once a second per object (a car has 200+ colliders and the tabs refresh 4 times a second).
        internal static float OverlayCollidersM = 30f;
        internal static readonly Dictionary<uint, (float at, string text)> _colText = new Dictionary<uint, (float, string)>();
        internal static string OverlayColliders(Ent e, bool aimed = false)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (e.Root == null || pl == null) return "";
            if (!aimed && (e.Root.transform.position - pl.transform.position).sqrMagnitude > OverlayCollidersM * OverlayCollidersM)
                return "colliders: come within " + OverlayCollidersM.ToString("F0") + " m (or aim at it)";
            float now = Time.realtimeSinceStartup;
            if (_colText.TryGetValue(e.NetId, out var cached) && now - cached.at < 1f) return cached.text;
            Scratch.Clear(); e.Root.GetComponentsInChildren(true, Scratch);
            int all = Scratch.Count, on = 0;
            foreach (var c in Scratch) if (c != null && c.enabled && c.gameObject.activeInHierarchy) on++;
            var rb = e.Root.GetComponent<Rigidbody>();
            string body = rb == null ? "no body" : e.Physical ? "physical copy" : rb.isKinematic ? "kinematic" : "dynamic";
            if (rb != null && !rb.detectCollisions) body += ", no collisions";
            if (e.PartsColOff) body += ", part colliders off (far)";
            int broken = 0; string first = null;
            void Check(attachablescript part)
            {
                if (part == null || part.tosave == null || part.tosave.P == null || pl.pickedUp == part.tosave.P) return;
                var r = EntityDiag.Reach(part, pl);
                if (!PartWatchRules.Broken(r.Visible, r.Targetable, r.Expected, r.Held)) return;
                broken++; if (first == null) first = EntityLifetimeStreaming.PrefabName(part.tosave.id);
            }
            if (e.Root.attachable != null && e.Root.attachable.attached && e.Root.attachable.slot != null) Check(e.Root.attachable);
            if (e.Items != null)
                foreach (var it in e.Items)
                    if (it != null && it.partslotscripts != null)
                        foreach (var ps in it.partslotscripts) if (ps != null && ps.hasPart()) Check(ps.part());
            string text = "colliders " + on + "/" + all + " on · " + body + (broken > 0 ? " · " + broken + (broken == 1 ? " part" : " parts") + " unreachable: " + first : "");
            if (_colText.Count > 512) _colText.Clear();
            _colText[e.NetId] = (now, text);
            return text;
        }

        /// `mp partwatch`: the checks' state. `mp partwatch report`: the bug report's parts.json now. `mp partwatch break <car> <slot> off|held|fix`: the part in that slot of the car's
        /// root broken on purpose here (colliders off / on the held layer) or put right — to test the checks.
        public static string PartWatchCommand(string[] a)
        {
            if (a.Length >= 4 && a[1] == "break")
            {
                if (!ObservedEntities.TryGetValue(uint.Parse(a[2]), out var car) || !Resolve(car) || car.Root == null) return "{\"error\":\"no such car here\"}";
                int s = int.Parse(a[3]);
                if (s < 0 || s >= car.Root.partslotscripts.Count || car.Root.partslotscripts[s] == null || !car.Root.partslotscripts[s].hasPart()) return "{\"error\":\"no part in slot " + s + "\"}";
                var part = car.Root.partslotscripts[s].part(); string mode = a.Length > 4 ? a[4] : "off";
                var slotCol = car.Root.partslotscripts[s].GetComponent<Collider>();
                foreach (var c in part.GetComponentsInChildren<Collider>(true))
                {
                    if (mode == "off") c.enabled = false;
                    else if (mode == "held") c.gameObject.layer = mainscript.pickedUpLayer;
                    else c.enabled = true;
                }
                if (slotCol != null && mode != "fix") slotCol.enabled = false; else if (slotCol != null) slotCol.enabled = true;
                if (mode == "fix") part.tosave.P.resetLayer();
                return "{\"broken\":" + Json.Str(part.name) + ",\"mode\":" + Json.Str(mode) + "}";
            }
            if (a.Length >= 2 && a[1] == "report") return EntityDiag.PartsDiagnostics() ?? "{\"error\":\"no player\"}";
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var rows = new List<string>();
            if (pl != null)
                foreach (var part in EntityPartWatch.ObservedNearParts)
                {
                    if (part == null || !EntityPartWatch.ObservedWatched.TryGetValue(part, out var w)) continue;
                    var r = EntityDiag.Reach(part, pl);
                    rows.Add("{\"part\":" + Json.Str(part.name) + ",\"visible\":" + (r.Visible ? "true" : "false") + ",\"targetable\":" + (r.Targetable ? "true" : "false")
                             + ",\"expected\":" + (r.Expected ? "true" : "false") + ",\"reported\":" + (w.Done ? "true" : "false") + "}");
                }
            return "{\"enabled\":" + (PartWatch ? "true" : "false") + ",\"flags\":" + EntityDiag.PartWatchFlags + ",\"marks\":" + PartWatchMarksSaved + ",\"near\":" + EntityPartWatch.ObservedNearParts.Count
 + ",\"last\":" + Json.Str(EntityDiag.PartWatchLast) + ",\"parts\":[" + string.Join(",", rows) + "]}";
        }
    }
}
