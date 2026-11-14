using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace HarmonyLib
{
    public class HarmonyFinalizer : Attribute { }
    public class HarmonyPriority : Attribute { public int priority; public HarmonyPriority(int value) { priority = value; } }
    public static class Priority { public const int First = 800; }
    public static class AccessTools { public delegate ref F FieldRef<T, F>(T instance); }
}
namespace UnityEngine
{
    public class Component : Object
    {
        internal GameObject Go;
        public string name;
        public GameObject gameObject => Go ?? (Go = new GameObject());
        private Transform _transform;
        public Transform transform => this as Transform ?? (_transform ?? (_transform = new Transform()));
        public Component ParentComponent, ChildComponent;
        public T GetComponent<T>() where T : class => this as T ?? ChildComponent as T;
        public T GetComponentInParent<T>() where T : class => ParentComponent as T;
        public T GetComponentInChildren<T>() where T : class => ChildComponent as T;
        public T[] GetComponentsInChildren<T>() => Children.OfType<T>().ToArray();
        public readonly List<Component> Children = new List<Component>();
        public bool CompareTag(string tag) => Tag == tag;
        public string Tag;
    }
    public partial class Transform
    {
        public Transform parent;
        public Transform root => parent?.root ?? this;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one, lossyScale = Vector3.one, forward = Vector3.forward;
        public int childCount => Children.Count;
        public Transform GetChild(int index) => (Transform)Children[index];
        public void SetParent(Transform value, bool worldPositionStays) { parent = value; value?.Children.Add(this); }
        public Vector3 InverseTransformPoint(Vector3 p) => p - position;
        public Vector3 TransformPoint(Vector3 p) => p + position;
        public Vector3 InverseTransformDirection(Vector3 p) => p;
        public Vector3 TransformDirection(Vector3 p) => p;
    }
    public partial struct Vector3
    {
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this * (1 / magnitude) : zero;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a) => a * -1;
        public static Vector3 operator *(Vector3 a, float n) => new Vector3(a.x * n, a.y * n, a.z * n);
        public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
        public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);
        public override bool Equals(object value) => value is Vector3 v && x == v.x && y == v.y && z == v.z;
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }
    public partial struct Quaternion
    {
        public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        public static Quaternion LookRotation(Vector3 n) => identity;
        public static Quaternion AngleAxis(float a, Vector3 n) => identity;
        public static Quaternion operator *(Quaternion a, Quaternion b) => b;
    }
    public class Collider : Component { public bool isTrigger, enabled = true; public Bounds bounds; }
    public struct Bounds { public Vector3 center; public float SqrDistance(Vector3 p) => (p - center).sqrMagnitude; }
    public struct RaycastHit { public Collider collider; public Vector3 point, normal; }
    public struct Ray { public Ray(Vector3 p, Vector3 n) { } }
    public enum QueryTriggerInteraction { Collide }
    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;
        public static Collider World;
        public static bool Raycast(Vector3 p, Vector3 n, out RaycastHit hit, float d, int mask, QueryTriggerInteraction q) { hit = new RaycastHit { collider = World }; return World != null; }
    }
    public static class Random { public static int Range(int a, int b) => a; public static float Range(float a, float b) => a; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => new Color(1, 1, 1, 1); }
    public struct MinMaxGradient { public Color color; public static implicit operator MinMaxGradient(Color c) => new MinMaxGradient { color = c }; }
    public class ParticleSystem
    {
        public class Main { public MinMaxGradient startColor; }
        public class Emission { public bool enabled; }
        public Main main = new Main(); public Emission emission = new Emission(); public bool isPlaying; public int particleCount;
    }
}
public partial class mainscript
{
    public fpscontroller player;
    public GameObject[] ShootDecals = Array.Empty<GameObject>();
    public GameObject tankcaphole, ExplosionRed, ExplosionBlue, ExplosionPaint;
    public static byte[] AudioPriorities = new byte[10];
    public enum walkSoundType { sand }
    public enum particle { sand, spark }
    public static AudioClip GetWalkClip(walkSoundType type, int clip) => new AudioClip();
    public static GameObject PlayClipAtPoint(AudioClip clip, Vector3 p, float vol, params object[] args) => new GameObject();
    public static int GetDecal(Collider c) => 0;
    public void ShootStart(Vector3 p, Vector3 d) { }
    public void ShootEnd(Vector3 p, Vector3 d) { }
    public void Particle(particle t, Vector3 p, Vector3 n, int count) { }
    public GameObject GetExplosionType(int type) => type == 0 ? ExplosionRed : type == 2 ? ExplosionBlue : type == 3 ? ExplosionPaint : null;
}
public partial class tosaveitemscript { public tankscript[] tanks; public bool Held; }
public class weaponscript : Component
{
    public RaycastHit[] hit = Array.Empty<RaycastHit>(); public int tempInt; public bool alreadyShotFalse, automaticFalseShot;
    public float ActuallyShotTime = -1, ShotTime = -1;
    public Transform S = new Transform();
    public AudioClip[] ShotHitSand, ShotHitGlass, ShotHitFlesh, ShotHitConcrete;
    public void Shot() { }
    public void RShot(int s, int sound) { }
}
public class stepSoundScript : Component
{
    public mainscript.walkSoundType currentType; public float StepSoundVolume, stepSound, syncMultiplier; public AudioClip StepSoundClip; public int currentClipIndex;
    public void Step(float modifier) { }
}
public class fpscontroller : Component { public stepSoundScript stepScript; public AudioSource SBurp, SFart; public pickupable pickedUp; public void Burp() { } public void Fart() { } public void Shit() { } }
public class mpplayerscript : Component { }
public enum soundenum { burp, fart, shit }
public class soundMainScript { public static soundMainScript s = new soundMainScript(); public int GetLength(soundenum e) => 400; public AudioClip GetClip(soundenum e, int i) => new AudioClip(); }
public class pickupable { public sprayscript spray; public bool inRightHand; }
public class sprayscript : Component { public float valve, valveSpeed = 4; public bool hasPaint, firing; public pickupable P; public ParticleSystem particle; public AudioSource Saudio; }
public class fluid { public bool isExplosive() => true; }
public class tankscript : Component
{
    public bool neverExplode, alreadyExploded, alwaysExplode; public fluid F = new fluid(); public float explosionForceNeededToExplode;
    public int Calls; public void TryExplode(float forceAdd, bool add) { Calls++; }
}
public class tankcapscript : Component { public bool disableCollider, canToggle, canTune, noText; public float radius, valve, defPourAngle; public tankscript Tank; public void FStart() { } }
public class explosivescript : Component { public float forceNeeded; public int Calls; public void TryExplode(float force) { Calls++; } }
public class explosionscript : Component
{
    public ParticleSystem particle = new ParticleSystem(); public static Action<explosionscript> During;
    public void Explode(float pF, float distAdd) => During?.Invoke(this);
}
namespace TLDRevamp
{
    internal static partial class GameLookups
    {
        internal static readonly List<string> FieldRefs = new List<string>();
        internal static HarmonyLib.AccessTools.FieldRef<T, F> FieldRefAccess<T, F>(Type type, string name)
        {
            FieldRefs.Add(type.Name + "." + name);
            var method = new DynamicMethod("ref", typeof(F).MakeByRefType(), new[] { typeof(T) });
            var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda, type.GetField(name)); il.Emit(OpCodes.Ret);
            return (HarmonyLib.AccessTools.FieldRef<T, F>)method.CreateDelegate(typeof(HarmonyLib.AccessTools.FieldRef<T, F>));
        }
    }
    internal static class Plugin { internal static LogSink Log = new LogSink(); internal class LogSink { public void LogWarning(string message) { } } }
    internal static class DevHooks { internal static Action SprayTick; }
}
namespace TLDRevamp.Net
{
    internal static class PlayerCombat { internal static int LastN, LastPlayerHit = -1; internal static float LastPlayerT; internal static Vector3 LastOrigin, LastDir; }
    internal static partial class RemotePlayers
    {
        internal static Transform Bone;
        internal static Transform BodyPart(int id, int part) => Bone;
        internal static bool BodyHit(int id, Ray ray, float max, out int part, out RaycastHit hit) { part = 0; hit = new RaycastHit { point = new Vector3(2, 3, 4), normal = Vector3.up }; return Bone != null; }
    }
    internal static class HitCheck { internal const float ObjShotM = 1, ObjSizeM = 1; internal static bool Allow = true; internal static bool AllowOnObject(int from, string why, Vector3d at, float a, float b) => Allow; }
    public static partial class Entities
    {
        public sealed partial class Ent { public uint NetId; public List<tosaveitemscript> Items = new List<tosaveitemscript>(); public bool Spraying; }
        internal class SEnt { public int OwnerId; public bool HasState; public Vector3d Pos, Where; }
        internal static readonly Dictionary<uint, SEnt> Server = new Dictionary<uint, SEnt>();
        private static readonly NetWriter WS = new NetWriter();
        internal static bool Finite(Vector3d v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        internal static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w);
        internal static bool IsProxy(Component c) => c != null && ByNet.Values.Any(e => e.Proxy && (c.ParentComponent == e.Root || c == e.Root || e.Root?.tanks?.Contains(c) == true));
        internal static bool IsHeld(tosaveitemscript it) => it.Held;
        internal static bool Resolve(Ent e) => e.Root != null;
        internal static Ent PlacedOfTransform(Transform root) => ByNet.Values.FirstOrDefault(e => e.Root?.transform.root == root);
        private static IEnumerable<Ent> LookupSet() => ByNet.Values;
        internal static List<tankscript> TanksOf(Ent e) => e.Items.Where(it => it.tanks != null).SelectMany(it => it.tanks).ToList();
        internal static tankscript TankOf(uint net, int ti) => ByNet.TryGetValue(net, out var e) && ti >= 0 && ti < TanksOf(e).Count ? TanksOf(e)[ti] : null;
        internal static void ToServer(NetWriter w, bool reliable) => SendToServer(w, reliable);
    }
}
