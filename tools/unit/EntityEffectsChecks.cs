using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using TLDRevamp;
using System.Reflection;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static object Invoke(Type owner, string name, params object[] args) => owner.GetMethod(name, Static).Invoke(null, args);
    static object Patch(Type owner, string name, string method, params object[] args) => owner.GetNestedType(name, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);

    // Captured by compiling the unchanged four production files at e35f29f before the move.
    static readonly Dictionary<string, string> EffectBytes = new Dictionary<string, string>
    {
        ["sound0"] = "3900000000000000F43F00000000000004C00000000000C27240022C010000403F0000A03F07",
        ["sound1"] = "3901000000000000F43F00000000000004C00000000000C27240022C010000403F0000A03F07",
        ["sound2"] = "3902000000000000F43F00000000000004C00000000000C27240022C010000403F0000A03F07",
        ["sound3"] = "3903000000000000F43F00000000000004C00000000000C27240022C010000403F0000A03F07",
        ["shotEmpty"] = "2D052C0100000003000000000000F43F00000000000004C00000000000C2724000000000000000000000803F00",
        ["shotWorld"] = "2D052C0100000003000000000000F43F00000000000004C00000000000C2724000000000000000000000803F010200FF000000000000004000000000000008400000000000001040000000000000803F00000000",
        ["shotObject"] = "2D052C0100000003000000000000F43F00000000000004C00000000000C2724000000000000000000000803F010101002D01000006676C617373000000400000404000008040000000000000803F000000000000000000000000000000000000803F0000003F",
        ["shotPlayer"] = "2D052C0100000003000000000000F43F00000000000004C00000000000C2724000000000000000000000803F010302020700000000400000404000008040000000000000803F000000000000000000000000000000000000803F0000803F",
        ["shotHole"] = "2D052C0100000003000000000000F43F00000000000004C00000000000C2724000000000000000000000803F0104FFFF2D010000000000004000004040295C7F4000000000000000000000803F",
        ["shotFalse"] = "2D062C0100000003000000000000F43F00000000000004C00000000000C2724000000000000000000000803F00",
        ["tankRequest"] = "2E2D01000001000000484101",
        ["explosiveRequest"] = "2E2D01000002000000A04100",
        ["explosion"] = "2F00000000000000F43F00000000000004C00000000000C27240CDCCCC3DCDCC4CBE9A99993ECDCCCCBE0000484100001040CDCCCC3DCDCC4C3E9A99993ECDCCCC3E",
    };

    static void CaptureEffects(Type effects, Type blasts)
    {
        mainscript.s = new mainscript(); Time.time = 10; Entities.ByNet.Clear(); Entities.IsHost = false;
        void Save(string label)
        {
            Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == EffectBytes[label], label + " fixed expected bytes");
            Check(Entities.Packets.Last().Reliable == (label != "sound0"), label + " original reliability");
        }
        var snd = effects.GetNestedType("Snd", BindingFlags.NonPublic);
        for (int i = 0; i < 4; i++)
        {
            Invoke(effects, "SendPlayerSound", Enum.ToObject(snd, i), new Vector3(1.25f, -2.5f, 300.125f), 2, 300, .75f, 1.25f, 7); Save("sound" + i);
        }
        var gunItem = new tosaveitemscript(); var gun = new weaponscript { ParentComponent = gunItem, ActuallyShotTime = 10, tempInt = 3 };
        Entities.ByNet[300] = new Entities.Ent { NetId = 300, Root = gunItem, Items = { gunItem } };
        PlayerCombat.LastOrigin = new Vector3(1.25f, -2.5f, 300.125f); PlayerCombat.LastDir = Vector3.forward; PlayerCombat.LastN = 0; PlayerCombat.LastPlayerHit = -1;
        Invoke(effects, "CaptureShot", gun, 0, false); Save("shotEmpty");
        var c = new Collider(); c.gameObject.layer = 17;
        gun.hit = new[] { new RaycastHit { collider = c, point = new Vector3(2, 3, 4), normal = Vector3.up } }; PlayerCombat.LastN = 1;
        Invoke(effects, "CaptureShot", gun, 0, false); Save("shotWorld");
        var target = new tosaveitemscript(); c.transform.parent = target.transform; c.transform.name = "glass"; c.gameObject.layer = 23;
        Entities.ByNet[301] = new Entities.Ent { NetId = 301, Root = target, Items = { target } };
        mainscript.s.ShootDecals = new[] { new GameObject { name = "decal" } };
        c.transform.Children.Add(new Transform { name = "decal(Clone)", lossyScale = new Vector3(.5f, .5f, .5f) });
        Invoke(effects, "CaptureShot", gun, 0, false); Save("shotObject");
        PlayerCombat.LastN = 0; PlayerCombat.LastPlayerHit = 7; RemotePlayers.Bone = new Transform();
        mainscript.s.ShootDecals = new[] { new GameObject(), new GameObject(), new GameObject() };
        Invoke(effects, "CaptureShot", gun, 0, false); Save("shotPlayer");
        PlayerCombat.LastPlayerHit = -1;
        var tank = new tankscript { ParentComponent = target }; target.tanks = new System.Collections.Generic.List<tankscript> { tank };
        var cap = new tankcapscript { Tank = tank, disableCollider = true }; cap.transform.position = new Vector3(2, 3, 4);
        Set(effects, "_fxGun", gun); Patch(effects, "ShotFxHole", "Postfix", cap); Invoke(effects, "CaptureShot", gun, 0, false); Save("shotHole");
        ((System.Collections.IList)Field(effects, "_fxHoles")).Clear(); Set(effects, "_fxGun", null);
        gun.ActuallyShotTime = -1; gun.ShotTime = 10; Invoke(effects, "CaptureShot", gun, 0, false); Save("shotFalse");
        Entities.ByNet[301].Proxy = true; Patch(blasts, "CopyTank", "Prefix", tank, 12.5f, true); Save("tankRequest");
        var explosive = new explosivescript { ParentComponent = target }; Patch(blasts, "CopyExplosive", "Prefix", explosive, 20f); Save("explosiveRequest");
        mainscript.s.ExplosionRed = new GameObject { name = "red", Part = new explosionscript { name = "red" } };
        var x = new explosionscript { name = "red(Clone)" }; x.transform.position = new Vector3(1.25f, -2.5f, 300.125f); x.transform.rotation = new Quaternion(.1f, -.2f, .3f, -.4f);
        x.particle.main.startColor = new Color(.1f, .2f, .3f, .4f);
        Patch(blasts, "BlastHook", "Prefix", x, 12.5f, 2.25f); Patch(blasts, "BlastHook", "Finalizer", new object[] { null }); Invoke(blasts, "BlastTick"); Save("explosion");
    }

    static NetReader EffectReader(string label) => Reader(Convert.FromHexString(EffectBytes[label]));

    static void EffectsChecks()
    {
        var effects = typeof(EntityEffects); var blasts = typeof(EntityExplosions);
        Check(GameLookups.FieldRefs.Where(n => n.StartsWith("weaponscript.")).Take(3).SequenceEqual(new[] { "weaponscript.hit", "weaponscript.tempInt", "weaponscript.alreadyShotFalse" }), "kernel initialization warms the three gun delegates in their original order");
        Check((bool)Field(typeof(Entities), "_effectsLookupsReady"), "kernel initializes gun lookups before feature use");
        CaptureEffects(effects, blasts);
        foreach (string part in new[] { "PlayerSounds", "ShotFx", "Spray", "Explosions" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            Check(!source.Contains("using static") && !System.Text.RegularExpressions.Regex.IsMatch(source, @"Entities\.(W|WS|ByNet|Server)\b"), part + " has no shared writer or table access");
        }
        Check(!ReferenceEquals(Field(effects, "W"), Field(blasts, "W")) && !ReferenceEquals(Field(effects, "WS"), Field(blasts, "WS")) && !ReferenceEquals(Field(effects, "W"), Field(effects, "WS")), "effects and explosions own distinct capture and relay writers");
        Check(typeof(Entities).GetField("_replaying", Static) == null && typeof(Entities).GetField("_blastDepth", Static) == null && typeof(Entities).GetField("_spraying", Static) == null && typeof(Entities).GetField("GunHits", Static) == null, "effect and blast state has one owner");

        CoreMembers.Register();
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.ShotFxSent", "23") == "23" && EntityEffects.ShotFxSent == 23, "shot counter alias writes its owner");
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.ExplosionsSent", "29") == "29" && EntityExplosions.ExplosionsSent == 29, "blast counter alias writes its owner");
        Check(BridgeReflect.Get("TLDRevamp.Net.Entities._blastDepth") == "0", "private blast guard alias");
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.SprayFx", "False") == "false" && !EntityEffects.SprayFx, "spray switch forwards to its owner");
        EntityEffects.SprayFx = true;
        Check(Entities.ShotFxStats() == EntityEffects.ShotFxStats() && Entities.ExplosionStats() == EntityExplosions.ExplosionStats(), "compiled stats forwarders preserve JSON");

        foreach (var row in new[] { ("sound0", effects, "ServerPlayerSound"), ("sound1", effects, "ServerPlayerSound"), ("shotObject", effects, "ServerShotFx"), ("explosion", blasts, "ServerExplosionFx") })
        {
            Invoke(row.Item2, row.Item3, 7, EffectReader(row.Item1));
            Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == EffectBytes[row.Item1] && Entities.LastExcept == 7 && Entities.Packets.Last().Reliable == (row.Item1 != "sound0"), row.Item1 + " relays original bytes excluding sender");
        }
        Entities.TestServer(301, 9);
        EntityExplosions.ServerExplodeReq(7, EffectReader("tankRequest"));
        Check(Entities.Packets.Last().Peer == 9 && Convert.ToHexString(Entities.Packets.Last().Bytes) == EffectBytes["tankRequest"], "explosion request goes to current owner byte for byte");
        int sent = Entities.Packets.Count;
        EntityExplosions.ServerExplodeReq(9, EffectReader("tankRequest"));
        HitCheck.Allow = false; EntityExplosions.ServerExplodeReq(7, EffectReader("tankRequest")); HitCheck.Allow = true;
        Entities.ClearServer(); EntityExplosions.ServerExplodeReq(7, EffectReader("tankRequest"));
        EntityExplosions.ServerExplodeReq(7, Reader(new byte[] { 46 }));
        Check(Entities.Packets.Count == sent, "request guards reject owner echo, denied hit, unknown id and truncated net");

        long played = EntityEffects.PlayerSoundsPlayed;
        for (int i = 0; i < 4; i++) EntityEffects.ApplyPlayerSound(EffectReader("sound" + i));
        Check(EntityEffects.PlayerSoundsPlayed == played + 4 && EntityEffects.SoundsPlayedBy.All(n => n == 1), "all four sounds apply to their original counter slots");
        var badSound = Convert.FromHexString(EffectBytes["sound0"]); badSound[1] = 4;
        EntityEffects.ApplyPlayerSound(Reader(badSound)); EntityEffects.ApplyPlayerSound(Reader(badSound.Take(10).ToArray()));
        badSound = Convert.FromHexString(EffectBytes["sound0"]); Array.Copy(BitConverter.GetBytes(double.NaN), 0, badSound, 2, 8);
        EntityEffects.ApplyPlayerSound(Reader(badSound));
        Check(EntityEffects.PlayerSoundsPlayed == played + 4, "sound kind, length and finite guards preserve counters");
        EntityEffects.ApplyShotFx(EffectReader("shotEmpty"));
        long applied = EntityEffects.ShotFxApplied, rejected = EntityEffects.ShotFxRejected;
        var badShot = Convert.FromHexString(EffectBytes["shotEmpty"]); Array.Copy(BitConverter.GetBytes(double.NaN), 0, badShot, 8, 8);
        EntityEffects.ApplyShotFx(Reader(badShot)); EntityEffects.ApplyShotFx(Reader(badShot.Take(10).ToArray()));
        Check(EntityEffects.ShotFxApplied == applied && EntityEffects.ShotFxRejected == rejected + 1, "shot finite and truncated header guards");

        // Named collider matches win over a nearer fallback; duplicate paths use the nearest bounds.
        var root = new Transform(); var a = new Collider(); a.transform.name = "glass"; a.bounds.center = new Vector3(10, 0, 0);
        var b = new Collider(); b.transform.name = "glass"; b.bounds.center = new Vector3(2, 0, 0);
        var other = new Collider(); other.transform.name = "metal";
        a.transform.parent = root; b.transform.parent = root; other.transform.parent = root; root.Children.AddRange(new Component[] { a, b, other });
        Check(ReferenceEquals(Invoke(effects, "ColliderAt", root, "glass", Vector3.zero), b.transform), "collider path resolves the nearest duplicate path");
        Check(ReferenceEquals(Invoke(effects, "ColliderAt", root, "missing", Vector3.zero), other.transform), "missing collider path uses nearest fallback");
        b.enabled = a.enabled = other.enabled = false;
        Check(ReferenceEquals(Invoke(effects, "ColliderAt", root, "glass", Vector3.zero), root), "disabled collider fallback stays on root");
        var target = Entities.ByNet[301]; var tank = target.Root.tanks[0];
        var cap = new tankcapscript { Tank = tank, disableCollider = true };
        Set(effects, "_fxGun", new weaponscript()); Patch(effects, "ShotFxHole", "Postfix", cap);
        Patch(effects, "DisplayHolePour", "Prefix", cap); Check(EntityEffects.DisplayPour, "copy hole pour suppresses tank edits during Update");
        Patch(effects, "DisplayHolePour", "Postfix"); Check(!EntityEffects.DisplayPour, "copy hole pour scope ends at postfix");
        var priority = effects.GetNestedType("DisplayHolePour", BindingFlags.NonPublic).GetMethod("Prefix", Static).GetCustomAttribute<HarmonyLib.HarmonyPriority>();
        Check(priority.priority == HarmonyLib.Priority.First, "display pour prefix keeps Harmony First priority");
        Set(effects, "_fxGun", null); ((System.Collections.IList)Field(effects, "_fxHoles")).Clear();
        Check(Entities.ItemRef(target.Root, out uint net, out int index) && net == 301 && index == 0 && Entities.TankRef(tank, out net, out index) && net == 301 && index == 0, "kernel item and tank addresses stay reachable");

        var can = new tosaveitemscript { Held = true, ChildComponent = new sprayscript { valve = .2f, hasPaint = true } };
        var spray = (sprayscript)can.ChildComponent; var e = new Entities.Ent { Root = can, Proxy = true };
        Check(Entities.IsSpraying(can), "held spray above valve threshold is captured");
        can.Held = false; Check(!Entities.IsSpraying(can), "unheld spray is not captured");
        long shown = EntityEffects.SprayShown; EntityEffects.ShowSpraying(e, true); EntityEffects.ShowSpraying(e, true);
        int devTicks = 0; DevHooks.SprayTick = () => devTicks++;
        EntityEffects.SprayTick(.1f); Check(devTicks == 1 && spray.firing && spray.valve > .2f && EntityEffects.SprayShown == shown + 1, "spray hook precedes copy valve display and duplicate starts do not count twice");
        EntityEffects.SprayFx = false; spray.firing = false; float valve = spray.valve; EntityEffects.SprayTick(.1f);
        Check(!spray.firing && spray.valve == valve, "spray switch disables display only"); EntityEffects.SprayFx = true;
        EntityEffects.ShowSpraying(e, false); Check(!e.Spraying, "spray stop clears flag and registry");
        EntityEffects.ShowSpraying(e, true); e.Proxy = false; EntityEffects.SprayTick(.1f); Check(!e.Spraying, "spray copy gone or ownership change clears stale flag");
        EntityEffects.ResetSpraying(); DevHooks.SprayTick = null;
        Check(EntityEffects.SprayShown == shown + 2, "spray reset preserves cumulative counter");

        ExplosionGuards(tank, target);
        // Host capture copies the request before relay; a local receive can synchronously send another effect.
        Entities.IsHost = true;
        Entities.HostReceive = (kind, r) => { if (kind == Entities.PlayerSound) EntityEffects.ServerPlayerSound(0, r); };
        Invoke(effects, "SendPlayerSound", Enum.ToObject(effects.GetNestedType("Snd", BindingFlags.NonPublic), 0), new Vector3(1.25f, -2.5f, 300.125f), 2, 300, .75f, 1.25f, 7);
        Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == EffectBytes["sound0"], "host synchronous sound relay keeps complete message bytes");
        Entities.HostReceive = null;
        Entities.PeerDeliver = () => { Entities.PeerDeliver = null; Invoke(effects, "SendPlayerSound", Enum.ToObject(effects.GetNestedType("Snd", BindingFlags.NonPublic), 0), Vector3.zero, 0, 0, 1f, 1f, 0); };
        int relayAt = Entities.Packets.Count; EntityEffects.ServerShotFx(7, EffectReader("shotObject"));
        Check(Convert.ToHexString(Entities.Packets[relayAt].Bytes) == EffectBytes["shotObject"], "remote peers get shot relay before local reentrant sends");
        Entities.IsHost = false; Entities.ByNet.Clear(); Entities.Packets.Clear();

        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.Replace("\r\n", "\n").Contains("ReshareTick();\n            EntityExplosions.BlastTick();\n            MemberGoneTick();"), "blast tick keeps its exact scheduler position");
        Check(kernel.Contains("EntityEffects.ResetSpraying();") && !kernel.Contains("EntityExplosions.Reset"), "reset clears spray only and does not add a pending blast reset");
        Check(File.ReadAllText("src/TLDRevamp/Net/Entities.Ai.cs").Contains("EntityExplosions.BlastDepth == 0"), "AI forwarding preserves the zero-depth guard");
        Check(File.ReadAllText("src/TLDRevamp/Net/Entities.Edits.cs").Contains("|| EntityEffects.DisplayPour) return;"), "tank edits retain the display pour guard");
    }

    static void ExplosionGuards(tankscript tank, Entities.Ent target)
    {
        var blasts = typeof(EntityExplosions);
        var pending = (System.Collections.IList)Field(blasts, "Pending");
        pending.Clear();
        int sent = Entities.Packets.Count; long skipped = EntityExplosions.CopyBlastsSkipped;
        var x = new explosionscript { name = "red(Clone)" };
        Patch(blasts, "BlastHook", "Prefix", x, 1f, 0f);
        Check(EntityExplosions.BlastDepth == 1 && pending.Count == 1, "blast prefix records pending event before body");
        Check(!(bool)Patch(blasts, "CopyTank", "Prefix", tank, 20f, false), "blast blocks copy tank simulation");
        var explosive = new explosivescript { ParentComponent = target.Root };
        Check(!(bool)Patch(blasts, "CopyExplosive", "Prefix", explosive, 20f), "blast blocks copy explosive simulation");
        Check(Entities.Packets.Count == sent && EntityExplosions.CopyBlastsSkipped == skipped + 2, "blast copy suppression sends no owner requests");
        var error = new InvalidOperationException("blast body");
        Check(ReferenceEquals(Patch(blasts, "BlastHook", "Finalizer", error), error) && EntityExplosions.BlastDepth == 0, "finalizer balances depth and returns original exception");
        // The queued particle color is sampled at tick time, after Explode may have colored it.
        x.particle.main.startColor = new Color(.2f, .3f, .4f, .5f); EntityExplosions.BlastTick();
        Check(pending.Count == 0 && Entities.Packets.Last().Reliable && BitConverter.ToSingle(Entities.Packets.Last().Bytes, 50) == .2f, "pending blast samples final color at tick and clears after send");
        long applied = EntityExplosions.ExplosionsApplied;
        explosionscript.During = replay =>
        {
            Check(ReferenceEquals(Field(blasts, "_replaying"), replay), "replay guard names this explosion only");
            Patch(blasts, "BlastHook", "Prefix", replay, 12.5f, 2.25f);
            Check(pending.Count == 0, "replayed explosion does not echo");
            Patch(blasts, "BlastHook", "Prefix", new explosionscript { name = "red(Clone)" }, 4f, 0f);
            Check(pending.Count == 1 && EntityExplosions.BlastDepth == 2, "owned chain explosion is queued during replay");
            Patch(blasts, "BlastHook", "Finalizer", new object[] { null });
            Patch(blasts, "BlastHook", "Finalizer", new object[] { null });
        };
        EntityExplosions.ApplyExplosionFx(EffectReader("explosion"));
        Check(Field(blasts, "_replaying") == null && EntityExplosions.BlastDepth == 0 && EntityExplosions.ExplosionsApplied == applied + 1, "successful replay clears guard and increments once");
        EntityExplosions.BlastTick();
        explosionscript.During = replay => { throw error; };
        try { EntityExplosions.ApplyExplosionFx(EffectReader("explosion")); Check(false, "replay throws"); } catch (InvalidOperationException) { }
        Check(Field(blasts, "_replaying") == null && EntityExplosions.ExplosionsApplied == applied + 1, "failed replay clears guard without counting an apply");
        explosionscript.During = null;
        var bad = Convert.FromHexString(EffectBytes["explosion"]); Array.Copy(BitConverter.GetBytes(float.PositiveInfinity), 0, bad, 42, 4);
        long rejected = EntityExplosions.ExplosionsRejected;
        EntityExplosions.ApplyExplosionFx(Reader(bad)); EntityExplosions.ApplyExplosionFx(Reader(bad.Take(10).ToArray()));
        Check(EntityExplosions.ExplosionsRejected == rejected + 1 && EntityExplosions.ExplosionsApplied == applied + 1, "blast finite and truncated guards keep replay state clean");
        long run = EntityExplosions.ExplodeReqsRun;
        EntityExplosions.ApplyExplodeReq(EffectReader("tankRequest")); Check(EntityExplosions.ExplodeReqsRun == run, "copy ignores owner request");
        target.Proxy = false; EntityExplosions.ApplyExplodeReq(EffectReader("tankRequest")); Check(tank.Calls == 1 && EntityExplosions.ExplodeReqsRun == run + 1, "owner runs addressed tank request once");
        target.Root.ChildComponent = explosive; EntityExplosions.ApplyExplodeReq(EffectReader("explosiveRequest"));
        Check(explosive.Calls == 1 && EntityExplosions.ExplodeReqsRun == run + 2, "owner runs addressed explosive request once");
        bad = Convert.FromHexString(EffectBytes["tankRequest"]); Array.Copy(BitConverter.GetBytes(float.NaN), 0, bad, 7, 4);
        EntityExplosions.ApplyExplodeReq(Reader(bad)); EntityExplosions.ApplyExplodeReq(Reader(bad.Take(5).ToArray()));
        Check(EntityExplosions.ExplodeReqsRun == run + 2, "invalid owner requests cannot run game code");
    }
}

