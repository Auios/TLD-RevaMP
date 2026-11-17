using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal static partial class EntityDoors
    {
        private static Transform AttachFrameRoot(attachablescript at)
        {
            var it = at.AttachedToTosave();
            if (it != null) return it.transform;
            var poi = at.AttachedToPOI();
            return poi != null ? poi.transform : null;
        }

        internal static int[] AttachFramePath(attachablescript at, Transform frame)
        {
            var root = AttachFrameRoot(at);
            if (root == null || !frame.IsChildOf(root)) return null;
            var path = new List<int>();
            for (var t = frame; t != root; t = t.parent) path.Add(t.GetSiblingIndex());
            path.Reverse();
            return path.ToArray();
        }

        internal static bool DoorAttachFrame(Transform frame)
        {
            if (frame == null) return false;
            var it = frame.GetComponentInParent<tosaveitemscript>();
            if (it == null) return false;
            var e = Entities.SharedGroupOf(it);
            if (e == null) return HasFrame(it);
            foreach (var member in e.Items) if (member != null && HasFrame(member)) return true;
            return false;
            bool Under(Transform t) => t != null && frame.IsChildOf(t);
            bool HasFrame(tosaveitemscript member)
            {
                foreach (var u in EntityUsables.ItemUsables(member))
                    if (u != null && (Under(u.RT1) || Under(u.RT2) || Under(u.TDoor))) return true;
                var bus = member.busdoorscript;
                if (bus != null && bus.doors != null)
                    foreach (var d in bus.doors) if (d != null && d.doors != null)
                        foreach (var leaf in d.doors) if (leaf != null && Under(leaf.transform)) return true;
                return false;
            }
        }

        /// Collider lookup can fall back to the item root. Rebuild the point in the sender's actual moving frame.
        internal static bool RestoreAttachFrame(attachablescript at, int[] path, Vector3 point)
        {
            if (path == null) return true;
            if (at.slot != null || at.point == null) return false;
            var frame = AttachFrameRoot(at);
            foreach (int index in path)
            {
                if (frame == null || index < 0 || index >= frame.childCount) { EntityAttachments.AttachPosesBad++; return false; }
                frame = frame.GetChild(index);
            }
            if (frame == null || frame == at.transform || frame.IsChildOf(at.transform)) { EntityAttachments.AttachPosesBad++; return false; }
            at.point.SetParent(frame, true); at.point.localPosition = point;
            if (!at.parentAtAttach && DoorAttachFrame(frame))
            {
                bool was = Entities.ApplyingDoorAttach; Entities.ApplyingDoorAttach = true;
                try { at.parentAtAttach = true; at.Attach(at.point); }
                finally { Entities.ApplyingDoorAttach = was; }
            }
            return true;
        }

        private sealed class DoorAttachment
        {
            public int[] Path; public Vector3 Point, Position; public Quaternion Rotation; public Transform BoundPoint;
        }
        private static readonly Dictionary<uint, DoorAttachment> _storedDoorAttaches = new Dictionary<uint, DoorAttachment>();
        private static readonly List<uint> _restoredDoorAttaches = new List<uint>();

        [HarmonyLib.HarmonyPatch(typeof(attachablescript), nameof(attachablescript.Detach))]
        private static class ForgetDoorAttachment
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(attachablescript __instance)
            {
                if (Entities.ApplyingDoorDetach || Entities.ApplyingDoorAttach || __instance.tosave == null) return;
                var e = Entities.OwnEntity(__instance.tosave);
                if (e != null) _storedDoorAttaches.Remove(e.NetId);
            }
        }

        internal static void KeepDoorAttach(uint net, int[] path, Vector3 point, Vector3 position, Quaternion rotation, Transform boundPoint = null)
        {
            _storedDoorAttaches[net] = new DoorAttachment { Path = path, Point = point, Position = position, Rotation = rotation, BoundPoint = boundPoint };
        }

        internal static void ForgetStoredDoorAttach(uint net) => _storedDoorAttaches.Remove(net);
        internal static void BindStoredDoorAttach(uint net, Transform point)
        {
            if (_storedDoorAttaches.TryGetValue(net, out var door)) door.BoundPoint = point;
        }
        internal static void ResetDoorAttachments() => _storedDoorAttaches.Clear();

        internal static void RestoreStoredDoorAttaches()
        {
            _restoredDoorAttaches.Clear();
            foreach (var kv in _storedDoorAttaches)
            {
                if (!Entities.TryGetEntity(kv.Key, out var e)) { _restoredDoorAttaches.Add(kv.Key); continue; }
                if (EntityHands.HandManaged(e)) continue;   // LateSample runs after final hand placement; retry after its release glide
                var at = e.Root != null ? e.Root.attachable : null;
                if (at == null) continue;
                if (!at.attached) { _restoredDoorAttaches.Add(kv.Key); continue; }
                if (at.point == null || at.point == kv.Value.BoundPoint) continue;
                var s = kv.Value;
                if (!RestoreAttachFrame(at, s.Path, s.Point)) continue;
                Entities.ApplyDoorAttachPose(e, at, s.Position, s.Rotation);
                s.BoundPoint = at.point;
            }
            foreach (uint net in _restoredDoorAttaches) _storedDoorAttaches.Remove(net);
        }
    }
}
