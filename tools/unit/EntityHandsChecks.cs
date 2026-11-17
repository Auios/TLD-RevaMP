using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    // Captured by compiling the unmodified three production files at HEAD 31f5a24.
    static Type HandsOwner => typeof(EntityHands);
    static object HandsCall(string name, params object[] args) => HandsOwner.GetMethod(name, Static).Invoke(null, args);
    static void HandsVector(string name, NetWriter w)
    {
        string expected = name switch {
            "worn-on" => "552D01000007000000030000803E0000003F000040BF03000000000000",
            "worn-off" => "552D01000007000000FF",
            "share-worn" => "030000803E0000003F000040BF03000000000000",
            "worn-relay" or "worn-replay" => "552D01000002030000803E0000003F000040BF03000000000000",
            "worn-takeback-owner" => "192D0100000208000000",
            "carried-hand-state" => "17AD02072A00D43000000A2800001450000042720000030000000000006400C8002C01949103000010400000A4410000EA4103000000000000FA00F40112FD03000000000000",
            _ => throw new Exception(name)
        };
        Check(Convert.ToHexString(w.Buf.Take(w.Len).ToArray()) == expected, name + " matches expected bytes");
    }
    static void HandsChecks()
    {
        Entities.ByNet.Clear(); Entities.ClearServer(); Entities.Packets.Clear();
        Entities.MyId = 2; Entities.IsHost = false;
        var head = new Transform { position = new Vector3(10, 20, 30) };
        var pl = new fpscontroller { anim = new HandAnim { HHTarget = head, RHTarget = head } };
        mainscript.s = new mainscript { player = pl };
        var root = new tosaveitemscript { name = "hat" };
        root.transform.position = new Vector3(10.25f, 20.5f, 29.25f);
        var p = new pickupable { wear = new wearable { wearing = true, tipus = 3 }, tosave = root };
        root.P = p; pl.weared[3] = p;
        var e = new Entities.Ent { NetId = 301, Root = root, Epoch = 7, OwnerId = 2 };
        e.Items.Add(root); Entities.ByNet[301] = e;
        HandsCall("SendWorn", e, 3);
        var w = new NetWriter(); w.Bytes(Entities.Packets.Last().Bytes, 0, Entities.Packets.Last().Bytes.Length); HandsVector("worn-on", w);
        HandsCall("SendWorn", e, -1); w.Reset(); w.Bytes(Entities.Packets.Last().Bytes, 0, Entities.Packets.Last().Bytes.Length); HandsVector("worn-off", w);
        w.Reset(); HandsCall("WriteShareWorn", w, root); HandsVector("share-worn", w);
        Entities.TestServer(301, 2); object se = Entities.TestSeatServer(301);
        SeatValue(se, "Epoch", 7u);
        var on = Entities.Packets[^2].Bytes;
        HandsCall("ServerWorn", 2, Reader(on)); w.Reset(); w.Bytes(Entities.Packets.Last().Bytes, 0, Entities.Packets.Last().Bytes.Length); HandsVector("worn-relay", w);
        typeof(Entities).GetMethod("SendWornTo", Static).Invoke(null, new[] { (object)9, se });
        w.Reset(); w.Bytes(Entities.Packets.Last().Bytes, 0, Entities.Packets.Last().Bytes.Length); HandsVector("worn-replay", w);
        SeatValue(se, "OwnerId", 1); SeatValue(se, "PrevOwnerId", 2); SeatValue(se, "PrevOwnerAt", 9f); SeatValue(se, "WornBy", -1); Time.realtimeSinceStartup = 10;
        HandsCall("ServerWorn", 2, Reader(on));
        w.Reset(); w.Bytes(Entities.Packets[^2].Bytes, 0, Entities.Packets[^2].Bytes.Length); HandsVector("worn-takeback-owner", w);
        // Capture the unchanged state head with both the car frame and hand tail supplied by the actual feature.
        var carRoot = new tosaveitemscript { car = new carscript() }; carRoot.transform.position = new Vector3(8, 0, 0);
        var seat = new seatscript(); seat.transform.SetParent(carRoot.transform, true); carRoot.transform.Children.Add(seat);
        Entities.ByNet[401] = new Entities.Ent { NetId = 401, Root = carRoot }; pl.Bsitting = true; pl.seat = seat; pl.inHandP = p;
        object[] cp = { e, root.transform, Quaternion.identity, null, null };
        uint car = (uint)HandsCall("CarriedBy", cp);
        object[] hp = { e, true, false, false, null, null }; bool hand = (bool)HandsCall("LocalHandPose", hp);
        w.Reset(); EntityCodec.WriteStateHead(w, 301, 7, 42, 12.5, new Vector3d(10.25, 20.5, 29.25), Quaternion.identity, new Vector3(1, 2, 3), false,
            held: true, carriedBy: car, localPos: (Vector3)cp[3], localRot: (Quaternion)cp[4], handPose: hand, handPos: (Vector3)hp[4], handRot: (Quaternion)hp[5]);
        HandsVector("carried-hand-state", w);
        var state = Reader(w.Buf.Take(w.Len).ToArray());
        EntityCodec.ReadStateHead(state, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
        Check(EntityCodec.ReadCarriedBy == 401 && EntityCodec.ReadHandPose && state.Pos == state.End, "car and hand extensions precede state tail without changing scratch lifetime");
        var writer = Field(HandsOwner, "W");
        Check(!ReferenceEquals(writer, Field(typeof(Entities), "W")), "hands owns its worn writer");
        bool nested = false;
        Entities.IsHost = true; Entities.HostReceive = (kind, r) => { if (kind == Entities.Worn) EntityHands.ServerWorn(2, r); };
        Entities.PeerDeliver = () => { Entities.PeerDeliver = null; nested = true; EntityHands.SendWorn(e, -1); };
        int first = Entities.Packets.Count;
        EntityHands.SendWorn(e, 3);
        Check(nested && Entities.Packets[first].Bytes.SequenceEqual(on) && Convert.ToHexString(Entities.Packets[first + 1].Bytes) == "552D01000002030000803E0000003F000040BF03000000000000", "host request and relay captured before nested worn writer reuse");
        Entities.HostReceive = null; Entities.PeerDeliver = null; Entities.IsHost = false;
        // Nested local sends intentionally leave SentWornSlot set by the outer call, as before.
        Check(e.SentWornSlot == 3, "synchronous nested worn send preserves outer post-send assignment");
        WornGuardChecks(e, pl, on);
        HandDrawChecks(e, pl);
        CarriedChecks(e, carRoot);
        HandsBoundaryChecks(e);
        HandsCall("WornReset"); Entities.Cascades = 0; Entities.ClaimsSent = 0; EntityHands.WornSync = true; Entities.ByNet.Clear(); Entities.ClearServer(); Entities.Packets.Clear(); mainscript.s = null;
    }

    static void HandsBoundaryChecks(Entities.Ent e)
    {
        e.SentHeld = true; e.FastSendUntil = 0; EntityHands.HeldStateHz = 50; EntityHands.FastAfterReleaseS = 1.5f;
        Check(Entities.TestHeldInterval(e, 10) == .02f, "held item uses 50 Hz interval from real Tick expression");
        Entities.TestHeldRelease(e, 10, false); Check(e.FastSendUntil == 0, "ordinary state leaves fast release timer alone");
        Entities.TestHeldRelease(e, 10, true); e.SentHeld = false;
        Check(e.FastSendUntil == 11.5 && Entities.TestHeldInterval(e, 11.49) == .02f && Entities.TestHeldInterval(e, 11.5) == .05f, "release window keeps fast rate and ends at exact boundary");
        EntityHands.HeldStateHz = 20; e.SentHeld = true;
        Check(Entities.TestHeldInterval(e, 10) == .05f, "held stream setting at protocol rate disables boost"); EntityHands.HeldStateHz = 50;
        Check(BridgeReflect.Set("TLDRevamp.Net.EntityHands.WornClaimTries", "4") == "4" && EntityHands.WornClaimTries == 4, "full worn setting forwards to sole owner"); EntityHands.WornClaimTries = 3;
        Check(BridgeReflect.Get("TLDRevamp.Net.EntityHands.CarriedSent") == EntityHands.CarriedSent.ToString() && BridgeReflect.Get("TLDRevamp.Net.EntityHands._wornOwn.Count") == "0", "full counter and cache owner paths reach feature");
        Check(BridgeReflect.Call("TLDRevamp.Net.EntityHands.WornStatus", Array.Empty<string>()).Contains("sharedWorn"), "real owner diagnostic call forwards");
        foreach (string name in new[] { "CarriedSent", "RidingSent", "HeldStateHz", "HandItemDraw", "WornSync", "_wornOwn", "WornSent", "WornTooLate" })
            Check(typeof(Entities).GetField(name, Static) == null, "no second hands field owner " + name);
        foreach (string part in new[] { "Carried", "HandItems", "Worn" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            Check(!Regex.IsMatch(source, @"\bSEnt\b|Entities\.(?:W|WS|ByNet|Server)\b|using static"), part + " does not expose writers maps authority records or imports");
        }
        string main = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(main.IndexOf("EntityHands.TakeHandPose(e, sp, sq);") < main.IndexOf("EntityHands.TakeCarried(e, ref s, sp, sq, held, stored)"), "hand receive remains before carried release claim");
        Check(main.IndexOf("RabbitTick();") < main.IndexOf("EntityHands.WornTick();") && main.IndexOf("EntityHands.WornTick();") < main.IndexOf("GentleTick();"), "worn tick retains scheduler position");
        Check(File.ReadAllText("src/TLDRevamp/Net/Entities.HandItems.cs").Contains("[DefaultExecutionOrder(-9999)]"), "hand follower retains Unity order before IK");
    }

    static void WornGuardChecks(Entities.Ent e, fpscontroller pl, byte[] on)
    {
        Entities.IsHost = false; Entities.MyId = 1; e.Proxy = true;
        var se = Entities.TestSeatServer(301);
        void CleanServer()
        {
            SeatValue(se, "OwnerId", 2); SeatValue(se, "WornBy", -1); SeatValue(se, "Epoch", 7u);
            SeatValue(se, "PrevOwnerId", -1); SeatValue(se, "Held", false); SeatValue(se, "Stored", false); SeatValue(se, "HitchCar", 0u);
            EntityHands.WornLateOk = true; Entities.TestEpoch = true;
        }
        CleanServer();
        long applied = EntityHands.WornApplied, relayed = EntityHands.WornRelayed;
        for (int n = 1; n < on.Length; n++)
        {
            EntityHands.ServerWorn(2, Reader(on.Take(n).ToArray()));
            Check(EntityHands.WornRelayed == relayed, "truncated worn request rejected " + n);
        }
        foreach (var bad in new[] { 16, 254 })
        {
            var packet = on.ToArray(); packet[9] = (byte)bad; EntityHands.ServerWorn(2, Reader(packet));
            Check(EntityHands.WornRelayed == relayed, "worn invalid slot " + bad);
        }
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, 3f })
        {
            var packet = on.ToArray(); BitConverter.GetBytes(bad).CopyTo(packet, 10); EntityHands.ServerWorn(2, Reader(packet));
            Check(EntityHands.WornRelayed == relayed, "worn invalid head distance or finite guard");
        }
        EntityHands.ServerWorn(3, Reader(on)); Check(EntityHands.WornRelayed == relayed, "worn wrong owner without preceding claim refused");
        EntityHands.WornLateOk = false; Entities.TestEpoch = false;
        EntityHands.ServerWorn(2, Reader(on)); Check(EntityHands.WornRelayed == relayed, "legacy worn epoch guard");
        EntityHands.WornLateOk = true;
        EntityHands.ServerWorn(2, Reader(on)); Check(EntityHands.WornRelayed == relayed + 1, "late worn accepts current owner despite old epoch");
        byte[] relay = Entities.Packets.Last().Bytes;
        Check(Entities.LastExcept == 2 && Entities.Packets.Last().Reliable, "worn relay reliable and excludes sender");
        for (int n = 1; n < relay.Length; n++)
        {
            EntityHands.ApplyWorn(Reader(relay.Take(n).ToArray()));
            Check(EntityHands.WornApplied == applied, "truncated worn copy rejected " + n);
        }
        RemotePlayers.Head = new Transform { position = new Vector3(50, 0, 0) };
        var body = new Rigidbody { detectCollisions = true, interpolation = RigidbodyInterpolation.Interpolate };
        var collider = new Collider(); e.Root.Children.Add(body); e.Root.P.wear.c = new[] { collider };
        e.MovedByBody = true;
        EntityHands.ApplyWorn(Reader(relay));
        Check(e.WornBy == 2 && e.WornSlot == 3 && !e.MovedByBody && EntityHands.WornRides(e), "copy records wearer and stops body motion");
        EntityHands.WornLate();
        Check(!body.detectCollisions && !collider.enabled && body.interpolation == RigidbodyInterpolation.None && e.WornOffFor == e.Root, "worn late disables body and wear collisions");
        Check(e.Root.transform.position == new Vector3(50.25f, .5f, -.75f), "worn copy rides remote head unscaled");
        var root = e.Root; long placed = EntityHands.WornPlaced; e.Root = null; EntityHands.WornLate();
        Check(e.WornBy == 2 && EntityHands.WornPlaced == placed, "stream out retains worn copy for later placement");
        e.Root = root; RemotePlayers.Head = null; EntityHands.WornLate();
        Check(EntityHands.WornPlaced == placed && !EntityHands.WornRides(e), "missing remote body uses state fallback");
        EntityHands.ClearWorn(e); Check(e.WornBy == -1 && collider.enabled && body.detectCollisions, "clear restores collisions for loose item");
        EntityHands.ApplyWorn(Reader(relay)); RemotePlayers.Head = new Transform(); EntityHands.WornLate(); e.Held = true; EntityHands.ClearWorn(e); Check(!body.detectCollisions, "cleared held item still has no body collisions"); e.Held = false;
        e.Proxy = false; EntityHands.ApplyWorn(Reader(relay)); Check(e.WornBy == -1, "owner copy echo ignored"); e.Proxy = true;
        Entities.MyId = 2; EntityHands.ApplyWorn(Reader(relay)); Check(e.WornBy == -1, "wearer copy echo ignored"); Entities.MyId = 1;
        EntityHands.ApplyWorn(Reader(relay)); EntityHands.WornSync = false; EntityHands.ApplyWorn(Reader(relay));
        Check(e.WornBy == -1, "disabled worn sync clears previous draw"); EntityHands.WornSync = true;
        // Each reason must refuse before changing owner or broadcasting an Owner packet.
        foreach (string guard in new[] { "prior", "age", "Held", "Stored", "worn", "HitchCar" })
        {
            CleanServer(); SeatValue(se, "OwnerId", 1); SeatValue(se, "PrevOwnerId", 2); SeatValue(se, "PrevOwnerAt", 10f); Time.realtimeSinceStartup = 10;
            if (guard == "prior") SeatValue(se, "PrevOwnerId", 3);
            else if (guard == "age") SeatValue(se, "PrevOwnerAt", 7f);
            else if (guard == "worn") SeatValue(se, "WornBy", 1);
            else if (guard == "HitchCar") SeatValue(se, guard, 9u);
            else SeatValue(se, guard, true);
            long tooLate = EntityHands.WornTooLate; int sends = Entities.Packets.Count;
            EntityHands.ServerWorn(2, Reader(on));
            Check(EntityHands.WornTooLate == tooLate + 1 && Entities.Packets.Count == sends && (int)SeatValue(se, "OwnerId") == 1, "take back guard " + guard);
        }
        CleanServer(); SeatValue(se, "OwnerId", 1); SeatValue(se, "PrevOwnerId", 2); SeatValue(se, "PrevOwnerAt", 8f); Time.realtimeSinceStartup = 10;
        int cascades = Entities.Cascades;
        EntityHands.ServerWorn(2, Reader(on));
        Check((int)SeatValue(se, "OwnerId") == 2 && (uint)SeatValue(se, "Epoch") == 8 && (uint)SeatValue(se, "ClaimedFromEpoch") == 7 && Entities.Cascades == cascades + 1, "take back exact acceptance boundary records claim and cascades");
        Check(Entities.Packets[^2].Bytes[0] == Entities.Owner && Entities.Packets[^1].Bytes[0] == Entities.Worn, "take back broadcasts Owner before worn relay");
        root.transform.position = pl.anim.HHTarget.position + new Vector3(.25f, .5f, -.75f);
        var share = new NetWriter(); EntityHands.WriteShareWorn(share, root);
        EntityHands.WornInShare = false; var empty = new NetWriter(); EntityHands.WriteShareWorn(empty, root); Check(empty.Len == 0, "share flag removes only optional worn tail"); EntityHands.WornInShare = true;
        object[] read = { RawReader(Convert.ToHexString(share.Buf.Take(share.Len).ToArray())), null, null, null };
        Check((bool)HandsCall("ReadShareWorn", read) && (byte)read[1] == 3, "shared worn reader consumes original tail");
        CleanServer(); typeof(Entities).GetMethod("ReadShareWorn", Static).Invoke(null, new[] { read[0] = RawReader(Convert.ToHexString(share.Buf.Take(share.Len).ToArray())), se, (object)2 });
        Check((int)SeatValue(se, "WornBy") == 2, "shared worn is stored on retained server record");
        EntityHands.WornReset(); Set(HandsOwner, "_wornCheckAt", 0f); Time.unscaledTime = 20; Entities.MyId = 2; pl.Bsitting = false;
        e.Proxy = false; e.SentWornSlot = -1; e.WornBy = -1;
        root.transform.position = pl.anim.HHTarget.position + new Vector3(.25f, .5f, -.75f);
        long sent = EntityHands.WornSent; EntityHands.WornTick(); EntityHands.WornTick();
        Check(EntityHands.WornSent == sent + 1, "worn 10 Hz and same slot suppress duplicate sends");
        pl.weared[3] = null; Time.unscaledTime += .11f; EntityHands.WornTick();
        Check(e.SentWornSlot == -1 && Entities.Packets.Last().Bytes[9] == 255, "worn tick detects take off and sends off");
        pl.weared[3] = root.P; root.P.wear.wearing = true; e.Proxy = true; e.WornClaimAt = 0;
        EntityHands.WornClaimTries = 2; EntityHands.WornClaimEveryS = 1;
        for (int i = 0; i < 3; i++) { e.Proxy = true; Time.unscaledTime += 1.1f; EntityHands.WornTick(); }
        Check(pl.weared[3] == null && EntityHands.WornGivenUp > 0 && e.WornClaims == 0, "wear claim retries give up after configured attempts");
        Check(EntityHands.WornClaimedBack >= 2 && Entities.Packets.Any(x => Convert.ToHexString(x.Bytes) == "182D0100000F"), "wear claim uses unchanged reason and kernel send");
        pl.weared[3] = root.P; root.P.wear.wearing = true; e.Proxy = false; root.transform.position = new Vector3(100, 0, 0);
        long reseated = EntityHands.WornReseated; Time.unscaledTime += 1.1f; EntityHands.WornTick();
        Check(EntityHands.WornReseated == reseated + 1 && pl.WearCalls == 1, "owner reseats item moved off its head");
        float at = (float)Field(HandsOwner, "_wornCheckAt"); long count = EntityHands.WornSent;
        ((System.Collections.IList)Field(HandsOwner, "_wornTmp")).Add(e);
        EntityHands.WornReset();
        Check((float)Field(HandsOwner, "_wornCheckAt") == at && EntityHands.WornSent == count && EntityHands.WornClaimTries == 2, "reset preserves check timer counters and settings");
        Check(((System.Collections.ICollection)Field(HandsOwner, "_wornTmp")).Count > 0, "reset retains worn traversal scratch");
        EntityHands.WornClaimTries = 3; Entities.TestEpoch = true; RemotePlayers.Head = null;
    }

    static void HandDrawChecks(Entities.Ent e, fpscontroller pl)
    {
        pl.inHandP = e.Root.P; pl.anim.RHTarget.position = Vector3.zero;
        e.Root.transform.position = new Vector3(1, 0, 0);
        foreach (var flags in new[] { (false, false, false), (true, true, false), (true, false, true), (true, false, false) })
        {
            object[] args = { e, flags.Item1, flags.Item2, flags.Item3, null, null };
            Check((bool)HandsCall("LocalHandPose", args) == (flags == (true, false, false)), "local hand held stored locked guards " + flags);
        }
        e.Root.transform.position = new Vector3(3, 0, 0); object[] bad = { e, true, false, false, null, null };
        Check(!(bool)HandsCall("LocalHandPose", bad), "local hand offset above two metres refused");
        e.Proxy = true; e.OwnerId = 2; e.Epoch = 7; e.Held = true; e.Stored = e.PoseLocked = false; e.WornBy = -1;
        RemotePlayers.All[2] = new RemotePlayers.RemoteHand { Has = true, Go = new GameObject(), Mp = new mpplayerscript { anim = new HandAnim { RHTarget = new Transform() } } };
        e.HandDraw.Reset(); Time.timeAsDouble = 100; EntityCodec.ReadHandPose = true; EntityCodec.ReadHandPos = new Vector3(.5f, 0, 0); EntityCodec.ReadHandRot = Quaternion.identity;
        EntityHands.TakeHandPose(e, new Vector3d(4, 0, 0), Quaternion.identity);
        Check(e.HandOwner == 2 && e.HandEpoch == 7 && e.HandPose && e.HandDraw.HasShown, "hand receive seeds draw and records owner epoch");
        Check(EntityHands.ItemHand(e) != null && EntityHands.HandManaged(e), "valid remote hand owns drawing");
        e.Epoch++; Check(EntityHands.ItemHand(e) == null, "old epoch hand refused"); e.Epoch--;
        e.OwnerId++; Check(EntityHands.ItemHand(e) == null, "old owner hand refused"); e.OwnerId--;
        e.WornBy = 2; Check(EntityHands.ItemHand(e) == null, "worn item yields hand"); e.WornBy = -1;
        EntityHands.HandItemDraw = false; Check(EntityHands.ItemHand(e) == null, "draw switch yields hand"); EntityHands.HandItemDraw = true;
        e.HandPos = new Vector3(.5f, 0, 0); e.HandFrom = e.HandPos; e.HandSince = 100; EntityHands.HandItemPullM = -1;
        Entities.InSession = false; EntityHands.HandItemsLate(); Check(e.HandDraw.Source == null, "hand late session guard"); Entities.InSession = true;
        EntityHands.HandItemsLate(); Check(e.HandDraw.Source != null, "late draw enters hand source");
        EntityCodec.ReadHandPose = false; EntityHands.TakeHandPose(e, new Vector3d(4, 0, 0), Quaternion.identity);
        Time.timeAsDouble = 100.02; EntityHands.HandItemsLate();
        Check(e.HandDraw.Source == null && EntityHands.HandManaged(e), "drop during pickup glide switches source and keeps blend managed");
        Time.timeAsDouble = 100.2; EntityHands.HandItemsLate(); Check(!EntityHands.HandManaged(e), "release glide eventually yields to ordinary mover");
        e.HandPose = true; e.HandPos = e.HandFrom = new Vector3(.5f, 0, 0);
        RemotePlayers.All[2].Mp.anim.RHTarget.position = new Vector3(10, 0, 0);
        e.Ip = new PoseInterpolator { Ready = true, Value = new PoseInterpolator.Sample { Pos = default, Rot = Quaternion.identity } };
        var rb = new Rigidbody(); e.Root.ChildComponent = rb; e.Physical = e.MovedByBody = true;
        long exits = EntityPhysics.PhysicalExits;
        EntityHands.HandItemPullM = .08f; Time.timeAsDouble = 100.4; EntityHands.HandItemsLate();
        Time.timeAsDouble = 100.6; EntityHands.HandItemsLate();
        Check(Math.Abs(e.Root.transform.position.x - .08f) < .00001 && rb.isKinematic && !e.Physical && !e.MovedByBody && EntityPhysics.PhysicalExits == exits + 1, "hand pull caps world correction and takes over physical mover");
        RemotePlayers.All.Clear(); Time.timeAsDouble = 100.7; EntityHands.HandItemsLate();
        Check(e.HandDraw.Source == null && EntityHands.HandManaged(e), "disappearing remote holder blends back to interpolation");
        RemotePlayers.All.Clear(); EntityHands.HandItemPullM = .08f;
    }

    static void CarriedChecks(Entities.Ent e, tosaveitemscript carRoot)
    {
        e.Proxy = true; e.CarriedBy = 401; e.Held = true; EntityCodec.ReadCarriedBy = 0; EntityCodec.ReadLockedState = false;
        object[] args = { e, new PoseInterpolator.Sample(), new Vector3d(5, 6, 7), Quaternion.identity, false, false };
        Entities.TestCargoClaim = true; long claims = EntityHands.CarriedReleasedClaims;
        Check((bool)HandsCall("TakeCarried", args) && EntityHands.CarriedReleasedClaims == claims + 1, "carried loose release claims into locally owned car");
        EntityCodec.ReadLockedState = true; Check(!(bool)HandsCall("TakeCarried", args) && e.CarriedBy == 0, "locked carried release cannot claim");
        EntityCodec.ReadLockedState = false; Entities.TestCargoClaim = false;
        EntityCodec.ReadCarriedBy = 401; EntityCodec.ReadLocalPos = new Vector3(1, 2, 3); EntityCodec.ReadLocalRot = Quaternion.identity;
        e.Ip = new PoseInterpolator(); var previous = e.Ip;
        Check(!(bool)HandsCall("TakeCarried", args) && e.CarriedBy == 401 && e.Ip != previous && ((PoseInterpolator.Sample)args[1]).Pos.x == 1, "frame switch starts fresh local interpolator");
        e.Ip.Ready = true; e.Ip.Value = (PoseInterpolator.Sample)args[1];
        object[] shown = { e, 1d, .02f, null, null }; HandsCall("EvalShown", shown);
        Check(((Vector3d)shown[3]).x == 9 && ((Vector3d)shown[3]).y == 2, "car frame is evaluated through current car transform");
        Entities.ByNet.Remove(401); HandsCall("EvalShown", shown);
        Check(((Vector3d)shown[3]).x == 5 && EntityHands.CarriedNoCar > 0, "missing car uses last world pose");
        Entities.ByNet[401] = new Entities.Ent { NetId = 401, Root = carRoot, LastVel = new Vector3(12, 0, 0), Proxy = true };
        EntityHands.TakeOverCarried(e); Check(e.CarriedBy == 0 && e.LastVel.x == 12, "copy car takeover starts with owner velocity");
        e.CarriedBy = 401; var rb = new Rigidbody { PointVelocity = new Vector3(15, 0, 0) }; carRoot.ChildComponent = rb; Entities.ByNet[401].Proxy = false;
        EntityHands.TakeOverCarried(e); Check(e.LastVel.x == 15, "own moving car takeover uses point velocity");
        Entities.TestCargoClaim = false; EntityCodec.ReadCarriedBy = 0; e.CarriedBy = 0;
    }
}
