using System;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public partial struct Bounds
    {
        public Vector3 size { get => extents * 2; set => extents = value * .5f; }
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;
        public Bounds(Vector3 c, Vector3 s) { center = c; extents = s * .5f; }
        public void Encapsulate(Vector3 p) { var lo = min; var hi = max; lo = new Vector3(Math.Min(lo.x, p.x), Math.Min(lo.y, p.y), Math.Min(lo.z, p.z)); hi = new Vector3(Math.Max(hi.x, p.x), Math.Max(hi.y, p.y), Math.Max(hi.z, p.z)); center = (lo + hi) * .5f; extents = (hi - lo) * .5f; }
    }
    public partial struct Vector3 { public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
    public partial class Rigidbody { public float maxDepenetrationVelocity; }
    public static partial class Physics
    {
        public static void SyncTransforms() { }
        public static int OverlapBoxNonAlloc(Vector3 c, Vector3 e, Collider[] hits, Quaternion q, int mask, QueryTriggerInteraction t) { int n = Math.Min(hits.Length, Overlaps.Length); Array.Copy(Overlaps, hits, n); return n; }
    }
}
public partial class settingsscript { public class setting : Settings { } }
public partial class PoseInterpolator { public Quaternion LastRot = Quaternion.identity; }
public partial class pickupable
{
    public bool cantPickupWhileMount;
    public void JustUnPhysicsLock() { physlock = null; }
    public void RefreshCrafted(bool a, bool b) { crafted = a; }
}
public partial class mountStuff
{
    public int Stores;
    public void Store(pickupable p, bool b) { Stores++; p.physlock = this; }
    public void QuickFreeze() { }
    public void Scan() { }
}
public class layerScript : Component { public pickupable p; }
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public sealed partial class Ent
        {
            public double LockOffSince;
            public float ReleaseClearSince = -1;
            public uint LockFrame, LockContainer, ReceivedStateEpoch;
            public int LockItem = -1, LockMount = -1;
            public Vector3 LockLocalPos;
            public Quaternion LockLocalRot = Quaternion.identity, FarRot = Quaternion.identity;
            public Vector3d FarPos;
            public mountStuff PoseLockMount;
        }
        private sealed partial class SEnt { public Vector3 LockLocalPos; public Quaternion LockLocalRot, Rot; }
        private static bool RidesParent(Ent e) => e.Root?.attachable?.attached == true;
        private static bool RidesBolted(Ent e) => RidesParent(e);
        private static bool AttachedCarPart(Ent e) => false;
        private static void ShowCarriedCollisions(Ent e) { foreach (var rb in e.Root.GetComponentsInChildren<Rigidbody>(true)) rb.detectCollisions = true; }
    }
}
