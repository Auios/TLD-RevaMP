using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarmonyLib { public class HarmonyTargetMethods : Attribute { } }

public partial class nyulscript
{
    public aiscript AI; public movecreaturescript move; public sensoryscript senses;
    public bool started, attacking, roaring, idleSound, alreadyEvil;
    public float size, runValue, targetForgetTime = 10, lastRoarTime, lastAttackTime, lastSeeTarget, lastIdle, currentIdle, idleDistance = 100, minIdle = 1, maxIdle = 2;
    public GameObject playerDeathTrigger, GIdleSound; public playerdeathtriggerscript attackTriggerScript; public breakablescript breakable;
    public RabbitAnim anim; public AudioClip[] Sroar, Sattack, Sidle, Sdie;
    public static Action DuringEvil;
    public void SetBasedOnSize(float value) { size = value; started = true; }
    public void GetEvil(bool natural) { alreadyEvil = true; DuringEvil?.Invoke(); }
    public void Roar() { } public void Attack() { } public void Die() { }
}
public class RabbitAnim { public bool isActiveAndEnabled = true; public float Run; public string Trigger; public void SetFloat(string name, float value) { Run = value; } public float GetFloat(string name) => Run; public void SetTrigger(string name) { Trigger = name; } }
public class movecreaturescript : Component { public Rigidbody RB; public bool modifier; public float speed, moveRot; public Vector3 moveDir; }
public class sensoryscript : Component { public bool hasEyes = true, hasEars = true; public void LooseEyes() { hasEyes = false; } public void LooseEars() { hasEars = false; } }
public class playerdeathtriggerscript : Component { public Collider selfCol; public float damagePlayer = 10; }
public partial class aiscript { public bool goToTarget; public Vector3 lastVelocity, interestPos; public movecreaturescript move; public ragdollactivatescript ragdoll; }
public partial class tosaveitemscript { public nyulscript nyul; }
public partial class carscript { public Rigidbody RB; }
namespace UnityEngine
{
    public partial class Rigidbody { public float mass = 1; }
    public partial struct Vector3 { public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - n * Dot(v, n); public static float SignedAngle(Vector3 a, Vector3 b, Vector3 n) => 0; }
    public partial struct Bounds { public Vector3 extents; public void Expand(float v) { extents += Vector3.one * (v / 2); } public bool Contains(Vector3 p) => Math.Abs(p.x - center.x) <= extents.x && Math.Abs(p.y - center.y) <= extents.y && Math.Abs(p.z - center.z) <= extents.z; }
}
namespace TLDRevamp.Fixes { internal static class Rabbits { internal static string Stats() => "{}"; } }
public partial class mainscript { public static GameObject PlayClipAtPoint(AudioClip clip, Vector3 p, float vol, float pitch, bool spati, int priority) => new GameObject(); }
namespace TLDRevamp
{
    internal static partial class GameLookups { internal static System.Reflection.MethodInfo[] Methods(Type type, string name, System.Reflection.BindingFlags flags) => type.GetMethods(flags); }
}
namespace TLDRevamp.Net
{
    internal static partial class RecordCodec
    {
        internal static RabbitRecord Record;
        internal static RabbitRecord Decode(byte[] rec) => Record;
        internal sealed class RabbitRecord
        {
            internal readonly List<RabbitItem> items = new List<RabbitItem>(), nyul = new List<RabbitItem>(), car = new List<RabbitItem>();
        }
        internal sealed class RabbitItem { public uint id; public int prefabID; public RabbitTransform transform = new RabbitTransform(); }
        internal sealed class RabbitTransform { public Vector3d pos; }
    }
    public static partial class Entities
    {
        public sealed partial class Ent { public bool CrashTaking; public float OwnerAt; public byte[] RabbitSnapshot; }
        private sealed partial class SEnt { public float Speed; public uint LockFrame, RabbitHandoverEpoch; public int TelemetryCategory; public byte[] RabbitSnapshot; }
        internal static long StatesSkippedRabbitAnimation;
        internal static float HandoverWindowS = 2, CargoClaimCooldownS = .25f;
        internal static readonly List<carscript> _localCars = new List<carscript>();
        internal static readonly Dictionary<int, HashSet<uint>> Known = new Dictionary<int, HashSet<uint>>();
        internal static readonly HashSet<int> RabbitFar = new HashSet<int>();
        private static HashSet<uint> KnownBy(int id) { if (!Known.TryGetValue(id, out var ids)) Known[id] = ids = new HashSet<uint>(); return ids; }
        private static bool RabbitReceiverNear(int id, SEnt se) => !RabbitFar.Contains(id);
        private static Bounds CarLocalBounds(carscript car) => new Bounds { extents = Vector3.one };
        private static Ent ProxyOf(Transform root) { foreach (var e in ByNet.Values) if (e.Proxy && e.Root?.transform.root == root) return e; return null; }
        private static int TelemetryPrefabCategory(int prefab) => prefab;
    }
}
