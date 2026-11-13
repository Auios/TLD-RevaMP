using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// Fitted-part observations shared by player reports and developer tools.
    public static partial class Entities
    {
        public static float PartWatchNearbyM = 12f;   // report and watch radius
        public static long PartWatchFlags;
        public static string PartWatchLast = "";

        private static readonly List<Collider> _pwCols = new List<Collider>();

        internal struct PartReach { public bool Visible, Targetable, Expected; public int Cols, Live, UseLayer, Held; }

        /// The game's own rule (partslotscript.Craft): crafted into a slot that switches colliders off, or onto an item that
        /// is (its holder's slot), the part's colliders are off on purpose — and so are those of what is fitted onto it.
        internal static bool CollidersExpected(attachablescript part)
        {
            var at = part;
            for (int depth = 0; at != null && at.slot != null && depth < 8; depth++)
            {
                if (at.slot.disableCollidersOnCraft) return false;
                var holder = at.slot.pick;
                at = holder != null && holder.tosave != null ? holder.tosave.attachable : null;
            }
            return true;
        }

        /// The part as the game's use ray finds it: its own colliders on useLayer, or its slot's (the dismount hits either).
        internal static PartReach Reach(attachablescript part, fpscontroller pl)
        {
            var r = new PartReach();
            _pwCols.Clear(); part.GetComponentsInChildren(true, _pwCols);
            int mask = pl.useLayer.value;
            foreach (var c in _pwCols)
            {
                if (c == null) continue;
                r.Cols++;
                bool live = c.enabled && c.gameObject.activeInHierarchy;
                if (live) r.Live++;
                if (c.gameObject.layer == mainscript.pickedUpLayer) r.Held++;
                if (live && (mask & (1 << c.gameObject.layer)) != 0) r.UseLayer++;
            }
            var sc = part.slot != null ? part.slot.GetComponent<Collider>() : null;
            bool slotReach = sc != null && sc.enabled && sc.gameObject.activeInHierarchy && (mask & (1 << sc.gameObject.layer)) != 0;
            r.Targetable = r.UseLayer > 0 || slotReach;
            r.Expected = CollidersExpected(part);
            var renderers = DevHooks.PartRenderers?.Invoke(part);
            if (renderers != null && renderers.Count > 0)
            { foreach (var rd in renderers) if (rd != null && rd.enabled && rd.gameObject.activeInHierarchy) { r.Visible = true; break; } }
            else foreach (var rd in part.GetComponentsInChildren<Renderer>(true)) if (rd.enabled && rd.gameObject.activeInHierarchy) { r.Visible = true; break; }
            return r;
        }

        /// The fitted parts within PartWatchNearbyM of the player: in a slot of an item of a shared group near it.
        internal static void NearParts(fpscontroller pl, List<attachablescript> into)
        {
            into.Clear();
            var at = pl.transform.position;
            float groupR = PartWatchNearbyM + 10f;
            foreach (var e in ByNet.Values)
            {
                if (e.Root == null || e.Items == null || (e.Root.transform.position - at).sqrMagnitude > groupR * groupR) continue;
                foreach (var it in e.Items)
                {
                    if (it == null || it.partslotscripts == null) continue;
                    foreach (var ps in it.partslotscripts)
                    {
                        if (ps == null || !ps.hasPart()) continue;
                        var part = ps.part();
                        if (part == null || part.tosave == null || part.tosave.P == null || into.Contains(part)) continue;
                        if ((part.transform.position - at).sqrMagnitude > PartWatchNearbyM * PartWatchNearbyM) continue;
                        into.Add(part);
                    }
                }
            }
        }

        internal static bool Shows(Transform root)
        {
            foreach (var rd in root.GetComponentsInChildren<Renderer>()) if (rd.enabled && rd.gameObject.activeInHierarchy) return true;
            return false;
        }

        internal static int SharedCount => ByNet.Count;   // Debugging V2's "shared objects here" field

        /// The bug report's parts.json: every fitted part within PartWatchNearbyM as the player can reach it — shown,
        /// reachable by the use ray, whether the game keeps its colliders on, the dismount conditions, its colliders and
        /// their layers (Entities.MountFacts) — and what the crosshair's rays hit. A part whose colliders were off was in no
        /// file before: lock.json starts from what the crosshair hits, and that part is what it can't hit (10:57's hood).
        internal static string PartsDiagnostics()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || pl.Cam == null) return null;
            var parts = new List<attachablescript>(); NearParts(pl, parts);
            var b = new System.Text.StringBuilder("{\"radiusM\":").Append(Json.Num(PartWatchNearbyM)).Append(",\"reachM\":").Append(Json.Num(pl.FrayRange))
                .Append(",\"watch\":{\"flags\":").Append(PartWatchFlags).Append(",\"last\":").Append(Json.Str(PartWatchLast)).Append("},\"crosshair\":{");
            var cam = pl.Cam.transform; var ray = new Ray(cam.position, cam.forward);
            bool firstMask = true;
            foreach (var (label, mask) in new[] { ("use", pl.useLayer.value), ("pickup", pl.pickupLayer.value), ("attach", pl.attachLayer.value) })
            {
                var hits = Physics.RaycastAll(ray, pl.FrayRange + 2f, mask);
                System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                if (!firstMask) b.Append(',');
                firstMask = false;
                b.Append(Json.Str(label)).Append(":[");
                for (int i = 0; i < hits.Length && i < 4; i++)
                {
                    var c = hits[i].collider; var root = c.transform.root;
                    if (i > 0) b.Append(',');
                    b.Append("{\"name\":").Append(Json.Str(c.name)).Append(",\"layer\":").Append(Json.Str(LayerMask.LayerToName(c.gameObject.layer)))
                     .Append(",\"root\":").Append(Json.Str(root.name)).Append(",\"net\":").Append(NetOfRoot(root)).Append(",\"m\":").Append(Json.Num(hits[i].distance))
                     .Append(",\"shows\":").Append(Shows(root) ? "true" : "false").Append(",\"trigger\":").Append(c.isTrigger ? "true" : "false").Append('}');
                }
                b.Append(']');
            }
            b.Append("},\"parts\":[");
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i]; var ps = part.slot; var P = part.tosave.P;
                var r = Reach(part, pl);
                var so = ps != null ? ps.tosaveitem : null;
                var group = EditEntityOf(part.tosave);
                if (i > 0) b.Append(',');
                b.Append("{\"holder\":").Append(Json.Str(so != null ? PrefabName(so.id) : "?")).Append(",\"entity\":").Append(group != null ? group.NetId : 0)
                 .Append(",\"copy\":").Append(group != null && group.Proxy ? "true" : "false")
                 .Append(",\"slotIndex\":").Append(so != null && so.partslotscripts != null ? so.partslotscripts.IndexOf(ps) : -1)
                 .Append(",\"slot\":").Append(Json.Str(ps != null ? ps.name : "")).Append(",\"m\":").Append(Json.Num((part.transform.position - pl.transform.position).magnitude))
                 .Append(",\"visible\":").Append(r.Visible ? "true" : "false").Append(",\"reachable\":").Append(r.Targetable ? "true" : "false")
                 .Append(",\"collidersExpected\":").Append(r.Expected ? "true" : "false").Append(",\"broken\":").Append(PartWatchRules.Broken(r.Visible, r.Targetable, r.Expected, r.Held) ? "true" : "false");
                if (ps != null)
                {
                    var sc = ps.GetComponent<Collider>();
                    b.Append(",\"slotCollider\":").Append(Json.Str(sc == null ? "none" : LayerMask.LayerToName(sc.gameObject.layer) + (sc.enabled && sc.gameObject.activeInHierarchy ? "" : ":off")))
                     .Append(",\"disableCollidersOnCraft\":").Append(ps.disableCollidersOnCraft ? "true" : "false");
                    b.Append(",\"slotState\":{\"slot\":").Append(Json.Str(ps.name)).Append(",\"cantCraft\":").Append(ps.cantCraft ? "true" : "false")
                     .Append(",\"cantUnCraft\":").Append(ps.cantUnCraft ? "true" : "false").Append(",\"attachTarget\":").Append(ps.attachTarget ? "true" : "false");
                    MountFacts(ps, part, P, pl, b, -1);
                    b.Append('}');
                }
                b.Append('}');
            }
            return b.Append("]}").ToString();
        }

        private static uint NetOfRoot(Transform root)
        {
            foreach (var e in ByNet.Values) if (e.Root != null && e.Root.transform == root) return e.NetId;
            return 0;
        }
    }
}
