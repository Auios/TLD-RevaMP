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
    internal static partial class EntitySeats
    {
        internal static long DriverSeatRefused;
        internal static void SeatCollisionIgnore(TestPlayer player, TestSeat seat) => Entities.SeatCollisionIgnore(player, seat);
    }
    internal static partial class EntityHands
    {
        internal static long WornRefused;
        internal static void ClearWorn(Entities.Ent e) { e.WornBy = -1; }
    }
    internal static partial class EntityCarTuning { internal static void CopyTanksOnOwnerChange(Entities.Ent e, bool proxy) { } }
    public static partial class Entities
    {
        internal static void TrackRagdollLimb(GameObject go, bool copy) { if (copy) ProxyItems.Add(go); else ProxyItems.Remove(go); }
        internal static bool RagdollRabbitBodyGone(aiscript rab) => false;
        static long GameFrozenReleased, RagdollLimbsRestored, OwnerClockResets;
        static readonly Dictionary<TestCar, float> DamageGraceUntil = new Dictionary<TestCar, float>();
        static readonly Dictionary<object, Ent> ProxyWheelOwner = new Dictionary<object, Ent>();
        static readonly Dictionary<object, int> ProxyWheelIndex = new Dictionary<object, int>();
        static readonly Dictionary<object, Ent> ProxyEngineOwner = new Dictionary<object, Ent>();
        static int MarkersStaleCalls;
        static void MarkersStale() => MarkersStaleCalls++;   // the real one only flags a later prune (Entities.Placed)
        internal static bool TryGetEntity(uint net, out Ent e) => ByNet.TryGetValue(net, out e);
        internal static bool LooseCarriedRelease(uint previous, uint car, bool held, bool stored) => EntityProxyPresentation.LooseRelease(previous, car, held, stored);
        internal static bool ClaimReleasedCarried(Ent e) => EntityCargoInteraction.ClaimCargo(e, released: true, reason: ClaimReason.CargoRelease);
        internal static bool TryLockedHandWorld(Ent e, out Vector3d pos, out Quaternion rot) => LockedWorld(e, out pos, out rot);
        static void ShowStored(Ent e, bool show) { }
        static void ShowHeld(Ent e, bool show) { e.Held = show; }

        static void ShowCarriedCollisions(Ent e) { }



        static bool HostGrabLetGo = true;
        static TestPickup _grabRefusedHere;
        static long PickupLocksSkipped;

        static bool Ridden(Ent e) => false;
        static void BindProxyEngine(Ent e) { }
    }
    internal static partial class EntityCargoInteraction
    {
        private static readonly NetWriter W = new NetWriter();
        internal static bool SeamlessClaims;
        internal static long LetGoClaimTries;
        internal static void PlaceAtNewest(Entities.Ent e) { }
        internal static void TraceClaim(Entities.Ent e, Entities.ClaimReason reason) { }
    }

    internal static partial class EntityLocks
    {
        internal static bool GhostEndsOnOwn = true;
        internal static void EndGhostOnOwn(Entities.Ent e) { e.ReleaseGhost = false; }
    }


    internal static partial class EntityCargoInteraction
    {
        internal static bool LetGoClaims = true;
        internal static float LetGoClaimS = 3f;
        internal static bool ClaimTrace;
    }


    internal static partial class EntityLifetimeStreaming
    {
    }


    internal static partial class EntityRagdolls
    {
    }


    internal static partial class EntityPhysics
    {
        internal static void DropPhysical(Entities.Ent e) { }
        internal static void SetPartColliders(Entities.Ent e, bool on) { }
        internal static void DetachContact(Entities.Ent e) { }
        internal static void AttachContact(Entities.Ent e) { }
    }


    internal static partial class EntityHitches
    {
        internal static Rigidbody[] HitchBodies(Entities.Ent e) => Array.Empty<Rigidbody>();
    }


    internal static partial class EntityProxyPresentation
    {
        internal static object[] WheelsOf(Entities.Ent e) => Array.Empty<object>();
    }

}

namespace TLDRevamp { static class DevHooks { internal static System.Func<tosaveitemscript, bool> HeldItem; } }

namespace TLDRevamp.Net { internal static partial class EntityHitches { internal static bool HasCommand(uint net) => false; internal static void HitchFreezeBodies(Entities.Ent e) { } } }
