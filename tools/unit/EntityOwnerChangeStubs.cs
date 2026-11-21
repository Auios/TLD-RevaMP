using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup = 100; public static double realtimeSinceStartupAsDouble => realtimeSinceStartup; }
    public struct Vector3 { public float x, y, z; public static Vector3 zero => default; }
    public struct Quaternion { public float x, y, z, w; public Quaternion(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; } public static Quaternion identity => new() { w = 1 }; }
    public static class Mathf { public static float Abs(float v) => Math.Abs(v); public static int RoundToInt(float v) => (int)Math.Round(v); public static int Clamp(int v, int lo, int hi) => Math.Clamp(v, lo, hi); public static float Sqrt(float v) => MathF.Sqrt(v); public static float Max(float a, float b) => Math.Max(a, b); }
}
public struct Vector3d
{
    public double x, y, z;
    public Vector3d(double a, double b, double c) { x = a; y = b; z = c; }
    public double sqrMagnitude => x * x + y * y + z * z;
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
}
public class OwnerChangeMain { public static OwnerChangeMain s; public Player player; public static Vector3d GlobalFromUnityPos(Vector3 p) => new(p.x, p.y, p.z); }
public class Player { public Transform transform = new(); }
public class Transform { public Vector3 position; }
public class itemPlaceRemoveScript { public static itemPlaceRemoveScript s; public double itemFreezeDist, itemUnFreezeDist; }
namespace TLDRevamp
{
    internal static class Plugin
    {
        internal static Logger Log = new();
        internal class Logger { public void LogWarning(string s) => Net.Entities.Trace.Add("warning:" + s); public void LogInfo(string s) => Net.Entities.Trace.Add("info:" + s); }
    }
    internal static class SessionLog
    {
        internal static double St => Time.realtimeSinceStartup;
        internal static readonly List<string> History = new(), HistoryRows = new();
        internal static void Event(string kind, uint net = 0, uint other = 0, int item = -1, int slot = -1, int from = -1, int to = -1, string result = "ok", uint epoch = 0, int count = 0, float value = 0)
        {
            string row = $"event:{kind}:{net}:{other}:{from}:{to}:{epoch}:{count}:{result}";
            if (kind != "owner-history") Net.Entities.Trace.Add(row);
            else if (result == "now") History.Add(row);   // one per dump
            else HistoryRows.Add(row + ":" + value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
namespace TLDRevamp.Net
{
    internal static class DedicatedServer { internal static bool Enabled = true; }
    internal static class EntityRabbits { internal static float RabbitNearM = 100, RabbitSwitchM = 10, RabbitCarMoveMps = 1, RabbitHoldS = 5; internal static long RabbitOwnerMoves; internal static string RabbitLastMove; }
    internal static class EntityHands { internal static long WornTooLate, WornTakenBack, WornRefused; }
    internal static class EntitySeats { internal static long DriverSeatRefused; }
    internal static class EntityPhysics { internal static float CrashLeaseS = 4; internal static long CrashGranted, CrashRefused, CrashLeasesKept; }
    internal static class EntityAuthorityInterest { internal static void ShareQuotaForget(int p) => Entities.Trace.Add("quota-forget:" + p); }
    internal static class EntityWorldLeases { internal static void ServerLeaseLeave(int p) => Entities.Trace.Add("lease-leave:" + p); }
    internal static class Mp
    {
        internal static Peers Server = new();
        internal class Peers
        {
            internal void ReadyPeers(List<KeyValuePair<int, Vector3d>> into) { into.Clear(); into.Add(new(1, new(500, 0, 0))); into.Add(new(2, new(1, 0, 0))); }
            internal bool SendToPlayer(int to, NetWriter w, bool reliable) { if (Entities.RecordTrace) Entities.CaptureSend("peer:" + to + ":" + reliable, w); return true; }
            internal void SendToAll(NetWriter w, bool reliable, int except, Vector3d? near, double range)
            {
                if (except != 1) SendToPlayer(1, w, reliable);
                if (except != 2) SendToPlayer(2, w, reliable);
            }
        }
    }
    public static partial class Entities
    {
        internal static readonly List<string> Trace = new();
        internal static bool RecordTrace = true;
        internal static Action DuringDeliver;
        private static bool _tpaSending, _tpaSendFailed;
        public const byte Owner = 25;
        private static readonly Dictionary<uint, SEnt> Server = new();
        private static readonly NetWriter W = new(), WS = new();
        private static readonly Dictionary<int, HashSet<uint>> Known = new();
        private static readonly HashSet<int> _hitchDeparted = new();
        private static readonly Dictionary<uint, int> _hitchTransfers = new();
        private static readonly Dictionary<uint, Op> _hitchOps = new();
        private class Op { public bool Connect; }
        private sealed partial class SEnt { internal byte[] RagdollKeyState; internal uint RagdollKeyEpoch; }
        public class Ent { public Root Root; }
        public class Root { public string name = "fixture"; }
        private static readonly Dictionary<uint, Ent> ByNet = new();
        internal static float HandoverAcceptS = 5, HandFarM, HandNearM;
        internal static int HandPerTick = 16;
        internal static long Handoffs, HandoffKeptCarried, HandoffKeptMounted, HandoffLogged, OwnerCascaded, SilentTakeovers;
        internal static long HeldClearedOnAttach, StaleAttachesForwarded, ClaimsRefused, RefusedCrash, RefusedHeld, RefusedStored, RefusedDriven;
        private static readonly Dictionary<uint, int> _refuseLogged = new();
        private static readonly List<KeyValuePair<int, Vector3d>> _handPlayers = new();
        private static float _handAcc;
        internal struct UsableKey { internal uint Net, Epoch; internal int Item; }
        private static bool ServerWornNow(SEnt e) => false;
        private static string DriverSeatClaimRefusal(SEnt e, int from) => null;
        private static HashSet<uint> KnownBy(int p) => Known.TryGetValue(p, out var k) ? k : Known[p] = new() { 7, 8, 9 };
        private static void OwnerLog(SEnt e, int to, string why) { if (RecordTrace) Trace.Add($"log:{e.NetId}:{e.OwnerId}>{to}@{e.Epoch}:{why}:claim={e.ClaimedBy}:crash={e.CrashReturnTo}"); }
        private static string SendObservation() => $":at-send={EntityRabbits.RabbitOwnerMoves},{Handoffs},{EntityPhysics.CrashGranted},{EntityHands.WornTakenBack},{OwnerCascaded},{SilentTakeovers}:" + string.Join(",", System.Linq.Enumerable.Select(Server.Values, Fields));
        internal static void CaptureSend(string target, NetWriter w) { if (RecordTrace) Trace.Add(target + ":" + Convert.ToHexString(w.Buf, 0, w.Len) + SendObservation()); }
        private static void Deliver(NetWriter w) { CaptureSend("local:0", w); DuringDeliver?.Invoke(); }
        private static void CascadeHitches(SEnt e) => Trace.Add("cascade-hitches:" + e.NetId);
        private static bool Mounted(SEnt e) => false;
        private static bool LooseSplit(SEnt e) => false;
        private static bool HitchTrailer(SEnt e) => false;
        private static void BeginHitchTransfer(SEnt e, int to) => Trace.Add("begin-hitch:" + e.NetId + ":" + to);
        private static void HitchOwnerKnows(SEnt e, int to) => Trace.Add("hitch-knows:" + e.NetId + ":" + to);
        private static void SendHitchCommand(SEnt e) => Trace.Add("hitch-command:" + e.NetId);
        private static void HitchEvent(Op op, string why) => Trace.Add("hitch-event:" + why);
        private static void InterestForget(int p) => Trace.Add("interest-forget:" + p);
        private static void EditCheckForget(int p) => Trace.Add("edit-forget:" + p);
        private static void SetLiveRecordFloor(UsableKey k, double st) => Trace.Add($"floor:{k.Net}@{k.Epoch}:{st}");
        private static void KeepAttach(SEnt e, uint parent, byte[] msg, int len) { e.AttachParent = parent; Trace.Add("keep-attach:" + e.NetId + ":" + parent); }
    }
}
