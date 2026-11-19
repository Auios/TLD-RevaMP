using static TLDRevamp.Net.EntityHealthRules;
using static TLDRevamp.Net.EntityHealthWatch;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPartWatch
    {
        public static bool PartWatch = true;   // A/B: false = no automatic checks of fitted parts

        public static bool PartWatchMarks = true;   // false: the event only, no automatic mark

        public static float PartWatchNearbyEveryS = 2f, PartWatchLookEveryS = 0.2f;
        internal sealed class WatchedPart
        {
            public bool Done; public float LostAt = -1f, LookAt = -1f;
            public readonly List<Renderer> Renderers = new List<Renderer>();
        }

        private static readonly Dictionary<attachablescript, WatchedPart> _watched = new Dictionary<attachablescript, WatchedPart>();

        internal static System.Collections.Generic.IReadOnlyDictionary<attachablescript, WatchedPart> ObservedWatched => _watched;

        private static readonly List<attachablescript> _nearParts = new List<attachablescript>();

        internal static System.Collections.Generic.IReadOnlyList<attachablescript> ObservedNearParts => _nearParts;

        private static float _pwScanAt = -99f, _pwLookAt = -99f;

        internal static void PartWatchTick()
        {
            if (!PartWatch) return;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || pl.Cam == null) return;
            float now = Time.realtimeSinceStartup;
            if (now - _pwScanAt >= PartWatchNearbyEveryS) { _pwScanAt = now; PartWatchScan(pl, now); EntityItemWatch.ItemWatchScan(pl, now); }
            if (now - _pwLookAt >= PartWatchLookEveryS) { _pwLookAt = now; PartWatchLook(pl, now); EntityItemWatch.ItemWatchLook(pl, now); }
        }

        private static void PartWatchScan(fpscontroller pl, float now)
        {
            EntityDiag.NearParts(pl, _nearParts);
            // gone parts (destroyed objects compare equal to null)
            List<attachablescript> dead = null;
            foreach (var k in _watched.Keys) if (k == null) (dead ?? (dead = new List<attachablescript>())).Add(k);
            if (dead != null) foreach (var k in dead) _watched.Remove(k);
            foreach (var part in _nearParts)
            {
                if (!_watched.TryGetValue(part, out var w)) _watched[part] = w = new WatchedPart();
                w.Renderers.Clear(); part.GetComponentsInChildren(true, w.Renderers);
                if (pl.pickedUp == part.tosave.P || HeldAbove(part.transform, pl)) { w.LostAt = -1f; continue; }   // carried, or on something carried
                var r = EntityDiag.Reach(part, pl);
                if (!PartWatchRules.Broken(r.Visible, r.Targetable, r.Expected, r.Held, r.Live)) { w.LostAt = -1f; continue; }
                if (w.LostAt < 0f) w.LostAt = now;
                else if (now - w.LostAt >= PartWatchTiming.NearbyAfterS) PartWatchFound(part, w, r, PartWatchRules.Why(r.Live, r.Held), "nearby", null, now);
            }
        }

        private static void PartWatchLook(fpscontroller pl, float now)
        {
            if (_nearParts.Count == 0) return;
            var cam = pl.Cam.transform; var ray = new Ray(cam.position, cam.forward);
            float reach = pl.FrayRange; attachablescript best = null; float bestD = float.MaxValue;
            foreach (var part in _nearParts)
            {
                if (part == null || !_watched.TryGetValue(part, out var w)) continue;
                foreach (var rd in w.Renderers)
                    if (rd != null && rd.enabled && rd.gameObject.activeInHierarchy && rd.bounds.IntersectRay(ray, out float d) && d <= reach && d < bestD) { bestD = d; best = part; }
            }
            foreach (var kv in _watched) if (kv.Key != best) kv.Value.LookAt = -1f;
            if (best == null || pl.pickedUp == best.tosave.P || HeldAbove(best.transform, pl)) return;
            var bw = _watched[best];
            var r = EntityDiag.Reach(best, pl);
            string why = null, blocker = null;
            if (!r.Targetable) { if (PartWatchRules.Broken(r.Visible, false, r.Expected, r.Held, r.Live)) why = PartWatchRules.Why(r.Live, r.Held); }
            else if (Physics.Raycast(ray, out var hit, reach, pl.useLayer) && hit.distance < bestD - 0.05f && !OfPart(hit.collider, best) && !EntityDiag.Shows(hit.collider.transform.root))
            { why = PartWatchRules.Blocked; blocker = hit.collider.transform.root.name; }
            if (why == null) { bw.LookAt = -1f; return; }
            if (bw.LookAt < 0f) bw.LookAt = now;
            else if (now - bw.LookAt >= PartWatchTiming.LookAfterS) PartWatchFound(best, bw, r, why, "crosshair", blocker, now);
        }

        private static bool OfPart(Collider c, attachablescript part) =>
            c.transform.IsChildOf(part.transform) || (part.slot != null && c.transform == part.slot.transform);

        private static void PartWatchFound(attachablescript part, WatchedPart w, PartReach r, string why, string how, string blocker, float now)
        {
            if (w.Done) return;
            w.Done = true; EntityDiag.PartWatchFlags++;
            var ts = part.tosave;
            var own = OwnEntity(ts); var group = EditEntityOf(ts);
            uint partNet = own != null ? own.NetId : 0, groupNet = group != null ? group.NetId : 0;
            var so = part.slot != null ? part.slot.tosaveitem : null;
            int slot = so != null && so.partslotscripts != null ? so.partslotscripts.IndexOf(part.slot) : -1;
            string kind = EntityLifetimeStreaming.PrefabName(ts.id), on = so != null ? EntityLifetimeStreaming.PrefabName(so.id) : "?";
            EntityDiag.PartWatchLast = kind + " net " + (partNet != 0 ? partNet : groupNet) + " on " + on + " #" + slot + ": " + why + " — (" + how + ", colliders " + r.Live + "/" + r.Cols + " on, " + r.UseLayer + " on the use layer, "
                            + r.Held + " on the held layer" + (blocker != null ? ", in front: " + blocker : "") + ", " + (own != null ? (own.Proxy ? "a copy" : "ours") : group != null ? (group.Proxy ? "a copy's part" : "our part") : "not shared") + ")";
            SessionLog.Event("part-untargetable", partNet != 0 ? partNet : groupNet, other: groupNet, slot: slot, count: r.Cols, value: r.Live, dx: r.UseLayer, dy: r.Held, result: why);
            Plugin.Log.LogWarning("part watch: " + EntityDiag.PartWatchLast);
            if (PartWatchMarks && AutoMark("auto: " + EntityDiag.PartWatchLast, partNet != 0 ? partNet : groupNet)) PartWatchMarksSaved++;
        }

        internal static IReadOnlyList<Renderer> RenderersOf(attachablescript part) => _watched.TryGetValue(part, out var w) ? w.Renderers : null;

        public static long PartWatchMarksSaved;
    }
}
