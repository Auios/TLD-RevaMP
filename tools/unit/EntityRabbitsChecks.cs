using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static Type RabbitOwner = typeof(EntityRabbits);
    static object RabbitCall(string name, params object[] args) => RabbitOwner.GetMethod(name, Static).Invoke(null, args);
    static object RabbitPatch(string name, string method, params object[] args) => typeof(EntityRabbits).GetNestedType(name, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);
    static IDictionary RabbitMap(string name) => (IDictionary)Field(typeof(EntityRabbits), name);
    static IList RabbitList(string name) => (IList)Field(typeof(EntityRabbits), name);
    static object RabbitKernel(string name, params object[] args) => typeof(Entities).GetMethod(name, Static).Invoke(null, args);
    static void RabbitVector(string name)
    {
        // Captured by running the unchanged HEAD send bodies before production ownership moved.
        string expected = name switch {
            "state" => "5A2D01000007000000015A0164000000C80000002C010000",
            "FX" => "5B2D010000018201",
            "bite" => "302D01000000030000604000000000000000000000000002",
            "damage" => "2802CCCCCC3E0000803F0000000000000000010000000000000000000000002D01000000",
            _ => throw new Exception(name)
        };
        Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == expected, "rabbit " + name + " matches expected bytes at " + RabbitOwner.Name);
        Check(Entities.Packets.Last().Reliable, "rabbit " + name + " stays reliable");
    }
    static void RabbitChecks()
    {
        foreach (var owner in new[] { typeof(Entities).GetNestedType("RabbitBaseline", BindingFlags.NonPublic), typeof(EntityRabbits) }.Where(t => t != null))
        {
            RabbitOwner = owner;
            RabbitWireChecks();
        }
        RabbitOwner = typeof(EntityRabbits);
        RabbitCall("RabbitReset");
        RabbitStateChecks();
        RabbitFxChecks();
        RabbitBiteChecks();
        RabbitOwnerChecks();
        RabbitNearChecks();
        RabbitBoundaryChecks();
        RabbitCall("RabbitReset"); Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.Known.Clear();
        RemotePlayers.Targets.Clear(); Entities._localCars.Clear();
    }
    static void RabbitWireChecks()
    {
        Entities.ByNet.Clear(); Entities.Packets.Clear(); RemotePlayers.Targets.Clear();
        var root = new tosaveitemscript(); var ny = new nyulscript { ParentComponent = root, AI = new aiscript { fleeing = true, interestPos = new Vector3(1, 2, 3) }, runValue = .45f };
        root.nyul = ny; ny.AI.alive = true; Time.time = 5; ny.lastSeeTarget = 4;
        var br = new breakablescript { ParentComponent = root, decreaseAfterAttack = true };
        root.transform.Children.Add(br);
        var e = new Entities.Ent { NetId = 301, Epoch = 7, Root = root, Items = new System.Collections.Generic.List<tosaveitemscript> { root } };
        Entities.ByNet[301] = e;
        RabbitCall("SendRabbitHandover", e, (uint)7);
        RabbitVector("state");
        RabbitCall("SendRabbitFx", ny, (byte)1, 129);
        RabbitVector("FX");
        Set(RabbitOwner, "_rabTrigger", true);
        RabbitCall("RabbitBreakOnCopy", br, 2f); RabbitCall("RabbitBreakOnCopy", br, 5f);
        RabbitCall("RabbitBreakFlush", 1f);
        RabbitVector("bite");
        var body = new Transform { position = new Vector3(1, 0, 0) };
        RemotePlayers.Targets.Add((2, body, body, Vector3.zero, Vector3.up, 0));
        ny.attackTriggerScript = new playerdeathtriggerscript { selfCol = new Collider(), damagePlayer = 10 };
        Time.deltaTime = .02f;
        RabbitCall("RabbitRemoteBite", e, ny); RabbitCall("RabbitRemoteBite", e, ny); RabbitCall("RabbitDamageFlush", 1f);
        RabbitVector("damage");
        RemotePlayers.Targets.Clear(); Set(RabbitOwner, "_rabTrigger", false);
    }

    static (Entities.Ent e, nyulscript ny) RabbitFixture(bool proxy = false)
    {
        Entities.ByNet.Clear(); Entities.Packets.Clear(); RemotePlayers.Targets.Clear(); Entities.Known.Clear();
        RabbitCall("RabbitReset");
        foreach (string timer in new[] { "_rabScanAt", "_rabSendAt", "_rabDmgFlushAt", "_rabBreakFlushAt", "_rabSrvAcc" }) Set(RabbitOwner, timer, 0f);
        Entities.IsHost = false; Entities.InSession = true; Entities.TestAudience = true;
        Entities.HostReceive = null; Entities.PeerReceive = null;
        mainscript.s = new mainscript { player = new fpscontroller() };
        Time.time = Time.unscaledTime = Time.realtimeSinceStartup = 10; Time.deltaTime = .02f;
        var root = new tosaveitemscript(); var body = new Rigidbody { detectCollisions = true }; root.ChildComponent = body;
        var ny = new nyulscript { ParentComponent = root, AI = new aiscript { fleeing = true, interestPos = new Vector3(1, 2, 3) }, move = new movecreaturescript { RB = body, speed = 2 },
            runValue = .45f, lastSeeTarget = 9, playerDeathTrigger = new GameObject(), anim = new RabbitAnim(), senses = new sensoryscript() };
        root.nyul = ny;
        var e = new Entities.Ent { NetId = 301, Epoch = 7, Root = root, Items = { root }, Proxy = proxy, OwnerId = 1, OwnerAt = 9 };
        Entities.ByNet[301] = e;
        RemotePlayers.Targets.Add((2, new Transform { position = Vector3.one }, new Transform(), Vector3.zero, Vector3.up, 0));
        return (e, ny);
    }

    static void RabbitStateChecks()
    {
        var (e, ny) = RabbitFixture();
        RabbitCall("RabbitTick"); RabbitVector("state"); byte[] state = Entities.Packets.Last().Bytes;
        int count = Entities.Packets.Count;
        Time.unscaledTime += .11f; RabbitCall("RabbitTick"); Check(Entities.Packets.Count == count, "unchanged rabbit state has no heartbeat");
        ny.AI.interestPos = new Vector3(20, 2, 3); Time.unscaledTime += .11f; RabbitCall("RabbitTick");
        Check(Entities.Packets.Count == count, "moving target alone does not add animation traffic");
        ny.runValue += .005f; Time.unscaledTime += .11f; RabbitCall("RabbitTick"); Check(Entities.Packets.Count == count, "run jitter remains suppressed");
        e.Epoch++; Time.unscaledTime += .11f; RabbitCall("RabbitTick"); Check(Entities.Packets.Count == count + 1, "new rabbit epoch forces state");
        ny.AI.alive = false; ny.attacking = ny.roaring = true; Time.unscaledTime += .11f; RabbitCall("RabbitTick");
        var dead = RabbitStateCodec.Read(Reader(Entities.Packets.Last().Bytes));
        Check(dead.Flags == 128 && dead.Run == 0 && !dead.Seen && dead.X == 0 && dead.Y == 0 && dead.Z == 0, "dead rabbit state drops animation and target bits");
        long skipped = Entities.StatesSkippedDeadAnimation; ny.runValue = .2f; Time.unscaledTime += .11f; RabbitCall("RabbitTick");
        Check(Entities.StatesSkippedDeadAnimation == skipped + 1, "dead animation raw opportunity still counted");
        RemotePlayers.Targets.Clear(); e.Epoch++; Time.unscaledTime += .11f; count = Entities.Packets.Count; RabbitCall("RabbitTick"); Check(Entities.Packets.Count == count, "no remote players means no rabbit send");

        e.Proxy = true; e.Epoch = 7; e.Root = null; long applied = EntityRabbits.RabbitStatesApplied;
        RabbitCall("ApplyRabbitState", Reader(state)); Check(e.RabbitSnapshot != null && EntityRabbits.RabbitStatesApplied == applied + 1, "streamed out rabbit caches reliable state");
        var cache = e.RabbitSnapshot;
        for (int len = 1; len < state.Length; len++) RabbitCall("ApplyRabbitState", Reader(state.Take(len).ToArray()));
        Check(ReferenceEquals(cache, e.RabbitSnapshot), "truncated rabbit state cannot replace cache");
        e.Epoch++; RabbitCall("ApplyRabbitState", Reader(state)); Check(ReferenceEquals(cache, e.RabbitSnapshot), "stale rabbit epoch cannot replace cache");
        e.Epoch = 7; e.Root = new tosaveitemscript { nyul = ny }; RabbitCall("RestoreRabbitState", e);
        Check(RabbitMap("RabViews").Contains(ny) && RabbitMap("RabSeen").Contains(ny), "streamed in rabbit restores view and seen state");
        ny.playerDeathTrigger.SetActive(true); RabbitCall("RabbitTick");
        Check(!ny.playerDeathTrigger.activeSelf && ny.anim.Run > 0, "copy never bites and displays smoothed animation");
        ny.attacking = ny.roaring = ny.move.modifier = true; ny.AI.fleeing = true; ny.move.RB.velocity = new Vector3(3, 0, 0); ny.AI.move = ny.move;
        RabbitCall("RabbitRole", e, false);
        Check(!ny.attacking && !ny.roaring && !ny.move.modifier && !ny.AI.fleeing && ny.AI.lastVelocity.x == 3 && ny.AI.goToTarget, "new owner clears attacks and carries handed velocity");
        Check(ny.AI.interestPos.x == 1 && ny.lastSeeTarget == Time.time && !RabbitMap("RabViews").Contains(ny), "new owner carries global target and last see timer");
        e.Proxy = false; e.OwnerAt = Time.realtimeSinceStartup; ny.AI.interestPos = Vector3.zero;
        RabbitCall("ApplyRabbitState", Reader(state)); Check(ny.AI.interestPos.x == 1, "owner accepts final target in handover window");
        e.OwnerAt -= 3; ny.AI.interestPos = Vector3.zero; RabbitCall("ApplyRabbitState", Reader(state)); Check(ny.AI.interestPos == Vector3.zero, "established owner ignores late target");
        ny.AI.interestPos = new Vector3(float.NaN, 0, 0); count = Entities.Packets.Count; RabbitCall("SendRabbitHandover", e, 7u); Check(Entities.Packets.Count == count, "nonfinite handover target never sends");

        Entities.TestServer(301, 1); var se = ((IDictionary)Field(typeof(Entities), "Server"))[301u]; var type = se.GetType();
        void SetServer(string name, object value) => type.GetField(name, Static | BindingFlags.Instance).SetValue(se, value);
        SetServer("Epoch", 7u); SetServer("PrevOwnerId", 2); SetServer("PrevOwnerAt", Time.realtimeSinceStartup);
        Entities.Known[0] = new HashSet<uint> { 301 }; Entities.Known[1] = new HashSet<uint> { 301 }; Entities.Known[3] = new HashSet<uint> { 301 }; Entities.Known[4] = new HashSet<uint>(); Entities.RabbitFar.Add(3);
        Entities.Packets.Clear(); RabbitKernel("ServerRabbitState", 1, Reader(state));
        Check(Entities.Packets.Count == 1 && Entities.Packets[0].Peer == 0 && Entities.Packets[0].Bytes.SequenceEqual(state), "server caches and relays exact state to near known nonowners");
        Entities.Packets.Clear(); RabbitKernel("ServerRabbitState", 2, Reader(state));
        Check(Entities.Packets.Select(p => p.Peer).SequenceEqual(new[] { 1, 0 }), "one departing-owner target reaches new owner before other copies");
        long stale = EntityRabbits.RabbitStatesStale; RabbitKernel("ServerRabbitState", 2, Reader(state)); RabbitKernel("ServerRabbitState", 5, Reader(state));
        Check(EntityRabbits.RabbitStatesStale == stale + 2, "departing stream and unrelated owner are stale");
        SetServer("RabbitHandoverEpoch", 0u); SetServer("PrevOwnerAt", Time.realtimeSinceStartup - 10); RabbitKernel("ServerRabbitState", 2, Reader(state));
        SetServer("Epoch", 8u); RabbitKernel("ServerRabbitState", 1, Reader(state));
        Check(EntityRabbits.RabbitStatesStale == stale + 4, "expired handover and old epoch remain stale");
        count = Entities.Packets.Count; RabbitKernel("SendRabbitSnapshot", 4, se); Check(Entities.Packets.Count == count, "replay rejects cached old epoch");
        SetServer("Epoch", 7u); RabbitKernel("SendRabbitSnapshot", 4, se); Check(Entities.Packets.Last().Peer == 4 && Entities.Packets.Last().Bytes.SequenceEqual(state), "reentry replays exact durable rabbit bytes");
        Entities.RabbitFar.Clear();
    }

    static byte[] RabbitFxBytes(byte kind, int arg)
    {
        var w = new NetWriter(); w.U8(Entities.RabbitFx); w.U32(301); w.U8(kind); w.VarU32((uint)(arg + 1)); return w.Buf.Take(w.Len).ToArray();
    }
    static void RabbitFxChecks()
    {
        var (e, ny) = RabbitFixture(true);
        ny.Sroar = ny.Sattack = ny.Sidle = ny.Sdie = new[] { new AudioClip() };
        long applied = EntityRabbits.RabbitFxApplied;
        for (byte kind = 0; kind < 7; kind++) RabbitCall("ApplyRabbitFx", Reader(RabbitFxBytes(kind, 0)));
        Check(EntityRabbits.RabbitFxApplied == applied + 7 && !ny.senses.hasEyes && !ny.senses.hasEars && ny.alreadyEvil && ny.GIdleSound != null && ny.anim.Trigger == "Attack", "all rabbit FX apply with original clip and animation selection");
        Check((bool)RabbitMap("RabGreen")[ny] && !(bool)Field(RabbitOwner, "_rabReplay"), "EMP green state retained and replay flag restored");
        ny.alreadyEvil = false; nyulscript.DuringEvil = () => throw new Exception("fixture");
        try { RabbitCall("ApplyRabbitFx", Reader(RabbitFxBytes(6, 1))); Check(false, "FX throws"); } catch (TargetInvocationException) { }
        nyulscript.DuringEvil = null;
        Check(!(bool)Field(RabbitOwner, "_rabReplay"), "FX exception clears replay guard");
        e.Proxy = false; applied = EntityRabbits.RabbitFxApplied;
        RabbitCall("ApplyRabbitFx", Reader(RabbitFxBytes(0, 0))); Check(EntityRabbits.RabbitFxApplied == applied, "owner ignores echoed voice and animation");
        ny.senses.hasEyes = true; RabbitCall("ApplyRabbitFx", Reader(RabbitFxBytes(4, 0))); Check(!ny.senses.hasEyes && EntityRabbits.RabbitFxApplied == applied + 1, "copy-rolled eyes also apply on owner");
        var fx = RabbitFxBytes(1, 0); applied = EntityRabbits.RabbitFxApplied;
        for (int len = 1; len < fx.Length; len++) RabbitCall("ApplyRabbitFx", Reader(fx.Take(len).ToArray()));
        Check(EntityRabbits.RabbitFxApplied == applied, "truncated FX never applies");
        e.Root = null; RabbitCall("ApplyRabbitFx", Reader(fx)); Check(EntityRabbits.RabbitFxApplied == applied, "unresolved FX remains ignored"); e.Root = new tosaveitemscript { nyul = ny }; ny.ParentComponent = e.Root;

        var clip = new AudioClip(); ny.Sattack = new[] { new AudioClip(), clip }; int count = Entities.Packets.Count;
        RabbitPatch("RabAttack", "Prefix", ny); RabbitPatch("RabClipCapture", "Postfix", clip); RabbitPatch("RabAttack", "Postfix", ny);
        Check(Entities.Packets.Count == count + 1 && Entities.Packets.Last().Bytes.SequenceEqual(RabbitFxBytes(1, 1)), "voice capture preserves the game's chosen clip index");
        RabbitPatch("RabAttack", "Finalizer", new object[] { null }); Check(Field(RabbitOwner, "_voiceOf") == null, "voice finalizer clears captured rabbit");
        e.Proxy = true; ny.ParentComponent = e.Root;
        Check(!(bool)RabbitPatch("RabRoar", "Prefix", ny) && !(bool)RabbitPatch("RabAttack", "Prefix", ny), "copy cannot start local roar or attack");
        e.Proxy = false; Set(RabbitOwner, "_rabReplay", true); count = Entities.Packets.Count;
        RabbitCall("Voice", ny, (byte)1); RabbitCall("SenseLost", new sensoryscript { ParentComponent = ny }, (byte)4);
        Check(Entities.Packets.Count == count && Field(RabbitOwner, "_voiceOf") == null, "replayed events never start a second send"); Set(RabbitOwner, "_rabReplay", false);
        long sizes = EntityRabbits.RabbitSizesRolled; EntityRabbits.RabbitNextSize = .95f; ny.started = false;
        EntityRabbits.RabbitBeforeShare(e.Root); EntityRabbits.RabbitBeforeShare(e.Root);
        Check(ny.size == .95f && EntityRabbits.RabbitNextSize == -1 && EntityRabbits.RabbitSizesRolled == sizes + 1, "size rolls once before sharing and consumes override");
        long stored = EntityRabbits.RabStoredSkips; var a = new aiscript();
        Check((bool)RabbitCall("RabBodyGone", a) && EntityRabbits.RabStoredSkips == stored + 1, "missing stored body skips AI");
        a.ChildComponent = new Rigidbody(); a.ragdoll = new ragdollactivatescript { RBs = new Rigidbody[] { null } };
        Check((bool)RabbitCall("RabBodyGone", a) && EntityRabbits.RabStoredSkips == stored + 2, "missing limb body skips AI");
        a.ragdoll.RBs = Array.Empty<Rigidbody>(); a.move = new movecreaturescript();
        Check(!(bool)RabbitCall("RabBodyGone", a) && a.ragdoll.RB != null && a.move.RB != null, "stored AI body caches heal when body exists");
        var charge = new Entities.RabCharge { At = new Vector3d(10, 0, 0), Speed = 8, Until = Time.time + 1, OwnSpeed = 2 };
        EntityRabbits.SetRabbitCharge(ny.move, charge); RabbitCall("Charge", ny.move);
        Check(ny.move.speed == 8 && ny.move.moveDir.x == 1, "charge stage uses global target and requested speed");
        Time.time += 2; RabbitCall("Charge", ny.move); Check(ny.move.speed == 2 && !EntityRabbits.ObservedRabCharges.ContainsKey(ny.move), "expired charge restores original speed");
        Time.time = 10; ny.idleSound = true; ny.lastIdle = 0; ny.currentIdle = 1; mainscript.s.player.transform.position = new Vector3(100, 0, 0);
        RabbitCall("RemoteIdle", ny); Check(ny.lastIdle == 10 && ny.GIdleSound != null, "remote player in idle range stages the same local idle sound");
    }

    static void RabbitBiteChecks()
    {
        var (e, ny) = RabbitFixture(); var br = new breakablescript { ParentComponent = e.Root, decreaseAfterAttack = true }; e.Root.transform.Children.Add(br);
        Check(!EntityRabbits.RabbitBreakOnCopy(br, 1), "ordinary force is not treated as bite");
        var trigger = new playerdeathtriggerscript { ParentComponent = ny }; RabbitPatch("RabTriggerStay", "Prefix", trigger);
        br.decreaseAfterAttack = false; long skipped = EntityRabbits.RabbitBreakHitsSkipped;
        Check(EntityRabbits.RabbitBreakOnCopy(br, 2) && EntityRabbits.RabbitBreakHitsSkipped == skipped + 1 && RabbitMap("_rabHits").Count == 0, "nondecreasing weak bite is skipped before aggregation");
        br.decreaseAfterAttack = true; EntityRabbits.RabbitBreakOnCopy(br, 2); EntityRabbits.RabbitBreakOnCopy(br, 5); RabbitCall("RabbitBreakFlush", 10f); RabbitVector("bite");
        int count = Entities.Packets.Count; EntityRabbits.RabbitBreakOnCopy(br, 7); RabbitCall("RabbitBreakFlush", 10.05f);
        Check(Entities.Packets.Count == count && RabbitMap("_rabHits").Count == 1, "bite force waits for tenth-second flush");
        RabbitCall("RabbitBreakFlush", 10.11f); Check(Entities.Packets.Count == count + 1 && RabbitMap("_rabHits").Count == 0, "due bite flush clears aggregate");
        RabbitPatch("RabTriggerStay", "Finalizer", new object[] { null }); Check(!EntityRabbits.RabbitBreakOnCopy(br, 2), "trigger finalizer ends bite scope");
        ny.attackTriggerScript = new playerdeathtriggerscript { selfCol = new Collider(), damagePlayer = 10 };
        RabbitCall("RabbitRemoteBite", e, ny); RabbitCall("RabbitRemoteBite", e, ny); RabbitCall("RabbitDamageFlush", 11f);
        var packet = Entities.Packets.Last().Bytes; Check(packet[0] == PlayerCombat.PlayerDamage && RabbitMap("_rabDmg").Count == 0 && RabbitMap("_rabDmgDir").Count == 0 && RabbitMap("_rabDmgAt").Count == 0 && RabbitMap("_rabDmgNet").Count == 0, "remote bite flush uses combat writer and clears all victim maps");
        count = Entities.Packets.Count; RabbitCall("RabbitRemoteBite", e, ny); RabbitCall("RabbitDamageFlush", 11.05f); Check(Entities.Packets.Count == count, "damage accumulates until next flush");
        RabbitCall("RabbitDamageFlush", 11.11f); Check(Entities.Packets.Count == count + 1, "damage flush sends pending victim sum");
        ny.attackTriggerScript.selfCol.enabled = false; RabbitCall("RabbitRemoteBite", e, ny); Check(RabbitMap("_rabDmg").Count == 0, "disabled attack collider sends no damage");
        // The kernel captures the outer FX before synchronous host handling reuses the feature writer.
        Entities.IsHost = true; byte[] outer = RabbitFxBytes(1, 129);
        Entities.HostReceive = (kind, reader) => { Entities.HostReceive = null; RabbitCall("SendRabbitFx", ny, (byte)2, 0); };
        count = Entities.Packets.Count; RabbitCall("SendRabbitFx", ny, (byte)1, 129);
        Check(Entities.Packets.Count == count + 2 && Entities.Packets[count].Bytes.SequenceEqual(outer) && Entities.Packets[count + 1].Bytes.SequenceEqual(RabbitFxBytes(2, 0)), "host request survives nested reuse of rabbit writer");
        Entities.IsHost = false;
    }

    static void RabbitOwnerChecks()
    {
        RabbitFixture(); var server = (IDictionary)Field(typeof(Entities), "Server"); server.Clear();
        Entities.TestServer(301, 1); var se = server[301u]; var type = se.GetType();
        void Put(string n, object v) => type.GetField(n, Static | BindingFlags.Instance).SetValue(se, v);
        int Owner() => (int)type.GetField("OwnerId").GetValue(se);
        Put("Rabbit", true); Put("Epoch", 7u); Put("Car", false);
        mainscript.s.player.transform.position = new Vector3(40, 0, 0);
        Mp.Server.Peers.Clear(); Mp.Server.Peers.Add(new KeyValuePair<int, Vector3d>(1, new Vector3d(30, 0, 0))); Mp.Server.Peers.Add(new KeyValuePair<int, Vector3d>(2, new Vector3d(5, 0, 0)));
        EntityRabbits.ServerRabbitTick(.24f); Check(Owner() == 1, "owner choice preserves quarter-second cadence");
        EntityRabbits.ServerRabbitTick(.01f); Check(Owner() == 1, "unknown peer cannot acquire rabbit");
        Entities.Known[2] = new HashSet<uint> { 301 }; long moved = EntityRabbits.RabbitOwnerMoves; int cascades = Entities.Cascades;
        EntityRabbits.ServerRabbitTick(.25f);
        Check(Owner() == 2 && EntityRabbits.RabbitOwnerMoves == moved + 1 && Entities.Cascades == cascades + 1 && Entities.Packets.Last().Bytes.SequenceEqual(Convert.FromHexString("192D0100000208000000")), "kernel chooses known nearer owner then broadcasts incremented epoch and cascades");
        Put("OwnerId", 1); Mp.Server.Peers[0] = new KeyValuePair<int, Vector3d>(1, new Vector3d(9, 0, 0));
        EntityRabbits.ServerRabbitTick(.25f); Check(Owner() == 1, "switch hysteresis retains current owner");
        Mp.Server.Peers[0] = new KeyValuePair<int, Vector3d>(1, new Vector3d(30, 0, 0));
        foreach (var gate in new[] { ("Held", (object)true, (object)false), ("Stored", (object)true, (object)false), ("LockFrame", (object)1u, (object)0u), ("PartIndex", (object)0, (object)(-1)), ("CrashReturnTo", (object)0, (object)(-1)), ("RabbitHoldUntil", (object)11f, (object)0f) })
        {
            Put(gate.Item1, gate.Item2); EntityRabbits.ServerRabbitTick(.25f); Check(Owner() == 1, "nearest owner respects " + gate.Item1); Put(gate.Item1, gate.Item3);
        }
        EntityRabbits.RabbitNearM = 3; EntityRabbits.ServerRabbitTick(.25f); Check(Owner() == 1, "nearest player outside range does not take rabbit"); EntityRabbits.RabbitNearM = 60;
        Mp.Server.Peers[1] = new KeyValuePair<int, Vector3d>(2, new Vector3d(100, 0, 0));
        Entities.TestServer(302, 2); var car = server[302u]; car.GetType().GetField("HasState").SetValue(car, true); car.GetType().GetField("Speed").SetValue(car, 2f); car.GetType().GetField("Where").SetValue(car, new Vector3d(1, 0, 0));
        EntityRabbits.ServerRabbitTick(.25f); Check(Owner() == 2, "moving car anchors the simulator near rabbit");
        Put("OwnerId", 1); car.GetType().GetField("Speed").SetValue(car, 0f); EntityRabbits.ServerRabbitTick(.25f); Check(Owner() == 1, "parked undriven car is not an anchor");
        car.GetType().GetField("Driven").SetValue(car, true); car.GetType().GetField("DrivenAt").SetValue(car, Time.realtimeSinceStartup); EntityRabbits.ServerRabbitTick(.25f); Check(Owner() == 2, "fresh driven car remains an anchor below speed threshold");
        Put("RabbitHoldUntil", 0f); Entities.RabHold(301); Check((float)type.GetField("RabbitHoldUntil").GetValue(se) == Time.realtimeSinceStartup + EntityRabbits.RabbitHoldS, "host provisional rabbit claim applies original hold");
        var record = new RecordCodec.RabbitRecord(); record.items.Add(new RecordCodec.RabbitItem { id = 42, prefabID = 103, transform = new RecordCodec.RabbitTransform { pos = new Vector3d(1, 2, 3) } }); record.nyul.Add(new RecordCodec.RabbitItem { id = 42 }); RecordCodec.Record = record;
        Put("Rabbit", false); RabbitKernel("ShareFacts", Array.Empty<byte>(), se); Check((bool)type.GetField("Rabbit").GetValue(se) && ((Vector3d)type.GetField("Where").GetValue(se)).x == 1, "share facts preserve record position and root rabbit kind"); RecordCodec.Record = null;
        server.Clear(); Mp.Server.Peers.Clear();
    }

    static void RabbitNearChecks()
    {
        var (e, ny) = RabbitFixture(true); e.OwnerAt = 0;
        RabbitList("RabEnts").Add(e); Entities._localCars.Clear();
        var car = new carscript { RB = new Rigidbody() }; Entities._localCars.Add(car);
        long byCar = EntityRabbits.RabbitClaimedByCar;
        e.Held = true; EntityRabbits.RabbitNearTick(.02f); Check(e.Proxy, "held copy cannot be claimed by approaching car"); e.Held = false;
        EntityRabbits.RabbitNearTick(.02f); Check(!e.Proxy && e.OwnerId == Entities.MyId && EntityRabbits.RabbitClaimedByCar == byCar + 1 && Entities.Packets.Last().Bytes[0] == Entities.Claim, "near car takes rabbit through kernel claim transaction");
        e.Proxy = true; e.OwnerAt = Time.realtimeSinceStartup; EntityRabbits.RabbitNearTick(.02f); Check(e.Proxy, "claim cooldown suppresses repeat provisional takeover");
        e.OwnerAt = 0; ny.move.RB.position = new Vector3(20, 0, 0); EntityRabbits.RabbitNearTick(.02f); Check(e.Proxy, "far car cannot claim rabbit");
        car.RB.PointVelocity = new Vector3(300, 0, 0); EntityRabbits.RabbitNearTick(.02f); Check(!e.Proxy, "closing speed extends claim horizon by configured steps");
        ny.move.RB.position = Vector3.zero; Entities._localCars.Clear();
        var root = new tosaveitemscript { P = new pickupable(), ChildComponent = new Rigidbody { detectCollisions = true } };
        var obj = new Entities.Ent { NetId = 302, Root = root, Proxy = true, Items = { root } }; Entities.ByNet[302] = obj;
        Entities.TestCargoClaim = true; long objects = EntityRabbits.RabbitClaimedObjects; EntityRabbits.RabbitNearTick(.02f);
        Check(EntityRabbits.RabbitClaimedObjects == objects + 1, "own rabbit requests nearby loose object through cargo claim");
        obj.Driven = true; root.car = new carscript(); EntityRabbits.RabbitNearTick(.02f); Check(EntityRabbits.RabbitClaimedObjects == objects + 1, "rabbit cannot take driven car");
        obj.Driven = false; obj.OwnerAt = 0; EntityRabbits.RabbitNearTick(.02f); Check(!obj.Proxy && EntityRabbits.RabbitClaimedObjects == objects + 2, "rabbit takes parked car with same provisional claim");
        // Limb contact is the fallback for a dead rabbit whose root is not the touched body.
        e.Proxy = true; e.OwnerAt = 0; root.car = car; obj.Proxy = false;
        var limb = new Rigidbody(); ((IDictionary)Field(typeof(EntityRagdolls), "RagdollLimbOwner"))[limb.gameObject] = e;
        byCar = EntityRabbits.RabbitClaimedByCar;
        EntityRabbits.RabbitContact(302, new Collision { rigidbody = limb });
        Check(!e.Proxy && EntityRabbits.RabbitClaimedByCar == byCar + 1, "car contact takes dead rabbit through limb owner fallback");
        obj.Proxy = true; root.car = null; e.Proxy = false; objects = EntityRabbits.RabbitClaimedObjects;
        var body = (Rigidbody)root.ChildComponent; body.transform.SetParent(root.transform);
        EntityRabbits.RabbitContact(301, new Collision { rigidbody = body }); Check(EntityRabbits.RabbitClaimedObjects == objects + 1, "rabbit contact claims loose copy as fallback");
        obj.Stored = true; EntityRabbits.RabbitContact(301, new Collision { rigidbody = body }); Check(EntityRabbits.RabbitClaimedObjects == objects + 1, "stored contact copy remains protected"); obj.Stored = false;
        var col = new Collider { bounds = new Bounds { center = new Vector3(2, 0, 0), extents = Vector3.one } }; e.Root.Children.Add(col);
        Time.frameCount = 1; RabbitMap("_rabRadius").Clear(); float radius = (float)RabbitCall("RabRadius", e); col.bounds = new Bounds { extents = Vector3.one * 4 };
        Check((float)RabbitCall("RabRadius", e) == radius, "hit radius cache persists between refresh frames"); Time.frameCount = 300;
        Check((float)RabbitCall("RabRadius", e) > radius, "hit radius refreshes at original frame cadence");
        long moves = EntityRabbits.RabbitOwnerMoves; Set(RabbitOwner, "_rabScanAt", 55f); Set(RabbitOwner, "_rabReplay", true); Set(RabbitOwner, "_rabTrigger", true); Set(RabbitOwner, "_voiceOf", ny);
        EntityRabbits.SetRabbitCharge(ny.move, new Entities.RabCharge()); RabbitCall("RabbitReset");
        foreach (string map in new[] { "RabViews", "RabLastSent", "RabGreen", "RabSeen", "_rabDmg", "_rabDmgDir", "_rabDmgAt", "_rabDmgNet", "_rabHits", "_rabTriggerOf", "_rabRadius", "RabCharges" }) Check(RabbitMap(map).Count == 0, "rabbit reset clears " + map);
        Check(RabbitList("RabEnts").Count == 0 && RabbitList("_rabMine").Count == 0, "reset clears original scratch entity lists");
        Check((float)Field(RabbitOwner, "_rabScanAt") == 55 && (bool)Field(RabbitOwner, "_rabReplay") && (bool)Field(RabbitOwner, "_rabTrigger") && Field(RabbitOwner, "_voiceOf") == ny && EntityRabbits.RabbitOwnerMoves == moves, "reset preserves original timer, guard, voice and counter omissions");
        Set(RabbitOwner, "_rabReplay", false); Set(RabbitOwner, "_rabTrigger", false); Set(RabbitOwner, "_voiceOf", null); Entities.TestCargoClaim = false; Time.frameCount = 0;
    }

    static void RabbitBoundaryChecks()
    {
        string[] allowed = { "Ent", "RabCharge", "ClaimReason", "RabbitOfItem", "IsProxy", "InSession", "ShareSpawn", "RabbitFx", "SendToServer", "TryGetEntity", "ResolveEntity", "CollectRabbits", "IsFinite", "WasteFixes", "CreatureAudience", "StatesSkippedRabbitAnimation", "StatesSkippedDeadAnimation", "SkippedByInterest", "BodyR", "BreakRef", "BreakHit", "HasRabbitCars", "LocalRabbitCars", "RabbitCarBounds", "ClaimNow", "IsHost", "RabHold", "PlacedHandItems", "ClaimRabbitObject", "RabbitProxyOf", "KnownRabbits", "RabbitHandoverActive", "CaptureRabbitSnapshot", "ChooseRabbitOwner" };
        foreach (string part in new[] { "Rabbits", "RabbitRole", "RabbitState", "RabbitOwners" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs"); source = Regex.Replace(source, @"//[^\r\n]*", "");
            Check(!Regex.IsMatch(source, @"using\s+static\s+(?:TLDRevamp\.Net\.)?Entities"), part + " has no static kernel import");
            Check(!Regex.IsMatch(source, @"Entities\.(?:W|WS|ByNet|Server)\b"), part + " has no map or writer exposure");
            Check(Regex.Matches(source, @"Entities\.(\w+)").Cast<Match>().All(m => allowed.Contains(m.Groups[1].Value)), part + " uses only named kernel calls");
        }
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs"), physical = File.ReadAllText("src/TLDRevamp/Net/Entities.Physical.cs"), features = File.ReadAllText("src/TLDRevamp/Net/Entities.Features.cs");
        Check(kernel.IndexOf("ServerHandoffTick(dt);") < kernel.IndexOf("EntityRabbits.ServerRabbitTick(dt);") && kernel.IndexOf("EntityRabbits.ServerRabbitTick(dt);") < kernel.IndexOf("EntityWorldClock.SleepTick(dt);"), "server rabbit scheduling keeps its exact slot");
        Check(kernel.IndexOf("EntityAI.AiTick();") < kernel.IndexOf("EntityRabbits.RabbitTick();") && kernel.IndexOf("EntityRabbits.RabbitTick();") < kernel.IndexOf("EntityHands.WornTick();"), "local rabbit scheduling keeps its exact slot");
        Check(kernel.IndexOf("EntityAI.AiReset();") < kernel.IndexOf("EntityRabbits.RabbitReset();") && kernel.IndexOf("EntityRabbits.RabbitReset();") < kernel.IndexOf("PoiUsablesReset();"), "rabbit reset retains original session ordering");
        Check(physical.IndexOf("ItemNearTick(dt);") < physical.IndexOf("EntityRabbits.RabbitNearTick(dt);"), "rabbit claims stay after item claims in FixedTick");
        Check(features.Contains("W.Reset(); WriteClaim(W, e.NetId, reason); ToServer(W, true);") && features.Contains("WS.Reset(); WS.Bytes(r.Buf, 0, r.End);"), "claim and server state transactions retain shared kernel writers");
        Check(Field(typeof(Entities), "_rabbitLookupsReady") is true && TLDRevamp.GameLookups.FieldRefs.Contains("nyulscript.lastRoarTime") && TLDRevamp.GameLookups.FieldRefs.Contains("nyulscript.lastAttackTime") && TLDRevamp.GameLookups.FieldRefs.Contains("nyulscript.lastSeeTarget") && TLDRevamp.GameLookups.FieldRefs.Contains("nyulscript.alreadyEvil") && TLDRevamp.GameLookups.FieldRefs.Contains("nyulscript.GIdleSound"), "kernel warmup retains all five rabbit lookup caches");
        foreach (string name in new[] { "RabbitStatesSent", "RabbitStatesApplied", "RabbitStatesStale", "RabbitFxSent", "RabbitFxApplied", "RabbitDamageSent", "RabbitOwnerMoves", "RabbitClaimedByCar", "RabbitClaimedObjects", "RabbitBreakHitsSent", "RabbitBreakHitsSkipped", "RabbitSizesRolled", "RabStoredSkips" })
            Check(TLDRevamp.BridgeReflect.Get("TLDRevamp.Net.EntityRabbits." + name) == Field(RabbitOwner, name).ToString(), "bridge full counter path reads sole rabbit backing value: " + name);
        Check(TLDRevamp.BridgeReflect.Get("TLDRevamp.Net.EntityRabbits._rabHits.Count") == "0", "bridge preserves nested historical rabbit map paths");
        float hold = EntityRabbits.RabbitHoldS; TLDRevamp.BridgeReflect.Set("TLDRevamp.Net.EntityRabbits.RabbitHoldS", "6.25");
        Check(EntityRabbits.RabbitHoldS == 6.25f, "bridge setting and compiled owner call share one backing value"); EntityRabbits.RabbitHoldS = hold;
    }
}

