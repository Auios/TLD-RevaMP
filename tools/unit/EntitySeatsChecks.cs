using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    // Vectors captured from aa5fc86 before moving the five feature files.
    static Type SeatsOwner => typeof(EntitySeats);
    static object SeatCall(string name, params object[] args) => SeatsOwner.GetMethod(name, Static).Invoke(null, args);
    static object SeatHook(string name, string method, params object[] args) => SeatsOwner.GetNestedType(name, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);
    static void SeatVector(string name, string expected)
    {
        string actual = Convert.ToHexString(Entities.Packets.Last().Bytes);
        Check(actual == expected, name + " expected bytes");
        Check(Entities.Packets.Last().Reliable, name + " reliable");
    }
    static object SeatValue(object value, string name) => value.GetType().GetField(name).GetValue(value);
    static void SeatValue(object value, string name, object next) => value.GetType().GetField(name).SetValue(value, next);
    static IDictionary SeatMap(string name) => (IDictionary)Field(SeatsOwner, name);
    static object KernelSeatCall(string name, params object[] args) => typeof(Entities).GetMethod(name, Static).Invoke(null, args);
    static byte[] KeyWire(uint net, bool held, ushort ms = 0) => Bytes(w => { w.Write((byte)96); w.Write(net); w.Write((byte)(held ? 1 : 0)); w.Write(ms); });
    static byte[] ShotWire(uint net, sbyte steer = 0, byte hb = 0, ushort held = 0, ushort edges = 0) => Bytes(w => { w.Write((byte)39); w.Write(net); w.Write(steer); w.Write(hb); w.Write(held); w.Write(edges); });
    static void SeatsChecks()
    {
        Entities.ByNet.Clear(); Entities.ClearServer(); Entities.Packets.Clear(); Entities.IsHost = false; Entities.InSession = true;
        var car = new carscript { ignitionUsable = new usablescript(), startingTime = 1 };
        var root = new tosaveitemscript { car = car }; root.transform.ChildComponent = root; car.ParentComponent = root;
        var seat = new seatscript { Car = car, driverSeat1 = true, sitPos = new Transform() }; seat.transform.parent = root.transform;
        root.transform.Children.Add(seat); root.Children.Add(seat);
        var e = new Entities.Ent { NetId = 301, Root = root, Proxy = true, OwnerId = 2 }; e.Items.Add(root); Entities.ByNet[301] = e;
        var pl = new fpscontroller { Bsitting = true, seat = seat }; mainscript.s = new mainscript { player = pl };
        Time.time = Time.realtimeSinceStartup = 10;
        SeatCall("SendKey", 301u, true, 0f); SeatVector("key press", "602D010000010000");
        SeatCall("SendKey", 301u, false, .375f); SeatVector("key release", "602D010000007701");
        SeatCall("SendKey", 301u, false, 100f); SeatVector("key clamped hold", "602D01000000FFFF");
        Entities.TestServer(301, 2);
        SeatCall("ServerKey", 1, Reader(Entities.Packets[^2].Bytes)); SeatVector("key relay", "602D010000007701");
        Check(Entities.Packets.Last().Peer == 2, "key goes to current owner");
        car.isPlayerDriving2 = true; car.isteer = -.5f; car.ihandbrake = .25f; car.ihorn = true; car.iignition = true;
        car.iLightsBDown = car.ihandbrakeBDown = car.iignitionDown = car.ig3Down = car.iupshiftDown = true;
        SeatHook("ShotgunCapture", "Postfix", car); SeatCall("ShotgunTick", .025f);
        int sends = Entities.Packets.Count; SeatCall("ShotgunTick", .025f); SeatVector("shotgun active", "272D010000C04003008708");
        Check(Entities.Packets.Count == sends + 1 && !car.ihorn && !car.iLightsBDown, "20 Hz accumulator and copy buttons suppressed");
        var active = Entities.Packets.Last().Bytes;
        SeatCall("ServerShotgun", 1, Reader(active)); SeatVector("shotgun relay", "272D010000C04003008708");
        pl.Bsitting = false; SeatCall("ShotgunTick", 0f); SeatVector("shotgun release", "272D010000000000000000");
        SeatCall("ShotgunReset");
        DriverGuardChecks(e, pl, seat);
        KeyGuardChecks(e, pl, seat);
        ShotgunGuardChecks(e, pl, seat);
        SeatFrameChecks(e, pl, seat);
        SeatBoundaryChecks();
        SeatCall("ShotgunReset"); SeatMap("_keyHolds").Clear(); SeatMap("_keySeen").Clear(); Set(SeatsOwner, "_keyCar", null);
        Entities.ByNet.Clear(); Entities.ClearServer(); mainscript.s.player = null;
    }

    static void DriverGuardChecks(Entities.Ent e, fpscontroller pl, seatscript seat)
    {
        seat.driverSeat0 = true; pl.Bsitting = true; Entities.MyId = 1;
        Set(typeof(Entities), "_claimedFor", 0u);
        KernelSeatCall("CheckDriverSeat"); SeatVector("driver claim", "182D01000001");
        Check(Entities.DriverClaimIs(301) && Entities.ClaimsSent == 1, "one driver claim per sit");
        int sent = Entities.Packets.Count; KernelSeatCall("CheckDriverSeat"); Check(Entities.Packets.Count == sent, "waiting driver claim does not repeat at seat check");
        Time.unscaledTime = 20; KernelSeatCall("DriveClaimTick"); SeatVector("driver retry", "182D01000002");
        sent = Entities.Packets.Count; Time.unscaledTime = 21; KernelSeatCall("DriveClaimTick"); Check(Entities.Packets.Count == sent, "retry waits 1.5 seconds");
        seat.driverSeat0 = false; Time.unscaledTime = 22; KernelSeatCall("DriveClaimTick"); KernelSeatCall("CheckDriverSeat");
        Check(Entities.Packets.Count == sent && !Entities.DriverClaimIs(301), "shotgun passenger never claims and clears per-sit claim");
        seat.driverSeat0 = true;
        Check((int)SeatCall("DriverSeatTakenBy", 301u, 1) == 0, "host local driver checked first");
        DedicatedServer.Enabled = true; Check((int)SeatCall("DriverSeatTakenBy", 301u, 1) == -1, "dedicated host ignores local player"); DedicatedServer.Enabled = false;
        pl.Bsitting = false; Mp.Server.Seats.Add(new System.Collections.Generic.KeyValuePair<int, int>(2, 0));
        Check((int)SeatCall("DriverSeatTakenBy", 301u, 1) == 2 && (int)SeatCall("DriverSeatTakenBy", 301u, 2) == -1, "remote driver binding excludes claimer");
        var se = Entities.TestSeatServer(301);
        string Refusal(int from) => (string)KernelSeatCall("DriverSeatClaimRefusal", se, from);
        Check(Refusal(1).Contains("sits at the wheel"), "remote driver blocks takeover");
        Mp.Server.Seats.Clear(); Time.realtimeSinceStartup = 30; Entities.TestSeatClaim(301, 2, 29);
        Check(Refusal(1).Contains("just took the wheel") && Refusal(2) == null, "claim hold protects first driver but permits same claimer");
        Time.realtimeSinceStartup = 30.5f; Check(Refusal(1) == null, "hold expires at exact boundary");
        Entities.TestSeatClaim(301, 7, 30); Check(Refusal(1) == null, "hold requires claimed player still owning car");
        SeatValue(se, "PartIndex", 0); Entities.TestSeatClaim(301, 2, 30); Check(Refusal(1) == null, "car parts do not inherit driver hold"); SeatValue(se, "PartIndex", -1);

        byte[] claim = Bytes(w => { w.Write((byte)24); w.Write(301u); w.Write((byte)1); });
        long refused = EntitySeats.DriverSeatRefused;
        SeatValue(se, "CrashReturnTo", 3); KernelSeatCall("ServerClaim", 1, Reader(claim));
        Check(Entities.RefusedCrash == 1 && EntitySeats.DriverSeatRefused == refused && (int)SeatValue(se, "OwnerId") == 2, "crash lease refusal precedes seat hold");
        SeatValue(se, "CrashReturnTo", -1); SeatValue(se, "HitchCar", 10u); KernelSeatCall("ServerClaim", 1, Reader(claim)); SeatValue(se, "HitchCar", 0u);
        foreach (string flag in new[] { "Held", "Stored" })
        { SeatValue(se, flag, true); KernelSeatCall("ServerClaim", 1, Reader(claim)); SeatValue(se, flag, false); }
SeatValue(se, "WornBy", 2); KernelSeatCall("ServerClaim", 1, Reader(claim)); SeatValue(se, "WornBy", -1);
        SeatValue(se, "Driven", true); SeatValue(se, "DrivenAt", Time.realtimeSinceStartup); KernelSeatCall("ServerClaim", 1, Reader(claim)); SeatValue(se, "Driven", false);
        Check(EntitySeats.DriverSeatRefused == refused && Entities.RefusedHeld == 1 && Entities.RefusedStored == 1 && EntityHands.WornRefused == 1, "authority guards retain their own counters before seat arbitration");
        KernelSeatCall("ServerClaim", 1, Reader(claim)); SeatVector("driver refusal owner reply", "192D0100000200000000");
        Check(EntitySeats.DriverSeatRefused == refused + 1 && Entities.Packets[^2].Peer == 1 && Entities.Packets.Last().Peer == 2 && (int)SeatValue(se, "OwnerId") == 2, "hold refusal replies current owner to claimer: " + EntitySeats.DriverSeatRefused + "/" + refused + " peer=" + Entities.Packets.Last().Peer + " owner=" + SeatValue(se, "OwnerId"));
        Time.realtimeSinceStartup = 33; KernelSeatCall("ServerClaim", 1, Reader(claim)); SeatVector("driver grant owner broadcast", "192D0100000101000000");
        Check((int)SeatValue(se, "OwnerId") == 1 && (uint)SeatValue(se, "Epoch") == 1 && (uint)SeatValue(se, "ClaimedFromEpoch") == 0 && Entities.Cascades == 1, "grant records prior epoch then changes owner and epoch before cascade");

        var spare = new seatscript { sitPos = new Transform() }; spare.transform.parent = e.Root.transform; e.Root.transform.Children.Add(spare);
        SeatMap("_seats").Clear(); pl.Bsitting = true; pl.seat = seat; e.Driven = true; e.OwnerId = 2; Set(typeof(Entities), "_claimedFor", 301u);
        SeatCall("DriverSeatConflictTick", pl, e);
        Check(pl.seat == spare && EntitySeats.DriverSeatMoved == 1 && !Entities.DriverClaimIs(301), "answered contested claim moves to ordinary passenger seat");
        spare.Free = false; pl.seat = seat; pl.Bsitting = true; Set(typeof(Entities), "_claimedFor", 301u); SeatCall("DriverSeatConflictTick", pl, e);
        Check(!pl.Bsitting && EntitySeats.DriverSeatOut == 1, "no ordinary passenger seat forces exit");
        pl.seat = seat; pl.Bsitting = true; e.Driven = false; spare.Free = true;
        RemotePlayers.Players.Add((2, "other", 0)); RemotePlayers.SeatNet = 301; RemotePlayers.SeatIdx = 0; Set(typeof(Entities), "_claimedFor", 301u);
        SeatCall("DriverSeatConflictTick", pl, e); Check(pl.seat == spare && EntitySeats.DriverSeatMoved == 2, "same remote seat binding detects conflict without driven state"); RemotePlayers.Players.Clear();
        e.Driven = false; seat.driverSeat0 = false; pl.seat = seat; Entities.MyId = 0; Entities.TestServer(301, 2);
    }

    static void KeyGuardChecks(Entities.Ent e, fpscontroller pl, seatscript seat)
    {
        var car = e.Root.car; var holds = SeatMap("_keyHolds"); var seen = SeatMap("_keySeen");
        holds.Clear(); seen.Clear(); Set(SeatsOwner, "_keyCar", null); pl.seat = seat; pl.Bsitting = true;
        long refused = EntitySeats.KeyHoldsRefused; int sent = Entities.Packets.Count;
        SeatCall("SendKey", 0u, true, 0f); Check(Entities.Packets.Count == sent, "net zero key emits nothing");
        foreach (byte[] wire in new[] { new byte[] { 96, 1 }, KeyWire(999, true) }) SeatCall("ServerKey", 1, Reader(wire));
        SeatCall("ServerKey", 2, Reader(KeyWire(301, true)));
        var se = Entities.TestSeatServer(301); SeatValue(se, "Car", false); SeatCall("ServerKey", 1, Reader(KeyWire(301, true))); SeatValue(se, "Car", true);
        Entities.SeatToken = false; SeatCall("ServerKey", 1, Reader(KeyWire(301, true))); Entities.SeatToken = true;
        Entities.SeatNear = false; SeatCall("ServerKey", 1, Reader(KeyWire(301, true)));
        Check(EntitySeats.KeyHoldsRefused == refused + 6 && Entities.EditsOutOfReach == 1 && Entities.Packets.Count == sent, "key malformed owner type token and reach guards refuse without sending");
        Entities.SeatToken = false; SeatCall("ServerKey", 0, Reader(KeyWire(301, true)));
        Check(Entities.Packets.Count == sent + 1 && Entities.Packets.Last().Peer == 2, "host key bypasses peer token and reach checks"); Entities.SeatToken = Entities.SeatNear = true;
        SeatCall("ApplyKey", Reader(KeyWire(301, true))); Check(holds.Count == 0, "key echo on copy cannot simulate");
        e.Proxy = false; Time.realtimeSinceStartup = 40;
        SeatCall("ApplyKey", Reader(KeyWire(301, false, 375))); Check(holds.Count == 0, "orphan release cannot invent a press");
        SeatCall("ApplyKey", Reader(KeyWire(301, true))); var hold = holds[car];
        Time.realtimeSinceStartup = 40.5f; SeatCall("ApplyKey", Reader(KeyWire(301, true)));
        Check((float)SeatValue(hold, "PressAt") == 40 && !(bool)SeatValue(hold, "Active") && (float)SeatValue(hold, "Until") == 42, "resend extends timeout without restarting snapshot");
        SeatCall("ApplyKey", Reader(KeyWire(301, false, 375)));
        Check((bool)SeatValue(hold, "Active") && (float)SeatValue(hold, "Until") == 40.875f, "short release uses held duration rather than trip time");
        Time.time = 80; SeatCall("KeyTick"); Time.realtimeSinceStartup = 41; SeatCall("KeyTick");
        Check(holds.Count == 1 && (int)SeatValue(hold, "Frames") == 2 && car.ignitionUsable.lastHoldTime == 80, "tap remains for at least two owner frames");
        SeatCall("KeyTick"); Check(holds.Count == 0 && ((IList)Field(SeatsOwner, "_keyDone")).Count == 1, "expired key removed after snapshot traversal");
        Time.realtimeSinceStartup = 50; SeatCall("ApplyKey", Reader(KeyWire(301, true))); hold = holds[car];
        Time.realtimeSinceStartup = 51.1f; SeatCall("KeyTick"); Check(!(bool)SeatValue(hold, "Active"), "start waits past tap margin");
        Time.realtimeSinceStartup = 51.2f; SeatCall("KeyTick"); Check((bool)SeatValue(hold, "Active"), "long hold starts after game toggle time plus margin");
        SeatCall("ApplyKey", Reader(KeyWire(301, false, 1200))); Check((float)SeatValue(hold, "Until") == 51.2f, "active crank release ends no later than now");
        holds.Clear(); car.startingTime = 10; Time.realtimeSinceStartup = 60; SeatCall("ApplyKey", Reader(KeyWire(301, true)));
        Time.realtimeSinceStartup = 61.6f; SeatCall("KeyTick"); Check(holds.Count == 0, "unreleased inactive key times out before a very long startingTime"); car.startingTime = 1;
        Time.realtimeSinceStartup = 70; SeatCall("ApplyKey", Reader(KeyWire(301, true))); e.Proxy = true; SeatCall("KeyTick"); Check(holds.Count == 0, "ownership loss removes remote key playback");
        car.ignitionUsable.Held = true; Time.time = 100; Time.realtimeSinceStartup = 100;
        Check(!(bool)SeatHook("KeyOnCopy", "Prefix", car), "copy key snapshot suppresses vanilla ignition");
        sent = Entities.Packets.Count; SeatCall("KeyTick"); Check(Entities.Packets.Count == sent + 1 && Entities.Packets.Last().Bytes.SequenceEqual(KeyWire(301, true)), "copy starts key press from observed frame");
        Time.time = 100.05f; Time.realtimeSinceStartup = 100.49f; SeatHook("KeyOnCopy", "Prefix", car); SeatCall("KeyTick"); Check(Entities.Packets.Count == sent + 1, "held key waits resend interval");
        Time.time = 100.1f; Time.realtimeSinceStartup = 100.5f; SeatHook("KeyOnCopy", "Prefix", car); SeatCall("KeyTick"); Check(Entities.Packets.Count == sent + 2, "held key resends on interval");
        car.ignitionUsable.Held = false; Time.time = 100.3f; SeatCall("KeyTick"); Check(Entities.Packets.Last().Bytes.SequenceEqual(KeyWire(301, false, 100)) && seen.Count == 0, "copy release sends first-to-last observed duration and clears stale seen keys");
        e.Proxy = false; Check((bool)SeatHook("KeyOnCopy", "Prefix", car), "owner key keeps vanilla ignition"); e.Proxy = true;
        Entities.InSession = false; Check((bool)SeatHook("KeyOnCopy", "Prefix", car), "outside session key keeps vanilla ignition"); Entities.InSession = true;
        Entities.IsHost = true; sent = Entities.Packets.Count;
        Entities.HostReceive = (kind, reader) => SeatCall("ServerKey", 0, reader);
        Entities.PeerReceive = (peer, reader) => { Entities.PeerReceive = null; SeatCall("SendKey", 301u, false, .375f); };
        SeatCall("SendKey", 301u, true, 0f);
        Check(Entities.Packets.Skip(sent).Select(p => p.Peer).SequenceEqual(new[] { -2, 2, -2, 2 }) &&
            Entities.Packets[sent].Bytes.SequenceEqual(KeyWire(301, true)) && Entities.Packets[sent + 1].Bytes.SequenceEqual(KeyWire(301, true)) &&
            Entities.Packets[sent + 2].Bytes.SequenceEqual(KeyWire(301, false, 375)) && Entities.Packets[sent + 3].Bytes.SequenceEqual(KeyWire(301, false, 375)), "synchronous host key relay captures outer bytes before nested feature writer reuse");
        Entities.HostReceive = null; Entities.PeerReceive = null; Entities.IsHost = false;
    }

    static void ShotgunGuardChecks(Entities.Ent e, fpscontroller pl, seatscript seat)
    {
        var car = e.Root.car; var remote = SeatMap("RemoteShot"); remote.Clear();
        int sent = Entities.Packets.Count; SeatCall("ServerShotgun", 2, Reader(ShotWire(301))); HitCheck.Allow = false; SeatCall("ServerShotgun", 1, Reader(ShotWire(301))); HitCheck.Allow = true;
        SeatCall("ServerShotgun", 1, Reader(new byte[] { 39, 1 })); Check(Entities.Packets.Count == sent, "passenger self-owner reach and bad net guards do not relay");
        SeatCall("ApplyShotgun", Reader(ShotWire(301, 127, 128, 3, 7))); Check(remote.Count == 0, "passenger echo on copy ignored");
        e.Proxy = false; Time.realtimeSinceStartup = 110;
        SeatCall("ApplyShotgun", Reader(new byte[] { 39, 1 })); Check(remote.Count == 0, "truncated passenger payload ignored");
        SeatCall("ApplyShotgun", Reader(ShotWire(301, 64, 128, 3, 1))); SeatCall("ApplyShotgun", Reader(ShotWire(301, 64, 128, 3, 6)));
        var shot = remote[car]; Check((ushort)SeatValue(shot, "Edges") == 7, "passenger edges accumulate between owner frames");
        SeatHook("ShotgunInject", "Postfix", car);
        Check((ushort)SeatValue(shot, "Edges") == 0 && (ushort)SeatValue(shot, "FrameEdges") == 7 && car.ihorn && car.iignition && car.iLightsBDown && car.ihandbrakeBDown && car.iignitionDown, "reset input transfers edges once and injects held buttons");
        car.isteer = .75f; car.ihandbrake = .9f; SeatCall("MergeRemote", car, true);
        Check(car.isteer == 1 && car.ihandbrake == .9f, "owner local steer adds clamped passenger steer and max handbrake");
        settingsscript.s = new settingsscript(); car.driving2 = car.multiControlling = false; int lights = car.LightCalls, brakes = car.HandbrakeCalls;
        SeatHook("ShotgunNoLocalPlayer", "Prefix", car); Check(car.LightCalls == lights + 1 && car.HandbrakeCalls == brakes + 1 && car.steer == car.isteer * car.steerAngle, "remote passenger controls owner car without local driver");
        car.driving2 = true; SeatHook("ShotgunNoLocalPlayer", "Prefix", car); Check(car.LightCalls == lights + 1, "local driving2 keeps ordinary game control path"); car.driving2 = false;
        SeatHook("ShotgunInject", "Postfix", car); Check((ushort)SeatValue(shot, "FrameEdges") == 0, "edges are consumed on following reset frame");
        SeatValue(shot, "FrameEdges", (ushort)7); Time.realtimeSinceStartup = 110.31f; car.isteer = -.25f; SeatCall("MergeRemote", car, false);
        Check(car.isteer == -.25f && (ushort)SeatValue(shot, "FrameEdges") == 0, "stale passenger input cannot steer or replay edges");
        e.Proxy = true; pl.Bsitting = true; pl.seat = seat; car.isPlayerDriving2 = true; car.isPlayerDriving = false;
        car.isteer = 2; car.ihandbrake = 2; car.iLightsBDown = true; SeatHook("ShotgunCapture", "Postfix", car);
        SeatCall("ShotgunTick", .05f); Check(Entities.Packets.Last().Bytes[5] == 127 && Entities.Packets.Last().Bytes[6] == 255, "passenger quantization clamps inputs");
        car.iLightsBDown = true; SeatHook("ShotgunCapture", "Postfix", car); pl.Bsitting = false; SeatCall("ShotgunTick", 0f);
        Check(Entities.Packets.Last().Bytes[5] == 0 && Entities.Packets.Last().Bytes[6] == 0 && Entities.Packets.Last().Bytes[9] == 1, "release clears held controls but preserves unsent edges");
        Set(SeatsOwner, "_shotSendAcc", .02f); Set(SeatsOwner, "_shotHeld", (ushort)3); SeatCall("ShotgunReset");
        Check(remote.Count == 0 && Field(SeatsOwner, "_shotCar") == null && !(bool)Field(SeatsOwner, "_shotWasActive") && (ushort)Field(SeatsOwner, "_shotEdges") == 0 && (float)Field(SeatsOwner, "_shotSendAcc") == .02f && (ushort)Field(SeatsOwner, "_shotHeld") == 3, "reset clears only original shotgun fields and keeps accumulator and held lifetime");
    }

    static void SeatFrameChecks(Entities.Ent e, fpscontroller pl, seatscript seat)
    {
        pl.Bsitting = true; pl.seat = seat; e.Proxy = true; e.Root.car.isPlayerDriving = false;
        var pc = new CharacterController(); var cc = new Collider(); pl.ChildComponent = pc; e.Root.transform.Children.Add(cc);
        SeatHook("SeatEnterCollisionFix", "Postfix", pl, seat); Check(EntitySeats.SeatIgnoredPairs == 1 && Physics.Ignored.Last().Item3, "enter copy ignores car collider");
        long logged = EntitySeats.SeatLeavesLogged, calls = EntitySeats.LocalGetOutCalls;
        object[] args = { pl, Vector3.zero, false, null }; SeatHook("SeatExitCollisionRestore", "Prefix", args); SeatHook("SeatExitCollisionRestore", "Postfix", pl, false, args[3]);
        Check(EntitySeats.SeatLeavesLogged == logged && EntitySeats.LocalGetOutCalls == calls + 1 && EntitySeats.SeatIgnoredPairs == 1, "ordinary GetOut probe stays seated without leave or collision restore");
        pl.Bsitting = false; SeatHook("SeatExitCollisionRestore", "Postfix", pl, false, args[3]); Check(EntitySeats.SeatLeavesLogged == logged + 1 && EntitySeats.SeatIgnoredPairs == 0 && !Physics.Ignored.Last().Item3, "actual exit logs once and restores capsule pairs");
        RemotePlayers.Occupied = true; object[] free = { seat, true }; SeatHook("SeatFreeOverride", "Postfix", free); Check(!(bool)free[1], "remote seat binding overrides vanilla free result"); RemotePlayers.Occupied = false;
        pl.Bsitting = true;
        var body = new Rigidbody { isKinematic = true, interpolation = RigidbodyInterpolation.Interpolate, position = new Vector3(10, 0, 0) }; body.transform.position = new Vector3(8, 0, 0); seat.ParentComponent = body;
        Check(EntitySeats.SeatAimFrame(out var drawn, out var actual) && actual == body && EntitySeats.SeatAimPoint(drawn, body, new Vector3(9, 1, 0)) == new Vector3(11, 1, 0), "seat aim translates drawn ray into physics frame");
        var camera = new Component(); pl.Cam = camera; camera.transform.localPosition = new Vector3(1, 2, 3); camera.transform.localRotation = new Quaternion(0, 0, 0, 1);
        object[] aim = { pl, null }; SeatHook("SeatAimRays", "Pre", aim); camera.transform.localPosition = Vector3.zero; SeatHook("SeatAimRays", "After", pl, aim[1]); Check(camera.transform.localPosition == new Vector3(1, 2, 3) && EntitySeats.SeatAimFrames == 1, "aim finalizer restores captured camera local pose");
        body.isKinematic = false; Check(!EntitySeats.SeatAimFrame(out _, out _), "owned physics seat needs no aim frame"); body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None; Check(!EntitySeats.SeatAimFrame(out _, out _), "uninterpolated seat needs no aim frame");
        SeatMap("_seats").Clear();
        var rear = new partslotscript { name = "rear" }; var rearSeat = new seatscript { sitPos = new Transform() }; rearSeat.transform.parent = rear.transform; rear.transform.Children.Add(rearSeat); e.Root.partslotscripts = new System.Collections.Generic.List<partslotscript> { rear };
        Check(EntitySeats.VehicleSeats(e.Root).Contains(rearSeat), "rear slot body seats appended");
        var stool = new tosaveitemscript(); stool.transform.ChildComponent = stool;
        var mount = new Transform { ChildComponent = new mountStuff(), parent = e.Root.transform }; stool.transform.parent = mount;
        var itemSeat = new seatscript { sitPos = new Transform() }; itemSeat.transform.parent = stool.transform; stool.transform.Children.Add(itemSeat); e.Root.transform.Children.Add(itemSeat);
        var item = new Entities.Ent { NetId = 302, Root = stool }; Entities.ByNet[302] = item;
        SeatMap("_seats").Clear(); Check(!EntitySeats.VehicleSeats(e.Root).Contains(itemSeat), "bus excludes seats in mounted item");
        pl.seat = itemSeat; long itemSent = EntitySeats.ItemSeatsSent; EntitySeats.LocalSeat(out uint net, out int idx);
        Check(net == 302 && idx == 0 && EntitySeats.ItemSeatsSent == itemSent + 1 && EntitySeats.SeatScript(302, 0, out _) == itemSeat, "mounted stool sends its own nearest object and seat index");
        Transform cachedRoot = null, cachedSeat = null; Check(EntitySeats.SeatTransformCached(302, 0, ref cachedRoot, ref cachedSeat) == itemSeat.sitPos && EntitySeats.SeatTransform(302, -1) == null, "remote cached seat resolves item sit point and bounds");
        Check(EntitySeats.SeatWhy(999, 0).Contains("net absent") && EntitySeats.SeatWhy(302, 0).Contains("idx 0/"), "seatwhy retains existing diagnostic fields");
        pl.seat = seat; Entities.ByNet.Remove(302); e.Root.partslotscripts = null;
    }

    static void SeatBoundaryChecks()
    {
        foreach (string part in new[] { "Seat", "Seats", "SeatAim", "IgnitionKey", "Shotgun" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            Check(!Regex.IsMatch(source, @"using\s+static\s+(?:global::)?(?:TLDRevamp\.Net\.)?Entities\s*;|Entities\.(?:W|WS|ByNet|Server)\b|\bSEnt\b"), part + " keeps maps writers and authority records in kernel");
            Check(source.Contains("internal static partial class EntitySeats"), part + " has feature owner");
        }
        foreach (string name in new[] { "_keySeen", "_keyCar", "_keyNet", "_keySentAt", "_keyFirstAt", "_keyLastAt", "_keyHolds", "_keyDone", "_ignoredPairs", "_seatedIn", "_seats", "RemoteShot", "_shotCar", "_shotSteer", "_shotHandbrake", "_shotSendAcc", "_shotHeld", "_shotEdges", "_shotWasActive", "DriverSeatRefused", "DriverSeatMoved", "DriverSeatOut", "LocalGetOutCalls", "ItemSeatsSent", "ShotgunSent", "ShotgunApplied", "SeatAimFrames" })
            Check(typeof(Entities).GetField(name, Static) == null && SeatsOwner.GetField(name, Static) != null, name + " has one feature field owner");
        Check(!ReferenceEquals(Field(SeatsOwner, "W"), Field(typeof(Entities), "W")) && !ReferenceEquals(Field(SeatsOwner, "WS"), Field(typeof(Entities), "WS")) && !ReferenceEquals(Field(SeatsOwner, "W"), Field(SeatsOwner, "WS")), "seats owns separate client/key and raw relay writers");
        Check(BridgeReflect.Set("TLDRevamp.Net.EntitySeats.KeyResendS", "0.6") == "0.6" && EntitySeats.KeyResendS == .6f, "key owner call writes one owner"); EntitySeats.KeyResendS = .5f;
        Check(BridgeReflect.Get("TLDRevamp.Net.EntitySeats.RemoteShot.Count") == "0" && BridgeReflect.Get("TLDRevamp.Net.EntitySeats._keyHolds.Count") == "0", "full private cache owner paths resolve feature owner");
        Check(BridgeReflect.Call("TLDRevamp.Net.EntitySeats.SeatGuardStats", Array.Empty<string>()).Contains("refused"), "compiled seatguard owner call remains callable");
    }
}
