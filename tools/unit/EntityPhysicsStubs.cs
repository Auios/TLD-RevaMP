using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public enum ConfigurableJointMotion { Locked, Free }
    public class ConfigurableJoint : Joint { public ConfigurableJointMotion xMotion, yMotion, zMotion; }
    public enum ForceMode { Impulse }
    public partial class Object { public static void Destroy(Component component) { } }
    public partial class Rigidbody
    {
        public void AddForceAtPosition(Vector3 force, Vector3 point, ForceMode mode) => AddForceAtPosition(force, point);
        public void MovePosition(Vector3 p) { position = p; }
        public void MoveRotation(Quaternion q) { rotation = q; }
    }
    public static class PhysicsComponentExtensions
    {
        public static T[] GetComponents<T>(this Component c) where T : class => c.Children.ConvertAll(v => v as T).FindAll(v => v != null).ToArray();
    }
    public partial struct Vector3
    {
        public static Vector3 ClampMagnitude(Vector3 v, float max) => v.magnitude > max ? v.normalized * max : v;
    }
    public partial struct Quaternion { public void ToAngleAxis(out float a, out Vector3 axis) { a = 0; axis = Vector3.up; } }
    public partial struct Bounds { public bool Intersects(Bounds b) => Math.Abs(center.x - b.center.x) <= extents.x + b.extents.x && Math.Abs(center.y - b.center.y) <= extents.y + b.extents.y && Math.Abs(center.z - b.center.z) <= extents.z + b.extents.z; }
    public static partial class Time { public static float fixedDeltaTime = .02f; }
}
public partial class carscript { public void Throttle() { } }
public partial class PoseInterpolator
{
    public float CorrCm; public bool Snap;
    public bool TakeSnap() { bool snap = Snap; Snap = false; return snap; }
    public void Absorb(Vector3d p, Quaternion q) { LastPos = p; LastRot = q; }
    public double NewestAgeS(double now) => now;
}
public partial class mainscript { public static RigidbodyInterpolation RBI() => RigidbodyInterpolation.None; }
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public sealed partial class Ent
        {
            public RigidbodyInterpolation OrigInterp, PhysInterp;
            public Transform[] LimbTransforms;
            public readonly List<Rigidbody> JointBodies = new List<Rigidbody>();
            public List<Collider> PartCols;
            public bool GhostOn, HasServoPrev;
            public float FreeUntil = -10, FreeFrom = -10, RestOffSince = -1, TelemetryServoCorrection;
            public Transform FreeHit;
            public Vector3d ServoPrevPos; public Quaternion ServoPrevRot;
        }
    }
    internal static class PhysicsFixture
    {
        internal const byte ContactImpulse = 36, CrashClaim = 43, CrashRelease = 44;
        internal static bool InSession = true, ResolveOk = true, Trailer, IsProxy, FollowingOk = true;
        internal static uint PhysicsFootingNet;
        internal static Action StandSeriesStep;
        internal static readonly Dictionary<uint, Entities.Ent> Local = new Dictionary<uint, Entities.Ent>();
        internal static readonly List<string> Events = new List<string>();
        internal static Action<NetWriter> DuringSend;
        internal static wheelgraphicsscript[] Wheels = Array.Empty<wheelgraphicsscript>();
        internal static List<tosaveitemscript> Tow = new List<tosaveitemscript>();
        internal static List<tosaveitemscript> TowPartners(tosaveitemscript root) => Tow;
        internal static Entities.Ent PhysicsRoot(Transform root) { foreach (var e in Local.Values) if (e.Root?.transform == root) return e; return null; }
        internal static bool PhysicsNetOf(Transform root, out uint net, out bool proxy) { var e = PhysicsRoot(root); net = e?.NetId ?? 0; proxy = e?.Proxy ?? false; return e != null; }
        internal static void PhysicsContacts(uint net, Collision c) => Events.Add("contacts:" + net);
        internal static bool TryPhysicsContact(uint net, out Entities.Ent e) => Local.TryGetValue(net, out e);
        internal static bool TryPhysicsStand(uint net, out Entities.Ent e) => Local.TryGetValue(net, out e);
        internal static bool ResolvePhysics(Entities.Ent e) => ResolveOk;
        internal static bool PhysicsTrailer(Entities.Ent e) => Trailer;
        internal static bool PhysicsIsProxy(Component c) => IsProxy;
        internal static wheelgraphicsscript[] PhysicsWheels(Entities.Ent e) => Wheels;
        internal static bool PhysicsPartCameOff(Entities.Ent e, int i) => e.Split?.Contains(i) ?? false;
        internal static Transform[] PhysicsLimbs(Entities.Ent e) => e.LimbTransforms;
        internal static bool Following(Entities.Ent e) => FollowingOk;
        internal static void SetCrashProxy(Entities.Ent e, bool proxy, bool keepMotion) { Events.Add("proxy:" + proxy + ":" + keepMotion); e.Proxy = proxy; }
        internal static void SendPhysicsRequest(NetWriter writer, bool reliable) { Events.Add("send:" + reliable + ":" + Convert.ToHexString(writer.Buf.AsSpan(0, writer.Len))); DuringSend?.Invoke(writer); }
        internal static void PhysicsSnap(Entities.Ent e, Vector3 target, Vector3 from) => Events.Add("snap:" + e.NetId);
        internal static void EvalShown(Entities.Ent e, double now, float dt, out Vector3d pos, out Quaternion rot) { pos = e.Ip.Value.Pos; rot = e.Ip.Value.Rot; }
        internal static int PingMs() => 0;
        internal static void Event(string kind, uint net = 0, uint epoch = 0, string result = null, float value = 0, float dx = 0, float dy = 0, int count = 0, bool sample = false) => Events.Add("event:" + kind + ":" + result);
    }
}

namespace UnityEngine { public partial class Collision { public Vector3 impulse; } public static partial class Mathf { public const float Deg2Rad = (float)(Math.PI / 180); } }
public partial class tosaveitemscript { public Rigidbody RB; }

namespace UnityEngine { public static partial class Mathf { public static float Clamp(double v, float min, float max) => Clamp((float)v, min, max); } }

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static uint _telemetryFooting;
        private static bool NetOf(Transform root, out uint net, out bool proxy) { var e = PlacedOfTransform(root); net = e?.NetId ?? 0; proxy = e?.Proxy ?? false; return e != null; }
        private static bool CameOff(Ent e, int item) => e.Split?.Contains(item) ?? false;
        private static void TelemetryProxySnap(Ent e, Vector3 target, Vector3 from) { }
        internal static void SetProxy(Ent e, bool proxy, bool keepMotion) { e.Proxy = proxy; }
    }
}

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static void CargoContact(uint net, Collision collision) => EntityCargoInteraction.CargoContact(net, collision);
        private static void ItemContact(uint net, Collision collision) => EntityCargoInteraction.ItemContact(net, collision);
    }
}
