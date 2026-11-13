using System;

namespace TLDRevamp
{
    /// Optional observations installed by the developer plugin. An empty hook does no work.
    internal static partial class DevHooks
    {
        internal static Func<int> ReproCapacity;
        internal static Action<bool, byte, int, uint> MessageNoted;
        internal static Action MessageHistoryReset;
        internal static Action BridgePump, BridgeStopped, UsableDragTick, UsableProbeTick;
        internal static Func<bool> TestToolsActive;
        internal static bool Testing => TestToolsActive?.Invoke() ?? false;
        internal static Action<uint, int, uint, int, string> OwnerDecision;
        internal static Func<bool> InputCapture, Playback;
        internal static Func<string> LagInfo, VrState, VrBody;
        internal static Action<long, long> FinishFrame;
        internal static Action ModsLoaded, ModsTick, SessionStopped, BotsTick, TrafficReset, PlayerEditionApplied;
        internal static Action<byte[], int, int, int, bool, bool> Packet;
        internal static Action<uint, bool, int, double> TpaObject;
        internal static bool CapturingInput => InputCapture?.Invoke() ?? false;
        internal static bool Playing => Playback?.Invoke() ?? false;
    }
}
