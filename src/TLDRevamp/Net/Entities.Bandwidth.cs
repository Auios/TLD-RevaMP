using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public static long StatesSkippedIdentical, StatesSkippedCorpseRoot, StatesSkippedUnknown, StatesSkippedCreatureRange, StatesSkippedDeadAnimation;

        public static long StatesSkippedPose, StatesSkippedLocked, StatesSkippedAttached, StatesSkippedRabbitAnimation, StatesSkippedRagdoll;

        internal static bool StateKnown(int player, uint net) => Known.TryGetValue(player, out var known) && known.Contains(net);

        private static void RelayCreature(SEnt se, int from, NetReader r)
        {
            if (Mp.Server == null) { ServerFwdAll(from, r, false); return; }
            WS.Reset(); WS.Bytes(r.Buf, 0, r.End);
            Mp.Server.SendCreatureState(WS, from, se.NetId, se.Where);
            if (from != 0) Deliver(WS);
        }

        internal static bool CreatureVisible(int player, uint net, bool hasPosition, Vector3d viewer, Vector3d at)
        {
            if (!StateKnown(player, net)) { StatesSkippedUnknown++; SkippedByInterest(player); return false; }
            double dx = viewer.x - at.x, dz = viewer.z - at.z;
            if (hasPosition && dx * dx + dz * dz > EntityAuthorityInterest.StateInterestM * EntityAuthorityInterest.StateInterestM) { StatesSkippedCreatureRange++; SkippedByInterest(player); return false; }
            return true;
        }

        // Rabbit death leaves its unused root coasting; detached limbs can already be lying still.
        internal static Transform RagdollFrame(Ent e)
        {
            if (e.Root.car != null) return e.Root.transform;
            var limbs = EntityRagdolls.Limbs(e);
            if (limbs == null || !EntityRagdolls.Limp(e)) return e.Root.transform;
            foreach (var limb in limbs) if (limb != null && limb != e.Root.transform && limb.GetComponent<Rigidbody>() != null) return limb;
            foreach (var limb in limbs) if (limb != null && limb != e.Root.transform) return limb;
            return e.Root.transform;
        }

        internal static void BandwidthSnapshot(long[] into)
        {
            into[0] = StatesSkippedIdentical; into[1] = StatesSkippedCorpseRoot; into[2] = StatesSkippedUnknown;
            into[3] = StatesSkippedCreatureRange; into[4] = StatesSkippedDeadAnimation;
            if (into.Length < 10) return;
            into[5] = StatesSkippedPose; into[6] = StatesSkippedLocked; into[7] = StatesSkippedAttached;
            into[8] = StatesSkippedRabbitAnimation; into[9] = StatesSkippedRagdoll;
        }
    }
}
