using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Plugin;

namespace TLDRevamp
{
    [BepInPlugin("tldrevamp.dev", "TLD Revamp Developer Tools", Plugin.BaseVersion)]
    [BepInDependency("tldrevamp.core")]
    public sealed class DevPlugin : BaseUnityPlugin
    {
        internal static bool BridgeEnabled;
        public static int BridgePort = 27050;
        private DebugBridge _bridge;
        internal static int PatchFailures;
        internal static readonly System.Collections.Generic.List<string> FailedPatches = new System.Collections.Generic.List<string>();
        private static void ApplyStartupSwitches()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-tldset");
            if (!BridgeEnabled || at < 0 || at + 1 >= args.Length) return;
            foreach (var pair in args[at + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                try { Log.LogInfo("-tldset " + pair + ": " + (eq > 0 ? BridgeReflect.Set(pair.Substring(0, eq), pair.Substring(eq + 1)) : "no '='")); }
                catch (Exception e) { Log.LogWarning("-tldset " + pair + " failed: " + e.Message); }
            }

        }

        private void Awake()
        {
            var Config = Plugin.Instance.Config;
            BridgePort = Config.Bind("Debug", "BridgePort", 27050, "Developer command port on the loopback interface.").Value;
            BridgeEnabled = Config.Bind("Debug", "Bridge", false,
                "Developer test tooling: a command port on 127.0.0.1:27050 for automated tests. Leave it off.").Value && !IsPlayerEdition;
            // a second copy of the game on a test machine (a third player: tools/thirdplayer.py) takes another port: -tldbridge N
            var cl = Environment.GetCommandLineArgs();
            int ci = Array.IndexOf(cl, "-tldbridge");
            if (ci >= 0 && ci + 1 < cl.Length && int.TryParse(cl[ci + 1], out int bp) && bp > 1024 && bp < 65536) BridgePort = bp;
            // a player edition ignores the config's Bridge; only an explicit -tldbridge on the command line (the test tools) starts it
            if (IsPlayerEdition && ci >= 0) BridgeEnabled = true;
            ObjectTrail.On = BridgeEnabled;
            if (BridgeEnabled)
            {
                // a taken port (another game's bridge, a test machine's second Steam on 27050) leaves the bridge off, not the
                // whole developer plugin: the exception used to end Awake here, silently in BepInEx's log
                try { _bridge = new DebugBridge(BridgePort); _bridge.Start(); }
                catch (System.Net.Sockets.SocketException e)
                {
                    Log.LogWarning($"Bridge: port {BridgePort} is taken ({e.SocketErrorCode}): bridge off. Set [Debug] BridgePort, or -tldbridge N");
                    _bridge = null; BridgeEnabled = false; ObjectTrail.On = false;
                }
            }

            DevHooks.BridgePump = () => _bridge?.PumpMainThread();
            DevHooks.BridgeStopped = () => _bridge?.Stop();
            DevHooks.TestToolsActive = () => BridgeEnabled;

            ShowWipMark = Config.Bind("Branding", "ShowWipMark", true,
                "Draw the TLD RevaMP <version> · WIP mark in the bottom-right corner. It is also configurable under Settings → Revamp → Branding.");
            Plugin.Instance.Overlay.ShowWip = ShowWipMark.Value;
            ShowWipMark.SettingChanged += (_, __) => { if (Plugin.Instance.Overlay != null) Plugin.Instance.Overlay.ShowWip = ShowWipMark.Value; };
            KeyItemTraffic = Config.Bind("Keys", "ItemTraffic", new BepInEx.Configuration.KeyboardShortcut(KeyCode.Insert),
                "Shows or hides each object's network traffic (an arrow and a rate over it; also Settings → Revamp → Debugging).");
            KeyDebugLayout = Config.Bind("Keys", "DebugLayout", new BepInEx.Configuration.KeyboardShortcut(KeyCode.RightShift),
                "Debugging V2 (Settings → Revamp → Debugging: V2, Debug overlay on): edit the overlay's layout — switch parts off, drag the panels.");
            KeyItemHealth = Config.Bind("Keys", "ItemHealth", new BepInEx.Configuration.KeyboardShortcut(KeyCode.Home),
                "Item health: aim at a shared object (or sit in a car, or hold an item) in a multiplayer game and press it — every check of it, what is wrong, why, and what happened. Again closes.");
            KeyDebugView = Config.Bind("Keys", "DebugView", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None),
                "Cycles Off / Focus / Problems / Everything. Unbound by default; choose a free key, leaving F11, Page Up/Down, Scroll Lock, Insert and Ctrl+Insert available.");
            KeyOwnership = Config.Bind("Keys", "OwnershipOverlay", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None),
                "Shows or hides ownership and sync error labels (also Settings > Revamp > Debugging). Unbound by default.");
            KeyOwnerGhost = Config.Bind("Keys", "OwnerGhost", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None),
                "Shows or hides the crosshair object's last received owner pose. Unbound by default.");
            KeyLinks = Config.Bind("Keys", "LinkView", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None),
                "Shows or hides attachment, lock and rider links. Unbound by default.");
            KeyCompare = Config.Bind("Keys", "ObjectComparison", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None),
                "Shows or hides live partner views of the focused shared object. Unbound by default; also Settings > Revamp > Debugging.");
            KeyEvents = Config.Bind("Keys", "ObjectEvents", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None),
                "Shows or hides recent telemetry events for the focused object. Unbound by default; also Settings > Revamp > Debugging.");

            KeyBindings.Contribute(("Item health", KeyItemHealth), ("Debug layout (V2)", KeyDebugLayout), ("Item traffic", KeyItemTraffic), ("Debug view (Legacy)", KeyDebugView), ("Ownership labels (Legacy)", KeyOwnership), ("Owner ghost (Legacy)", KeyOwnerGhost), ("Links (Legacy)", KeyLinks), ("Object comparison", KeyCompare), ("Object events", KeyEvents));
            Net.EntityDebug.HoldForTest = Net.FillLab.HoldNet;
            Diagnostics.ErrorLog.DiagnosticMessage = Diagnostics.ErrorLogTools.OnLog;
            DevHooks.CameraStep = Cinematic.CinematicCameraTools.Tick;
            DevHooks.CameraReset = Cinematic.CinematicCameraTools.Reset;
            DevHooks.EntityScans = () => { Net.EntityPartWatch.PartWatchTick(); Net.EntityHealthWatch.HealthWatchTick(); };
            DevHooks.EntitySessionReset = Net.EntityHealthWatch.ResetAutoMarks;
            DevHooks.RepairFailed = Net.EntityHealthWatch.RepairFailedNote;
            DevHooks.PartRenderers = Net.EntityPartWatch.RenderersOf;
            DevHooks.CrashHeld = Net.EntityCrashAuthorityTools.TestHeld;
            DevHooks.CrashReset = Net.EntityCrashAuthorityTools.ResetHeld;
            DevHooks.CommandNote = Net.EntityAnomalyTools.CommandNote;
            DevHooks.RoadComparison = () => Fixes.PureRoadsTools.Compare;
            DevHooks.RoadHeightComparison = Fixes.PureRoadsTools.CompareHeight;

            DevHooks.OwnerDecision += Net.EntityDebug.OwnerLog;
            DevHooks.ReproDriving += Net.EntityReproPlayback.ApplyDriveInput;
            DevHooks.CopyJump += Net.EntityDebug.LogJump;
            DevHooks.EntityWatch += Net.EntityMountLab.WatchTick;
            DevHooks.EntityTrace += Net.EntityMountLab.TraceTick;
            DevHooks.SlotRef = Net.EntityMountLab.SlotRef;
            Replay.ReplayTruthCapture.Install();
            DevHooks.EntityLateSample = Net.EntityCarDoorsTools.LateSample;
            DevHooks.StandSeriesStep = () => Net.EntityFootingTools.Step();
            DevHooks.KeyTestTick = Net.EntityIgnitionKeyTools.KeyTestTick;
            DevHooks.SprayTick = Net.EntitySprayTools.SprayHoldTick;
            DevHooks.PushTick = Net.EntityPushTools.TestPushTick;
            DevHooks.TowTick = Net.EntityCarDoorsTools.TowTick;
            DevHooks.UsableDragTick = Net.EntityLiveUsablesTools.DragUsableTick;
            DevHooks.UsableProbeTick = Net.EntityLiveUsablesTools.UsableProbeTick;
            DevHooks.UsableReset = Net.EntityLiveUsablesTools.Reset;
            DevHooks.UsableHeld = Net.EntityLiveUsablesTools.Held;
            DevHooks.HeldItem = item => Net.EntityCargoTools.ForceHeld != null && Net.EntityCargoTools.ForceHeld == item;
            DevHooks.PreventDrop = (player, destroyed) => DevHooks.Playing && !destroyed && player.pickedUp != null && Net.EntityCargoTools.ForceHeld == player.pickedUp.tosave;
            DevHooks.DropState = net => net == Net.EntityPlaceCheckTools.TestDropStates;
            DevHooks.ActingAsPlayer = () => Net.EntityProxyTools.TestAsPlayer;
            DevHooks.SameAccount = () => Net.SessionTools.TestSameAccount;
            DevHooks.RestoreDrop = () => Net.PlayerSaveTools.TestRestoreDrop;
            DevHooks.ReproCapacity = () => ReproRecorderTools.TestFullAt;
            DevHooks.GameData = () => Net.WorldCheckTools.TestGameData;
            DevHooks.RefuseSend = Net.SteamTransportTools.Refuse;
            DevHooks.PassengerSteer = steer => Net.EntityShotgunTools.TestSteer != 0f ? Net.EntityShotgunTools.TestSteer : steer;
            DevHooks.PassengerLights = Net.EntityShotgunTools.TakeLights;
            DevHooks.VoiceToneTick = Net.VoiceTools.ToneTick;
            DevHooks.VoiceToneStopped = Net.VoiceTools.StopTone;
            DevHooks.VoiceProbeStatus = Net.VoiceTools.ProbeStatus;
            DevHooks.BotStatus = Net.LegacyBots.BotStatus;
            DevHooks.ClaimSteamId = () => Net.ClientSimulation.TestClaimSteamId;
            DevHooks.ClientMessage = Net.ClientSimulation.Message;
            DevHooks.ClientTick = Net.ClientSimulation.Tick;
            DevHooks.SessionStopped += Net.LegacyBots.Reset;
            DevHooks.BotsTick += Net.LegacyBots.Tick;
            DevHooks.TrailReset = ObjectTrail.Clear;
            DevHooks.TrailEvent = ObjectTrail.Add;
            DevHooks.SaveVeto = () => TestSafetyTools.BlockAutoSave;
            DevHooks.AutosaveOff = () => Net.MpTools.TestAutosaveOff;
            ScriptProfilerTools.Install();
            DevHooks.VrSimulation = () => VR.Native.VrSim.On;
            DevHooks.VrSimulationTick = VR.Native.VrSim.Tick;
            DevHooks.VrHand = hand => new VR.Native.VrHandInput(VR.Native.VrSim.On && VR.Native.VrSim.Hand[hand], VR.Native.VrSim.Pos[hand], VR.Native.VrSim.Rot[hand], VR.Native.VrSim.Trigger[hand], VR.Native.VrSim.Grip[hand], VR.Native.VrSim.Primary[hand]);
            DevHooks.TrailerBrake = () => Fixes.TrailerBrakesTools.TestAlone;
            Net.JoinLoadingPreview.Install();
            DevHooks.RebaseTick = RebaseLab.Tick;
            DevHooks.MessageNoted = Net.NetMeterTools.Note;
            DevHooks.MessageHistoryReset = Net.NetMeterTools.ResetHistory;
            DevCommands.Install(); DevOverlay.Install();
            DevHooks.InputCapture = () => DebugV2.Editing;
            DevHooks.Playback = () => ReproPlayback.Active;
            DevHooks.LagInfo = () => Net.NetLab.LagInfo;
            DevHooks.VrState = () => VR.VrDiagnostics.Json(Fixes.VrContractBridge.Contracts);
            DevHooks.VrBody = VR.Native.VrBody.Json;
            DevHooks.FinishFrame += RenderLab.FinishFrameBounds;
            DevHooks.ModsLoaded += AutopilotBridge.Install; DevHooks.ModsTick += AutopilotBridge.Tick;
            AutopilotBridge.Install();
            DevHooks.SessionStopped += Net.PlayerBots.Reset; DevHooks.BotsTick += Net.PlayerBots.Tick;
            DevHooks.Packet += Net.ItemTraffic.Packet; DevHooks.TrafficReset += Net.ItemTraffic.Reset;
            DevHooks.TpaObject += ObjectPanelEvents.TpaObject;
            DevHooks.PlayerEditionApplied += () => { Fixes.LoadWatch.Enabled = false; Net.EntityItemWatch.ItemWatch = false; Net.EntityHealthWatch.HealthWatch = false; Net.EntityHealthWatch.AutoMarks = false; Net.EntityPartWatch.PartWatch = false; Net.EntityPartWatch.PartWatchMarks = false; };
            ReproRecorder.SimulationSnapshot = () => NetworkConditions.ReproRow(NetworkConditions.Current);
            Net.MpServer.DiagnosticMessage += (server, from, type, reader) => Net.ServerPanels.For(server).PanelMessage(from, type, reader);
            Net.MpServer.DiagnosticPeerLeft += (server, player) => Net.ServerPanels.For(server).PeerLeft(player);
            Net.MpClient.DiagnosticMessage += Net.ObjectPanelWire.Client;
            if (!PlayerEdition.On) DevSettings.Install();
            PlayerEdition.Apply(KeyBindings.ByName());
            if (Config.Bind("Mods", "ScanLoaderMods", false,
                    "For mod authors: read every TLD Loader mod in BepInEx/TLDLoaderMods and write what it would need for multiplayer (a report and a code skeleton) to BepInEx/TLDRevampModScan.").Value)
                try { LoaderScan.ScanAll(); } catch (Exception e) { Log.LogWarning("modscan: " + e.Message); }

            Log.LogInfo(BridgeEnabled ? "Bridge on 127.0.0.1:" + BridgePort : "bridge off");
            ApplyStartupSwitches();
            var harmony = new Harmony("tldrevamp.dev");
            var patchTypes = Array.FindAll(typeof(DevPlugin).Assembly.GetTypes(), type => type.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0);
            foreach (var type in patchTypes)
            {
                try { var applied = harmony.CreateClassProcessor(type).Patch(); if (applied != null && applied.Count > 0) GameLookups.PatchApplied(); Logger.LogInfo("Patched " + type.Name); }
                catch (Exception e)
                {
                    PatchFailures++; FailedPatches.Add(type.Name);
                    Plugin.PatchFailures++; Plugin.FailedPatches.Add(type.Name);
                    Logger.LogError("Patch failed for " + type.Name + ": " + e);
                }
            }
            if (PatchFailures > 0) Logger.LogError(PatchFailures + " patch(es) failed to apply: " + string.Join(", ", FailedPatches));
            var go = new GameObject("TLDRevampDevRunner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go); go.AddComponent<DevRunner>().useGUILayout = false;
        }
    }

    public sealed class DevRunner : MonoBehaviour
    {
        private int _debugViewRevision = int.MinValue;
        private readonly float[] _faultLogAt = new float[4];
        private void Run(int slot, Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                Runner.Faults++;
                float now = Time.realtimeSinceStartup;
                if (now - _faultLogAt[slot] < 10f) return;
                _faultLogAt[slot] = now; Plugin.Log.LogError("Developer runner: " + e);
            }
        }
        private void Update()
        {
            Run(0, () => { MpLab.Tick(); Net.NetLab.Tick(); });
            Run(1, () => { Net.VehicleLab.Tick(); Net.StreamLab.Tick(); WorldGenLab.AllocTick(); RenderLab.Tick(); });
            Run(2, () => { DebugV2.Tick(); ItemHealth.Tick(); });
            Run(3, Keys);
        }
        private void Keys()
        {
            var p = Plugin.Instance;
            if (Plugin.KeyDown(Plugin.KeyItemTraffic)) { Net.ItemTraffic.SetVisible(!Net.ItemTraffic.Show); RevampSettings.Save(); p.Overlay.Toast(Net.ItemTraffic.Show ? "Item traffic on" : "Item traffic off", 2f); }
            if (Plugin.KeyDown(Plugin.KeyOwnership)) { WorldOverlays.Ownership = !WorldOverlays.Ownership; RevampSettings.Save(); }
            if (Plugin.KeyDown(Plugin.KeyOwnerGhost)) { WorldOverlays.Ghost = !WorldOverlays.Ghost; RevampSettings.Save(); }
            if (Plugin.KeyDown(Plugin.KeyLinks)) { WorldOverlays.Links = !WorldOverlays.Links; RevampSettings.Save(); }
            if (Plugin.KeyDown(Plugin.KeyCompare)) { ObjectPanels.Compare = !ObjectPanels.Compare; RevampSettings.Save(); }
            if (Plugin.KeyDown(Plugin.KeyEvents)) { ObjectPanels.Events = !ObjectPanels.Events; RevampSettings.Save(); }
            if (Plugin.KeyDown(Plugin.KeyDebugView)) { DebugView.Mode = DebugView.Next(DebugView.Mode); RevampSettings.Save(); }
            if (_debugViewRevision == int.MinValue) _debugViewRevision = DebugView.Revision;
            if (_debugViewRevision != DebugView.Revision)
            {
                _debugViewRevision = DebugView.Revision; p.Overlay.Toast("Debug view: " + DebugView.Mode, 2f);
            }
            ObjectPanels.Tick();

        }
        private void OnGUI()
        {
            if (Net.JoinLoading.Showing) return;
            NetworkConditions.Draw(); ObjectPanels.Draw(); Net.Banner.Draw(); ItemHealth.Draw(); DebugV2.DrawEditor();
        }
    }
}
