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
    static readonly List<string> physicsRows = new List<string>();
    static object PhysicsCall(string name, params object[] args) => typeof(EntityPhysics).GetMethods(Static).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
    static void PhysicsRow(string name, object value) => physicsRows.Add(name + "=" + value);
    static void PhysicsHook(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    static Entities.Ent PhysicsSetup()
    {
        foreach (string name in new[] { "_held", "_after", "_jointed", "_jointsDone" }) ((IList)Field(typeof(EntityPhysics), name)).Clear();
        foreach (string name in new[] { "PhysicalCars", "_freedJoints" }) ((IDictionary)Field(typeof(EntityPhysics), name)).Clear();
        foreach (var f in typeof(EntityPhysics).GetFields(Static).Where(f => f.FieldType == typeof(long) || f.FieldType == typeof(float) && f.Name.StartsWith("ContactImpulseMax"))) f.SetValue(null, Activator.CreateInstance(f.FieldType));
        EntityPhysics.CrashAuthority = EntityPhysics.CrashCopyCheck = EntityPhysics.CopyJointsFree = EntityPhysics.RestSnap = true;
        EntityPhysics.GhostOnReturn = EntityPhysics.ContactRelayOn = false;
        EntityPhysics.LastCrash = EntityPhysics.LastFree = "";
        PhysicsFixture.Local.Clear(); PhysicsFixture.Events.Clear(); PhysicsFixture.Tow.Clear(); PhysicsFixture.Trailer = PhysicsFixture.IsProxy = false;
        PhysicsFixture.InSession = PhysicsFixture.ResolveOk = true; PhysicsFixture.DuringSend = null; PhysicsFixture.PhysicsFootingNet = 0;
        Time.time = Time.unscaledTime = 10; Time.realtimeSinceStartupAsDouble = 0;
        var rb = new Rigidbody { position = Vector3.zero, interpolation = RigidbodyInterpolation.None, velocity = new Vector3(1, 0, 0) };
        var root = new tosaveitemscript { name = "car", ChildComponent = rb, RB = rb, car = new carscript() };
        var e = new Entities.Ent { NetId = 0x1020304, Epoch = 7, Root = root, Proxy = true, Ip = new PoseInterpolator { Ready = true, Value = new PoseInterpolator.Sample { Rot = Quaternion.identity } }, LastVel = new Vector3(2, 0, 0) };
        PhysicsFixture.Local[e.NetId] = e;
        return e;
    }
    static string PhysicsState(Entities.Ent e) => e.Proxy + ":" + e.Physical + ":" + e.CrashTaking + ":" + e.MovedByBody + ":" + e.Root.RB.isKinematic + ":" + EntityPhysics.CrashClaims + ":" + EntityPhysics.CrashStaleRefused + ":" + EntityPhysics.CrashRetakesSkipped + ":" + EntityPhysics.CrashTowingSkipped + ":" + ((IList)Field(typeof(EntityPhysics), "_held")).Count + ":" + ((IDictionary)Field(typeof(EntityPhysics), "PhysicalCars")).Count + ":" + string.Join("|", PhysicsFixture.Events);
    static void PhysicsChecks()
    {
        var oldCulture = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            physicsRows.Clear();
            // Refusal precedence, boundary speed and copy pose guards, before provisional ownership or bytes.
            for (int scenario = 0; scenario < 17; scenario++)
            {
                var e = PhysicsSetup(); var mine = new Rigidbody(); float speed = 10;
                if (scenario == 0) PhysicsFixture.Trailer = true;
                if (scenario == 1) PhysicsFixture.Tow.Add(e.Root);
                if (scenario == 2) EntityPhysics.CrashAuthority = false;
                if (scenario == 3) e.Proxy = false;
                if (scenario == 4) speed = 3.999f;
                if (scenario == 5) e.CrashTaking = true;
                if (scenario == 6) speed = 150.001f;
                if (scenario == 7) e.Ip.Ready = false;
                if (scenario == 8) e.Root.RB.position = new Vector3(8.001f, 0, 0);
                if (scenario == 9) { Time.realtimeSinceStartupAsDouble = 1.001; e.Ip.Value.Vel = Vector3.one; }
                if (scenario == 10) { Time.realtimeSinceStartupAsDouble = 999; e.Ip.Value.Vel = Vector3.zero; }
                if (scenario == 11) e.Root.RB.position = new Vector3(8, 0, 0);
                if (scenario == 12) speed = 4;
                if (scenario == 13) speed = 150;
                if (scenario == 14) { speed = 151; EntityPhysics.CrashCopyCheck = false; }
                if (scenario == 15) { var partner = new Entities.Ent { NetId = 99, Root = new tosaveitemscript(), Proxy = true }; mine.transform.parent = partner.Root.transform; PhysicsFixture.Local[99] = partner; }
                if (scenario == 16) { PhysicsFixture.Trailer = true; PhysicsFixture.Tow.Add(e.Root); EntityPhysics.CrashAuthority = false; }
                bool took = EntityPhysics.TryTakeCrash(e, mine, speed);
                PhysicsRow("take:" + scenario, took + ":" + PhysicsState(e));
                if (took) { PhysicsFixture.Events.Clear(); PhysicsRow("retake:" + scenario, EntityPhysics.TryTakeCrash(e, mine, speed) + ":" + PhysicsState(e)); }
            }
            // Lease hold thresholds are strict >, with the calm window and reverse removal order preserved.
            foreach (float elapsed in new[] { .999f, 1f, 1.001f, 1.3f, 1.301f, 3f, 3.001f })
            {
                var e = PhysicsSetup(); var mine = new Rigidbody { velocity = new Vector3(20, 0, 0) };
                EntityPhysics.TryTakeCrash(e, mine, 10); PhysicsFixture.Events.Clear();
                Time.time = 10 + elapsed; EntityPhysics.CrashHoldTick();
                PhysicsRow("hold-moving:" + elapsed, EntityPhysics.CrashReleases + ":" + PhysicsState(e));
            }
            {
                var e = PhysicsSetup(); e.Root.RB.velocity = Vector3.zero; var mine = new Rigidbody();
                EntityPhysics.TryTakeCrash(e, mine, 10); PhysicsFixture.Events.Clear();
                foreach (float at in new[] { 10.5f, 11f, 11.001f }) { Time.time = at; EntityPhysics.CrashHoldTick(); PhysicsRow("calm:" + at, EntityPhysics.CrashReleases + ":" + PhysicsState(e)); }
                PhysicsFixture.Events.Clear(); PhysicsRow("wait-apart", EntityPhysics.TryTakeCrash(e, mine, 10) + ":" + PhysicsState(e));
                Time.time += .251f; EntityPhysics.CrashHoldTick(); PhysicsRow("after-apart", EntityPhysics.TryTakeCrash(e, mine, 10) + ":" + PhysicsState(e));
            }
            foreach (bool lostRoot in new[] { false, true })
            {
                var e = PhysicsSetup(); EntityPhysics.TryTakeCrash(e, new Rigidbody(), 10); PhysicsFixture.Events.Clear();
                if (lostRoot) e.Root = null; else e.Proxy = true;
                EntityPhysics.CrashHoldTick(); PhysicsRow("lost:" + lostRoot, e.CrashTaking + ":" + ((IList)Field(typeof(EntityPhysics), "_held")).Count + ":" + EntityPhysics.CrashReleases + ":" + string.Join("|", PhysicsFixture.Events));
            }
            // The old contact path still runs all contact claims before its relay switch.
            for (int scenario = 0; scenario < 9; scenario++)
            {
                var e = PhysicsSetup(); EntityPhysics.ContactRelayOn = scenario != 0;
                var other = e.Root.RB; other.transform.parent = e.Root.transform;
                var c = new Collision { rigidbody = other, impulse = new Vector3(.5f, -2, 3) };
                var relay = new EntityPhysics.ContactRelay { NetId = 55 };
                if (scenario == 1) PhysicsFixture.InSession = false;
                if (scenario == 2) relay.NetId = 0;
                if (scenario == 3) c.rigidbody = null;
                if (scenario == 4) e.Proxy = false;
                if (scenario == 5) relay.NetId = e.NetId;
                if (scenario == 6) c.impulse = new Vector3(.499f, 0, 0);
                if (scenario == 7) c.impulse = new Vector3(.5f, 0, 0);
                PhysicsHook(relay, "OnCollisionEnter", c);
                PhysicsRow("contact-enter:" + scenario, EntityPhysics.ContactSent + ":" + EntityPhysics.ContactImpulseMaxSent + ":" + string.Join("|", PhysicsFixture.Events));
                PhysicsFixture.Events.Clear(); PhysicsHook(relay, "OnCollisionStay", c); PhysicsHook(relay, "OnCollisionStay", c);
                Time.unscaledTime = 10.1f; PhysicsHook(relay, "OnCollisionStay", c);
                PhysicsRow("contact-stay:" + scenario, EntityPhysics.ContactSent + ":" + string.Join("|", PhysicsFixture.Events));
            }
            for (int scenario = 0; scenario < 6; scenario++)
            {
                var e = PhysicsSetup(); e.Proxy = false;
                if (scenario == 0) PhysicsFixture.Local.Clear();
                if (scenario == 1) PhysicsFixture.ResolveOk = false;
                if (scenario == 2) e.Proxy = true;
                if (scenario == 3) e.Root.ChildComponent = null;
                if (scenario == 4) e.Root.RB.isKinematic = true;
                EntityPhysics.ApplyContactImpulse(e.NetId, new Vector3(1, 2, 3), new Vector3d(4, 5, 6));
                PhysicsRow("contact-apply:" + scenario, EntityPhysics.ContactApplied + ":" + e.Root.RB.ForceCalls + ":" + e.Root.RB.Force.x + ":" + e.Root.RB.ForceAt.x + ":" + EntityPhysics.ContactImpulseMaxApplied);
            }
            // Joint desire and release guards do no allocation; transitions keep the original three axes.
            for (int scenario = 0; scenario < 10; scenario++)
            {
                var e = PhysicsSetup(); var own = new Rigidbody { isKinematic = true }; var on = new Rigidbody();
                var j = new ConfigurableJoint { ChildComponent = own, connectedBody = on };
                e.Root.attachable = new attachablescript { attached = true, J = j };
                if (scenario == 0) EntityPhysics.CopyJointsFree = false;
                if (scenario == 1) PhysicsFixture.InSession = false;
                if (scenario == 2) e.Proxy = false;
                if (scenario == 3) e.Root.attachable.attached = false;
                if (scenario == 4) e.Root.attachable.parentAtAttach = true;
                if (scenario == 5) e.Root.attachable.J = new Joint();
                if (scenario == 6) j.ChildComponent = null;
                if (scenario == 7) own.isKinematic = false;
                if (scenario == 8) on.isKinematic = true;
                ((IList)Field(typeof(EntityPhysics), "_jointed")).Add(e);
                EntityPhysics.CopyJointTick(); PhysicsRow("joint-free:" + scenario, EntityPhysics.WantJointFree(e, j) + ":" + j.xMotion + ":" + j.yMotion + ":" + j.zMotion + ":" + EntityPhysics.CopyJointsFreed);
                e.Proxy = false; EntityPhysics.CopyJointTick(); PhysicsRow("joint-lock:" + scenario, j.xMotion + ":" + EntityPhysics.CopyJointsLocked + ":" + ((IDictionary)Field(typeof(EntityPhysics), "_freedJoints")).Count);
            }
            {
                var e = PhysicsSetup(); var rb = e.Root.RB; rb.isKinematic = true; e.MovedByBody = true; e.OrigInterp = RigidbodyInterpolation.None;
                EntityPhysics.EnterPhysical(e, rb); PhysicsRow("enter", PhysicsState(e) + ":" + rb.interpolation + ":" + rb.velocity.x + ":" + EntityPhysics.PhysicalEnters);
                EntityPhysics.ExitPhysical(e, rb); PhysicsRow("exit", PhysicsState(e) + ":" + rb.interpolation + ":" + EntityPhysics.PhysicalExits);
                EntityPhysics.EnterPhysical(e, rb); EntityPhysics.DropPhysical(e); PhysicsRow("drop", PhysicsState(e) + ":" + rb.interpolation);
            }
            foreach (float sinceFree in new[] { -.01f, 0f, .2f, .4f, 1f })
            {
                var e = PhysicsSetup(); var rb = e.Root.RB; e.Ip.Value.Pos = new Vector3d(1, 0, 0); e.FreeUntil = 10; Time.time = 10 + sinceFree;
                EntityPhysics.Servo(e, rb, .02f); PhysicsRow("servo:" + sinceFree, rb.position.x + ":" + rb.velocity.x + ":" + e.HasServoPrev + ":" + e.TelemetryServoCorrection);
                e.Ip.Snap = true; EntityPhysics.Servo(e, rb, .02f); PhysicsRow("servo-snap:" + sinceFree, rb.position.x + ":" + rb.velocity.x + ":" + e.HasServoPrev + ":" + EntityPhysics.CopyTeleports);
            }
            for (int scenario = 0; scenario < 7; scenario++)
            {
                var e = PhysicsSetup(); var pl = new fpscontroller { RB = new Rigidbody() }; mainscript.s = new mainscript { player = pl };
                e.Root.RB.interpolation = RigidbodyInterpolation.Interpolate; e.Proxy = e.MovedByBody = true; PhysicsFixture.PhysicsFootingNet = e.NetId;
                Set(typeof(EntityPhysics), "_standInterp", false);
                if (scenario == 0) PhysicsFixture.InSession = false;
                if (scenario == 1) pl.Bsitting = true;
                if (scenario == 2) pl.RB.isKinematic = true;
                if (scenario == 3) e.Proxy = false;
                if (scenario == 4) e.MovedByBody = false;
                if (scenario == 5) e.Root.RB.interpolation = RigidbodyInterpolation.None;
                EntityPhysics.StandInterpTick(); PhysicsRow("stand:" + scenario, pl.RB.interpolation + ":" + EntityPhysics.StandInterpFrames);
                PhysicsFixture.PhysicsFootingNet = 0; EntityPhysics.StandInterpTick(); PhysicsRow("stand-off:" + scenario, pl.RB.interpolation + ":" + EntityPhysics.StandInterpFrames);
            }
            // The unchanged steady physics phases retain their zero-allocation path after warmup.
            {
                var e = PhysicsSetup(); EntityPhysics.RestSnap = false; e.FreeUntil = -10;
                for (int i = 0; i < 100; i++) { EntityPhysics.Servo(e, e.Root.RB, .02f); EntityPhysics.CopyJointTick(); EntityPhysics.CrashHoldTick(); }
                long start = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) { EntityPhysics.Servo(e, e.Root.RB, .02f); EntityPhysics.CopyJointTick(); EntityPhysics.CrashHoldTick(); }
                Check(GC.GetAllocatedBytesForCurrentThread() == start, "physics steady servo and empty joint/crash paths allocate zero bytes");
            }
            {
                var e = PhysicsSetup(); var rb = e.Root.RB; rb.isKinematic = true;
                e.HasTorques = true; e.WBrake = new[] { 11f }; e.WMotor = new[] { 22f };
                var w = new WheelCollider(); var w2 = new WheelCollider();
                PhysicsFixture.Wheels = new[] { new wheelgraphicsscript { W = w, W2 = w2 } };
                EntityPhysics.EnterPhysical(e, rb);
                typeof(EntityPhysics).GetNestedType("CopyWheelTorques", BindingFlags.NonPublic).GetMethod("Postfix", Static).Invoke(null, new object[] { e.Root.car });
                Check(w.brakeTorque == 11 && w.motorTorque == 22 && w2.brakeTorque == 11 && w2.motorTorque == 22, "physical wheel torques reach both wheel bodies");
                EntityPhysics.DropPhysical(e); w.motorTorque = 99;
                typeof(EntityPhysics).GetNestedType("CopyWheelTorques", BindingFlags.NonPublic).GetMethod("Postfix", Static).Invoke(null, new object[] { e.Root.car });
                Check(w.motorTorque == 99, "dropping physical mode removes the torque entry");
                PhysicsFixture.Wheels = Array.Empty<wheelgraphicsscript>();
            }
            {
                var e = PhysicsSetup(); EntityPhysics.TryTakeCrash(e, new Rigidbody { velocity = new Vector3(20, 0, 0) }, 10);
                var jointed = new Entities.Ent { Root = new tosaveitemscript(), Proxy = true };
                var joint = new ConfigurableJoint { ChildComponent = new Rigidbody { isKinematic = true }, connectedBody = new Rigidbody() };
                jointed.Root.attachable = new attachablescript { attached = true, J = joint };
                ((IList)Field(typeof(EntityPhysics), "_jointed")).Add(jointed); EntityPhysics.CopyJointTick();
                for (int i = 0; i < 100; i++) { EntityPhysics.CopyJointTick(); EntityPhysics.CrashHoldTick(); }
                long start = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) { EntityPhysics.CopyJointTick(); EntityPhysics.CrashHoldTick(); }
                Check(GC.GetAllocatedBytesForCurrentThread() == start, "active joint and moving held-crash paths allocate zero bytes");
            }
            if (Environment.GetCommandLineArgs().Contains("--capture-physics")) foreach (string row in physicsRows) Console.WriteLine("PHYSICSROW=" + row);
            else
            {
                var expected = File.ReadAllLines("tools/unit/EntityPhysicsRows.txt"); Check(expected.Length == physicsRows.Count, "physics HEAD row count");
                for (int i = 0; i < expected.Length; i++) Check(expected[i] == physicsRows[i], "physics HEAD row " + i + ": " + physicsRows[i]);
            }
            PhysicsBoundaryChecks();
            PhysicsSetup(); mainscript.s = null;
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }
    static void PhysicsBoundaryChecks()
    {
        CoreMembers.Register();
        var owner = typeof(EntityPhysics);
        Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, "physics has an internal static owner");
        foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly))
        {
            if (!(member is FieldInfo || member is PropertyInfo || member is MethodInfo)) continue;
            bool retained = typeof(Entities).GetMember(member.Name, Static).Length != 0;
            foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "physics alias " + prefix + member.Name);
        }
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.CrashApartCheckS", "0.5") == "0.5" && EntityPhysics.CrashApartCheckS == .5f, "physics compiled setting has one backing field");
        EntityPhysics.CrashApartCheckS = .25f;
        Check(BridgeReflect.Set("Entities.LodPartsOffKept", "12") == "12" && EntityPhysics.LodPartsOffKept == 12, "physics alias writes a field without a facade");
        Check(!ReferenceEquals(Field(owner, "W"), Field(typeof(Entities), "W")), "physics owns its writer");
        string[] allowed = { "Ent", "InSession", "ContactImpulse", "CrashClaim", "CrashRelease", "PhysicsRoot", "PhysicsNetOf", "PhysicsTrailer", "PhysicsWheels", "PhysicsPartCameOff", "PhysicsLimbs", "PhysicsIsProxy", "PhysicsSnap", "SetCrashProxy", "SendPhysicsRequest", "TryPhysicsContact", "ResolvePhysics", "PhysicsFootingNet", "TryPhysicsStand", "PhysicsContacts" };
        foreach (string part in new[] { "Contact", "CopyJoints", "CrashAuthority", "Footing", "Physical" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            string feature = source.Substring(source.IndexOf("    internal static partial class EntityPhysics")).Split("    public static partial class Entities")[0];
            string code = Regex.Replace(feature, @"//[^\r\n]*", "");
            Check(!Regex.IsMatch(code, @"\b(ByNet|Server|WS|SEnt|Placed)\b") && !code.Contains("using static"), part + " physics has no kernel map or server record access");
            foreach (Match call in Regex.Matches(code, @"Entities\.(\w+)")) Check(allowed.Contains(call.Groups[1].Value), "narrow physics dependency " + call.Value);
        }
        string boundary = File.ReadAllText("src/TLDRevamp/Net/Entities.Features.cs").Split("// Physics boundary.")[1].Split("// End physics boundary.")[0];
        Check(boundary.Contains("CargoContact(net, collision); RabbitContact(net, collision); ItemContact(net, collision);"), "collision claim order remains cargo rabbit item");
        Check(boundary.Contains("=> SetProxy(entity, proxy, keepMotion);") && boundary.Contains("=> ToServer(writer, reliable);"), "crash proxy transition and capture-before-dispatch transport stay on kernel");
        Check(!boundary.Contains("IEnumerable<Ent>") && !boundary.Contains("Dictionary<uint, Ent>") && !boundary.Contains("Dictionary<uint, SEnt>"), "physics boundary does not hand out kernel tables or enumerators");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.Replace("\r\n", "\n").Contains("EntityPhysics.ResetFreedJoints();\n            EntityLocks.ResetGentleBodies();\n            EntityPhysics.ResetCrashHolds(); DevHooks.CrashReset?.Invoke(); EntityAttachments.ResetCarriedOff();"), "reset restores joints then gentle bodies then clears holds and carried parts");
        Check(kernel.Contains("DevHooks.TrafficReset?.Invoke();") && kernel.IndexOf("EntityPhysics.ResetStandInterpolation();") < kernel.IndexOf("foreach (var e in ByNet.Values)"), "standing interpolation reset stays before proxy promotion");
        string physical = File.ReadAllText("src/TLDRevamp/Net/Entities.Physical.cs");
        Check(physical.Contains("private static readonly List<carscript> _localCars") && !physical.Contains("Entities.Placed()"), "kernel retains its shared local-car cache and placed scheduler");
        Check(physical.Contains("private static class SeatCopiesAfterRebase") && physical.IndexOf("private static class SeatCopiesAfterRebase") > physical.IndexOf("public static partial class Entities"), "map-wide rebase patch keeps its kernel nested owner");
        var torques = owner.GetNestedType("CopyWheelTorques", BindingFlags.NonPublic).GetMethod("Postfix", Static);
        Check(!torques.GetCustomAttributes(typeof(HarmonyLib.HarmonyPriority), false).Any(), "torque postfix retains its default priority with unchanged relative metadata order");
        // Both real boundary lookups preserve missing and found identities without exposing the map.
        Entities.ByNet.Clear(); var e = new Entities.Ent { NetId = 123 }; Entities.ByNet[123] = e;
        Check(Entities.TryPhysicsContact(123, out var contact) && ReferenceEquals(e, contact) && !Entities.TryPhysicsContact(124, out _), "contact boundary looks up one identity");
        Check(Entities.TryPhysicsStand(123, out var stand) && ReferenceEquals(e, stand) && !Entities.TryPhysicsStand(124, out _), "standing boundary looks up one identity");
        {
            var held = PhysicsSetup(); EntityPhysics.TryTakeCrash(held, new Rigidbody(), 10);
            EntityPhysics.ResetCrashHolds();
            Check(((IList)Field(owner, "_held")).Count == 0 && ((IList)Field(owner, "_after")).Count == 0 && held.CrashTaking && EntityPhysics.CrashClaims == 1, "hold reset clears only its lists and preserves the original per-entity flag and cumulative counter");
            var waiting = PhysicsSetup(); var partner = new Rigidbody();
            EntityPhysics.TryTakeCrash(waiting, partner, 10); waiting.Proxy = true; EntityPhysics.CrashHoldTick();
            Check(!EntityPhysics.TryTakeCrash(waiting, partner, 10) && EntityPhysics.CrashRetakesSkipped == 1 && ((IList)Field(owner, "_after")).Count == 1, "a refused held copy cannot be retaken until apart");
            Time.time = 10.249f; EntityPhysics.CrashHoldTick();
            Check(((IList)Field(owner, "_after")).Count == 1, "after-crash collider checks wait for the quarter-second timer");
            Time.time = 10.25f; EntityPhysics.CrashHoldTick();
            Check(((IList)Field(owner, "_after")).Count == 0 && EntityPhysics.TryTakeCrash(waiting, partner, 10), "apart check at its timer boundary permits a later takeover");
            EntityPhysics.ResetCrashHolds();
            EntityPhysics.ResetPhysicalCars();
            Check(((IDictionary)Field(owner, "PhysicalCars")).Count == 0 && EntityPhysics.CrashClaims == 2, "physical reset clears only its registry");
            var e2 = PhysicsSetup(); var j = new ConfigurableJoint { ChildComponent = new Rigidbody { isKinematic = true }, connectedBody = new Rigidbody() };
            e2.Root.attachable = new attachablescript { attached = true, J = j };
            EntityPhysics.BeginJointRefresh(); EntityPhysics.AddJointed(e2); EntityPhysics.CopyJointTick();
            EntityPhysics.ResetFreedJoints();
            Check(j.xMotion == ConfigurableJointMotion.Locked && j.yMotion == ConfigurableJointMotion.Locked && j.zMotion == ConfigurableJointMotion.Locked && EntityPhysics.CopyJointsLocked == 1 && EntityPhysics.ObservedJointed.Count == 1, "joint reset restores axes and preserves jointed refresh cache and cumulative counts");
            Check(EntityPhysics.ObservedFreedJoints.Count == 0, "joint reset clears freed registry");
            var pl = new fpscontroller { RB = new Rigidbody { interpolation = RigidbodyInterpolation.Interpolate } }; mainscript.s = new mainscript { player = pl };
            Set(owner, "_standInterp", true); EntityPhysics.StandInterpFrames = 9; EntityPhysics.ResetStandInterpolation();
            Check(pl.RB.interpolation == RigidbodyInterpolation.None && !(bool)Field(owner, "_standInterp") && EntityPhysics.StandInterpFrames == 9, "standing reset restores interpolation and keeps its counter");
        }
        Entities.ByNet.Clear(); EntityPhysics.LodPartsOffKept = 0;
    }

}
