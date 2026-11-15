using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public enum RigidbodyConstraints { None, FreezeAll }
    public partial class Rigidbody { public RigidbodyConstraints constraints; public bool IsSleeping() => false; }
}
public partial class fpscontroller
{
    public float throwForce, minThrowForce = .1f;
    public Transform Th = new Transform();
    public List<pickupable> inventory = new List<pickupable>();
    public usablescript lookedUsable;
    public void Pickup(pickupable p) { pickedUp = p; }
    public void Drop(bool destroyed) { }
    public void InvSwitchTo(int i, bool b) { }
    public void InvSwapTo(int i) { }
    public void InvStore(int i) { }
    public void InvUnStore(int i) { }
    public void RightHandGrip() { }
    public void RightHandRelease() { }
}
public partial class pickupable
{
    public mountStuff physlock;
    public void PhysLock(mountStuff m) { physlock = m; }
    public void UnPhysicsLock() { physlock = null; }
}
public partial class mountStuff { public tosaveitemscript parentTosave; }
public partial class attachablescript { public object utanfuto; }
public class partslotscript : Component { public attachablescript Part; public attachablescript part() => Part; public bool UnCraft(bool b) => true; }
public partial class save_attachable
{
    public Vector3d gpos = new Vector3d(1, 2, 3), lpos = new Vector3d(4, 5, 6), poiid = new Vector3d(7, 8, 9);
    public int indexType = 2, attachType = 3, poigenid = 4;
    public save_attachable(attachablescript a, int i) { }
}
public partial class usablescript
{
    public uint tosaveid; public bool setid, setidPoi;
    public int poigenid, usableid; public Vector3d poiid; public float tunedFloat;
    public void SyncMulti(bool b) { }
}
public partial class carscript
{
    public float ithrottle, ibrake, iclutch, clutch, avgRpm, speed;
    public bool driving, BhandBrake, whlocked;
}
namespace TLDRevamp
{
    internal static partial class DevHooks { internal static bool Playing; }
    internal static class ReproRecorder
    {
        internal static bool Recording = true;
        internal static int Skipped;
        internal static readonly List<ReproRow> Rows = new List<ReproRow>();
        internal static void Add(ReproRow row) { if (Recording && !DevHooks.Playing) Rows.Add(row); }
        internal static void Skip() { Skipped++; }
    }
}
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static bool PlayerActing = true, _hitchApplying, _applyingLive;
        internal static uint TelemetryNet(Component c) => ByNet.Values.FirstOrDefault(e => e.Root == c || e.Root == c.ParentComponent || e.Root.car == c)?.NetId ?? 0;
        private static tosaveitemscript ItemOf(Component c, uint id, bool hasId) => c.ParentComponent as tosaveitemscript;
        internal static mountStuff[] LockMounts(tosaveitemscript it) => it.Children.OfType<mountStuff>().ToArray();
    }
}
