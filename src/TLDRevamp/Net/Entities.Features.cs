using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public const byte RadioSync = 54;
        public static float RadioResendS { get => EntityRadio.RadioResendS; set => EntityRadio.RadioResendS = value; }
        public static string RadioStatus() => EntityRadio.RadioStatus();

        public const byte WorldClock = 97;   // f64 st, f32 world seconds, f32 rate, u16 sleep epoch, bool snap
        public static float SleepClockS { get => EntityWorldClock.SleepClockS; set => EntityWorldClock.SleepClockS = value; }
        internal static void ApplyWorldTime(float wt, bool force) => EntityWorldClock.ApplyWorldTime(wt, force);

        public const byte StormSync = 56;
        public static float StormResendS { get => EntityWeather.StormResendS; set => EntityWeather.StormResendS = value; }
        public static long StormSyncsSent { get => EntityWeather.StormSyncsSent; set => EntityWeather.StormSyncsSent = value; }
        public static long StormSyncsApplied { get => EntityWeather.StormSyncsApplied; set => EntityWeather.StormSyncsApplied = value; }
        public static long StormsMade { get => EntityWeather.StormsMade; set => EntityWeather.StormsMade = value; }
        public static long StormsRemoved { get => EntityWeather.StormsRemoved; set => EntityWeather.StormsRemoved = value; }
        public static long StormSnaps { get => EntityWeather.StormSnaps; set => EntityWeather.StormSnaps = value; }
        internal static System.Collections.Generic.IReadOnlyDictionary<sandstormscript, uint> ObservedStormIds => EntityWeather.ObservedStormIds;
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, sandstormscript> ObservedStormById => EntityWeather.ObservedStormById;

        internal static float HostClockRate => EntityWorldClock.HostClockRate;
        internal static bool IsFinite(float value) => Finite(value);
        internal static bool IsFinite(double value) => Finite(value);
        internal static void SendToPeer(int playerId, NetWriter w, bool reliable) => ServerSendTo(playerId, w, reliable);
        internal static void SendToPeers(NetWriter w, bool reliable, int except, Vector3d? near = null, double range = 0) => ServerSendAll(w, reliable, except, near, range);
    }
}
