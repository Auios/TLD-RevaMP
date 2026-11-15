using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

// Compile the feature owners and core registry without Dev startup or Unity. Transport is captured at the kernel seam.
static partial class EntitySplitTest
{
    static int checks;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    static object Field(Type owner, string name) => owner.GetField(name, Static).GetValue(null);
    static void Set(Type owner, string name, object value) => owner.GetField(name, Static).SetValue(null, value);
    static byte[] Bytes(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); write(writer); return stream.ToArray();
    }
    static NetReader Reader(byte[] bytes)
    {
        var r = new NetReader(); r.Load(bytes.Length); Array.Copy(bytes, r.Buf, bytes.Length); r.U8(); return r;
    }
    static void Hook(Type owner, string name, params object[] args) => owner.GetNestedType(name, BindingFlags.NonPublic).GetMethod("Prefix", Static).Invoke(null, args);

    static void Main()
    {
        UsablesChecks();
        TelemetryChecks();
        ReproChecks();
        RabbitChecks();
        AIChecks();
        RagdollsChecks();
        HandsChecks();
        SeatsChecks();
        TuningChecks();
        DoorChecks();
        CodecVectors();
        CarSignalChecks();
        EffectsChecks();
        CoreMembers.Register();
        foreach (var feature in new[] { (typeof(EntityRadio), "Radio"), (typeof(EntityWorldClock), "Clock"), (typeof(EntityWeather), "Storms"), (typeof(EntityCodec), "StateCodec"), (typeof(EntityCarSignals), "CarSignals"), (typeof(EntityEffects), "PlayerSounds"), (typeof(EntityExplosions), "Explosions"), (typeof(EntityCarTuning), "CarTune"), (typeof(EntityDoors), "CarDoors"), (typeof(EntitySeats), "Seat"), (typeof(EntityHands), "Carried"), (typeof(EntityRagdolls), "Death"), (typeof(EntityAI), "Ai"), (typeof(EntityRabbits), "Rabbits"), (typeof(EntityReproHooks), "Repro") })
        {
            var type = feature.Item1;
            Check(type.IsNotPublic && type.IsAbstract && type.IsSealed, type.Name + " is internal static");
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + feature.Item2 + ".cs");
            Check(!Regex.IsMatch(source, @"using\s+static\s+(?:global::)?(?:TLDRevamp\.Net\.)?Entities\s*;"), type.Name + " uses explicit kernel calls");
            Check(!Regex.IsMatch(source, @"Entities\.(?:W|WS|ByNet|Server)\b"), type.Name + " does not expose kernel state");
            foreach (var member in type.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
            {
                string old = "Entities." + member.Name;
                bool retained = typeof(Entities).GetMember(member.Name, Static).Length > 0;
                string target = retained ? old : type.FullName + "." + member.Name;
                Check(Reflect.MemberPath(old) == target, "short core alias " + old);
                Check(Reflect.MemberPath("TLDRevamp.Net." + old) == (retained ? "TLDRevamp.Net." + old : target), "full core alias " + old);
            }
        }
        foreach (string part in new[] { "DetachCodec", "RotCodec", "StateCodec" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Split("    public static partial class Entities")[0];
            source = Regex.Replace(source, @"//[^\r\n]*", "");
            Check(!Regex.IsMatch(source, @"using\s+static\s+(?:global::)?(?:TLDRevamp\.Net\.)?Entities\s*;"), part + " has no static kernel import");
            Check(Regex.Matches(source, @"Entities\.(\w+)").Cast<Match>().All(m => new[] { "State", "DetachSync", "IsFinite" }.Contains(m.Groups[1].Value)), part + " uses only protocol and finite kernel facts");
        }
        foreach (string part in new[] { "CarTune", "Blood", "CopyFluids", "CarDoors", "BusDoors", "DoorAttachments", "Carried", "HandItems", "Worn", "Death", "Corpses", "Ragdoll", "Ai", "Tumbleweeds", "Repro", "ReproDrive" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            Check(!Regex.IsMatch(source, @"using\s+static\s+(?:global::)?(?:TLDRevamp\.Net\.)?Entities\s*;|Entities\.(?:W|WS|ByNet|Server)\b"), part + " uses only narrow qualified kernel calls");
        }
        Check(!ReferenceEquals(Field(typeof(EntityCarTuning), "W"), Field(typeof(Entities), "W")) && typeof(EntityDoors).GetField("W", Static) == null, "tuning owns its writer and doors adds no standalone writer");
        Check(typeof(Entities).GetField("_tuneHere", Static) == null && typeof(Entities).GetField("CopyTankTruth", Static) == null && typeof(Entities).GetField("_storedDoorAttaches", Static) == null, "tuning fluid and door caches have one owner");
        Check(BridgeReflect.Get("TLDRevamp.Net.Entities._storedDoorAttaches.Count") == "0" && BridgeReflect.Get("TLDRevamp.Net.Entities._tuneHere.Count") == "0", "historical full cache paths resolve to feature owners");
        Check(typeof(Entities).GetField("ReadHandPose", Static) == null && typeof(Entities).GetField("ReadFinalState", Static) == null && typeof(Entities).GetField("SignalCars", Static) == null && typeof(Entities).GetField("SignalsApplied", Static) == null, "codec scratch and car signal state have one owner");
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.ReadHandPose", "False") == "false" && !EntityCodec.ReadHandPose, "codec scratch full alias writes owner");
        Check(BridgeReflect.Get("Entities.ReadLockItem") == EntityCodec.ReadLockItem.ToString(), "codec scratch short alias reads owner");
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.SignalsApplied", "17") == "17" && EntityCarSignals.SignalsApplied == 17 && BridgeReflect.Get("Entities.SignalsApplied") == "17", "car counter full and short aliases share state");
        string plugin = File.ReadAllText("src/TLDRevamp/Plugin.cs");
        Check(plugin.IndexOf("CoreMembers.Register();") > plugin.IndexOf("private void Awake()") && plugin.IndexOf("CoreMembers.Register();") < plugin.IndexOf("TestSafety.Init();"), "core aliases register at startup before settings and bridge use");
        Check(BridgeReflect.Set("Entities.RadioResendS", "7") == "7" && EntityRadio.RadioResendS == 7, "radio setting forwards to one owner");
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.RadioSent", "11") == "11" && EntityRadio.RadioSent == 11, "moved radio counter full set");
        Check(BridgeReflect.Get("Entities.RadioSent") == "11", "moved radio counter short get");
        Check(BridgeReflect.Set("Entities._worldEpoch", "3") == "3", "clock private field alias");
        Check(BridgeReflect.Set("Entities.StormSpawnsSkipped", "13") == "13" && EntityWeather.StormSpawnsSkipped == 13, "weather moved field alias");
        BridgeReflect.Call("Entities.ClockReset", Array.Empty<string>());
        Check(BridgeReflect.Get("Entities._worldEpoch") == "0" && EntityRadio.RadioSent == 11, "old clock call resets features and preserves counters");
        Check(!ReferenceEquals(Field(typeof(EntityRadio), "W"), Field(typeof(EntityWorldClock), "W")) && !ReferenceEquals(Field(typeof(EntityRadio), "W"), Field(typeof(EntityWeather), "W")) && !ReferenceEquals(Field(typeof(EntityWorldClock), "W"), Field(typeof(EntityWeather), "W")), "each feature owns a separate writer");

        mainscript.s = new mainscript(); napszakvaltakozas.s = new napszakvaltakozas();
        var station = new mainscript.radioCH { name = "FM", Index = 2, startTime = 90, currentClip = new AudioClip { name = "clip", length = 100 } };
        mainscript.s.radioChs.Add(station);
        Entities.IsHost = true; ModObjects.ClockTime = 10; Time.time = 100; Time.timeAsDouble = 100; Time.timeSinceLevelLoadAsDouble = 100;
        EntityWorldClock.HostClockTick();
        EntityRadio.RadioTick(0.1f);
        var radio = Bytes(w => { w.Write((byte)54); w.Write(10d); w.Write(1f); w.Write((byte)1); w.Write((ushort)3); w.Write(10f); w.Write((byte)0); });
        Check(Entities.Packets.Last().Bytes.SequenceEqual(radio) && Entities.Packets.Last().Reliable, "radio legacy bytes and reliable broadcast");
        int sends = Entities.Packets.Count; EntityRadio.RadioTick(0.1f); Check(Entities.Packets.Count == sends, "unchanged station waits for resend");
        EntityRadio.ServerRadioTo(5); Check(Entities.Packets.Last().Peer == 5 && Entities.Packets.Last().Bytes.SequenceEqual(radio), "radio join replay uses same bytes");
        Time.timeScale = 0; EntityWorldClock.HostClockTick(); EntityRadio.RadioTick(0.1f);
        Check(Entities.Packets.Count == sends + 2 && Entities.HostClockRate == 0, "rate change invalidates radio immediately");
        Entities.IsHost = false;
        EntityRadio.ApplyRadio(Reader(radio)); long applied = EntityRadio.RadioApplied;
        EntityRadio.ApplyRadio(Reader(radio));
        Check(applied == 1 && EntityRadio.RadioApplied == applied && TLDRevamp.Fixes.SessionClocks.RadioStale == 1, "radio stale anchor rejected");
        Check((double)Field(typeof(EntityRadio), "_radioStamp") == 10 && station.cTime == 10, "radio receive and projection");
        Set(typeof(EntityRadio), "_radioInner", true);
        EntityRadio.ResetSession();
        Check((double)Field(typeof(EntityRadio), "_radioStamp") == -1 && (double)Field(typeof(EntityRadio), "_radioSentAt") == -100 && EntityRadio.RadioApplied == applied && EntityRadio.RadioResendS == 7 && (bool)Field(typeof(EntityRadio), "_radioInner"), "radio reset preserves settings counters and recursion guard");
        Set(typeof(EntityRadio), "_radioInner", false);

        Entities.IsHost = true; Time.timeScale = 1; EntityWorldClock.ClockReset(); EntityWorldClock.HostClockTick();
        EntityWorldClock.ServerWorldClockTo(9);
        var clock = Bytes(w => { w.Write((byte)97); w.Write(10d); w.Write(100f); w.Write(1f); w.Write((ushort)0); w.Write(true); w.Write(2400d); w.Write(-1f); });
        Check(Entities.Packets.Last().Bytes.SequenceEqual(clock) && Entities.Packets.Last().Reliable && Entities.Packets.Last().Peer == 9, "clock join bytes and reliable snap");
        EntityWorldClock.ServerTimeTick(0.1f);
        Check(!Entities.Packets.Last().Reliable && Entities.Packets.Last().Bytes[19] == 0, "periodic clock remains unreliable without snap");
        Entities.IsHost = false; EntityWorldClock.ReceiveWorldClock(Reader(clock)); EntityWorldClock.ReceiveWorldClock(Reader(Entities.Packets.Last().Bytes));
        Check(TLDRevamp.Fixes.SessionClocks.WorldReceived == 1 && TLDRevamp.Fixes.SessionClocks.WorldStale == 1, "clock stale anchor rejected");
        EntityWorldClock.ReceiveWorldClock(Reader(clock));
        Check(TLDRevamp.Fixes.SessionClocks.WorldReceived == 2, "equal stamp forced clock still applies");
        Hook(typeof(EntityWorldClock), "WorldFollowsHost", napszakvaltakozas.s);
        Check(TLDRevamp.Fixes.SessionClocks.WorldSnapped == 1, "world patch follows initial host anchor");
        mainscript.s.sleeping = true; EntityWorldClock.SleepTick(0.25f);
        Check(Entities.Packets.Last().Bytes.SequenceEqual(Bytes(w => { w.Write(Entities.SleepSync); w.Write(100f); w.Write(10d); w.Write(0.5f); })) && Entities.Packets.Last().Reliable, "sleep request legacy bytes");
        mainscript.s.sleeping = false; EntityWorldClock.SleepTick(0);
        Check(Entities.Packets.Last().Bytes.SequenceEqual(Bytes(w => { w.Write(Entities.SleepSync); w.Write(100f); w.Write(10d); w.Write(-1f); })), "wake request legacy bytes");
        Check((bool)Field(typeof(EntityWorldClock), "_worldAwaitingWake"), "client wake awaits host epoch");
        long sleeps = EntityWorldClock.SleepClocksSent;
        Set(typeof(EntityWorldClock), "_sleepClockAcc", 0.125f); EntityWorldClock.ClockReset();
        Check(EntityWorldClock.SleepClocksSent == sleeps && (float)Field(typeof(EntityWorldClock), "_sleepClockAcc") == 0.125f, "clock reset preserves cumulative count and sleep accumulator lifetime");
        Entities.IsHost = true; mainscript.s.sleeping = true; EntityWorldClock.SleepTick(0.25f);
        Check(Entities.Packets[^2].Peer == -2 && Entities.Packets[^1].Bytes[0] == 97 && (ushort)Field(typeof(EntityWorldClock), "_worldEpoch") == 1, "synchronous host sleep sends request before snapped reply");
        mainscript.s.sleeping = false;

        var storm = new GameObject().Storm; storm.prefabid = 2; storm.transform.position = new Vector3(4, 0, 6); storm.transform.eulerAngles = new Vector3(0, 8, 0);
        storm.size = 10; storm.xdir = 12; storm.ydir = 14; storm.speed = 16; storm.life = 18; napszakvaltakozas.s.sandStorms.Add(storm);
        EntityWeather.StormTick(0.1f);
        var storms = Bytes(w => { w.Write((byte)56); w.Write((byte)1); w.Write(1u); w.Write((byte)2); w.Write(4d); w.Write(6d); w.Write(8f); w.Write(10f); w.Write(12f); w.Write(14f); w.Write(16f); w.Write(18f); });
        Check(Entities.Packets.Last().Bytes.SequenceEqual(storms) && Entities.Packets.Last().Reliable, "storm legacy bytes and reliable broadcast");
        sends = Entities.Packets.Count; EntityWeather.StormTick(0.1f); Check(Entities.Packets.Count == sends, "storm unchanged count waits for resend");
        Entities.IsHost = false; itemdatabase.s = new itemdatabase { storms = new[] { new GameObject(), new GameObject(), new GameObject() } };
        EntityWeather.ApplyStorms(Reader(storms));
        Check(EntityWeather.StormSyncsApplied == 1 && EntityWeather.StormsMade == 1 && storm.gameObject.Destroyed, "storm receive replaces local storm with host state");
        var copy = EntityWeather.ObservedStormById[1];
        Check(copy.started && copy.size == 10 && copy.xdir == 12 && copy.ydir == 14 && copy.speed == 16 && copy.life == 18, "storm receive preserves host values");
        uint next = (uint)Field(typeof(EntityWeather), "_stormNext"); EntityWeather.StormReset();
        Check(EntityWeather.ObservedStormById.Count == 0 && EntityWeather.ObservedStormIds.Count == 0 && !copy.gameObject.Destroyed && (uint)Field(typeof(EntityWeather), "_stormNext") == next && EntityWeather.StormsMade == 1, "storm reset keeps local objects next id and counters");
        ResyncEditsChecks();
        Console.WriteLine("ALL PASS (" + checks + " entity split checks)");
    }
}
