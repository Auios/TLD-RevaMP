namespace TLDRevamp
{
    internal static class DevMembers
    {
        internal static void Install()
        {
            Reflect.RegisterMovedMembers(typeof(Net.Entities), typeof(Net.EntityDebug), typeof(Net.EntityMountLab), typeof(Net.EntityReproPlayback), typeof(Net.EntityItemCheck), typeof(Net.EntityItemWatch), typeof(Net.EntityContactCensus), typeof(Net.EntityFakeInput), typeof(Net.EntityTrack));
            Reflect.RegisterMovedMembers(typeof(Net.RemotePlayers), typeof(Net.RemoteReproPlayback));
            Reflect.RegisterMovedMembers(typeof(Replay.ReplayRecorder), typeof(Replay.ReplayTruthCapture));
            Reflect.RegisterMovedMembers(typeof(Replay.ReplayViewer), typeof(Replay.ReplayTruthVerify));
            Reflect.RegisterMovedMembers(typeof(Plugin), typeof(DevPlugin));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.BugReport), typeof(TLDRevamp.BugReportTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Diagnostics.ErrorLog), typeof(TLDRevamp.Diagnostics.ErrorLogTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.FallRescue), typeof(TLDRevamp.Fixes.FallRescueTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.FastNoise), typeof(TLDRevamp.Fixes.FastNoiseTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.FastRandom), typeof(TLDRevamp.Fixes.FastRandomTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.FluidTypeGuard), typeof(TLDRevamp.Fixes.FluidTypeGuardTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.IdleCarParticles), typeof(TLDRevamp.Fixes.IdleCarParticlesTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.LoadFreeze), typeof(TLDRevamp.Fixes.LoadFreezeTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.OpenXrBridge), typeof(TLDRevamp.Fixes.OpenXrBridgeTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.PartConditionIdle), typeof(TLDRevamp.Fixes.PartConditionIdleTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.Rabbits), typeof(TLDRevamp.Fixes.RabbitsTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.StreamRace), typeof(TLDRevamp.Fixes.StreamRaceTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.WornDestroyed), typeof(TLDRevamp.Fixes.WornDestroyedTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.KeyBindings), typeof(TLDRevamp.KeyBindingsTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.MarkPanel), typeof(TLDRevamp.MarkPanelTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.DeveloperAccess), typeof(TLDRevamp.Net.DeveloperAccessTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.Entities), typeof(TLDRevamp.Net.EntityAiTools), typeof(TLDRevamp.Net.EntityAnomalyTools), typeof(TLDRevamp.Net.EntityBloodTools), typeof(TLDRevamp.Net.EntityBusDoorsTools), typeof(TLDRevamp.Net.EntityCarDoorsTools), typeof(TLDRevamp.Net.EntityCarSignalsTools), typeof(TLDRevamp.Net.EntityCarTuneTools), typeof(TLDRevamp.Net.EntityCargoTools), typeof(TLDRevamp.Net.EntityCopyJointsTools), typeof(TLDRevamp.Net.EntityCorpsesTools), typeof(TLDRevamp.Net.EntityCrashAuthorityTools), typeof(TLDRevamp.Net.EntityDeathTools), typeof(TLDRevamp.Net.EntityDoorAttachmentsTools), typeof(TLDRevamp.Net.EntityEditCheckTools), typeof(TLDRevamp.Net.EntityFootingTools), typeof(TLDRevamp.Net.EntityHealthWatchTools), typeof(TLDRevamp.Net.EntityHitchTools), typeof(TLDRevamp.Net.EntityIgnitionKeyTools), typeof(TLDRevamp.Net.EntityLeasesTools), typeof(TLDRevamp.Net.EntityLifecycleTools), typeof(TLDRevamp.Net.EntityLiveUsablesTools), typeof(TLDRevamp.Net.EntityPartWatchTools), typeof(TLDRevamp.Net.EntityPhysLockTools), typeof(TLDRevamp.Net.EntityPlaceCheckTools), typeof(TLDRevamp.Net.EntityPlayerSoundsTools), typeof(TLDRevamp.Net.EntityPoiUsablesTools), typeof(TLDRevamp.Net.EntityProxyTools), typeof(TLDRevamp.Net.EntityPushGripTools), typeof(TLDRevamp.Net.EntityPushTools), typeof(TLDRevamp.Net.EntityRabbitsTools), typeof(TLDRevamp.Net.EntityRadioTools), typeof(TLDRevamp.Net.EntityRagdollTools), typeof(TLDRevamp.Net.EntityRepairTools), typeof(TLDRevamp.Net.EntitySeatTools), typeof(TLDRevamp.Net.EntityShotFxTools), typeof(TLDRevamp.Net.EntityShotgunTools), typeof(TLDRevamp.Net.EntitySprayTools), typeof(TLDRevamp.Net.EntityStormsTools), typeof(TLDRevamp.Net.EntityTumbleweedsTools), typeof(TLDRevamp.Net.EntityWornTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.ItemSnapshot), typeof(TLDRevamp.Net.ItemSnapshotTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.JoinLoading), typeof(TLDRevamp.Net.JoinLoadingTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.ModBrowser), typeof(TLDRevamp.Net.ModBrowserTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.MpScreen), typeof(TLDRevamp.Net.MpScreenTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.MpServer), typeof(TLDRevamp.Net.SessionTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.NetMeter), typeof(TLDRevamp.Net.NetMeterTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.PlayerActs), typeof(TLDRevamp.Net.PlayerActsTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.PlayerCombat), typeof(TLDRevamp.Net.PlayerCombatTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.PlayerSave), typeof(TLDRevamp.Net.PlayerSaveTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.SessionSettingsPolicy), typeof(TLDRevamp.Net.SessionSettingsPolicyTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.SteamCompat), typeof(TLDRevamp.Net.SteamCompatTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.SteamTransport), typeof(TLDRevamp.Net.SteamTransportTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.Voice), typeof(TLDRevamp.Net.VoiceTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Net.WorldCheck), typeof(TLDRevamp.Net.WorldCheckTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.ReproRecorder), typeof(TLDRevamp.ReproRecorderTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.ScriptProfiler), typeof(TLDRevamp.ScriptProfilerTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.SessionLog), typeof(TLDRevamp.SessionLogTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.TestSafety), typeof(TLDRevamp.TestSafetyTools));
            Reflect.RegisterMovedMembers(typeof(Net.Mp), typeof(Net.LegacyBots));
            Reflect.RegisterMovedMembers(typeof(Net.MpClient), typeof(Net.ClientSimulation));
            Reflect.RegisterMovedMembers(typeof(TestSafety), typeof(TestSafetyTools));
            Reflect.RegisterMovedMembers(typeof(Net.Mp), typeof(Net.MpTools));
            Reflect.RegisterMovedMembers(typeof(JoinerSaveBlock), typeof(JoinerSaveTools));
            Reflect.RegisterMovedMembers(typeof(DebugBridge), typeof(GroundPlacement), typeof(BridgeTiming));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.RebaseLab), typeof(TLDRevamp.RebaseLabTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.Fixes.TrailerBrakes), typeof(TLDRevamp.Fixes.TrailerBrakesTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.VR.Native.VrHands), typeof(TLDRevamp.VR.Native.VrHandsTools));
            Reflect.RegisterMovedMembers(typeof(TLDRevamp.VR.Native.VrNative), typeof(TLDRevamp.VR.Native.VrNativeTools));
            Reflect.RegisterMovedMembers(typeof(DebugView), typeof(DebugViewTools));
            Reflect.RegisterMovedMembers(typeof(Reflect), typeof(BridgeReflect));
        }
    }
}
