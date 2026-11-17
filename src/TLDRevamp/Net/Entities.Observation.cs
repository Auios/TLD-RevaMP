using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, Ent> ObservedEntities => ByNet;
        internal static uint ObservedFocus(int index) => TelemetryFocus[index];
        internal static uint ObservedFooting => _telemetryFooting;
        internal static bool ObservedHitchPending(uint net) => _hitchTransfers.ContainsKey(net) || EntityHitches.HasCommand(net);
        internal static uint ObservedParent(uint net) => Server.TryGetValue(net, out var e) ? e.AttachParent : 0;
        internal static bool ObservedHitch(uint net) => Server.TryGetValue(net, out var e) && e.Hitch;
        internal static bool ObservedServerEntity(uint net) => Server.ContainsKey(net);
        internal static bool ObservedServerPose(uint net, out Vector3d position, out Quaternion rotation)
        {
            if (Server.TryGetValue(net, out var e) && e.HasState) { position = e.Pos; rotation = e.Rot; return true; }
            position = default; rotation = Quaternion.identity; return false;
        }
        internal static Transform OverlayFrame(Ent e)
        {
            var limbs = e.Root.car == null && e.Rag != null && (e.Rag.Act == null || e.Rag.Act.ragdoll) ? EntityRagdolls.Limbs(e) : null;
            if (limbs != null)
            {
                foreach (var limb in limbs) if (limb != null && limb != e.Root.transform && limb.GetComponent<Rigidbody>() != null) return limb;
                foreach (var limb in limbs) if (limb != null && limb != e.Root.transform) return limb;
            }
            return e.Root.transform;
        }
        internal static bool OverlaySample(Ent e, out Vector3d p, out Quaternion q)
        {
            p = default; q = Quaternion.identity;
            if (!e.Proxy || e.ReceivedStateEpoch != e.Epoch) return false;
            if (e.PoseLocked && e.LockFrame != 0 && LockedWorld(e, out p, out q)) return true;
            if (e.Ip == null || !e.Ip.Ready) return false;
            if (e.CarriedBy != 0 && ByNet.TryGetValue(e.CarriedBy, out var car) && car.Root != null)
            {
                var ct = car.Root.transform;
                p = mainscript.GlobalFromUnityPos(e.PoseLocked ? ct.position + ct.rotation * (Vector3)e.Ip.LastPos : ct.TransformPoint((Vector3)e.Ip.LastPos));
                q = ct.rotation * e.Ip.LastRot;
                return true;
            }
            p = e.CarriedBy != 0 ? e.CarriedGlobal : e.Ip.LastPos;
            q = e.CarriedBy != 0 ? e.CarriedGlobalRot : e.Ip.LastRot;
            return true;
        }
        internal static bool OverlayBolted(Ent e) => RidesParent(e) && (e.PartIndex >= 0 || e.Items.Count <= 1 || true);
        internal static bool OverlayMoving(Ent e) => ObservationMoving(e.SettledState != null,
            e.PoseLocked || e.Stored || e.Held || RidesParent(e) || EntityProxyPresentation.AttachedCarPart(e) || EntityLocks.RidesOwnersContainer(e),
            e.LastVel.sqrMagnitude > RestSpeed * RestSpeed, e.Ip != null && e.Ip.LastPoseChanged);
        private static bool ObservationMoving(bool final, bool constrained, bool velocity, bool pose) => !final && !constrained && (velocity || pose);
    }
}
