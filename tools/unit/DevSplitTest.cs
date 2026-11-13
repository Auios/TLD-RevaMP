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
        Check(TLDRevamp.Reflect.Get("AliasOriginal.Existing") == "1", "core members resolve before Dev registers");
        TLDRevamp.Reflect.RegisterMovedMembers(typeof(AliasOriginal), typeof(AliasMoved));
        Check(TLDRevamp.Reflect.Set("AliasOriginal.Legacy", "true") == "true" && AliasMoved.Legacy, "old set path reaches the Dev owner");
        Check(TLDRevamp.Reflect.Get(typeof(AliasOriginal).FullName + ".Legacy") == "true", "full old get path reaches the Dev owner");
        Check(TLDRevamp.Reflect.Call("AliasOriginal.Add", new[] { "3" }) == "5", "old call path preserves optional arguments");
        Check(TLDRevamp.Reflect.Set("AliasOriginal.State.Value", "7") == "7" && AliasMoved.State.Value == 7, "aliased nested structs are written back");
        Check(TLDRevamp.Reflect.Get("AliasOriginal.Existing") == "1", "Dev aliases do not replace retained core members");
        Check(!References("src/TLDRevamp/TLDRevamp.csproj").Any(p => p.Contains("TLDRevamp.Dev")), "core must build independently of Dev");
        var references = References("src/TLDRevamp.Dev/TLDRevamp.Dev.csproj");
        Check(references.Any(p => p == "../TLDRevamp/TLDRevamp.csproj") && references.Any(p => p.Contains("TLDRevamp.Multiplayer")), "Dev depends on core and the multiplayer API");
        Check(File.Exists("src/TLDRevamp/Cinematic/CinematicShot.cs") && File.Exists("src/TLDRevamp/Net/HealthWatchRules.cs"), "shared player dependencies stay in core");
        string core = "src/TLDRevamp/bin/Release/netstandard2.1/TLDRevamp.dll", dev = "src/TLDRevamp.Dev/bin/Release/netstandard2.1/TLDRevamp.Dev.dll";
        if (File.Exists(core) && File.Exists(dev))
        {
            using var coreFile = File.OpenRead(core); using var corePe = new PEReader(coreFile);
            var coreMetadata = corePe.GetMetadataReader();
            Check(!coreMetadata.AssemblyReferences.Any(h => coreMetadata.GetString(coreMetadata.GetAssemblyReference(h).Name) == "TLDRevamp.Dev"), "compiled core has no Dev assembly dependency");
            using var devFile = File.OpenRead(dev); using var devPe = new PEReader(devFile);
            var devMetadata = devPe.GetMetadataReader();
            var types = devMetadata.TypeDefinitions.Select(h => devMetadata.GetTypeDefinition(h)).Select(t => devMetadata.GetString(t.Namespace) + "." + devMetadata.GetString(t.Name)).ToArray();
            Check(types.Contains("TLDRevamp.DebugV2") && types.Contains("TLDRevamp.Net.PlayerBotCore"), "developer features are compiled into Dev");
            Check(!types.Contains("TLDRevamp.DebugBridge") && !types.Contains("TLDRevamp.Net.Entities") && !types.Contains("TLDRevamp.Net.MpServer"), "Dev must not shadow a core partial class");
            Check(!types.Contains("TLDRevamp.Net.RemotePlayers") && !types.Contains("TLDRevamp.Replay.ReplayRecorder") && !types.Contains("TLDRevamp.Replay.ReplayViewer"), "Dev must not shadow remote-player or replay partials");
            foreach (string name in new[] { "EntityDebug", "EntityMountLab", "EntityReproPlayback", "EntityItemCheck", "EntityItemWatch", "EntityContactCensus", "EntityFakeInput", "EntityTrack", "RemoteReproPlayback" })
                Check(types.Contains("TLDRevamp.Net." + name), name + " is owned by Dev");
            var coreTypes = coreMetadata.TypeDefinitions.Select(h => coreMetadata.GetTypeDefinition(h)).Select(t => coreMetadata.GetString(t.Namespace) + "." + coreMetadata.GetString(t.Name)).ToArray();
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
