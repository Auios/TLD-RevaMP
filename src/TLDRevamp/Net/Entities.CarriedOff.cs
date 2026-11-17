using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal static partial class EntityAttachments
    {
        private static readonly NetWriter W = new NetWriter();
        // a part on a member carried away from its group: Entities.PartOff
        internal static long CarriedOffSent, CarriedOffApplied;
        private static readonly Dictionary<(uint, int), tosaveitemscript> _carriedOff = new Dictionary<(uint, int), tosaveitemscript>();
        private static Entities.Ent _detachCarrier;
        private static int _detachCarrierSlot;

        internal static bool CarriedFar(attachablescript at, out Entities.Ent carrier)
        {
            carrier = null;
            if (!Entities.AttachmentCarriesParts || at == null || at.tosave == null || at.slot == null || at.slot.tosaveitem == null || Entities.AttachmentRoot(at.tosave) != null) return false;
            var group = Entities.AttachmentGroup(at.tosave);
            if (group != null && group.Root != null) return false;
            carrier = Entities.AttachmentRoot(at.slot.tosaveitem);
            return carrier != null && carrier.ParentNet != 0 && carrier.PartIndex > 0;
        }

        /// Here, the part a PartDetached names by its carrier's slot: the one its sender unbolted, or what that slot holds.
        internal static tosaveitemscript CarriedItem(uint carrierNet, int slot, int owner)
        {
            if (owner == Entities.MyId && _carriedOff.TryGetValue((carrierNet, slot), out var mine) && mine != null) { _carriedOff.Remove((carrierNet, slot)); return mine; }
            if (!Entities.TryAttachmentCarrier(carrierNet, out var c) || c.Root == null || c.Root.partslotscripts == null || slot < 0 || slot >= c.Root.partslotscripts.Count) return null;
            var s = c.Root.partslotscripts[slot];
            var part = s != null ? s.part() : null;
            return part != null ? part.tosave : null;
        }

        private static void SendCarriedOff(tosaveitemscript ts, Entities.Ent carrier, int slot)
        {
            if (slot < 0) return;
            var t = ts.transform; var g = mainscript.GlobalFromUnityPos(t.position); var q = t.rotation;
            if (_carriedOff.Count > 64) _carriedOff.Clear();
            _carriedOff[(carrier.NetId, slot)] = ts;
            W.Reset(); W.U8(Entities.PartOff); W.U32(carrier.NetId); W.U32(carrier.Epoch); W.VarU32(0u);
            W.F64(g.x); W.F64(g.y); W.F64(g.z); W.F32(q.x); W.F32(q.y); W.F32(q.z); W.F32(q.w);
            W.VarU32((uint)slot);
            Entities.SendAttachmentMessage(W, true);
            CarriedOffSent++;
            SessionLog.Event("part-off-sent", carrier.NetId, item: slot, from: Entities.MyId, epoch: carrier.Epoch, result: "carried");
        }

        /// Detach hook, before: the slot is gone after the Detach.
        internal static void NoteCarrier(attachablescript at)
        {
            _detachCarrier = !Entities.AttachmentApplyingDetach && Entities.InSession && CarriedFar(at, out var carrier) ? carrier : null;
            _detachCarrierSlot = _detachCarrier != null && _detachCarrier.Root != null && _detachCarrier.Root.partslotscripts != null ? _detachCarrier.Root.partslotscripts.IndexOf(at.slot) : -1;
        }

        /// Detach hook, after: the part went out named by its carrier's slot.
        internal static bool SentCarriedOff(tosaveitemscript ts)
        {
            if (_detachCarrier == null) return false;
            if (ts != null) SendCarriedOff(ts, _detachCarrier, _detachCarrierSlot);
            _detachCarrier = null;
            return true;
        }

        internal static void ResetCarriedOff() => _carriedOff.Clear();
    }
}
