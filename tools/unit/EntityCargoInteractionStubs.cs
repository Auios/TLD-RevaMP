using System;
using System.Linq;
using UnityEngine;

// Game and kernel dependencies only. Cargo algorithms are compiled from the production files.
namespace UnityEngine
{
    public partial struct Vector3 { public static Vector3 down => -up; }
    public partial class GameObject { public T AddComponent<T>() where T : new() => new T(); }
    public partial class Rigidbody
    {
        public Vector3 worldCenterOfMass, Force, ForceAt;
        public int ForceCalls;
        public void AddForceAtPosition(Vector3 force, Vector3 point) { Force = force; ForceAt = point; ForceCalls++; }
    }
    public static partial class Physics
    {
        public static RaycastHit[] Sweeps = Array.Empty<RaycastHit>();
        public static int CapsuleCalls, BoxCalls;
        public static int OverlapCapsuleNonAlloc(Vector3 a, Vector3 b, float r, Collider[] hits, int mask, QueryTriggerInteraction query)
        { CapsuleCalls++; int n = Math.Min(hits.Length, Overlaps.Length); Array.Copy(Overlaps, hits, n); return n; }
        public static int BoxCastNonAlloc(Vector3 a, Vector3 b, Vector3 d, RaycastHit[] hits, Quaternion q, float distance, int mask, QueryTriggerInteraction query)
        { BoxCalls++; int n = Math.Min(hits.Length, Sweeps.Length); Array.Copy(Sweeps, hits, n); return n; }
        public static int RaycastNonAlloc(Vector3 a, Vector3 b, RaycastHit[] hits, float distance, int mask, QueryTriggerInteraction query)
        { int n = Math.Min(hits.Length, Hits.Length); Array.Copy(Hits, hits, n); return n; }
    }
    public static class CargoComponentExtensions
    {
        public static void GetComponentsInChildren<T>(this Component root, bool all, System.Collections.Generic.List<T> into)
        { into.Clear(); into.AddRange(root.Children.OfType<T>()); }
    }
}
public class CargoCapsule : Collider { public float radius = .5f, height = 2; public Vector3 center; }
public class pushablescript : Component
{
    public bool both, onlyProjected;
    public float modifier = 1;
    public void Push(Vector3 dir, Vector3 pos, bool sendMulti) { }
}
public class pushgripable : Component { public carscript car; }
public partial class fpscontroller
{
    public Rigidbody RB;
    public CargoCapsule CBody;
    public bool pushgripping;
    public pushgripable pushgrip;
    public int GripStops;
    public void StopPushGripping() { GripStops++; pushgripping = false; }
}
public partial class carscript { public pushgripable[] pushgrip; }
public partial class mainscript { public float pushForce = 5; }
public partial class PoseInterpolator
{
    public bool Newest(double now, out Vector3d pos, out Quaternion rot, out Vector3 vel)
    { pos = Value.Pos; rot = Value.Rot; vel = Value.Vel; return Ready; }
}
namespace TLDRevamp { internal static partial class DevHooks { internal static Action PushTick; } }
namespace TLDRevamp.Net
{
    internal sealed class BodyContactRelay : MonoBehaviour { }
    public static partial class Entities
    {
        public sealed partial class Ent { public float LetGoAt = -100; }
        public const byte PushIn = 53;
        internal static bool ItemClaims = true;
        internal static float ItemMinSpeed = .5f;
        internal static float CargoGhostMarginM = .1f;
        internal static long LetGoInCarClaimed;
        private static bool CargoBody(Ent e) => e.Root.P != null || Limbs(e) != null && Limp(e);
        private static carscript OverlappedCar(Ent e) => null;
        private static Ent CarEnt(carscript car) => ByNet.Values.FirstOrDefault(e => e.Root?.car == car);
    }
}
