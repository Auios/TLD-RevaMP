using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityDoorAttachmentsTools
    {

        public static string GlueToDoor(uint itemNet, uint carNet, int item, int usable)
        {
            if (!ObservedEntities.TryGetValue(itemNet, out var e) || !Resolve(e) || e.Proxy || e.Root == null || e.Root.attachable == null || e.Root.attachable.attached)
                return "{\"error\":\"an owned loose attachable is required\"}";
            if (!UsableArgs(carNet, item, usable, out _, out var u)) return "{\"error\":\"no usable\"}";
            var frame = u.TDoor != null ? u.TDoor : u.RT1;
            if (frame == null) return "{\"error\":\"no moving door frame\"}";
            var col = frame.GetComponentInChildren<Collider>();
            if (col == null) return "{\"error\":\"no door collider\"}";
            var at = e.Root.attachable;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl != null && pl.pickedUp == e.Root.P) pl.Drop();
            var position = col.bounds.center;
            e.Root.transform.SetPositionAndRotation(position, frame.rotation);
            at.Attach(at.NewPoint(col.transform, position, Quaternion.identity), col);
            return DoorAttachmentPose(itemNet);
        }

        public static string DoorAttachmentPose(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null) return "{\"error\":\"entity is not loaded\"}";
            var at = e.Root.attachable;
            if (at == null || !at.attached || at.point == null) return "{\"attached\":false}";
            var f = AttachFrame(at); var t = at.transform; var iq = Quaternion.Inverse(f.rotation);
            var q = iq * t.rotation; var g = mainscript.GlobalFromUnityPos(t.position);
            return "{\"attached\":true,\"parented\":" + (RidesParent(e) ? "true" : "false") + ",\"frame\":" + Json.Str(f.name) +
                ",\"local\":" + TLDRevamp.Net.EntityCarDoorsTools.V3f(iq * (t.position - f.position)) + ",\"rotation\":[" + TLDRevamp.Net.Entities.UN(q.x) + "," + TLDRevamp.Net.Entities.UN(q.y) + "," + TLDRevamp.Net.Entities.UN(q.z) + "," + TLDRevamp.Net.Entities.UN(q.w) +
                "],\"pos\":[" + TLDRevamp.Net.Entities.UN(g.x) + "," + TLDRevamp.Net.Entities.UN(g.y) + "," + TLDRevamp.Net.Entities.UN(g.z) + "]}";
        }
        internal static bool UsableArgs(uint net, int item, int usable, out UsableKey k, out usablescript u)
        {
            k = new UsableKey { Net = net, Item = item, Usable = usable };
            u = null;
            if (!ObservedEntities.TryGetValue(net, out var e)) return false;
            k.Epoch = e.Epoch; u = LiveUsableOf(k, out _); return u != null;
        }
    }
}
