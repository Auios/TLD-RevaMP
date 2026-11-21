using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal enum ClaimReason : byte
        {
            Driver = 1, DriverRetry, Pickup, CargoNear, CargoContact, CargoRelease, Contents, ContentsPlaced,
            CarCargo, RabbitNear, RabbitContact, RabbitObjectNear, RabbitObjectContact, PushGrip, Wear, BodyNear, BodyContact, Debug, ItemNear, ItemContact
        }

        private static string ClaimName(ClaimReason reason) => "claim: " + reason.ToString();
        public static long OwnerReconfirmed;
        internal static float ReconfirmEveryS = 2f;
        private static readonly System.Collections.Generic.Dictionary<uint, float> _reconfirmedAt = new System.Collections.Generic.Dictionary<uint, float>();
        internal static void WriteClaim(NetWriter w, uint net, ClaimReason reason)
        {
            w.U8(Claim); w.U32(net); w.U8((byte)reason);
        }

        private static void ServerClaim(int from, NetReader r)
        {
            uint net = r.U32();
            var reason = (ClaimReason)r.U8();
            if (r.Bad || r.Remaining != 0 || reason < ClaimReason.Driver || reason > ClaimReason.ItemContact || !Server.TryGetValue(net, out var se)) return;
            if (se.OwnerId == from)
            {
                LogOwnerHistory(se);
                // theirs already: a claim from its owner means its game still has it as someone else's copy (an Owner
                // message that reached it while its copy was being fetched again, a re-sent record older than the grant).
                // Ignored, it claimed for ever and nobody simulated the object: a car frozen where it was, the friend at
                // its wheel unable to move it until the host sat in (2026-11-21). Tell it again.
                // at most one per object every ReconfirmEveryS: a client whose producer claims its own object (cargo,
                // rabbit claims check only their cooldown) costs a reply and one fresh state from it each time
                float nowR = Time.realtimeSinceStartup;
                if (from != 0 && (!_reconfirmedAt.TryGetValue(net, out float lastR) || nowR - lastR >= ReconfirmEveryS))
                {
                    if (_reconfirmedAt.Count > 4096) _reconfirmedAt.Clear();
                    _reconfirmedAt[net] = nowR;
                    W.Reset(); W.U8(Owner); W.U32(net); W.VarU32((uint)from); W.U32(se.Epoch);
                    ServerSendTo(from, W, true);
                    OwnerReconfirmed++;
                    if (OwnerReconfirmed <= 20) Plugin.Log.LogWarning($"claim from its own owner: net {net} by player {from} ({ClaimName(reason)}) — its game had it as a copy; told it again (epoch {se.Epoch})");
                }
                return;
            }
            var busy = Busy(se, ClaimBusy);
            if (busy == BusyKind.None) busy = Busy(se, BusyKind.Driven);   // hands, inventory and head come before the driver report
            if (busy != BusyKind.None)
            {
                // refused: the claimer may already simulate it (PickupClaim takes provisional ownership) —
                // tell it who owns it, or both machines simulate the object and their copies drift apart
                string why = busy switch
                {
                    BusyKind.Hitched => "hitched to car",
                    BusyKind.CrashLease => "crash lease (returns to " + se.CrashReturnTo + ")",
                    BusyKind.Held => "held",
                    BusyKind.Stored => "stored",
                    BusyKind.Worn => "worn",
                    _ => "driven"
                };
                RefuseClaim(from, se, why, reason);
                return;
            }
            string seatTaken = DriverSeatClaimRefusal(se, from);   // someone else at its wheel (Entities.Seat)
            if (seatTaken != null) { EntitySeats.DriverSeatRefused++; RefuseClaim(from, se, seatTaken, reason); return; }
            se.ClaimedBy = from; se.ClaimedAt = Time.realtimeSinceStartup; se.ClaimedFromEpoch = se.Epoch;
            ServerSetOwner(se, from, OwnerChange.Claim, CrashReturnRule.Keep, W,
                ClaimName(reason) + (se.Driven ? " (owner's driver report " + (Time.realtimeSinceStartup - se.DrivenAt).ToString("F1") + " s old)" : ""),
                beforeSend: static (changed, to) =>
                {
                    if (changed.Rabbit) changed.RabbitHoldUntil = Time.realtimeSinceStartup + EntityRabbits.RabbitHoldS;   // a car about to hit it: the nearest-player rule waits
                });
            CascadeOwner(se);
        }
    }
}
