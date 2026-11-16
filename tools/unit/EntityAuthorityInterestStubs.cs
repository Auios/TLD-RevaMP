using System;
using System.Collections.Generic;
using System.Linq;
using TLDRevamp.Net;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static int InterestPerTick = 150;
        private static readonly Dictionary<int, HashSet<uint>> _staleEnts = new Dictionary<int, HashSet<uint>>();
        private static readonly List<uint> _dueEnts = new List<uint>();
        private static HashSet<T> StaleOf<T>(Dictionary<int, HashSet<T>> map, int player) { if (!map.TryGetValue(player, out var set)) map[player] = set = new HashSet<T>(); return set; }
        private static readonly Dictionary<PoiKey, byte[]> ServerPoiUsables = new Dictionary<PoiKey, byte[]>();
        private sealed partial class SEnt { internal readonly Dictionary<int, byte[]> ItemState = new Dictionary<int, byte[]>(); }
        private static void Deliver(NetWriter writer) { }
        private static void WriteResyncAll(NetWriter writer, SEnt record) { writer.Reset(); writer.U32(record.NetId); }
    }
}

namespace AuthorityInterestFakes
{
    internal static class Protocol { internal const double NearM = 1000; }
    internal static class Transport { internal static Server Server; }
    internal sealed class Server
    {
        internal struct PeerSpot { internal int Id; internal bool HasPos; internal Vector3d Pos; }
        internal readonly List<PeerSpot> Spots = new List<PeerSpot>();
        internal void ReadySpots(List<PeerSpot> into) { into.Clear(); into.AddRange(Spots); }
        internal void SendToPlayer(int player, NetWriter writer, bool reliable) => Kernel.Packet(player, writer, reliable);
        internal void Kick(int player) => Kernel.Events.Add("kick:" + player);
    }
    internal static partial class Kernel
    {
        private const byte Resync = 31;
        internal static double Stamp = 12.25;
        internal sealed class FullRecord { internal uint NetId, Epoch; internal readonly Dictionary<int, byte[]> ItemState = new Dictionary<int, byte[]>(); }
        internal static int InterestCatchUpLimit = 150;
        internal static readonly Dictionary<int, HashSet<uint>> _staleEnts = new Dictionary<int, HashSet<uint>>();
        internal static readonly List<uint> Due = new List<uint>();
        internal static void MarkInterestStale(int player, uint net) { if (!_staleEnts.TryGetValue(player, out var set)) _staleEnts[player] = set = new HashSet<uint>(); set.Add(net); }
        internal static bool TakeInterestStale(int player, uint net) { if (!_staleEnts.TryGetValue(player, out var set)) _staleEnts[player] = set = new HashSet<uint>(); return set.Remove(net); }
        internal static int CountInterestStale() => _staleEnts.Values.Sum(s => s.Count);
        internal static void InterestGotAll(int player, uint net) { if (_staleEnts.TryGetValue(player, out var set)) set.Remove(net); }
        internal static bool NearInterestRecord(Vector3d viewer, Vector3d at) { double dx = viewer.x - at.x, dz = viewer.z - at.z; return dx * dx + dz * dz <= EntityAuthorityInterest.StateInterestM * EntityAuthorityInterest.StateInterestM; }
        internal static readonly Dictionary<uint, (Vector3d at, byte[] bytes)> Records = new Dictionary<uint, (Vector3d, byte[])>();
        internal static readonly Dictionary<Entities.PoiKey, byte[]> Pois = new Dictionary<Entities.PoiKey, byte[]>();
        internal static readonly List<string> Events = new List<string>();
        internal static Action<int> DuringSend;
        internal static void Packet(int player, NetWriter writer, bool reliable)
        { Events.Add(player + ":" + reliable + ":" + Convert.ToHexString(writer.Buf.AsSpan(0, writer.Len))); DuringSend?.Invoke(player); }
        internal static void SendToPeers(NetWriter writer, bool reliable, int except) => Events.Add("all:" + except + ":" + reliable + ":" + Convert.ToHexString(writer.Buf.AsSpan(0, writer.Len)));
        internal static void DeliverInterestMessage(NetWriter writer) => Packet(0, writer, false);
        internal static void RecordInterestSkip(int player) => Events.Add("skip:" + player);
        internal static void WriteInterestResync(NetWriter writer, uint net)
        { var b = Records[net].bytes; writer.Reset(); writer.Bytes(b, 0, b.Length); }
        internal static bool HasInterestPoi(Entities.PoiKey key) => Pois.ContainsKey(key);
        internal static bool TryInterestPoi(Entities.PoiKey key, out byte[] bytes) => Pois.TryGetValue(key, out bytes);
    }
}
