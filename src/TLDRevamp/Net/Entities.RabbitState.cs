using System.Collections.Generic;
using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    internal static partial class EntityRabbits
    {
        private sealed class RabView { public byte Flags; public float Run, ShownRun; public Vector3d Interest; public bool Has; }
        private static readonly Dictionary<nyulscript, RabView> RabViews = new Dictionary<nyulscript, RabView>();
        private static readonly Dictionary<nyulscript, bool> RabSeen = new Dictionary<nyulscript, bool>();

        internal static void ApplyRabbitState(NetReader r)
        {
            var s = RabbitStateCodec.Read(r);
            if (r.Bad || !Entities.TryGetEntity(s.Net, out var e) || e.Epoch != s.Epoch) return;
            if (!e.Proxy)
            {
                if (Entities.RabbitHandoverActive(e)) RabbitHandoverTarget(e, s);
                return;
            }
            e.RabbitSnapshot = Entities.CaptureRabbitSnapshot(r);
            RestoreRabbitState(e);
            RabbitStatesApplied++;
        }

        internal static void RestoreRabbitState(Ent e)
        {
            if (e.RabbitSnapshot == null || !e.Proxy || e.Root == null) return;
            var r = new NetReader { Buf = e.RabbitSnapshot, Pos = 1, End = e.RabbitSnapshot.Length };
            var s = RabbitStateCodec.Read(r);
            if (r.Bad || s.Epoch != e.Epoch) return;
            var ny = NyOf(e);
            if (ny == null) return;
            if (!RabViews.TryGetValue(ny, out var v)) RabViews[ny] = v = new RabView { ShownRun = ny.runValue };
            v.Flags = s.Flags; v.Run = s.Run / 200f; v.Interest = new Vector3d(s.X, s.Y, s.Z); v.Has = true;
            RabSeen[ny] = s.Seen;
        }

    }
}
