using PoiKey = TLDRevamp.Net.Entities.PoiKey;
using UnityEngine;
using System.Collections.Generic;

namespace TLDRevamp.Net
{
    /// Server: an object's state records (Resync) and a building's usables (PoiUsable) go only to the players near
    /// them — within StateInterestM (Protocol.NearM, the 20 Hz tier of player states: the game stores items away beyond
    /// 300 m, and a door 1 km away can't be made out; buildings stay loaded to their generator's removeDistance, 3.8 km
    /// on the beta, so a farther building's usable is moved when its player comes within range). Before v0.69.2 both went to every player wherever they were: with players spread over the map,
    /// every fuel tick of every car and every door anyone moved reached everyone (ServerSendAll), most of it for
    /// objects 50 km away — or not known to that player at all, and dropped on arrival.
    /// A player who was away when it changed is noted (stale); when they come within range, they get the server's
    /// latest state of it at once — an object's every item record (WriteResyncAll, as for a joiner), a usable's last
    /// change (ServerPoiUsables). A player whose position isn't known yet gets everything, as before. The host's own
    /// world always gets everything (its world is the save).
    internal static partial class EntityAuthorityInterest
    {
        internal static bool InterestRelay = true;   // A/B: false = to every player (before v0.69.2)
        internal static double StateInterestM = Protocol.NearM;
        public static long ResyncRelayed, ResyncHeldBack, ResyncCaughtUp, PoiRelayed, PoiHeldBack, PoiCaughtUp;
        private static readonly Dictionary<int, HashSet<PoiKey>> _stalePoi = new Dictionary<int, HashSet<PoiKey>>();   // player → building usables likewise
        private static readonly List<MpServer.PeerSpot> _relaySpots = new List<MpServer.PeerSpot>();
        private static readonly NetWriter WFull = new NetWriter();
        private static readonly List<PoiKey> _duePoi = new List<PoiKey>();

        private static HashSet<T> StaleOf<T>(Dictionary<int, HashSet<T>> d, int player)
        {
            if (!d.TryGetValue(player, out var s)) d[player] = s = new HashSet<T>();
            return s;
        }

        private static bool Near(MpServer.PeerSpot s, double x, double z)
        {
            if (!s.HasPos) return true;
            double dx = s.Pos.x - x, dz = s.Pos.z - z;
            return dx * dx + dz * dz <= StateInterestM * StateInterestM;
        }

        /// ServerResync: the owner's changed records, to the players near the object; one who missed earlier ones gets
        /// all of them instead.
        internal static void RelayResync(uint net, Vector3d where, int from, NetWriter msg)
        {
            if (!InterestRelay || Mp.Server == null) { Entities.SendToPeers(msg, true, from); return; }
            Mp.Server.ReadySpots(_relaySpots);
            foreach (var s in _relaySpots)
            {
                if (s.Id == from) continue;
                if (!Near(s, where.x, where.z)) { Entities.MarkInterestStale(s.Id, net); ResyncHeldBack++; Entities.RecordInterestSkip(s.Id); continue; }
                if (Entities.TakeInterestStale(s.Id, net)) { Entities.WriteInterestResync(WFull, net); Mp.Server.SendToPlayer(s.Id, WFull, true); ResyncCaughtUp++; }
                else { Mp.Server.SendToPlayer(s.Id, msg, true); ResyncRelayed++; }
            }
            if (from != 0) Entities.DeliverInterestMessage(msg);
        }

        /// ServerPoiUsable: a usable's change, to the players near its building.
        internal static void RelayPoi(PoiKey k, NetWriter msg, int from, bool reliable = true)
        {
            if (!InterestRelay || Mp.Server == null) { Entities.SendToPeers(msg, reliable, from); return; }
            Mp.Server.ReadySpots(_relaySpots);
            foreach (var s in _relaySpots)
            {
                if (s.Id == from) continue;
                var stale = StaleOf(_stalePoi, s.Id);
                if (!Near(s, k.X, k.Z)) { stale.Add(k); PoiHeldBack++; Entities.RecordInterestSkip(s.Id); continue; }
                stale.Remove(k);
                Mp.Server.SendToPlayer(s.Id, msg, reliable); PoiRelayed++;
            }
            if (from != 0) Entities.DeliverInterestMessage(msg);
        }

        /// Twice a second (ServerInterestTick): what changed while a player was away, now that they are near it.
        internal static void ServerCatchUpTick()
        {
            if (!InterestRelay || Mp.Server == null) return;
            Mp.Server.ReadySpots(_relaySpots);
            foreach (var s in _relaySpots)
            {
                if (!s.HasPos) continue;
                Entities.CatchUpInterestRecords(s.Id, s.Pos, WFull);
                if (_stalePoi.TryGetValue(s.Id, out var pois) && pois.Count > 0)
                {
                    _duePoi.Clear();
                    foreach (var k in pois)
                        if (!Entities.HasInterestPoi(k) || Near(s, k.X, k.Z)) { _duePoi.Add(k); if (_duePoi.Count >= Entities.InterestCatchUpLimit) break; }
                    foreach (var k in _duePoi)
                    {
                        pois.Remove(k);
                        if (!Entities.TryInterestPoi(k, out var b)) continue;
                        WFull.Reset(); WFull.Bytes(b, 0, b.Length); Mp.Server.SendToPlayer(s.Id, WFull, true); PoiCaughtUp++;
                    }
                }
            }
        }

        internal static string InterestJson()
        {
            int ents = Entities.CountInterestStale(), pois = 0;
            foreach (var s in _stalePoi.Values) pois += s.Count;
            return "{\"on\":" + (InterestRelay ? "true" : "false") + ",\"rangeM\":" + StateInterestM.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
                   ",\"resync\":{\"relayed\":" + ResyncRelayed + ",\"heldBack\":" + ResyncHeldBack + ",\"caughtUp\":" + ResyncCaughtUp + ",\"owed\":" + ents + "}" +
                   ",\"poi\":{\"relayed\":" + PoiRelayed + ",\"heldBack\":" + PoiHeldBack + ",\"caughtUp\":" + PoiCaughtUp + ",\"owed\":" + pois + "}}";
        }

        internal static void MarkPoiStale(int player, PoiKey key) => StaleOf(_stalePoi, player).Add(key);
        internal static void ForgetRelay(int player) { _stalePoi.Remove(player); }
        internal static void ResetRelay() { _stalePoi.Clear(); }
        internal static void PreparePoiStale(int player) => StaleOf(_stalePoi, player);
    }

    public static partial class Entities
    {
        private static readonly Dictionary<int, HashSet<uint>> _staleEnts = new Dictionary<int, HashSet<uint>>();
        private static readonly List<uint> _dueEnts = new List<uint>();
        private static readonly List<MpServer.PeerSpot> _spots = new List<MpServer.PeerSpot>();

        private static HashSet<T> StaleOf<T>(Dictionary<int, HashSet<T>> d, int player)
        {
            if (!d.TryGetValue(player, out var s)) d[player] = s = new HashSet<T>();
            return s;
        }

        private static bool Near(MpServer.PeerSpot s, double x, double z)
        {
            if (!s.HasPos) return true;
            double dx = s.Pos.x - x, dz = s.Pos.z - z;
            return dx * dx + dz * dz <= EntityAuthorityInterest.StateInterestM * EntityAuthorityInterest.StateInterestM;
        }

        /// SendObject gave the player every record of it: nothing of it is owed any more.
        private static void InterestGotAll(int player, uint net) { if (_staleEnts.TryGetValue(player, out var s)) s.Remove(net); }

        /// A joiner: every usable changed this session is due — it goes out as they come near (ServerCatchUpTick).
        private static void PoiAllStale(int player)
        {
            EntityAuthorityInterest.PreparePoiStale(player);
            foreach (var k in ServerPoiUsables.Keys) EntityAuthorityInterest.MarkPoiStale(player, k);
        }

        private static void InterestForget(int player) { _staleEnts.Remove(player); EntityAuthorityInterest.ForgetRelay(player); _poseNear.Remove(player); _rabbitNear.Remove(player); _interestSkips.Remove(player); }
        private static void InterestReset() { _staleEnts.Clear(); EntityAuthorityInterest.ResetRelay(); _poseNear.Clear(); _rabbitNear.Clear(); _interestSkips.Clear(); }
    }
}
