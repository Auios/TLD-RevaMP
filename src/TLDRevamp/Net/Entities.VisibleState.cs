using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    internal static partial class EntityProxyPresentation
    {
        internal static bool ReadCompactMoving;

        internal static bool FinalState(bool locked, uint lockFrame, bool wasteFixes, BandwidthRules.Quiet quiet, bool moving, bool driven) =>
            locked && lockFrame != 0 || wasteFixes && (quiet == BandwidthRules.Quiet.Final || !moving && !driven);

        internal static double PoseStamp(double physics, double previous, bool first) =>
            first ? physics : System.Math.Max(physics, System.Math.Round(previous * 1000.0) / 1000.0 + 0.001);

        internal static bool LooseRelease(uint previousCar, uint car, bool held, bool stored) =>
            previousCar != 0 && car == 0 && !held && !stored;

        /// Let go by a holder in no car of ours (a seated holder's release is LooseRelease's), loose, at a spot in our car's box
        /// or touching the car: ours before the next physics step (Entities.Cargo ClaimLetGoInCar).
        internal static bool LetGoInCar(bool stored, bool locked, uint carriedBy, bool inBox, bool touching) =>
            !stored && !locked && carriedBy == 0 && (inBox || touching);

        internal static bool CargoBody(Ent e) => e.Root.P != null || Entities.PresentationLimbs(e) != null && Entities.PresentationLimp(e);

        internal static bool StateMoving(Ent e, bool pose, bool limbs, bool awake, ref bool velocityMoving, bool ground = false)
        {
            // Joint solver velocities can cross RestSpeed while every limb stays within the visible thresholds.
            // Such a body, or an upright animal animating in place, needs its final pose rather than solver keepalives.
            if (ground || e.Root.car == null && Entities.PresentationLimbs(e) != null && Entities.PresentationLimp(e))
            { velocityMoving = false; return pose || limbs; }
            return awake || pose || limbs;
        }

        // A release can precede the first falling physics step. Keep sending its motion until the real rest state.
        internal static bool ReleaseMotion(ref bool active, bool released, bool held, bool stored, ref bool moving,
                                          bool first = false, bool unlocked = false)
        {
            if (held || stored) active = false;
            else if (released || first || unlocked) { active = true; moving = true; }
            else if (!moving) active = false;
            return active;
        }

        internal static bool NewerState(double stamp, uint epoch, ref double previous, ref uint previousEpoch)
        {
            if (epoch == previousEpoch && stamp <= previous) return false;
            previous = stamp; previousEpoch = epoch; return true;
        }

        internal static byte[] FinalSnapshot(NetReader r, bool final)
        {
            if (!final) return null;
            var bytes = new byte[r.End]; System.Buffer.BlockCopy(r.Buf, 0, bytes, 0, r.End); return bytes;
        }

        // A quiet child's world fallback must follow its parent too: late join/interest may load the child first.
        internal static void ReplayLockedState(NetWriter w, byte[] bytes, Vector3d position, Quaternion rotation)
        {
            var r = new NetReader { Buf = bytes, Pos = 1, End = bytes.Length };
            ReadEntityHead(r, false, out uint net, out uint epoch, out double stamp, out _, out _, out var v,
                           out bool driven, out bool stored, out bool held, out bool spraying);
            w.Reset();
            EntityCodec.WriteStateHead(w, net, epoch, 0, stamp, position, rotation, v, driven, stored, held, spraying,
                           EntityCodec.ReadCarriedBy, EntityCodec.ReadLocalPos, EntityCodec.ReadLocalRot, EntityCodec.ReadFinalState, EntityCodec.ReadLockedState, EntityCodec.ReadLockItem, EntityCodec.ReadLockMount, EntityCodec.ReadLockContainer,
                           EntityCodec.ReadHandPose, EntityCodec.ReadHandPos, EntityCodec.ReadHandRot);
            w.Bytes(bytes, r.Pos, r.Remaining);
        }

        internal static void ReadEntityHead(NetReader r, bool compact, out uint net, out uint epoch, out double stamp,
                                           out Vector3d g, out Quaternion q, out Vector3 v, out bool driven, out bool stored, out bool held, out bool spraying)
        {
            ReadCompactMoving = false;
            EntityCodec.ReadHandPose = false; EntityCodec.ReadHandPos = default; EntityCodec.ReadHandRot = Quaternion.identity;
            if (!compact) { EntityCodec.ReadStateHead(r, out net, out epoch, out stamp, out g, out q, out v, out driven, out stored, out held, out spraying); return; }
            CompactPoseCodec.Read(r, out net, out epoch, out stamp, out double x, out double y, out double z, out float yaw, out ReadCompactMoving, out EntityCodec.ReadFinalState);
            g = new Vector3d(x, y, z); q = Quaternion.Euler(0, yaw, 0); v = Vector3.zero;
            driven = stored = held = spraying = false; EntityCodec.ReadCarriedBy = EntityCodec.ReadLockContainer = 0; EntityCodec.ReadLockedState = false; EntityCodec.ReadLockItem = EntityCodec.ReadLockMount = -1;
        }

        internal static Vector3 CompactVelocity(bool moving, Vector3d at, double stamp, Vector3d previous, double previousStamp) =>
            moving && previousStamp > 0 && stamp > previousStamp && stamp - previousStamp < 0.5
                ? (Vector3)(at - previous) / (float)(stamp - previousStamp) : Vector3.zero;

        // Slot parts may use a joint instead of transform parenting; trailers and articulated vehicles keep their pose stream.
        internal static bool AttachedCarPart(Ent e)
        {
            var at = e.Root != null && e.Root.car == null ? e.Root.attachable : null;
            return at != null && BandwidthRules.AttachedPart(false, at.attached, at.isPart, at.slot != null,
                   at.point != null && at.point.GetComponentInParent<carscript>() != null);
        }

        internal static bool CompactFlyer(Ent e) => e.Root.car == null && e.Root.P == null && e.Root.nyul == null &&
            e.Root.npc == null && e.Root.ragdollPos == null && e.Root.GetComponent<randomFlyer>() != null &&
            !(TLDRevamp.Multiplayer.MpApi.TryGetRagdoll(e.Root.gameObject, out _, out _) && Entities.PresentationLimp(e));   // a mod's limp body sends its limbs

        // nyulscript animals share the upright ground path. Locked, dead or ragdolled rabbits keep full poses.
        internal static bool CompactGround(Ent e) => e.PartIndex < 0 && e.Root.car == null && e.Root.npc == null &&
            (e.Root.P == null || e.Root.P.physlock == null) &&
            e.Root.nyul != null && e.Root.nyul.AI != null && e.Root.nyul.AI.alive && (Entities.PresentationLimbs(e) == null || !Entities.PresentationLimp(e));
    }

    public static partial class Entities
    {
        private static bool ReadCompactMoving { get => EntityProxyPresentation.ReadCompactMoving; set => EntityProxyPresentation.ReadCompactMoving = value; }

        private static bool FinalState(bool locked, uint lockFrame, bool wasteFixes, BandwidthRules.Quiet quiet, bool moving, bool driven) => EntityProxyPresentation.FinalState(locked, lockFrame, wasteFixes, quiet, moving, driven);

        private static double PoseStamp(double physics, double previous, bool first) => EntityProxyPresentation.PoseStamp(physics, previous, first);

        private static bool LooseRelease(uint previousCar, uint car, bool held, bool stored) => EntityProxyPresentation.LooseRelease(previousCar, car, held, stored);

        private static bool LetGoInCar(bool stored, bool locked, uint carriedBy, bool inBox, bool touching) => EntityProxyPresentation.LetGoInCar(stored, locked, carriedBy, inBox, touching);

        internal static bool CargoBody(Ent e) => EntityProxyPresentation.CargoBody(e);

        private static bool StateMoving(Ent e, bool pose, bool limbs, bool awake, ref bool velocityMoving, bool ground = false) => EntityProxyPresentation.StateMoving(e, pose, limbs, awake, ref velocityMoving, ground);

        private static bool ReleaseMotion(ref bool active, bool released, bool held, bool stored, ref bool moving,
                                          bool first = false, bool unlocked = false) => EntityProxyPresentation.ReleaseMotion(ref active, released, held, stored, ref moving, first, unlocked);

        private static bool NewerState(double stamp, uint epoch, ref double previous, ref uint previousEpoch) => EntityProxyPresentation.NewerState(stamp, epoch, ref previous, ref previousEpoch);

        private static byte[] FinalSnapshot(NetReader r, bool final) => EntityProxyPresentation.FinalSnapshot(r, final);

        private static void ReplayLockedState(NetWriter w, byte[] bytes, Vector3d position, Quaternion rotation) => EntityProxyPresentation.ReplayLockedState(w, bytes, position, rotation);

        private static void ReadEntityHead(NetReader r, bool compact, out uint net, out uint epoch, out double stamp,
                                           out Vector3d g, out Quaternion q, out Vector3 v, out bool driven, out bool stored, out bool held, out bool spraying) => EntityProxyPresentation.ReadEntityHead(r, compact, out net, out epoch, out stamp, out g, out q, out v, out driven, out stored, out held, out spraying);

        private static Vector3 CompactVelocity(bool moving, Vector3d at, double stamp, Vector3d previous, double previousStamp) => EntityProxyPresentation.CompactVelocity(moving, at, stamp, previous, previousStamp);

        internal static bool AttachedCarPart(Ent e) => EntityProxyPresentation.AttachedCarPart(e);

        internal static bool CompactFlyer(Ent e) => EntityProxyPresentation.CompactFlyer(e);

        internal static bool CompactGround(Ent e) => EntityProxyPresentation.CompactGround(e);

    }
}
