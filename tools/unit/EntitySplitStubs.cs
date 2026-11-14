using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPrefix : Attribute { }
}

namespace UnityEngine
{
    public class Object
    {
        public static GameObject Instantiate(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(); go.Storm.transform.position = pos; return go;
        }
        public static void Destroy(GameObject go) { go.Destroyed = true; napszakvaltakozas.s.sandStorms.Remove(go.Storm); }
    }
    public class GameObject { public sandstormscript Storm; public bool Destroyed; public GameObject() { Storm = new sandstormscript { gameObject = this }; } public T GetComponent<T>() where T : class => Storm as T; }
    public class Transform { public Vector3 position, eulerAngles; }
    public class AudioClip { public string name; public float length; }
    public struct Quaternion { public static Quaternion Euler(float x, float y, float z) => default; }
    public struct Vector3d { public double x, y, z; public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; } }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }
    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float a) => Math.Abs(a);
    }
    public static class Time
    {
        public static float time, unscaledTime, realtimeSinceStartup, timeScale = 1;
        public static double timeAsDouble, timeSinceLevelLoadAsDouble;
    }
}

public class mainscript
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
public class itemdatabase { public static itemdatabase s; public GameObject[] storms; }
public class settingsscript { public static settingsscript s; public Settings S = new Settings(); public class Settings { public bool BDevMode; } }
public class kaposztaleves { public static kaposztaleves s; public Settings settings = new Settings(); public class Settings { public bool daynight = true; } } // test stub, not game code

namespace TLDRevamp
{
    internal static class GameLookups { internal static Type RuntimeFoundType(string name, Type found) => found; }
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
    internal static class ModObjects { public static double ClockTime; }
    public static partial class Entities
    {
        public const byte SleepSync = 38;
        private static readonly NetWriter W = new NetWriter();
        internal static bool IsHost, InSession = true;
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        internal static readonly List<(byte[] Bytes, bool Reliable, int Peer)> Sent = new List<(byte[], bool, int)>();
        private static void ServerSendTo(int playerId, NetWriter w, bool reliable) => Sent.Add((w.Buf.Take(w.Len).ToArray(), reliable, playerId));
        private static void ServerSendAll(NetWriter w, bool reliable, int except, Vector3d? near = null, double range = 0) => Sent.Add((w.Buf.Take(w.Len).ToArray(), reliable, -1));
        internal static void SendToServer(NetWriter w, bool reliable)
        {
            Sent.Add((w.Buf.Take(w.Len).ToArray(), reliable, -2));
            // The kernel captures a request before synchronous host dispatch can reuse a feature's writer.
            if (IsHost)
            {
                var r = new NetReader(); r.Load(w.Len); Array.Copy(w.Buf, r.Buf, w.Len); r.U8();
                EntityWorldClock.ServerSleepSync(0, r.F32(), r.F64(), r.F32());
            }
        }
    }
}
