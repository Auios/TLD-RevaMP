using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static bool ObservedServerHasTune(uint net) => Server.TryGetValue(net, out var se) && se.Tune != null;
        internal readonly struct TumbleServerObservation
        {
            internal readonly uint NetId;
            internal readonly int OwnerId;
            internal readonly Vector3d Where;
            internal readonly bool HasState;
            internal readonly byte[] Record;
            internal TumbleServerObservation(uint net, int owner, Vector3d where, bool state, byte[] record)
            { NetId = net; OwnerId = owner; Where = where; HasState = state; Record = record; }
        }
        internal static bool ObserveTumbleServer(uint net, out TumbleServerObservation observation)
        {
            if (!Server.TryGetValue(net, out var se)) { observation = default; return false; }
            observation = new TumbleServerObservation(se.NetId, se.OwnerId, se.Where, se.HasState, se.Record != null ? (byte[])se.Record.Clone() : null);
            return true;
        }
        internal static void ScheduleCensus(uint net, bool restartRepair = false)
        {
            if (restartRepair) _repairWait.Remove(net);
            _censusDue[net] = new CensusDue { At = Time.realtimeSinceStartup, Attempt = 1 };
        }
        internal static ushort NextUsableSequence(UsableKey key)
        { _liveOut.TryGetValue(key, out ushort seq); _liveOut[key] = ++seq; return seq; }
        internal static void RememberLeaseRequest(string key) => EntityWorldLeases.RememberRequest(key);
        internal static void SetRabbitCharge(movecreaturescript move, RabCharge charge) => EntityRabbits.SetRabbitCharge(move, charge);
        internal static bool ApplyingState { get => _applyingState; set => _applyingState = value; }
        internal static void ObserveBreaks(uint net, out int count, out Transform done) => EntityAI.ObserveBreaks(net, out count, out done);
        internal readonly struct ServerObservation
        {
            internal readonly int OwnerId, CrashReturnTo;
            internal readonly uint Epoch, ParentNet, AttachParent, LockFrame;
            internal readonly bool Held, Stored, Driven, AtRest, Locked;
            internal readonly Vector3d Where;
            internal ServerObservation(int owner, uint epoch, bool held, bool stored, bool driven, int crashReturn, uint parent, uint attached, Vector3d where, bool rest, bool locked, uint frame)
            { OwnerId = owner; Epoch = epoch; Held = held; Stored = stored; Driven = driven; CrashReturnTo = crashReturn; ParentNet = parent; AttachParent = attached; Where = where; AtRest = rest; Locked = locked; LockFrame = frame; }
        }
        internal static bool ObserveServer(uint net, out ServerObservation observation)
        {
            if (!Server.TryGetValue(net, out var se)) { observation = default; return false; }
            observation = new ServerObservation(se.OwnerId, se.Epoch, se.Held, se.Stored, se.Driven, se.CrashReturnTo, se.ParentNet, se.AttachParent, se.Where, se.AtRest, se.Locked, se.LockFrame);
            return true;
        }
        private static void OwnerLog(SEnt se, int to, string why) => DevHooks.OwnerDecision?.Invoke(se.NetId, se.OwnerId, se.Epoch, to, why);
        private static void LogJump(Ent e, float dev, float devY, float dt) => DevHooks.CopyJump?.Invoke(e, dev, devY, dt);
    }
}

namespace TLDRevamp
{
    internal static partial class DevHooks
    {
        internal static System.Action<Net.Entities.Ent, float, float, float> CopyJump;
        internal static System.Action EntityWatch, EntityTrace;
        internal delegate bool SlotLookup(uint net, string slot, out int parent, out int index, out Vector3d local, out Vector3d global);
        internal static SlotLookup SlotRef;
    }
}
