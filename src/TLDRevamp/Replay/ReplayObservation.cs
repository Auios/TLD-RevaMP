using System;
using System.Collections.Generic;

namespace TLDRevamp.Replay
{
    internal static partial class ReplayRecorder
    {
        internal static IReadOnlyList<Tracked> ObservedObjects => _objects;
        internal static IReadOnlyDictionary<int, int> ObservedRemoteIndex => _remoteIndex;
        internal static fpscontroller ObservedPlayer => _player;
    }
    internal static partial class ReplayViewer
    {
        internal static ReplayRecorder.Take ObservedTake => _take;
        internal static ReplayRecording ObservedRecording => _rec;
        internal static IReadOnlyList<ReplayProxy> ObservedProxies => _proxies;
        internal static IReadOnlyList<ReplayLook> ObservedLooks => _looks;
        internal static IReadOnlyList<Body> ObservedBodies => _bodies;
        internal static void PauseForObservation() { _clock.Playing = false; }
        internal static void SeekForObservation(float time) { _clock.Seek(time); Apply(); }
    }
}

namespace TLDRevamp
{
    internal static partial class DevHooks
    {
        internal static Action ReplayCaptureStarted;
        internal static Action<float, double> ReplaySample;
        internal static Action<Replay.ReplayRecorder.Take> ReplayCaptureStopped;
    }
}
