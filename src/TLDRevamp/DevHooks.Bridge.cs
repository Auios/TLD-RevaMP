using System;
using UnityEngine;

namespace TLDRevamp
{
    internal static partial class DevHooks
    {
        internal static Action EntityLateSample, StandSeriesStep, KeyTestTick, SprayTick, PushTick, UsableReset, VoiceToneStopped;
        internal static Action<float> TowTick, VoiceToneTick;
        internal static Action<bool> VoiceProbeStatus;
        internal static Func<tosaveitemscript, bool> HeldItem;
        internal static Func<usablescript, bool> UsableHeld;
        internal static Func<uint, bool> DropState;
        internal static Func<bool> TrailerBrake, SaveVeto, AutosaveOff, ActingAsPlayer, SameAccount, RefuseSend, PassengerLights;
        internal static Func<float, float> PassengerSteer;
        internal static Func<string> RestoreDrop, GameData;
        internal static Action RebaseTick, TrailReset;
        internal static Action<TelemetryEvent> TrailEvent;
        internal static Func<bool> ProfileRunning;
        internal static Func<float> ProfileSlowMs;
        internal static Action<int> ProfileSlowCall, ProfileSlotCleared;
        internal static Action<bool, bool> ProfileFrame;
        internal static Action<int, long, long, long, long, bool> ProfileSample;
        internal static Func<bool> VrSimulation;
        internal static Action VrSimulationTick;
        internal static Func<int, VR.Native.VrHandInput> VrHand;
        internal static Func<Net.IJoinLoadingPreview> JoinPreview;
        internal static Action JoinPreviewEnd;
        internal static Func<string> BotStatus;
        internal static Func<ulong> ClaimSteamId;
        internal static Func<Net.MpClient, Net.NetReader, bool> ClientMessage;
        internal static Action<Net.MpClient> ClientTick;
        internal static Action<float> CameraStep;
        internal static Action<bool> CameraReset;
        internal static Action EntityScans, EntitySessionReset;
        internal static Action<Net.Entities.Ent, uint, string> RepairFailed;
        internal static Func<Net.Entities.Ent, bool> CrashHeld;
        internal static Func<string> CommandNote;
        internal static Func<attachablescript, System.Collections.Generic.IReadOnlyList<Renderer>> PartRenderers;

        internal static Action CrashReset;

        internal static Func<bool> RoadComparison;
        internal static Action<mapSettingScript, Vector3d, System.Collections.Generic.List<roadGenScript.roadClass>> RoadHeightComparison;
    }
}
