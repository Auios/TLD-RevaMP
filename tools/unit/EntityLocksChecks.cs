using System;
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
    static void LocksChecks()
    {
        var owner = typeof(EntityLocks);
        var snapshot = owner.GetFields(Static).Where(f => !f.IsInitOnly && !f.IsLiteral).Select(f => (Field: f, Value: f.GetValue(null))).ToArray();
        object Call(string name, params object[] args) => owner.GetMethod(name, Static).Invoke(null, args);
        void Put(string name, object value) => Set(owner, name, value);
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        void Packet(string name) { var p = Entities.Packets.Last(); Row(name, p.Peer + ":" + p.Reliable + ":" + Convert.ToHexString(p.Bytes)); }
        Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.InSession = true; Entities.IsHost = true;
        settingsscript.s = null; Call("PhysLockTick", 1f); Row("no-settings", Entities.Packets.Count);
        settingsscript.s = new settingsscript();
        foreach (int v in new[] { -3, 0, 2, 255, 400 })
        {
            settingsscript.s.S.IPhysicsLock = v; Call("PhysLockTick", .1f); Packet("setting-" + v);
        }
        int sent = Entities.Packets.Count; Call("PhysLockTick", 4.99f); Row("before-resend", Entities.Packets.Count - sent);
        Call("PhysLockTick", .01f); Packet("resend");
        Put("HostPhysicsLock", -1); Entities.IsHost = false;
        Call("ApplyPhysLock", new NetReader()); Row("bad-host", Field(owner, "HostPhysicsLock"));
        Call("ApplyPhysLock", Reader(new byte[] { 55, 7 })); Row("applied", Field(owner, "HostPhysicsLock"));
        var local = new settingsscript.setting { IPhysicsLock = 2 };
        Row("joiner-read", Call("PhysicsLockSetting", local));
        Entities.InSession = false; Row("solo-read", Call("PhysicsLockSetting", local));
        Entities.InSession = true; Entities.IsHost = true; Row("host-read", Call("PhysicsLockSetting", local));
        Call("ApplyPhysLock", Reader(new byte[] { 55, 9 })); Row("host-ignores", Field(owner, "HostPhysicsLock"));

        // Exhaust every combination, including attached versus scan/held/stored refusals.
        for (int bits = 0; bits < 512; bits++)
        {
            Entities.ByNet.Clear();
            bool scan = (bits & 1) != 0, session = (bits & 2) != 0, safe = (bits & 4) != 0;
            var car = new tosaveitemscript(); var item = new tosaveitemscript { attachable = new attachablescript { attached = (bits & 8) != 0 } };
            var mount = new mountStuff { parentTosave = car, ParentComponent = car }; mount.transform.parent = car.transform;
            var p = item.P = new pickupable { tosave = item, ParentComponent = item }; p.transform.parent = item.transform;
            var ce = new Entities.Ent { NetId = 1, Root = car, OwnerId = 2, Proxy = (bits & 16) != 0 };
            var pe = new Entities.Ent { NetId = 2, Root = item, OwnerId = (bits & 32) != 0 ? 2 : 3, Proxy = (bits & 64) != 0, Held = (bits & 128) != 0, Stored = (bits & 256) != 0 };
            Entities.ByNet[1] = ce; Entities.ByNet[2] = pe;
            Entities.InSession = session; Put("CopyLockSafe", safe); Put("_containerScan", scan); Put("_scanning", mount);
            long refused = (long)Field(owner, "ContainerStoresRefused"), attached = (long)Field(owner, "AttachedStoresRefused");
            var hook = owner.GetNestedType("ContainerStore", BindingFlags.NonPublic).GetMethod("Prefix", Static);
            Row("store-" + bits, hook.Invoke(null, new object[] { mount, p }) + ":" + ((long)Field(owner, "ContainerStoresRefused") - refused) + ":" + ((long)Field(owner, "AttachedStoresRefused") - attached));
        }
        Put("_containerScan", false); Put("_scanning", null); Put("CopyLockSafe", true); Entities.InSession = true;
        for (int bits = 0; bits < 8; bits++)
        {
            Put("CopyStartFreezeOff", (bits & 1) != 0); Entities.InSession = (bits & 2) != 0;
            Entities.ByNet.Clear(); var root = new tosaveitemscript(); var mount = new mountStuff { ParentComponent = root };
            Entities.ByNet[1] = new Entities.Ent { Root = root, Proxy = (bits & 4) != 0 };
            Row("freeze-" + bits, owner.GetNestedType("CopyStartFreeze", BindingFlags.NonPublic).GetMethod("Prefix", Static).Invoke(null, new object[] { mount }));
        }
        Entities.InSession = true; Put("CopyStartFreezeOff", true); Put("CopyLockByBox", false);
        foreach (float distance in new[] { 0f, 5f, 5.001f, 20f })
        foreach (bool held in new[] { false, true })
        {
            var root = new tosaveitemscript { name = "cargo" }; var mount = new mountStuff();
            var p = root.P = new pickupable { tosave = root, physlock = mount };
            var e = new Entities.Ent { Root = root, Proxy = true, Held = held, Ip = new PoseInterpolator { Ready = true, LastPos = new Vector3d(distance, 0, 0) } };
            foreach (double now in new[] { 10d, 10.999d, 11d })
            {
                Call("CheckCopyLock", e, now);
                Row("release-" + distance.ToString(CultureInfo.InvariantCulture) + "-" + held + "-" + now.ToString(CultureInfo.InvariantCulture), (p.physlock != null) + ":" + e.LockOffSince.ToString(CultureInfo.InvariantCulture) + ":" + e.ReleaseGhost + ":" + root.transform.position.x.ToString(CultureInfo.InvariantCulture));
            }
        }
        Put("CopyLockByBox", true);
        Entities.ByNet.Clear();
        var parent = new Entities.Ent { NetId = 90, Root = new tosaveitemscript() }; parent.Items.Add(parent.Root);
        var address = new mountStuff { parentTosave = parent.Root }; address.transform.parent = parent.Root.transform; parent.Root.Children.Add(address);
        var child = new Entities.Ent { NetId = 91, Root = new tosaveitemscript(), Proxy = true, Epoch = 3, ReceivedStateEpoch = 3, LockLocalPos = new Vector3(2, 0, 0) };
        child.Root.P = new pickupable { tosave = child.Root }; Entities.ByNet[90] = parent; Entities.ByNet[91] = child;
        parent.Root.transform.position = new Vector3(10, 0, 0);
        foreach (uint frame in new uint[] { 90, 90, 0, 999, 90 })
        {
            typeof(Entities).GetMethod("ApplyPoseLock", Static).Invoke(null, new object[] { child, true, frame, 0, 0, 0u });
            Row("pose-" + rows.Count, child.PoseLocked + ":" + child.LockFrame + ":" + (child.Root.P.physlock == address) + ":" + child.Root.P.transform.position.x.ToString(CultureInfo.InvariantCulture) + ":" + address.Stores);
        }
        var args = new object[] { child, address, child.Root.P.transform, null, null, null, null };
        Row("frame", typeof(Entities).GetMethod("LockedFrame", Static).Invoke(null, args)); Row("frame-address", args[5] + ":" + args[6]);
        Entities.ByNet.Clear(); Entities.Packets.Clear(); settingsscript.s = null;
        foreach (var saved in snapshot) saved.Field.SetValue(null, saved.Value);
        GoldenRows.Update("tools/unit/EntityLocksRows.txt", rows);
        { var expected = File.ReadAllLines("tools/unit/EntityLocksRows.txt"); Check(rows.SequenceEqual(expected), "lock rows match expected rows; first difference " + rows.Zip(expected).FirstOrDefault(p => p.First != p.Second)); }
        LocksSourceChecks();
    }

    static void LocksSourceChecks()
    {
        CoreMembers.Register();
        var owner = typeof(EntityLocks);
        Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, "locks has one internal static owner");
        foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
        {
            bool retained = typeof(Entities).GetMember(member.Name, Static).Length != 0;
            foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "lock alias " + prefix + member.Name);
        }
        foreach (string name in new[] { "PhysLockReads", "CopyLocksReleased", "StoreTrace", "CopyLockReleaseM" })
        {
            var field = owner.GetField(name, Static); var before = field.GetValue(null);
            BridgeReflect.Set("TLDRevamp.Net.Entities." + name, "17");
            Check(Convert.ToDouble(field.GetValue(null)) == 17 && BridgeReflect.Get("Entities." + name) == "17", "lock aliases share state " + name);
            field.SetValue(null, before);
        }
        Check(!ReferenceEquals(Field(owner, "W"), Field(typeof(Entities), "W")), "locks owns its writer");
        Check(((Collider[])Field(typeof(Entities), "_clearHits")).Length == 64, "cargo and lock overlap keep the original kernel scratch size");
        Check(typeof(Entities).GetField("_physLockAcc", Static) == null && typeof(Entities).GetField("_containerBox", Static) == null && typeof(Entities).GetField("ContainerMounts", Static) == null, "lock caches and timer have one owner");
        LockGuardChecks();
        string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");
        string phys = Read("src/TLDRevamp/Net/Entities.PhysLock.cs");
        string pose = Read("src/TLDRevamp/Net/Entities.LockPose.cs");
        foreach (string source in new[] { phys, pose }.Select(s => s.Split("    public static partial class Entities")[0]))
        {
            Check(!Regex.IsMatch(source, @"using\s+static\s+.*Entities|Entities\.(?:W|WS|ByNet|Server)\b|ObservedEntities|LookupSet\("), "locks has no map, writer or view access");
            Check(Regex.Matches(Regex.Replace(source, @"//[^\n]*", ""), @"Entities\.(\w+)").Cast<Match>().All(m => new[] { "Ent", "IsHost", "InSession", "PhysLockSync", "SendToPeers", "LockContainerOwnedBy", "LockContainerMotion", "LockScanAllows", "RefreshLockBodies", "LockRidesParent", "ShowLockCollisions", "IsLockCopy", "LockOverlapsCar" }.Contains(m.Groups[1].Value)), "lock kernel boundary is explicit and finite");
        }
        const string priority = "            // Clear crafted before the detach broadcast captures the released part, as under Entities.\n            [HarmonyPriority(Priority.Normal + 1)]\n";
        Check(phys.Contains(priority + "            [HarmonyPostfix]\n            private static void Postfix(attachablescript __instance, bool __runOriginal)"), "crafted cleanup stays ahead of detach broadcast and telemetry postfixes");
    }

    static void LockGuardChecks()
    {
        var owner = typeof(EntityLocks);
        var scan = owner.GetNestedType("ContainerScan", BindingFlags.NonPublic);
        var prefix = scan.GetMethod("Prefix", Static); var finalizer = scan.GetMethod("Finalizer", Static);
        var mount = new mountStuff();
        foreach (bool session in new[] { false, true })
        foreach (bool enabled in new[] { false, true })
        {
            Entities.InSession = session; EntityLocks.CopyContainersInert = enabled;
            Set(owner, "_containerScan", false); prefix.Invoke(null, new object[] { mount });
            Check((bool)Field(owner, "_containerScan") == (session && enabled), "scan guard preserves session and switch order");
            var error = new Exception("game scan failed");
            Check(ReferenceEquals(finalizer.Invoke(null, new object[] { error }), error) && !(bool)Field(owner, "_containerScan"), "scan finalizer clears guard and returns the same exception");
        }
        Entities.InSession = true; EntityLocks.CopyContainersInert = true; Set(owner, "_scanning", null);
        var store = owner.GetNestedType("ContainerStore", BindingFlags.NonPublic).GetMethod("Prefix", Static);
        Check((bool)store.Invoke(null, new object[] { mount, null }), "null store payload remains a game decision");
        var glued = owner.GetNestedType("GluedCraftedOff", BindingFlags.NonPublic).GetMethod("Postfix", Static);
        var part = new attachablescript { tosave = new tosaveitemscript { P = new pickupable { crafted = true } } };
        long cleared = EntityLocks.GluedCraftedCleared;
        glued.Invoke(null, new object[] { part, false }); Check(part.tosave.P.crafted && EntityLocks.GluedCraftedCleared == cleared, "refused detach keeps crafted flag");
        part.attached = true; glued.Invoke(null, new object[] { part, true }); Check(part.tosave.P.crafted, "still attached keeps crafted flag");
        part.attached = false; glued.Invoke(null, new object[] { part, true }); Check(!part.tosave.P.crafted && EntityLocks.GluedCraftedCleared == cleared + 1, "successful detach clears crafted once");
        EntityLocks.GluedCraftedCleared = cleared;
        long sent = EntityLocks.PhysLockSent; float resend = EntityLocks.PhysLockResendS;
        Set(owner, "_physLockAcc", 2.5f); Set(owner, "_physLockLast", 3); EntityLocks.HostPhysicsLock = 3;
        EntityLocks.ResetHostSetting();
        Check(EntityLocks.HostPhysicsLock == -1 && (int)Field(owner, "_physLockLast") == -1 && (float)Field(owner, "_physLockAcc") == 2.5f && EntityLocks.PhysLockSent == sent && EntityLocks.PhysLockResendS == resend, "setting reset preserves accumulator, counters and settings");
        Set(owner, "_physLockAcc", 0f);
        var rb = new Rigidbody { maxDepenetrationVelocity = 1 };
        var gentle = (Dictionary<Rigidbody, (float was, float until)>)Field(owner, "_gentle");
        gentle.Add(rb, (8, 20)); EntityLocks.ResetGentleBodies();
        Check(rb.maxDepenetrationVelocity == 8 && gentle.Count == 0, "session reset restores original body depenetration");
        var body = new Rigidbody { detectCollisions = false };
        var e = new Entities.Ent { Root = new tosaveitemscript(), Proxy = true, ReleaseGhost = true };
        e.Root.Children.Add(body); Physics.Overlaps = Array.Empty<Collider>();
        float before = Time.realtimeSinceStartup; long ghosts = EntityLocks.ReleaseGhostsCleared;
        Time.realtimeSinceStartup = 10; EntityLocks.ReleaseGhostTick(e);
        Time.realtimeSinceStartup = 10.499f; EntityLocks.ReleaseGhostTick(e);
        Check(e.ReleaseGhost && !body.detectCollisions, "release ghost waits for clear interval");
        Time.realtimeSinceStartup = 10.5f; EntityLocks.ReleaseGhostTick(e);
        Check(!e.ReleaseGhost && body.detectCollisions && EntityLocks.ReleaseGhostsCleared == ghosts + 1, "release ghost clears at exact interval");
        EntityLocks.ReleaseGhostsCleared = ghosts; Time.realtimeSinceStartup = before;
        LockMotionChecks();
    }

    static void LockMotionChecks()
    {
        var car = new Entities.Ent { NetId = 1, OwnerId = 2, Proxy = true, Root = new tosaveitemscript(), Ip = new PoseInterpolator { Ready = true, LastPos = new Vector3d(100, 0, 0) } };
        car.Root.transform.position = new Vector3(100, 0, 0); car.Root.transform.ChildComponent = car.Root;
        Entities.ByNet[1] = car;
        var e = new Entities.Ent { OwnerId = 2, Root = new tosaveitemscript(), Ip = new PoseInterpolator { Ready = true, LastPos = new Vector3d(101, 0, 0) } };
        var mount = new mountStuff { col = new BoxCollider { size = new Vector3(2, 2, 2) } };
        mount.transform.parent = car.Root.transform; mount.col.transform.parent = car.Root.transform;
        var p = e.Root.P = new pickupable { physlock = mount };
        Check(Entities.LockContainerOwnedBy(car.Root.transform, 2) && !Entities.LockContainerOwnedBy(car.Root.transform, 3), "container owner predicate stays in kernel");
        Check(Entities.LockContainerMotion(car.Root.transform, 2, out bool same, out var velocity, out var pose) && same && velocity == Vector3.zero && ReferenceEquals(pose, car.Ip), "container motion returns only the requested copy facts");
        long outside = EntityLocks.OutOfContainerSeen;
        Check(!EntityLocks.OwnerHasItOut(e, p), "same owner still inside mount box");
        e.Ip.LastPos = new Vector3d(102, 0, 0);
        Check(EntityLocks.OwnerHasItOut(e, p) && EntityLocks.OutOfContainerSeen == outside + 1, "same owner outside mount box increments observation once");
        car.LastVel = new Vector3(1, 0, 0);
        Check(!EntityLocks.OwnerHasItOut(e, p), "moving container keeps five metre release rule");
        car.LastVel = Vector3.zero; e.LastVel = new Vector3(1, 0, 0);
        Check(!EntityLocks.OwnerHasItOut(e, p), "moving item keeps five metre release rule");
        e.LastVel = Vector3.zero; e.OwnerId = 3;
        Check(EntityLocks.OwnerHasItOut(e, p), "different owner uses this machine's stationary container frame");
        car.Proxy = false;
        Check(!Entities.LockContainerMotion(car.Root.transform, 2, out same, out velocity, out pose) && !same && pose == null && velocity == Vector3.zero, "own container exports no invented copy motion");
        Check(EntityLocks.OwnerHasItOut(e, p), "unshared stationary container uses local mount box");
        EntityLocks.MountBox = false; e.Ip.LastPos = new Vector3d(104, 0, 0);
        Check(!EntityLocks.OwnerHasItOut(e, p), "missing collider box falls back to old release reach");
        e.Ip.LastPos = new Vector3d(106, 0, 0);
        Check(EntityLocks.OwnerHasItOut(e, p), "fallback container box refuses outside pose");
        EntityLocks.MountBox = true; EntityLocks.OutOfContainerSeen = outside;
        Entities.ByNet.Clear();
        var own = new Transform(); var self = new Collider(); self.transform.parent = own; own.ChildComponent = new carscript();
        Physics.Overlaps = new[] { self };
        Check(!Entities.LockOverlapsCar(default, own), "release overlap skips the item's own car root");
        var other = new Collider(); other.transform.ChildComponent = new carscript(); Physics.Overlaps = new[] { self, other };
        Check(Entities.LockOverlapsCar(default, own), "release overlap sees another car");
        Physics.Overlaps = Array.Empty<Collider>();
    }
}
