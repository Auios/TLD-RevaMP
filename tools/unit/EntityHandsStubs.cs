using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public partial class Rigidbody { public bool detectCollisions; public Vector3 PointVelocity; public Vector3 GetPointVelocity(Vector3 p) => PointVelocity; }
    public class MonoBehaviour : Component { }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public partial struct Vector3d
    {
        public static explicit operator Vector3(Vector3d p) => new Vector3((float)p.x, (float)p.y, (float)p.z);
        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3d operator *(Vector3d a, double b) => new Vector3d(a.x * b, a.y * b, a.z * b);
    }
    public partial struct Quaternion
    {
        public static float Angle(Quaternion a, Quaternion b) => 0;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float k) => k < 1 ? a : b;
    }
    public partial struct Vector3 { public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude; }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public float magnitude => (float)Math.Sqrt(x * x + y * y); }
}
public partial class tosaveitemscript { public pickupable P; }
public class wearable : Component { public bool wearing; public int tipus, player; public Collider[] c; }
public partial class pickupable : Component { public wearable wear; public tosaveitemscript tosave; }
public partial class fpscontroller
{
    public pickupable inHandP; public pickupable[] weared = new pickupable[16]; public HandAnim anim;
    public int TakeOffCalls, WearCalls;
    public void TakeOff(pickupable p, bool force) { TakeOffCalls++; weared[p.wear.tipus] = null; p.wear.wearing = false; }
    public void Wear(pickupable p) { WearCalls++; weared[p.wear.tipus] = p; p.wear.wearing = true; }
}
public class HandAnim { public Transform RHTarget, HHTarget; }
public partial class mpplayerscript { public HandAnim anim; }
public partial class itemdatabase { public GameObject[] items; }
public class PoseInterpolator
{
    public struct Sample { public Vector3d Pos; public Quaternion Rot; public Vector3 Vel; }
    public bool Ready; public Sample Value;
    public void Evaluate(double now, float dt, out Vector3d pos, out Quaternion rot) { pos = Value.Pos; rot = Value.Rot; }
}
namespace TLDRevamp.Net
{
    internal static class RecordCodec { internal static long WornStripped; }
    public static partial class RemotePlayers
    {
        internal sealed class RemoteHand { public bool Has; public GameObject Go; public mpplayerscript Mp; }
        internal static readonly Dictionary<int, RemoteHand> All = new Dictionary<int, RemoteHand>();
        internal static Transform Head, Body;
        internal static Transform WearAnchor(int id) => Head;
        internal static Transform BodyOf(int id) => Body;
    }
    public static partial class Entities
    {
        public sealed partial class Ent
        {
            public uint CarriedBy, HandEpoch;
            public int HandOwner, WornBy = -1, WornSlot = -1, SentWornSlot = -1, WornClaims;
            public bool HandPose, Held, Stored, PoseLocked, MovedByBody, Physical;
            public bool SentHeld; public double FastSendUntil;
            public double HandSince; public float WornClaimAt;
            public Vector3 HandFrom, HandPos, WornLp, LastVel;
            public Quaternion HandFromRot, HandRot, HandWorldRot, WornLq, CarriedGlobalRot;
            public Vector3d HandWorld, ShownPos, CarriedGlobal;
            internal readonly HandItemGlide HandDraw = new HandItemGlide();
            public tosaveitemscript WornOffFor; public PoseInterpolator Ip;
        }
        private sealed partial class SEnt
        {
            public int WornBy = -1, WornSlot = -1, PrevOwnerId = -1;
            public float PrevOwnerAt;
            public Vector3 WornLp; public Quaternion WornLq;
        }
        internal static bool TestEpoch = true, CargoClaims = true, TestCargoClaim;
        internal static int Stops, Exits;
        internal static float HandoverAcceptS = 2;
        private static bool ClaimEpochOk(SEnt se, int from, uint epoch) => TestEpoch;
        internal static void SetProxy(Ent e, bool proxy) { e.Proxy = proxy; }
        internal static void StopBodyMotion(Ent e) { Stops++; e.MovedByBody = false; }
        private static void ExitPhysical(Ent e, Rigidbody body) { Exits++; e.Physical = false; }
        internal static void Coast(Ent e) { }
        private static bool LockedWorld(Ent e, out Vector3d p, out Quaternion q) { p = default; q = Quaternion.identity; return false; }
        private static bool LooseRelease(uint previous, uint car, bool held, bool stored) => previous != 0 && car == 0 && !held && !stored;
        private static bool ClaimCargo(Ent e, bool released = false, ClaimReason reason = ClaimReason.CargoNear) => TestCargoClaim;
    }
}
