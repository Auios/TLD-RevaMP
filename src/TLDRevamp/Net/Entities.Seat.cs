using System.Collections.Generic;
using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    /// While a player is seated in a shared car that is a PROXY on their machine, the car's colliders are kinematic
    /// and the player's CharacterController depenetrates against them — shoving the player until the game's
    /// reposition method force-ejects them 40-60 m away (the passenger flicker/eject). The player's capsule ignores
    /// the seated car's colliders for the duration of the sit (restored on exit).
    internal static partial class EntitySeats
    {
        private static readonly List<(Collider pc, Collider cc)> _ignoredPairs = new List<(Collider, Collider)>();
        public static int SeatIgnoredPairs => _ignoredPairs.Count;   // diagnostics: the seated car's colliders our capsule ignores

        private static Collider PlayerCollider(fpscontroller pl)
        {
            var cc = pl.GetComponent<CharacterController>();
            if (cc != null) return cc;
            return pl.GetComponent<CapsuleCollider>();
        }

        internal static void SeatCollisionIgnore(fpscontroller pl, seatscript seat)
        {
            var pc = PlayerCollider(pl);
            if (pc == null) return;
            foreach (var col in seat.transform.root.GetComponentsInChildren<Collider>(true))
            {
                if (col == null || col == pc) continue;
                Physics.IgnoreCollision(pc, col, true);
                _ignoredPairs.Add((pc, col));
            }
        }

        internal static void SeatCollisionRestore(fpscontroller pl)
        {
            var pc = PlayerCollider(pl);
            if (pc == null) { _ignoredPairs.Clear(); return; }
            foreach (var pair in _ignoredPairs)
                if (pair.pc == pc && pair.cc != null) Physics.IgnoreCollision(pair.pc, pair.cc, false);
            _ignoredPairs.RemoveAll(pair => pair.pc == pc);
        }

        [HarmonyLib.HarmonyPatch(typeof(fpscontroller), nameof(fpscontroller.GetIn), new[] { typeof(seatscript) })]
        private static class SeatEnterCollisionFix
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(fpscontroller __instance, seatscript _u)
            {
                if (!Entities.InSession) return;
                if (mainscript.s != null && mainscript.s.player == __instance)
                { LocalSeat(out uint net, out int seat); if (net != 0) SessionLog.Event("seat-enter", net, slot: seat, from: Entities.MyId); }
                if (Entities.HasProxySeatRoot(_u.transform.root))
                { SeatCollisionIgnore(__instance, _u); return; }
            }
        }

        /// Seat availability is OURS: vanilla's FreeCanSit consults inUseMulti, the old MP's seat-reservation
        /// flag — its messages never flow in our session, so a seat the host once used stays locked on clients
        /// forever. Decide from our own remote-player seat bindings instead (the local player's occupancy is
        /// still handled by inUse, which the prefix keeps).
        [HarmonyLib.HarmonyPatch(typeof(seatscript), nameof(seatscript.FreeCanSit))]
        private static class SeatFreeOverride
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(seatscript __instance, ref bool __result)
            {
                if (!Entities.InSession || !__result) return;
                if (RemotePlayers.AnyRemoteInSeat(__instance)) __result = false;
            }
        }
        internal static float DriveClaimHoldS = 1.5f;
        public static long DriverSeatRefused, DriverSeatMoved, DriverSeatOut;
        private static readonly List<KeyValuePair<int, int>> _seatedIn = new List<KeyValuePair<int, int>>();

        /// Server: who other than `from` sits in this car's driver seat (-1: nobody, or the car isn't loaded on the host).
        internal static int DriverSeatTakenBy(uint net, int from)
        {
            if (!Entities.TryGetEntity(net, out var e) || !Entities.ResolveEntity(e) || e.Root == null || e.Root.car == null) return -1;
            var seats = VehicleSeats(e.Root);
            bool Driver(int i) => i >= 0 && i < seats.Length && seats[i] != null && seats[i].driverSeat0;
            if (from != 0 && !DedicatedServer.Enabled)
            {
                LocalSeat(out uint n, out int i);
                if (n == net && Driver(i)) return 0;
            }
            Entities.CollectSeatedPlayers(net, _seatedIn);
            foreach (var kv in _seatedIn) if (kv.Key != from && Driver(kv.Value)) return kv.Key;
            return -1;
        }

        /// Client, every 0.5 s while the local player sits in a driver seat: in someone else's car that someone else
        /// drives (its owner says so, or another player sits in this very seat), after our claim was answered.
        internal static void DriverSeatConflictTick(fpscontroller pl, Ent e)
        {
            if (e == null || !e.Proxy || e.OwnerId == Entities.MyId || !Entities.DriverClaimIs(e.NetId)) return;
            var seats = VehicleSeats(e.Root);
            int mine = System.Array.IndexOf(seats, pl.seat);
            bool other = e.Driven;
            string who = RemotePlayers.NameOf(e.OwnerId);
            foreach (var (id, name, _) in RemotePlayers.Each())
                if (RemotePlayers.SeatNetOf(id) == e.NetId && RemotePlayers.SeatIdxOf(id) == mine) { other = true; who = name; }
            if (!other) return;
            seatscript to = null;
            foreach (var st in seats)
                if (st != null && st != pl.seat && !st.driverSeat0 && !st.driverSeat1 && (st.mainseat == null || st.mainseat == st) && st.sitPos != null &&
                    st.gameObject.activeInHierarchy && st.FreeCanSit()) { to = st; break; }
            string note = (string.IsNullOrEmpty(who) ? "Someone else" : who) + " is driving this car.";
            if (to != null) { pl.GetIn(to); DriverSeatMoved++; }
            else
            {
                var st = pl.seat.transform; var car = st.root;
                float side = Vector3.Dot(st.position - car.position, car.right) > 0.2f ? 1f : -1f;
                pl.GetOut(st.position + st.up * 1.5f + car.right * (2f * side), true);
                DriverSeatOut++;
            }
            Entities.ClearDriverClaim();
            Session.Show(1, "", note);
            Plugin.Log.LogInfo($"driver seat of net {e.NetId}: {note} Moved {(to != null ? "to " + to.name : "out of the car")}.");
        }

        internal static string SeatGuardStats() =>
            "{\"refused\":" + DriverSeatRefused + ",\"moved\":" + DriverSeatMoved + ",\"out\":" + DriverSeatOut + "}";

        /// The game calls GetOut every frame a passenger sits with a place to get out to (fpscontroller.Update); it only
        /// gets out on the use key's press. The seat-leave event (and the repro recorder's seat leave, made from it) and the
        /// restore of the car's collisions came on every one of those calls: 19 seat-leave events in 0.2 s in a playtest's
        /// black box, and a seated passenger's capsule colliding with the car copy again from the frame after it sat down.
        /// Both now come only when the player is out.
        internal static bool SeatLeaveOnExit = true;   // A/B: false = before v0.69.2 (on every call)
        internal static long SeatLeavesLogged, LocalGetOutCalls;

        [HarmonyLib.HarmonyPatch(typeof(fpscontroller), nameof(fpscontroller.GetOut), new[] { typeof(Vector3), typeof(bool) })]
        private static class SeatExitCollisionRestore
        {
            [HarmonyLib.HarmonyPrefix]
            private static void Prefix(fpscontroller __instance, Vector3 pos, bool forced, out (bool sat, uint net, int seat) __state)
            {
                __state = (__instance.Bsitting, 0u, 0);
                if (Entities.InSession && mainscript.s != null && mainscript.s.player == __instance)
                {
                    LocalGetOutCalls++;
                    LocalSeat(out uint net, out int seat);
                    __state = (__instance.Bsitting, net, seat);
                    if (!SeatLeaveOnExit && net != 0) { SessionLog.Event("seat-leave", net, slot: seat, from: Entities.MyId, result: forced ? "forced" : "ok"); SeatLeavesLogged++; }
                }
                if (!Entities.InSession || !Mp.RemoteTrace) return;
                Plugin.Log.LogInfo($"SeatTrace: GetOut forced={forced} seated={__instance.Bsitting} seat={(__instance.seat != null ? __instance.seat.name : "null")} at={__instance.transform.position:F1}\n{System.Environment.StackTrace}");
            }

            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(fpscontroller __instance, bool forced, (bool sat, uint net, int seat) __state)
            {
                if (SeatLeaveOnExit && __instance.Bsitting) return;   // still seated: the call didn't get out
                if (SeatLeaveOnExit && __state.sat && __state.net != 0)
                { SessionLog.Event("seat-leave", __state.net, slot: __state.seat, from: Entities.MyId, result: forced ? "forced" : "ok"); SeatLeavesLogged++; }
                SeatCollisionRestore(__instance);
            }
        }
    }
}
