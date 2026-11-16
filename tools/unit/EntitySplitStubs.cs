using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type type, string method, params Type[] args) { } }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPostfix : Attribute { }
}

namespace UnityEngine
{
    public partial class Object
    {
        public string name;
        public static GameObject LastCreated;
        internal static object[] Found = Array.Empty<object>();
        internal static int Finds;
        public static T[] FindObjectsOfType<T>() { Finds++; return Found.OfType<T>().ToArray(); }
        public static GameObject Instantiate(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(); go.Storm.transform.position = pos; if (prefab.Part != null) { go.Part = (Component)Activator.CreateInstance(prefab.Part.GetType()); go.Part.Go = go; go.Part.name = prefab.Part.name + "(Clone)"; go.Part.transform.position = pos; go.Part.transform.rotation = rot; } LastCreated = go; return go;
        }
        public static void Destroy(GameObject go) { go.Destroyed = true; napszakvaltakozas.s.sandStorms.Remove(go.Storm); }
    }
    public partial class GameObject : Object { public Component Part; public Transform transform = new Transform(); public string name; public int layer; public sandstormscript Storm; public bool Destroyed, activeSelf = true, activeInHierarchy = true; public GameObject() { Storm = new sandstormscript { gameObject = this }; } public T GetComponent<T>() where T : class => Part as T ?? Storm as T; public T GetComponentInChildren<T>(bool all = false) where T : class => GetComponent<T>(); }
    public partial class Transform : Component { public Vector3 position, eulerAngles, localEulerAngles; }
    public partial class Light { public bool enabled; public float intensity; public GameObject gameObject = new GameObject(); }
    public class AudioSource : Component { public bool isPlaying; public float volume, pitch; public int priority; public AudioClip clip; }
    public class AudioClip { public string name; public float length; }
    public partial struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public static Quaternion Euler(float x, float y, float z) => identity;
    }
    public partial struct Vector3d { public double x, y, z; public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; } }
    public partial struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }
    public static partial class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float a) => Math.Abs(a);
        public static float Sqrt(float a) => (float)Math.Sqrt(a);
        public static float Clamp(float a, float min, float max) => Math.Clamp(a, min, max);
        public static int Clamp(int a, int min, int max) => Math.Clamp(a, min, max);
        public static int RoundToInt(float a) => (int)Math.Round(a);
        public static float Clamp01(float a) => Clamp(a, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
    public static partial class Time
    {
        public static float time, unscaledTime, realtimeSinceStartup, timeScale = 1, deltaTime;
        public static double timeAsDouble, timeSinceLevelLoadAsDouble, realtimeSinceStartupAsDouble;
    }
}

public partial class mainscript
{
    public static mainscript s;
    public List<radioCH> radioChs = new List<radioCH>(), radioChsAM = new List<radioCH>();
    public bool sleeping;
    public Vector3d visszarakva;
    public class radioCH
    {
        public bool custom; public int Index; public string name; public AudioClip currentClip; public float startTime, cTime;
        public int GetCurrentIndex() => Index;
        public void AdvanceRadio() { Index++; }
    }
    public void RADIO(List<radioCH> stations) { }
    public void RADIO2(List<radioCH> stations) { }
    public void RadioChannelFromMultiplayerUpd(int[] fm, float[] fmT, int[] am, float[] amT) { }
    public static Vector3d GlobalFromUnityPos(Vector3 p) => new Vector3d(p.x, p.y, p.z);
    public static Vector3 UnityPosFromGlobal(Vector3d p) => new Vector3((float)p.x, (float)p.y, (float)p.z);
}
public class napszakvaltakozas
{
    public static napszakvaltakozas s;
    public float tekeres, startTime, dt = 600, nt = 600;
    public List<sandstormscript> sandStorms = new List<sandstormscript>();
}
public class sandstormscript
{
    public Transform transform = new Transform(); public GameObject gameObject;
    public int prefabid; public float size, xdir, ydir, speed, life; public bool started;
}
public partial class itemdatabase { public static itemdatabase s; public GameObject[] storms; public GameObject Prefab; public GameObject Item(int id) => Prefab; }
public partial class tosaveitemscript : Component
{
    public carscript car;
    public headlightscript[] Headlights = Array.Empty<headlightscript>();
    public rearlights[] Rearlights = Array.Empty<rearlights>();
    public T[] GetComponentsInChildren<T>(bool all) => typeof(T) == typeof(headlightscript) ? (T[])(object)Headlights : typeof(T) == typeof(rearlights) ? (T[])(object)Rearlights : Children.OfType<T>().ToArray();
}
public class headlightscript { public usablescript usableSet; public int currentState; public Light[] Lights = Array.Empty<Light>(); public T[] GetComponentsInChildren<T>(bool all) => (T[])(object)Lights; }
public class rearlights { public GameObject[] FLight; public void Upd() { } }
public partial class usablescript : Component { public int currentTurnState; }
public partial class enginescript { public bool running, start; public gearstickscript gearstick; }
public partial class gearstickscript { public Transform TransmissionHandPos; }
public partial class carscript : Component
{
    public bool ignition, localIgnition, ihorn, isPlayerDriving2;
    public int gear; public Gear[] gears = new Gear[6];
    public float brake, handbrake, currenthorn, steerAngle = 30, steer, isteer, steerWheel, FsteerM = 2;
    public enginescript Engine; public usablescript lightUsable; public AudioSource SHornSound;
    public Transform TsteerWheel, TransmissionHandPos, HandBrakeHandPos, HornHandPos;
    public int Shifts, Handbrakes;
    public void ResetInput() { }
    public void ShiftTo(int value) { gear = value; Shifts++; }
    public void UpdMultiHandbrake(float value) { handbrake = value; Handbrakes++; }
}
public partial class settingsscript { public static settingsscript s; public Settings S = new Settings(); public class Settings { public bool BDevMode, CBenableHandBrake; public int IPhysicsLock; } }
public class kaposztaleves { public static kaposztaleves s; public Settings settings = new Settings(); public class Settings { public bool daynight = true; } } // test stub, not game code

namespace TLDRevamp
{
    internal static partial class GameLookups { internal static Type RuntimeFoundType(string name, Type found) => found; }
    internal static class Json { internal static string Str(string value) => System.Text.Json.JsonSerializer.Serialize(value); }
}

namespace TLDRevamp.Fixes
{
    internal static class SessionClocks
    {
        public static long RadioStale, RadioProjected, RadioClamped, RadioLocalAdvancesBlocked, WorldSent, WorldStale, WorldReceived, WorldSnapped, WorldSlewed;
    }
    internal static class CloudClock
    {
        public static double WorldTotal = 2400;
        public static float Progress = -1;
        public static float MySleepProgress() => 0.5f;
        public static void SleepProgress(float progress) => Progress = progress;
        public static void HostTotal(double total, float position, double stamp, float rate) { }
    }
    internal static class Sandstorms { public static void Prune() { } }
}

namespace TLDRevamp.Net
{
    public static partial class RemotePlayers
    {
        internal const float ReachS = .25f;
        internal static int Reaches;
        internal static Transform IgnitionPos(carscript car) => null;
        internal static void DriverReach(carscript car, Transform target, float seconds) { Reaches++; }
    }
    internal static class ModObjects { public static double ClockTime; }
    public static partial class Entities
    {
        public sealed partial class Ent
        {
            public int PartIndex = -1, SigGear, SentSigKey = -1, SigRepeat;
            public bool Proxy, SigHas, SigIgnition, SigBrake, SigHandbrake;
            public float SigHorn, SigSteer, SteerWheelShown;
            public tosaveitemscript Root;
        }
        internal static readonly Dictionary<uint, Ent> ByNet = new Dictionary<uint, Ent>();
        private static void RemoveSavedRecord(uint id) => EntityLifetimeStreaming.RemoveRecord(id);   // the kernel also drops component rows and chunk ids
        public const byte SleepSync = 38;
        public const byte State = 23, DetachSync = 42;
        private static readonly NetWriter W = new NetWriter();
        internal static bool IsHost, InSession = true;
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        internal static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        internal static readonly List<(byte[] Bytes, bool Reliable, int Peer)> Packets = new List<(byte[], bool, int)>();
        internal static Action<int, NetReader> PeerReceive;
        private static void ServerSendTo(int playerId, NetWriter w, bool reliable)
        {
            Packets.Add((w.Buf.Take(w.Len).ToArray(), reliable, playerId));
            if (PeerReceive != null)
            {
                var r = new NetReader(); r.Load(w.Len); Array.Copy(w.Buf, r.Buf, w.Len); r.U8(); PeerReceive(playerId, r);
            }
        }
        internal static int LastExcept;
        internal static Action PeerDeliver;
        internal static Action<byte, NetReader> HostReceive;
        private static void ServerSendAll(NetWriter w, bool reliable, int except, Vector3d? near = null, double range = 0)
        {
            Packets.Add((w.Buf.Take(w.Len).ToArray(), reliable, -1)); LastExcept = except; PeerDeliver?.Invoke();
        }
        internal static void SendToServer(NetWriter w, bool reliable)
        {
            Packets.Add((w.Buf.Take(w.Len).ToArray(), reliable, -2));
            // The kernel captures a request before synchronous host dispatch can reuse a feature's writer.
            if (IsHost)
            {
                var r = new NetReader(); r.Load(w.Len); Array.Copy(w.Buf, r.Buf, w.Len); byte kind = r.U8();
                if (kind == SleepSync) EntityWorldClock.ServerSleepSync(0, r.F32(), r.F64(), r.F32()); else HostReceive?.Invoke(kind, r);
            }
        }
    }
}
