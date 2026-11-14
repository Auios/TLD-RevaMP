using System;
using System.Collections.Generic;
using UnityEngine;

// Game/role side effects around the production SetProxy and ApplyProxyBodies methods.
public class TestHit : TestComponent { }
public class itemPlaceRemoveScript { public static itemPlaceRemoveScript s; public float itemUnFreezeDist; }

namespace UnityEngine
{
    public static class Physics
    {
        public static int Syncs;
        public static void SyncTransforms() { Syncs++; }
    }
    public static class Object
    {
        public static TestMount[] Mounts = Array.Empty<TestMount>();
        public static T[] FindObjectsOfType<T>() where T : class => (T[])(object)Mounts;
    }
}

namespace TLDRevamp.Net
{
    internal static class EntitySeats
    {
        internal static void LocalSeat(out uint net, out int seat) => Entities.LocalSeat(out net, out seat);
        internal static long DriverSeatRefused;
        internal static void SeatCollisionIgnore(TestPlayer player, TestSeat seat) => Entities.SeatCollisionIgnore(player, seat);
    }
    internal static partial class EntityHands
    {
        internal static long WornRefused;
        internal static void ClearWorn(Entities.Ent e) { e.WornBy = -1; }
    }
    internal static class EntityCarTuning { internal static void CopyTanksOnOwnerChange(Entities.Ent e, bool proxy) { } }
    public static partial class Entities
    {
        static long GameFrozenReleased, RagdollLimbsRestored, OwnerClockResets;
        static readonly Dictionary<TestCar, float> DamageGraceUntil = new Dictionary<TestCar, float>();
        static readonly Dictionary<object, Ent> ProxyWheelOwner = new Dictionary<object, Ent>();
        static readonly Dictionary<object, int> ProxyWheelIndex = new Dictionary<object, int>();
        static readonly Dictionary<object, Ent> ProxyEngineOwner = new Dictionary<object, Ent>();
        static int MarkersStaleCalls;
        static void MarkersStale() => MarkersStaleCalls++;   // the real one only flags a later prune (Entities.Placed)
        internal static bool TryGetEntity(uint net, out Ent e) => ByNet.TryGetValue(net, out e);
        internal static bool LooseCarriedRelease(uint previous, uint car, bool held, bool stored) => LooseRelease(previous, car, held, stored);
        internal static bool ClaimReleasedCarried(Ent e) => ClaimCargo(e, released: true, reason: ClaimReason.CargoRelease);
        internal static bool TryLockedHandWorld(Ent e, out Vector3d pos, out Quaternion rot) => LockedWorld(e, out pos, out rot);
        static void ShowStored(Ent e, bool show) { }
        static void ShowHeld(Ent e, bool show) { e.Held = show; }
        public static bool GhostEndsOnOwn = true;
        static void EndGhostOnOwn(Ent e) { e.ReleaseGhost = false; }
        static void ShowCarriedCollisions(Ent e) { }
        static bool LetGoClaims = true, SeamlessClaims;
        static float LetGoClaimS = 3f;
        static long LetGoClaimTries;
        static void PlaceAtNewest(Ent e) { }
        static bool HostGrabLetGo = true;
        static TestPickup _grabRefusedHere;
        static long PickupLocksSkipped;
        static bool ClaimTrace;
        static void TraceClaim(Ent e, ClaimReason reason) { }
        static void DropPhysical(Ent e) { }
        static void SetPartColliders(Ent e, bool on) { }
        static void DetachContact(Ent e) { }
        static void AttachContact(Ent e) { }
        static Rigidbody[] HitchBodies(Ent e) => Array.Empty<Rigidbody>();
        static bool Ridden(Ent e) => false;
        static object[] WheelsOf(Ent e) => Array.Empty<object>();
        static void BindProxyEngine(Ent e) { }
    }
}

namespace TLDRevamp { static class DevHooks { internal static System.Func<tosaveitemscript, bool> HeldItem; } }
