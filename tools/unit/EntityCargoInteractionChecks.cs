using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static Type CargoOwner => typeof(EntityCargoInteraction);
    static object CargoCall(string name, params object[] args) => CargoOwner.GetMethod(name, Static).Invoke(null, args);
    static void CargoInteractionChecks()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        string Wire() => string.Join("|", Entities.Packets.Select(p => p.Peer + ":" + p.Reliable + ":" + Convert.ToHexString(p.Bytes)));
        var saved = CargoOwner.GetFields(Static).Where(f => !f.IsLiteral && !f.IsInitOnly).Select(f => (Field: f, Value: f.GetValue(null))).ToArray();
        Entities.Ent Item(uint net = 301, bool proxy = true)
        {
            var root = new tosaveitemscript { name = "cargo", P = new pickupable() };
            var e = new Entities.Ent { NetId = net, Root = root, Proxy = proxy, OwnerId = 2, OwnerAt = 0, LetGoAt = -100, Items = { root } };
            Entities.ByNet[net] = e; return e;
        }
        void Clean()
        {
            Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.MyId = 1; Entities.InSession = true; Entities.IsHost = false;
            Entities.ClaimsSent = 0; Entities.CargoClaimedOnContact = 0; Entities.ItemClaimedOnContact = 0;
            Time.realtimeSinceStartup = 10; Time.realtimeSinceStartupAsDouble = 10; Time.time = 10; Time.deltaTime = .02f;
            Physics.Hits = Physics.Sweeps = Array.Empty<RaycastHit>(); Physics.Overlaps = Array.Empty<Collider>();
            mainscript.s = new mainscript { player = new fpscontroller { RB = new Rigidbody(), CBody = new CargoCapsule() } };
            foreach (string name in new[] { "BodyClaims", "LoosePartClaims", "LetGoClaims", "SeamlessClaims", "RideRelative" }) Set(CargoOwner, name, true);
            foreach (string name in new[] { "CargoClaimed", "LetGoClaimTries", "BodyClaimedNear", "BodyClaimedOnContact", "PushFrames", "PushesSent", "PushesApplied", "PushesRejected", "GripClaims", "GripLost", "GripRefused" }) Set(CargoOwner, name, 0L);
            Set(CargoOwner, "ClaimTrace", false); Set(CargoOwner, "CargoClaimCooldownS", 2f); Set(CargoOwner, "_gripNet", 0u); Set(CargoOwner, "_pushSendAcc", 0f);
            ((IDictionary)Field(CargoOwner, "PushOut")).Clear();
        }
        // A recent release bypasses the cooldown, but does not bypass body, attachment, worn or live-creature checks.
        for (int bits = 0; bits < 128; bits++)
        {
            Clean(); var e = Item(); e.OwnerAt = (bits & 1) != 0 ? 9 : 8; e.LetGoAt = (bits & 2) != 0 ? 9.5f : -100;
            bool released = (bits & 4) != 0;
            if ((bits & 8) != 0) e.Root.P = null;
            if ((bits & 16) != 0) e.Root.attachable = new attachablescript { attached = true };
            if ((bits & 32) != 0) e.WornBy = 2;
            if ((bits & 64) != 0) e.Root.ChildComponent = new newAiScript();
            bool result = (bool)CargoCall("ClaimCargo", e, released, Entities.ClaimReason.CargoRelease);
            Row("claim-" + bits, result + ":" + e.OwnerAt + ":" + e.LetGoAt + ":" + e.Proxy + ":" + e.OwnerId + ":" + EntityCargoInteraction.LetGoClaimTries + ":" + EntityCargoInteraction.CargoClaimed + ":" + Entities.ClaimsSent + ":" + Wire());
        }
        foreach (float age in new[] { .999f, 1f, 1.001f })
        {
            Clean(); var e = Item(); e.OwnerAt = 9; e.LetGoAt = 10 - age;
            Row("release-age-" + age, CargoCall("ClaimCargo", e, false, Entities.ClaimReason.CargoNear) + ":" + e.LetGoAt + ":" + Wire());
        }
        foreach (float cooldown in new[] { 1.999f, 2f, 2.001f })
        {
            Clean(); var e = Item(); e.OwnerAt = 10 - cooldown;
            Row("cooldown-" + cooldown, CargoCall("ClaimCargo", e, false, Entities.ClaimReason.Contents) + ":" + Wire());
        }
        for (int bits = 0; bits < 256; bits++)
        {
            Clean(); var e = Item(); e.Proxy = (bits & 1) == 0; if ((bits & 2) != 0) e.Root.car = new carscript();
            e.Held = (bits & 4) != 0; e.Stored = (bits & 8) != 0; e.Physical = (bits & 16) != 0; e.CrashTaking = (bits & 32) != 0;
            e.PartIndex = (bits & 64) != 0 ? 0 : -1; if ((bits & 128) != 0) e.Root.attachable = new attachablescript { attached = true };
            Row("pushable-" + bits, CargoCall("Pushable", e));
        }
        foreach (bool contact in new[] { false, true })
        foreach (float speed in new[] { 0f, .299f, .3f, 2f })
        foreach (float ahead in new[] { -1f, 0f, 1f })
        {
            Clean(); var e = Item(); var rb = new Rigidbody { isKinematic = true, worldCenterOfMass = new Vector3(ahead, 0, 0) }; rb.transform.parent = e.Root.transform;
            mainscript.s.player.RB.velocity = new Vector3(speed, 9, 0); Physics.Overlaps = new[] { new Collider { attachedRigidbody = rb } };
            if (contact) CargoCall("BodyContact", new Collision { rigidbody = rb }); else CargoCall("BodyNearTick", .02f);
            Row("body-" + contact + "-" + speed + "-" + ahead, e.Proxy + ":" + EntityCargoInteraction.BodyClaimedNear + ":" + EntityCargoInteraction.BodyClaimedOnContact + ":" + Wire());
        }
        foreach (float speed in new[] { 0f, .499f, .5f, 2f })
        foreach (float ahead in new[] { -1f, 0f, 1f })
        {
            Clean(); var mine = Item(302, false); var e = Item();
            mine.Root.ChildComponent = new Rigidbody { velocity = new Vector3(speed, 0, 0), detectCollisions = true };
            var rb = new Rigidbody { isKinematic = true, worldCenterOfMass = new Vector3(ahead, 0, 0) }; rb.transform.parent = e.Root.transform;
            CargoCall("ItemContact", 302u, new Collision { rigidbody = rb });
            Row("item-" + speed + "-" + ahead, e.Proxy + ":" + Entities.ItemClaimedOnContact + ":" + Wire());
        }
        Clean(); var gripEnt = Item(); var grip = new pushgripable { name = "bar" }; grip.transform.parent = gripEnt.Root.transform;
        mainscript.s.player.pushgrip = grip; mainscript.s.player.pushgripping = true;
        CargoCall("PushGripTick"); Row("grip-claim", CargoCall("GripStatus") + ":" + Wire());
        gripEnt.Proxy = true; CargoCall("PushGripTick"); Row("grip-lost", CargoCall("GripStatus"));
        mainscript.s.player.pushgripping = true; gripEnt.Driven = true; CargoCall("PushGripTick"); Row("grip-driven", CargoCall("GripStatus"));
        mainscript.s.player.pushgripping = false; CargoCall("PushGripTick"); Row("grip-idle", CargoCall("GripStatus"));
        Clean(); var pushed = Item(); var push = new pushablescript { ParentComponent = pushed.Root }; pushed.Root.Children.Add(push);
        var hook = CargoOwner.GetNestedType("PushToOwner", BindingFlags.NonPublic).GetMethod("Prefix", Static);
        for (int i = 0; i < 2; i++) Row("push-prefix-" + i, hook.Invoke(null, new object[] { push, new Vector3(1, 2, 3), new Vector3(4, 5, 6), true }));
        CargoCall("PushTick", .049f); Row("push-before-timer", Wire());
        CargoCall("PushTick", .001f); Row("push-at-timer", Wire() + ":" + EntityCargoInteraction.PushesSent + ":" + EntityCargoInteraction.PushFrames);
        Time.deltaTime = 1; hook.Invoke(null, new object[] { push, Vector3.forward, Vector3.zero, true }); CargoCall("PushTick", .05f); Row("push-clamp", Wire());
        foreach (float dt in new[] { -1f, 0f, .5f, 1f, 1.001f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        foreach (float direction in new[] { 1f, 2f, 2.001f, float.NaN })
        {
            Clean(); var e = Item(301, false); var rb = new Rigidbody { mass = 2 }; var p = new pushablescript { ChildComponent = rb, modifier = 3 }; e.Root.Children.Add(p);
            byte[] bytes = Bytes(w => { w.Write((byte)53); w.Write(301u); w.Write((byte)0); w.Write((byte)0); w.Write(direction); w.Write(0f); w.Write(0f); w.Write(1f); w.Write(2f); w.Write(3f); w.Write(dt); });
            CargoCall("ApplyPush", Reader(bytes));
            Row("apply-" + dt + "-" + direction, rb.ForceCalls + ":" + rb.Force.x + ":" + rb.ForceAt.x + ":" + EntityCargoInteraction.PushesApplied + ":" + EntityCargoInteraction.PushesRejected);
        }
        Clean(); var surfaceItem = Item();
        var own = new Collider(); own.transform.parent = surfaceItem.Root.transform;
        var loose = new tosaveitemscript(); loose.transform.ChildComponent = loose; var looseCol = new Collider(); looseCol.transform.parent = loose.transform;
        var car = new carscript { RB = new Rigidbody { PointVelocity = new Vector3(3, 0, 0) } }; var carCol = new Collider { attachedRigidbody = car.RB }; carCol.transform.parent = car.transform;
        car.transform.ChildComponent = car;
        Physics.Hits = new[] { new RaycastHit { collider = own, distance = .1f }, new RaycastHit { collider = looseCol, distance = .2f }, new RaycastHit { collider = carCol, distance = 1 } };
        foreach (bool skips in new[] { true, false })
        {
            EntityCargoInteraction.SurfaceSkipsItems = skips; object[] args = { surfaceItem, null }; var first = CargoCall("SurfaceUnder", args);
            Row("surface-" + skips, ReferenceEquals(first, car.transform) + ":" + ReferenceEquals(args[1], car.RB));
            var rb = new Rigidbody { velocity = new Vector3(10, 0, 0) }; Row("ride-" + skips, ((Vector3)CargoCall("RideVelocity", surfaceItem, rb)).x);
        }
        foreach (bool relative in new[] { true, false })
        {
            EntityCargoInteraction.SurfaceSkipsItems = true; EntityCargoInteraction.RideRelative = relative; car.RB.isKinematic = true;
            Entities.ByNet[400] = new Entities.Ent { NetId = 400, Root = new tosaveitemscript { car = car }, LastVel = new Vector3(4, 0, 0) };
            Row("ride-proxy-" + relative, ((Vector3)CargoCall("RideVelocity", surfaceItem, new Rigidbody { velocity = new Vector3(10, 0, 0) })).x);
        }
        foreach (int bits in Enumerable.Range(0, 32))
        {
            Clean(); var e = Item(); e.OwnerAt = 9.9f;
            e.Ip = new PoseInterpolator { Ready = (bits & 1) == 0, Value = new PoseInterpolator.Sample { Pos = new Vector3d((bits & 2) == 0 ? 0 : 3, 0, 0), Rot = Quaternion.identity } };
            e.CarriedBy = (bits & 4) != 0 ? 99u : 0; EntityCodec.ReadLockedState = (bits & 8) != 0;
            Entities._localCars.Clear(); Entities._localCars.Add(new carscript { RB = new Rigidbody() });
            Row("let-go-in-car-" + bits, typeof(Entities).GetMethod("CargoTestLetGo", Static).Invoke(null, new object[] { e, (bits & 16) != 0 }) + ":" + e.Proxy + ":" + Wire());
        }
        foreach (bool locked in new[] { false, true })
        foreach (bool carried in new[] { false, true })
        foreach (bool stored in new[] { false, true })
        {
            Clean(); var e = Item(); e.PoseLocked = locked; e.CarriedBy = carried ? 1u : 0; e.Stored = stored;
            e.Root.ChildComponent = new Rigidbody(); e.Ip = new PoseInterpolator { Ready = true, Value = new PoseInterpolator.Sample { Pos = new Vector3d(5, 6, 7), Rot = Quaternion.identity, Vel = new Vector3(2, 0, 0) } };
            CargoCall("PlaceAtNewest", e); Row("newest-" + locked + "-" + carried + "-" + stored, e.Root.transform.position.x + ":" + e.LastVel.x);
        }
        foreach (bool stored in new[] { false, true })
        foreach (float distance in new[] { 0f, 1.049f, 1.051f, 3f })
        {
            Clean(); var e = Item(); var container = new carscript(); e.Root.transform.position = new Vector3(distance, 0, 0);
            if (stored) { e.Root.P.physlock = new mountStuff(); e.Root.P.physlock.transform.parent = container.transform; }
            EntityCargoInteraction.ContentsOverCar = false; object[] args = { container, e, false };
            Row("contents-" + stored + "-" + distance, CargoCall("InCar", args) + ":" + args[2]);
        }
        foreach (int bits in Enumerable.Range(0, 8))
        {
            Clean(); var e = Item(); var container = new carscript(); var c = new Collider { bounds = new Bounds(Vector3.zero, Vector3.one) }; e.Root.Children.Add(c);
            var ownHit = new Collider(); ownHit.transform.parent = e.Root.transform; var carHit = new Collider(); carHit.transform.parent = container.transform;
            var other = new Collider(); Physics.Sweeps = new[] { new RaycastHit { collider = ownHit, distance = .01f }, new RaycastHit { collider = carHit, distance = 1 }, new RaycastHit { collider = other, distance = (bits & 1) == 0 ? 2 : .5f } };
            if ((bits & 2) != 0) c.isTrigger = true;
            Row("sweep-" + bits, CargoCall("SweepMeetsCar", e, container, (bits & 4) == 0 ? Vector3.forward : Vector3.zero, 3f));
            Physics.Overlaps = new[] { (bits & 1) == 0 ? carHit : other };
            Row("overlap-" + bits, CargoCall("OverlapsCar", e, container, .1f, Vector3.zero));
        }
        foreach (int from in new[] { 1, 2 })
        foreach (int length in new[] { 1, 4, 5, 7 })
        {
            Clean(); Entities.ClearServer(); Entities.TestServer(301, 2);
            var bytes = new byte[] { 53, 45, 1, 0, 0, 99, 77 }; var r = Reader(bytes); r.End = length;
            typeof(Entities).GetMethod("CargoTestServerPush", Static).Invoke(null, new object[] { from, r }); Row("relay-" + from + "-" + length, Wire() + ":" + r.Pos);
        }
        GoldenRows.Update("tools/unit/EntityCargoInteractionRows.txt", rows);
        var expected = File.ReadAllLines("tools/unit/EntityCargoInteractionRows.txt"); Check(expected.Length == rows.Count, "cargo expected row count");
        for (int i = 0; i < rows.Count; i++) Check(rows[i] == expected[i], "cargo expected row " + i + ": " + rows[i]);
        Clean(); var firstPush = Item(); var reentrant = Item(302); var reentrantPush = new pushablescript { ParentComponent = firstPush.Root }; firstPush.Root.Children.Add(reentrantPush);
        hook.Invoke(null, new object[] { reentrantPush, Vector3.forward, Vector3.zero, true });
        Entities.IsHost = true;
        Entities.HostReceive = (kind, reader) => { if (kind == Entities.PushIn) CargoCall("ClaimCargo", reentrant, false, Entities.ClaimReason.BodyNear); };
        CargoCall("PushTick", .05f);
        Check(Entities.Packets.Count == 2 && Entities.Packets[0].Bytes[0] == Entities.PushIn && Entities.Packets[0].Bytes.Length == 35 && Entities.Packets[1].Bytes.SequenceEqual(new byte[] { 24, 46, 1, 0, 0, 16 }), "host captures push bytes before a synchronous claim reuses the feature writer");
        Entities.IsHost = false; Entities.HostReceive = null;
        foreach (var value in saved) value.Field.SetValue(null, value.Value);
        ((IDictionary)Field(CargoOwner, "PushOut")).Clear();
        Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.MyId = 0; Entities.ClaimsSent = 0; Entities.CargoClaimedOnContact = 0; Entities.ItemClaimedOnContact = 0;
        Entities._localCars.Clear(); Entities.ClearServer(); EntityCodec.ReadLockedState = false;
        Physics.Hits = Physics.Sweeps = Array.Empty<RaycastHit>(); Physics.Overlaps = Array.Empty<Collider>(); mainscript.s = null;
        CargoBoundaryChecks();
        CultureInfo.CurrentCulture = culture;
    }

    static void CargoBoundaryChecks()
    {
        Check(CargoOwner.IsNotPublic && CargoOwner.IsAbstract && CargoOwner.IsSealed, "cargo has an internal static owner");
        float speed = EntityCargoInteraction.BodyMinSpeed;
        Check(BridgeReflect.Set("TLDRevamp.Net.EntityCargoInteraction.BodyMinSpeed", "0.75") == "0.75" && EntityCargoInteraction.BodyMinSpeed == .75f, "real owner cargo setting writes its only owner");
        EntityCargoInteraction.BodyMinSpeed = speed;
        Check(!ReferenceEquals(Field(CargoOwner, "W"), Field(typeof(Entities), "W")), "cargo owns its request writer");
        string[] allowed = { "Ent", "ClaimReason", "CargoInSession", "CargoLocalId", "CargoClaimsEnabled", "CargoItemClaims", "CargoItemMinSpeed", "PushMessage", "CargoCopyOf", "IsCargoBody", "CargoRidesParent", "CargoLimbs", "CargoLimp", "SetCargoProxy", "WriteCargoClaim", "SendCargoRequest", "CargoClaimSent", "CargoContactClaimed", "ItemContactClaimed", "CargoOverlappedCar", "CargoCarBounds", "CargoLockedAway", "IsPushCopy", "PushEntityOf", "GripEntityOf", "CargoRabbit", "CargoCarEntity", "CargoItemHeld", "TryCargoContactCar", "TryItemContactSource", "TryPushTarget", "IsFinite" };
        foreach (string part in new[] { "BodyPush", "Cargo", "ItemPush", "Push", "PushGrip" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            string feature = source.Split("    public static partial class Entities")[0];
            string code = Regex.Replace(feature, @"//[^\r\n]*", "");
            Check(!Regex.IsMatch(code, @"\b(ByNet|Server|SEnt|WS|_clearHits|Placed|LookupSet)\b") && !code.Contains("using static"), part + " does not reach kernel maps or scratch buffers");
            foreach (Match call in Regex.Matches(code, @"Entities\.(\w+)")) Check(allowed.Contains(call.Groups[1].Value), "narrow cargo dependency " + call.Value);
        }
        string cargo = File.ReadAllText("src/TLDRevamp/Net/Entities.Cargo.cs").Split("    public static partial class Entities")[1];
        string push = File.ReadAllText("src/TLDRevamp/Net/Entities.Push.cs");
        string item = File.ReadAllText("src/TLDRevamp/Net/Entities.ItemPush.cs").Split("    public static partial class Entities")[1];
        Check(push.Contains("[HarmonyLib.HarmonyPrefix, HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Normal + 1)]") && CargoOwner.GetNestedType("PushToOwner", BindingFlags.NonPublic).GetMethod("Prefix", Static).GetCustomAttribute<HarmonyLib.HarmonyPriority>()?.priority == HarmonyLib.Priority.Normal + 1, "push interception retains its original order before telemetry when the top-level owner moves later");
        foreach (string name in new[] { "CargoNearTick", "CargoNearScan", "CarCargoTick", "RefreshOwnCars", "ClaimContents", "ClaimIfPlacedInOwnCar", "ClaimLetGoInCar", "RidingCar", "RidingBox", "RidingWhyNot", "LogRidingSwitch", "ProxyOf", "OverlappedCar" })
            Check(Regex.IsMatch(cargo, @"\b" + name + @"\("), name + " stays on kernel");
        Check(item.Contains("foreach (var e in Placed())") && item.Contains("foreach (var x in LookupSet())"), "item scans and car cache validation stay in kernel");
        Check(push.Split("    public static partial class Entities")[1].Contains("WS.Bytes(r.Buf, 0, r.End);") && push.Contains("public const byte PushIn = 53;"), "raw owner relay and message constant stay in kernel");
        Check(cargo.Contains("internal static bool IsHeld(") && cargo.Contains("private static void ShowCarriedCollisions("), "held pipeline stays with the kernel and lock boundary");
        Check(File.ReadAllText("src/TLDRevamp/Net/Entities.Pickup.cs").Contains("if (se.HitchCar != 0)"), "server claim transaction stays on kernel");
        string physical = File.ReadAllText("src/TLDRevamp/Net/Entities.Physical.cs");
        Check(physical.IndexOf("CargoNearTick(dt);") < physical.IndexOf("EntityCargoInteraction.BodyNearTick(dt);") && physical.IndexOf("EntityCargoInteraction.BodyNearTick(dt);") < physical.IndexOf("ItemNearTick(dt);") && physical.IndexOf("ItemNearTick(dt);") < physical.IndexOf("EntityRabbits.RabbitNearTick(dt);"), "cargo body item rabbit claim scan order is unchanged");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.IndexOf("EntityCargoInteraction.PushTick(dt);") < kernel.IndexOf("EntityRadio.RadioTick(dt);") && kernel.IndexOf("PhysLockTick(dt);") < kernel.IndexOf("EntityCargoInteraction.PushGripTick();") && kernel.IndexOf("EntityCargoInteraction.PushGripTick();") < kernel.IndexOf("EntityWeather.StormTick(dt);"), "push and grip retain scheduler neighbors");
    }
}
