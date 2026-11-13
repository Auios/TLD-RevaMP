using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static bool ApplyingState { get => _applyingState; set => _applyingState = value; }
        internal static void ObserveBreaks(uint net, out int count, out Transform done)
        {
            _breaksHere.TryGetValue(net, out var kept);
            count = kept != null ? kept.Breaks.Count : 0;
            done = kept != null ? kept.Done : null;
        }
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
        internal static System.Action<fpscontroller, float> ItemScan, ItemLook;
        internal static System.Func<long> ItemFlags;
        internal static System.Func<string> ItemLast;
        internal delegate bool SlotLookup(uint net, string slot, out int parent, out int index, out Vector3d local, out Vector3d global);
        internal static SlotLookup SlotRef;
    }
}
