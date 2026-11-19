using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

static class DevSplitTest
{
    static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    static string[] References(string project) => XDocument.Load(project).Descendants("ProjectReference").Select(e => (string)e.Attribute("Include")).ToArray();
    static void Main()
    {
        Check(TLDRevamp.BridgeReflect.Get("AliasOriginal.Existing") == "1", "retained core member resolves");
        Check(TLDRevamp.BridgeReflect.Set("AliasMoved.Legacy", "true") == "true" && AliasMoved.Legacy, "real Dev owner setting works");
        Check(TLDRevamp.BridgeReflect.Call("AliasMoved.Add", new[] { "3" }) == "5", "real owner call preserves optional arguments");
        Check(TLDRevamp.BridgeReflect.Set("AliasMoved.State.Value", "7") == "7" && AliasMoved.State.Value == 7, "real owner nested structs are written back");
        Check(!References("src/TLDRevamp/TLDRevamp.csproj").Any(p => p.Contains("TLDRevamp.Dev")), "core must build independently of Dev");
        var references = References("src/TLDRevamp.Dev/TLDRevamp.Dev.csproj");
        Check(references.Any(p => p == "../TLDRevamp/TLDRevamp.csproj") && references.Any(p => p.Contains("TLDRevamp.Multiplayer")), "Dev depends on core and the multiplayer API");
        Check(File.Exists("src/TLDRevamp.Dev/Cinematic/CinematicShot.cs") && File.Exists("src/TLDRevamp.Dev/Net/HealthWatchRules.cs"), "shot lists and health rules belong to Dev");
        string core = "src/TLDRevamp/bin/Release/netstandard2.1/TLDRevamp.dll", dev = "src/TLDRevamp.Dev/bin/Release/netstandard2.1/TLDRevamp.Dev.dll";
        if (File.Exists(core) && File.Exists(dev))
        {
            using var coreFile = File.OpenRead(core); using var corePe = new PEReader(coreFile);
            var coreMetadata = corePe.GetMetadataReader();
            Check(!coreMetadata.AssemblyReferences.Any(h => coreMetadata.GetString(coreMetadata.GetAssemblyReference(h).Name) == "TLDRevamp.Dev"), "compiled core has no Dev assembly dependency");
            using var devFile = File.OpenRead(dev); using var devPe = new PEReader(devFile);
            var devMetadata = devPe.GetMetadataReader();
            var types = devMetadata.TypeDefinitions.Select(h => devMetadata.GetTypeDefinition(h)).Select(t => devMetadata.GetString(t.Namespace) + "." + devMetadata.GetString(t.Name)).ToArray();
            Check(types.Contains("TLDRevamp.DebugBridge"), "bridge belongs to Dev");
            Check(types.Contains("TLDRevamp.DebugV2") && types.Contains("TLDRevamp.Net.PlayerBotCore"), "developer features are compiled into Dev");
            Check(!types.Contains("TLDRevamp.Net.Entities") && !types.Contains("TLDRevamp.Net.MpServer"), "Dev must not shadow a core partial class");
            Check(!types.Contains("TLDRevamp.Net.RemotePlayers") && !types.Contains("TLDRevamp.Replay.ReplayRecorder") && !types.Contains("TLDRevamp.Replay.ReplayViewer"), "Dev must not shadow remote-player or replay partials");
            foreach (string name in new[] { "EntityDebug", "EntityMountLab", "EntityReproPlayback", "EntityItemCheck", "EntityItemWatch", "EntityContactCensus", "EntityFakeInput", "EntityTrack", "RemoteReproPlayback" })
                Check(types.Contains("TLDRevamp.Net." + name), name + " is owned by Dev");
            var coreTypes = coreMetadata.TypeDefinitions.Select(h => coreMetadata.GetTypeDefinition(h)).Select(t => coreMetadata.GetString(t.Namespace) + "." + coreMetadata.GetString(t.Name)).ToArray();
            Check(!coreTypes.Contains("TLDRevamp.DebugBridge"), "core contains no DebugBridge type");
            foreach (string name in new[] {
                "TLDRevamp.Cinematic.CinematicShot", "TLDRevamp.Cinematic.CinematicShotCodec",
                "TLDRevamp.Cinematic.CinematicShotStep", "TLDRevamp.Cinematic.CinematicShotFormat",
                "TLDRevamp.Cinematic.CinematicPlayback", "TLDRevamp.Cinematic.CinematicCommandPolicy",
                "TLDRevamp.Cinematic.CinematicCameraTools", "TLDRevamp.VR.Native.VrSim",
                "TLDRevamp.VR.VrBridgeVerb", "TLDRevamp.VR.VrBridgeCommandPolicy",
                "TLDRevamp.VR.Native.VrPerfTools", "TLDRevamp.Net.HealthWatchRules",
                "TLDRevamp.HealthCheck", "TLDRevamp.Net.EntityHealthRules", "TLDRevamp.Net.EntityHealthWatch",
                "TLDRevamp.Net.EntityPartWatch", "TLDRevamp.Net.PartWatchTiming",
                "TLDRevamp.DebugView", "TLDRevamp.DebugViewMode" })
                Check(types.Contains(name) && !coreTypes.Contains(name), name + " belongs only to Dev");
            Check(coreTypes.Contains("TLDRevamp.DebugBox") && coreTypes.Contains("TLDRevamp.DebugLayout"), "shared player layout stays in core");

            string[] CoreMembers(string name)
            {
                var type = coreMetadata.TypeDefinitions.Select(h => coreMetadata.GetTypeDefinition(h))
                    .Single(t => coreMetadata.GetString(t.Namespace) + "." + coreMetadata.GetString(t.Name) == name);
                return type.GetMethods().Select(h => coreMetadata.GetString(coreMetadata.GetMethodDefinition(h).Name))
                    .Concat(type.GetFields().Select(h => coreMetadata.GetString(coreMetadata.GetFieldDefinition(h).Name)))
                    .Concat(type.GetProperties().Select(h => coreMetadata.GetString(coreMetadata.GetPropertyDefinition(h).Name))).ToArray();
            }
            var cameraMembers = CoreMembers("TLDRevamp.Cinematic.CinematicCamera");
            foreach (string name in new[] { "Playback", "CurrentShot", "LoadShot", "UnloadShot", "ApplyShot", "_shot", "_shotError", "_nextCut", "ShotStatus", "Screenshot", "ShotFolder" })
                Check(!cameraMembers.Contains(name), "core camera contains no shot tool: " + name);
            var entityMembers = CoreMembers("TLDRevamp.Net.Entities");
            foreach (string name in new[] { "HealthWatch", "HealthWatchTick", "HealthWatchCycle", "HealthWatchOne", "HealthWatchFound", "AutoMark", "AutoMarkWaitingTick", "ResetAutoMarks", "HealthWatchLine", "RepairFailedNote", "CheckSanity", "OwnerGone", "GroundOver", "FloorBelow", "PartWatchTick", "PartWatchScan", "PartWatchLook", "PartWatchFound", "_testHeld", "TestHeld", "LastDevCmd", "LastDevAt" })
                Check(!entityMembers.Contains(name), "core entities contain no developer behavior: " + name);
            foreach (string name in new[] { "GroundLayers", "PartsDiagnostics", "SharedCount", "PartWatchNearbyM", "PartWatchFlags", "PartWatchLast", "DeleteShared" })
                Check(CoreMembers("TLDRevamp.Net." + (name == "GroundLayers" ? "EntityGround" : name.StartsWith("PartWatch") || name == "PartsDiagnostics" ? "EntityDiag" : "Entities")).Contains(name), "shared player entity API stays in core: " + name);
            var perfMembers = CoreMembers("TLDRevamp.VR.Native.VrPerf");
            foreach (string name in new[] { "PostEffects", "PostList", "CullList", "Visible", "Hide", "Cull", "CullRestore", "PostEffect", "PostLayer" })
                Check(!perfMembers.Contains(name), "core VR contains no rendering experiment: " + name);
            var errorMembers = CoreMembers("TLDRevamp.Diagnostics.ErrorLog");
            foreach (string name in new[] { "Kinds", "Order", "Stacks", "Errors", "Exceptions", "Observe" })
                Check(!errorMembers.Contains(name), "raw developer error history leaves core: " + name);
            Check(errorMembers.Contains("IntervalJson") && errorMembers.Contains("ReportSnapshot"), "player error telemetry stays in core");

            Check(types.Contains("TLDRevamp.Replay.ReplayTruth") && !coreTypes.Contains("TLDRevamp.Replay.ReplayTruth"), "truth frames belong only to Dev");
            Console.WriteLine("PASS compiled assembly boundary");
        }
        else Console.WriteLine("SKIP compiled metadata: build both plugins with the game references first");
        Console.WriteLine("ALL PASS (developer plugin boundary)");
    }

    static class AliasOriginal { internal static int Existing = 1; }
    static class AliasMoved
    {
        internal static int Existing = 2;
        internal static bool Legacy;
        internal struct ValueState { internal int Value; }
        internal static ValueState State;
        internal static int Add(int value, int extra = 2) => value + extra;
    }
}
