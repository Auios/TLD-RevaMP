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
        internal static int PatchFailures;
        internal static readonly System.Collections.Generic.List<string> FailedPatches = new System.Collections.Generic.List<string>();
        private static void ApplyStartupSwitches()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-tldset");
            if (!Plugin.BridgeEnabled || at < 0 || at + 1 >= args.Length) return;
            foreach (var pair in args[at + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string member = Reflect.MemberPath(pair.Substring(0, eq)), owner = member;
                Type type = null;
                while (type == null && owner.LastIndexOf('.') > 0)
                { owner = owner.Substring(0, owner.LastIndexOf('.')); type = Reflect.FindType(owner); }
                if (type?.Assembly != typeof(DevPlugin).Assembly) continue;
                try { Plugin.Log.LogInfo("-tldset " + pair + ": " + Reflect.Set(member, pair.Substring(eq + 1))); }
                catch (Exception e) { Plugin.Log.LogWarning("-tldset " + pair + " failed: " + e.Message); }
            }
        }

        private void Awake()
        {
            var Config = Plugin.Instance.Config;
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
            Reflect.RegisterMovedMembers(typeof(Net.Entities), typeof(Net.EntityDebug), typeof(Net.EntityMountLab), typeof(Net.EntityReproPlayback), typeof(Net.EntityItemCheck), typeof(Net.EntityItemWatch), typeof(Net.EntityContactCensus), typeof(Net.EntityFakeInput), typeof(Net.EntityTrack));
            Reflect.RegisterMovedMembers(typeof(Net.RemotePlayers), typeof(Net.RemoteReproPlayback));
            Reflect.RegisterMovedMembers(typeof(Replay.ReplayRecorder), typeof(Replay.ReplayTruthCapture));
            Reflect.RegisterMovedMembers(typeof(Replay.ReplayViewer), typeof(Replay.ReplayTruthVerify));
            DevHooks.OwnerDecision += Net.EntityDebug.OwnerLog;
            DevHooks.ReproDriving += Net.EntityReproPlayback.ApplyDriveInput;
            DevHooks.CopyJump += Net.EntityDebug.LogJump;
            DevHooks.EntityWatch += Net.EntityMountLab.WatchTick;
            DevHooks.EntityTrace += Net.EntityMountLab.TraceTick;
            DevHooks.ItemScan += Net.EntityItemWatch.ItemWatchScan;
            DevHooks.ItemLook += Net.EntityItemWatch.ItemWatchLook;
            DevHooks.ItemFlags = () => Net.EntityItemWatch.ItemWatchFlags;
            DevHooks.ItemLast = () => Net.EntityItemWatch.ItemWatchLast;
            DevHooks.SlotRef = Net.EntityMountLab.SlotRef;
            Replay.ReplayTruthCapture.Install();
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
            DevHooks.PlayerEditionApplied += () => { Fixes.LoadWatch.Enabled = false; Net.EntityItemWatch.ItemWatch = false; };
            ReproRecorder.SimulationSnapshot = () => NetworkConditions.ReproRow(NetworkConditions.Current);
            Net.MpServer.DiagnosticMessage += (server, from, type, reader) => Net.ServerPanels.For(server).PanelMessage(from, type, reader);
            Net.MpServer.DiagnosticPeerLeft += (server, player) => Net.ServerPanels.For(server).PeerLeft(player);
            Net.MpClient.DiagnosticMessage += Net.ObjectPanelWire.Client;
            if (!PlayerEdition.On) DevSettings.Install();
            PlayerEdition.Apply(KeyBindings.ByName());
            if (Config.Bind("Mods", "ScanLoaderMods", false,
                    "For mod authors: read every TLD Loader mod in BepInEx/TLDLoaderMods and write what it would need for multiplayer (a report and a code skeleton) to BepInEx/TLDRevampModScan.").Value)
                try { LoaderScan.ScanAll(); } catch (Exception e) { Log.LogWarning("modscan: " + e.Message); }

            ApplyStartupSwitches();
            var harmony = new Harmony("tldrevamp.dev");
            var patchTypes = Array.FindAll(typeof(DevPlugin).Assembly.GetTypes(), type => type.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0);
            foreach (var type in patchTypes)
            {
                try { harmony.CreateClassProcessor(type).Patch(); Logger.LogInfo("Patched " + type.Name); }
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
