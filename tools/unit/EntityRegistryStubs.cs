#pragma warning disable CS8981
global using tosaveitemscript = RegistryFakes.Item;
global using savedatascript = RegistryFakes.Save;
global using mainscript = RegistryFakes.Main;
global using carscript = RegistryFakes.Car;
global using wheelgraphicsscript = RegistryFakes.Wheel;
global using ragdollactivatescript = RegistryFakes.Ragdoll;
global using itemPlaceRemoveScript = RegistryFakes.Placer;
global using menuhandler = RegistryFakes.Menu;
global using attachablescript = RegistryFakes.Attach;
global using ItemSnapshot = RegistryFakes.Snapshot;
global using RecordCodec = RegistryFakes.Codec;
using System;
using System.Collections.Generic;
using System.Linq;
using TLDRevamp.Net;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        public bool Dead;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b) || ReferenceEquals(a, null) && !ReferenceEquals(b, null) && b.Dead || ReferenceEquals(b, null) && !ReferenceEquals(a, null) && a.Dead;
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object o) { RegistryFakes.Trace.Add("destroy-queued:" + RegistryFakes.Label(o) + ":suppress=" + Entities.SuppressDestroyEcho); RegistryFakes.Deferred.Add(o); }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T GetComponentInChildren<T>() where T : class => gameObject.GetComponentInChildren<T>();
        public T[] GetComponentsInChildren<T>(bool active = true) where T : class => gameObject.GetComponentsInChildren<T>(active);
    }
    public class GameObject : Object
    {
        public string name;
        public bool activeSelf = true, activeInHierarchy = true;
        public Transform transform;
        public readonly List<Component> Components = new();
        public GameObject(string name = "object") { this.name = name; transform = new Transform { gameObject = this }; Components.Add(transform); }
        public T AddComponent<T>() where T : Component, new() { var c = new T { gameObject = this }; Components.Add(c); return c; }
        public T GetComponent<T>() where T : class => Components.OfType<T>().FirstOrDefault(c => c is not Object o || !o.Dead);
        public T GetComponentInChildren<T>() where T : class => GetComponentsInChildren<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool active = true) where T : class => Components.OfType<T>().Concat(transform.Children.SelectMany(t => t.gameObject.GetComponentsInChildren<T>(active))).ToArray();
        public void SetActive(bool on) { activeSelf = on; RegistryFakes.Trace.Add("visible:" + name + ":" + on); }
    }
    public class Transform : Component
    {
        public Transform parent;
        public List<Transform> Children = new();
        public Vector3 position, eulerAngles;
        public Quaternion rotation = Quaternion.identity;
        public Transform root => parent?.root ?? this;
        public Vector3 forward => new Vector3(0, 0, 1);
        public bool IsChildOf(Transform t) => this == t || parent != null && parent.IsChildOf(t);
        public T GetComponentInParent<T>() where T : class => GetComponent<T>() ?? parent?.GetComponentInParent<T>();
        public void SetPositionAndRotation(Vector3 p, Quaternion q) { position = p; rotation = q; RegistryFakes.Trace.Add("pose:" + gameObject.name + ":" + p); }
        public Vector3 InverseTransformPoint(Vector3 p) => p - position;
    }
    public class Rigidbody : Component { public bool isKinematic; public Vector3 velocity; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 Project(Vector3 a, Vector3 b) => new(0, 0, a.z);
        public override string ToString() => $"{x},{y},{z}";
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new(0, 0, 0, 1);
        public static Quaternion Euler(Vector3 v) => new(v.x, v.y, v.z, 1);
        public static Quaternion Inverse(Quaternion q) => new(-q.x, -q.y, -q.z, q.w);
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(a.x + b.x, a.y + b.y, a.z + b.z, 1);
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;
        public Vector3 eulerAngles => new(x, y, z);
    }
    public static class Time { public static float realtimeSinceStartup = 10, time = 10; public static double unscaledTimeAsDouble => time; public static int frameCount = 1; }
    public static class StackTraceUtility { public static string ExtractStackTrace() => "fixture-stack"; }
}
public struct Vector3d
{
    public double x, y, z;
    public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
    public static Vector3d zero => default;
    public double magnitude => Math.Sqrt(x * x + y * y + z * z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    public static explicit operator Vector3(Vector3d a) => new((float)a.x, (float)a.y, (float)a.z);
    public override string ToString() => $"{x},{y},{z}";
}
public class vector3 { public Vector3 v; public vector3(Vector3 v = default) { this.v = v; } public Vector3 Load() => v; } // test stub, not game code
public class posRotd { public Vector3d pos; public vector3 rot; public posRotd(Vector3d p = default, Vector3 r = default) { pos = p; rot = new(r); } } // test stub, not game code
public class save_item { public uint id; public int prefabID; public posRotd transform = new(); } // test stub, not game code
public class save_attachable { public enum attachTypeEmu { toItem = 1 } public uint id, parentid; public int attachType, index, indexType; } // test stub, not game code
public class save_tankscript { public uint id; public save_tankscript() { } public save_tankscript(save_tankscript t) { id = t.id; } } // test stub, not game code
public class save_lock { public uint id; public uint[] IDs; } // test stub, not game code
public class itemDataClass // test stub, not game code
{
    public List<save_item> items = new(); public List<save_attachable> attachable = new(); public List<save_tankscript> tanks = new(); public List<save_lock> physLocks = new();
    public void Append(itemDataClass d) { items.AddRange(d.items); attachable.AddRange(d.attachable); tanks.AddRange(d.tanks); physLocks.AddRange(d.physLocks); }
}
public class mountStuff : Component { } // test stub, not game code
public class pickupable : Component // test stub, not game code
{
    public string name => gameObject.name;
    public mountStuff physlock; public Transform disableThisWhenStored; public bool crafted; public RegistryFakes.Wear wear;
    public int Inventory; public int inInventory() => Inventory;
}

public static class RegistryFakes
{
    public static readonly List<string> Trace = new();
    public static readonly List<UnityEngine.Object> Deferred = new();
    public static string Label(object o) => o switch { GameObject g => g.name, Component c => c.gameObject.name + "/" + c.GetType().Name, _ => o?.ToString() ?? "null" };
    public static void Flush()
    {
        var list = Deferred.ToArray(); Deferred.Clear();
        foreach (var o in list) { if (o.Dead) continue; Trace.Add("destroy-now:" + Label(o)); o.Dead = true; if (o is GameObject g) foreach (var c in g.Components) { c.Dead = true; if (c is Item it) { Save.s.items.Remove(it.idInSave); Entities.DestroyHook(it); } } }
    }
    public class Item : Component
    {
        public uint idInSave; public int id; public string name => gameObject.name;
        public Car car; public Attach attachable; public pickupable P; public Rigidbody RB; public bool buried;
        public bool Savable = true; public List<Item> Group;
        public void JustDestroy() => UnityEngine.Object.Destroy(gameObject);
        public void DistUnFreezeRB() { RB.isKinematic = false; Trace.Add("unfreeze:" + name); }
    }
    public static Item Make(uint id, string name, bool car = false)
    {
        var g = new GameObject(name); var i = g.AddComponent<Item>(); i.idInSave = id; i.id = (int)id; i.RB = g.AddComponent<Rigidbody>(); i.Group = new() { i };
        if (car) { i.car = g.AddComponent<Car>(); i.car.Engine = new object(); g.AddComponent<Wheel>(); }
        Save.s.items[id] = i; return i;
    }
    public class Car : Component { public object Engine; public float speed, lspeed; public Vector3 LV; }
    public class Wheel : Component { }
    public class Ragdoll : Component { public Rigidbody[] RBs; public bool ragdoll; }
    public class Attach : Component { public bool attached; public object utanfuto; public Transform point; public Slot slot; public void Detach() { attached = false; Trace.Add("detach:" + gameObject.name + ":" + Entities.ApplyingDetach); } }
    public class Slot { public Attach Part; public Attach part() => Part; }
    public class Wear { public int tipus; }
    public class Player : Component { public bool Bsitting; public Component seat; public pickupable[] weared = new pickupable[4]; public void Wear(pickupable p) { weared[p.wear.tipus] = p; Trace.Add("wear:" + p.gameObject.name); } }
    public class Main { public static Main s; public Player player; public static Vector3 UnityPosFromGlobal(Vector3d v) => (Vector3)v; public static Vector3d GlobalFromUnityPos(Vector3 v) => new(v.x, v.y, v.z); }
    public class Placer { public static Placer s = new(); public float itemUnFreezeDist = 100, itemRemoveDist = 200; }
    public class Menu { public static Menu s; public Map currentMainMap; public class Map { public List<Center> genArounds = new(); } public class Center { public Vector3 upos; } }
    public class Save
    {
        public static Save s = new(); public Dictionary<uint, Item> items = new(); public Data data = new(); public Dictionary<(int x, int y), HashSet<uint>> itemChunks = new(); public uint Next = 1000;
        public class Data { public itemDataClass itemData = new(); }
        public uint GetNewKey() => Next++;
        public static bool IndexOfID(List<save_item> list, uint id, out int k) { k = list.FindIndex(r => r != null && r.id == id); return k >= 0; }
        public static (int x, int y) SnapChunkPos(Vector3d p) => ((int)Math.Floor(p.x / 100), (int)Math.Floor(p.z / 100));
        public void AddToChunk(Vector3d p, uint id) { var c = SnapChunkPos(p); if (!itemChunks.TryGetValue(c, out var ids)) itemChunks[c] = ids = new(); ids.Add(id); }
    }
    public static class Snapshot
    {
        public static readonly System.Reflection.FieldInfo[] ListFields = typeof(itemDataClass).GetFields();
        public static List<Item> Group(Item it) => new(it.Group ?? new() { it });
        public static itemDataClass CaptureGroup(List<Item> items, List<itemDataClass> per)
        {
            Trace.Add("capture:" + string.Join(",", items.Select(i => i?.name ?? "null"))); var d = new itemDataClass();
            foreach (var i in items) { var row = new itemDataClass(); if (i != null && i.Savable) row.items.Add(new save_item { id = i.idInSave, prefabID = i.id, transform = new(Main.GlobalFromUnityPos(i.transform.position)) }); per.Add(row); d.Append(row); }
            return d;
        }
        public static Dictionary<uint, uint> SpawnGroup(itemDataClass d, List<uint> order)
        {
            var map = new Dictionary<uint, uint>(); foreach (var r in d.items) { var id = Save.s.GetNewKey(); map[r.id] = id; order.Add(id); if (r.prefabID != -1) Make(id, "spawn-" + r.prefabID, r.prefabID == 10); }
            Trace.Add("spawn-group:" + string.Join(",", order)); return map;
        }
        public static Dictionary<uint, uint> SpawnGroup(itemDataClass d) => SpawnGroup(d, new());
        public static void Move(itemDataClass d, Vector3d delta) { foreach (var r in d.items) r.transform.pos += delta; Trace.Add("record-move:" + delta); }
        public static void DropForeignRefs(itemDataClass d, Dictionary<uint, uint> map, string why) => Trace.Add("drop-foreign:" + why);
        public static void Remap(itemDataClass d, Dictionary<uint, uint> map) { foreach (var r in d.items) r.id = map[r.id]; foreach (var a in d.attachable) { a.id = map[a.id]; a.parentid = map.GetValueOrDefault(a.parentid); } }
    }
    public static class Codec
    {
        // A deterministic save codec. Transport headers and all writers are the real production implementations.
        public static byte[] Encode(itemDataClass d) { var w = new NetWriter(); w.VarU32((uint)d.items.Count); foreach (var r in d.items) { w.U32(r.id); w.I32(r.prefabID); w.F64(r.transform.pos.x); w.F64(r.transform.pos.y); w.F64(r.transform.pos.z); } return w.Buf.AsSpan(0, w.Len).ToArray(); }
        public static itemDataClass Decode(byte[] bytes) { if (bytes.Length == 0) return null; var r = new NetReader { Buf = bytes, End = bytes.Length }; var d = new itemDataClass(); int n = (int)r.VarU32(); for (int i = 0; i < n && !r.Bad; i++) d.items.Add(new save_item { id = r.U32(), prefabID = r.I32(), transform = new(new(r.F64(), r.F64(), r.F64())) }); return r.Bad ? null : d; }
    }
}

namespace TLDRevamp
{
    static class Plugin { public static Logger Log = new(); internal class Logger { public void LogWarning(object o) => RegistryFakes.Trace.Add("warning:" + o); public void LogInfo(object o) { if (!Convert.ToString(o).StartsWith("Far store shared at hosting start")) RegistryFakes.Trace.Add("info:" + o); } } }
    static class SessionLog
    {
        public static string Sid; public static void EndWorld(bool scene) => RegistryFakes.Trace.Add("end-world:" + scene);
        public static void Event(string name, uint net = 0, uint other = 0, int item = -1, int from = -1, int to = -1, uint epoch = 0, int count = 0, float value = 0, string result = "") => RegistryFakes.Trace.Add($"event:{name}:{net}:{other}:{item}:{from}:{to}:{epoch}:{count}:{result}");
    }
    static class DevHooks { public static Action CrashReset, TrafficReset, EntitySessionReset, SessionStopped; public static Func<string> RestoreDrop; public static Func<bool> SaveVeto; }
    static class Json { public static string Str(string s) => System.Text.Json.JsonSerializer.Serialize(s); }
    static class TelemetryHooks { public static void DrainSaves() => RegistryFakes.Trace.Add("drain-saves"); }
    static class TestSafety { public static bool InHostWorld; }
    namespace Fixes { static class VrContractBridge { public static void SanitizeReconnect() => RegistryFakes.Trace.Add("vr-reset"); } static class TerrainHorizon { public static void RestoreFarMeshes() => RegistryFakes.Trace.Add("horizon-restore"); } }
}
namespace TLDRevamp.Net
{
    static class SteamTransport { public const int SendReliable = 1, SendUnreliable = 0; }
    static partial class Mp { public static ServerFake Server; public static ClientFake Client; public static bool SceneTearingDown, PendingSceneStop; public static void SceneStopForTest() => StopIfScenePending(); public static Vector3d LocalGlobal() => new(20, 0, 30); public static string SaveTail(bool sessionEnd = false) { RegistryFakes.Trace.Add("save-tail:" + sessionEnd); return "saved"; } }
    class ClientFake { public int MyId = 2; public object Player; public bool Ready = true; public void Leave() => RegistryFakes.Trace.Add("client-leave"); public void Dispose() => RegistryFakes.Trace.Add("client-dispose"); public void SendRaw(NetWriter w, int flags) => RegistryFakes.Trace.Add("client-send:" + flags + ":" + Entities.Bytes(w)); }
    class ServerFake
    {
        public bool Sent = true; public Action DuringSend;
        public void Dispose() => RegistryFakes.Trace.Add("server-dispose"); public void Players(List<Session.Player> ps) { ps.Add(new() { Id = 0, Host = true }); ps.Add(new() { Id = 2, SteamId = 123 }); }
        public bool SendToPlayer(int id, NetWriter w, bool reliable) { RegistryFakes.Trace.Add("peer:" + id + ":" + reliable + ":" + Entities.Bytes(w)); DuringSend?.Invoke(); return Sent; }
        public void SendToAll(NetWriter w, bool reliable, int except, Vector3d? near, double range) { RegistryFakes.Trace.Add("peers:" + except + ":" + reliable + ":" + Entities.Bytes(w)); DuringSend?.Invoke(); }
        public void ReadyPeers(List<KeyValuePair<int, Vector3d>> p) { p.Clear(); p.Add(new(2, default)); p.Add(new(3, default)); }
    }
    public class PoseInterpolator { public void Reset() => RegistryFakes.Trace.Add("hand-reset"); }
    static class Tpa { public static void ResetTelemetry() => RegistryFakes.Trace.Add("tpa-reset"); public static void ObjectApplied(uint net) => RegistryFakes.Trace.Add("tpa-applied:" + net); }
    static partial class EntityTeleportRefresh { public static bool Deferred; public static bool DeferTpa(byte type, NetReader r) => Deferred; public static void TpaCopyMessage(byte t, NetReader r) => RegistryFakes.Trace.Add("tpa-copy:" + t); }
    static partial class EntityCarSignals { public static void ResetSignals() => RegistryFakes.Trace.Add("signals-reset"); public static void ReadSignals(NetReader r, Entities.Ent e) => RegistryFakes.Trace.Add("signals-read"); }
    static partial class EntityCarTuning { public static void CopyTanksOnOwnerChange(Entities.Ent e, bool p) => RegistryFakes.Trace.Add("tanks-role:" + e.NetId + ":" + p); public static void CopyTanksAre(uint id, List<save_tankscript> ts) => RegistryFakes.Trace.Add("tank-truth:" + id); public static void SendTuneTo(int p, uint n, byte[] t) => RegistryFakes.Trace.Add("tune-replay:" + p + ":" + n); }
    static partial class EntitySeats { public static void SeatCollisionIgnore(object a, object b) => RegistryFakes.Trace.Add("seat-ignore"); public static void ShotgunReset() => RegistryFakes.Trace.Add("shotgun-reset"); }
    static partial class EntityHands
    {
        public static void WriteShareWorn(NetWriter w, tosaveitemscript root) { w.U8(255); RegistryFakes.Trace.Add("share-worn"); }
        public static void ClearWorn(Entities.Ent e) { e.WornBy = -1; RegistryFakes.Trace.Add("clear-worn"); }
        public static void TakeOverCarried(Entities.Ent e) { e.CarriedBy = 0; RegistryFakes.Trace.Add("take-carried"); }
        public static void WornReset() => RegistryFakes.Trace.Add("worn-reset");
    }
    static partial class EntityRagdolls
    {
        public static HashSet<Entities.Ent> RagdollCopies = new(); public static Dictionary<GameObject, Entities.Ent> RagdollLimbOwner = new();
        static readonly List<GameObject> _ragKeys = new();
        public static void DeathReset() => RegistryFakes.Trace.Add("death-reset");
        public static void ReadRagdoll(NetReader r, Entities.Ent e, Vector3d p, Quaternion q) => RegistryFakes.Trace.Add("rag-read");
    }
    static partial class EntityEffects { public static void ResetSpraying() => RegistryFakes.Trace.Add("spray-reset"); }
    static partial class EntityLocks { public static void ResetGentleBodies() => RegistryFakes.Trace.Add("gentle-reset"); public static void ResetHostSetting() => RegistryFakes.Trace.Add("host-setting-reset"); }
    static partial class EntityWeather { public static void StormReset() => RegistryFakes.Trace.Add("storm-reset"); }
    static partial class EntityWorldLeases { public static void RestoreBuiltIns() => RegistryFakes.Trace.Add("builtins-restore"); public static void ResetLeaseSession() => RegistryFakes.Trace.Add("leases-reset"); public static void ReloadClient() => RegistryFakes.Trace.Add("leases-reload"); public static void ForgetBuiltIns() => RegistryFakes.Trace.Add("builtins-forget"); public static void ServerLeaseLeave(int p) => RegistryFakes.Trace.Add("lease-leave:" + p); }
    static partial class EntityAttachments { public static void ResetCarriedOff() => RegistryFakes.Trace.Add("carriedoff-reset"); }
    static partial class EntityWorldClock { public static void ClockReset() => RegistryFakes.Trace.Add("clock-reset"); public static void ServerWorldClockTo(int p) => RegistryFakes.Trace.Add("clock-join:" + p); }
    static partial class EntityRadio { public static void ServerRadioTo(int p) => RegistryFakes.Trace.Add("radio-join:" + p); }
    static partial class EntityAI { public static void BreaksReset() => RegistryFakes.Trace.Add("breaks-reset"); public static void AiReset() => RegistryFakes.Trace.Add("ai-reset"); public static void ServerWorldBreaksTo(int p) => RegistryFakes.Trace.Add("breaks-join:" + p); }
    static partial class EntityEdits { public static void ResetTankEditTruth() => RegistryFakes.Trace.Add("tank-truth-reset"); }
    static partial class EntityRabbits { public static void RabbitReset() => RegistryFakes.Trace.Add("rabbit-reset"); public static void RabbitRole(Entities.Ent e, bool p) => RegistryFakes.Trace.Add("rabbit-role:" + e.NetId + ":" + p); public static void RestoreRabbitState(Entities.Ent e) => RegistryFakes.Trace.Add("rabbit-restore:" + e.NetId); }
    static partial class EntityHitches { public static void ClearCommands() => RegistryFakes.Trace.Add("hitch-commands-reset"); public static void RemoveCommand(uint n) => RegistryFakes.Trace.Add("hitch-command-remove:" + n); public static void HitchThaw(Entities.Ent e) => RegistryFakes.Trace.Add("hitch-thaw:" + e.NetId); public static void HitchRestore(Entities.Ent e) => RegistryFakes.Trace.Add("hitch-restore:" + e.NetId); }
    static partial class EntityAuthorityInterest { public static void ForgetRelay(int p) => RegistryFakes.Trace.Add("relay-forget:" + p); public static void ResetRelay() => RegistryFakes.Trace.Add("relay-reset"); }
    static partial class EntityLifetimeStreaming
    {
        public static HashSet<tosaveitemscript> Deleted = new(); public static bool TakePlayerDeleted(tosaveitemscript it) => Deleted.Remove(it); public static void ClearPlayerDeleted() => Deleted.Clear();
    }
    internal static partial class EntityPhysics
    {
        public class ContactRelay : Component { public uint NetId; }
        public static void ResetFreedJoints() => RegistryFakes.Trace.Add("joints-reset"); public static void ResetCrashHolds() => RegistryFakes.Trace.Add("crash-reset"); public static void ResetStandInterpolation() => RegistryFakes.Trace.Add("stand-reset"); public static void ResetPhysicalCars() => RegistryFakes.Trace.Add("physical-reset");
    }
    static class JoinLoading { public static void End(string why) => RegistryFakes.Trace.Add("join-end:" + why); }
    static class Session { public static string EndReason; public class Player { public int Id; public bool Host; public ulong SteamId; } public static void Reset() => RegistryFakes.Trace.Add("session-reset"); }
    static class GameMode { public static void RestoreOwn() => RegistryFakes.Trace.Add("gamemode-restore"); }
    static class DeveloperAccess { public static void ResetClient() => RegistryFakes.Trace.Add("developer-reset"); }
    static class SessionSettingsSnapshot { public static void UnbindHost() => RegistryFakes.Trace.Add("settings-unbind"); }
    static class SessionSettingsDefaults { public static void ResetSessionPolicy() => RegistryFakes.Trace.Add("policy-reset"); }
    static class SessionRules { public static void DeathAbandoned() => RegistryFakes.Trace.Add("death-abandon"); public static void DeathReset() => RegistryFakes.Trace.Add("death-state-reset"); }
    static class RemotePlayers { public static void Clear() => RegistryFakes.Trace.Add("players-clear"); }
    static class ModObjects { public static void Reset(bool h) => RegistryFakes.Trace.Add("mods-reset:" + h); public static void ResetClock() => RegistryFakes.Trace.Add("mods-clock-reset"); }
    static class WorldCheck { public static void SessionEnds() => RegistryFakes.Trace.Add("world-end"); }
    static class Reports { public static void Reset() => RegistryFakes.Trace.Add("reports-reset"); }
    static class TelemetryMarks { public static void Reset() => RegistryFakes.Trace.Add("marks-reset"); }
    static class Protocol { public const byte JoinLoading = 110; public static Vector3d ReadPos(NetReader r) => new(r.F64(), r.F64(), r.F64()); public static void WritePos(NetWriter w, Vector3d p) { w.F64(p.x); w.F64(p.y); w.F64(p.z); } }
    static partial class PlayerSave
    {
        public static bool Enabled = true, ShareWornLate = true, WithSaveFile = true, RestoreConfirmed = true; public const int MaxItems = 64; public const byte Restore = 68;
        static NetReader _pending; static List<uint> _restoredReport; static readonly Dictionary<ulong, Rec> _recs = new(); static int _seed;
        public static string Last = "", WornLog = ""; public static long ItemsRestored, WornRestored, Restored, Pocketed, ItemsDropped;
        class BudgetFake { public void SchedulePeriodic(double at) => RegistryFakes.Trace.Add("record-budget:" + at); }
        static BudgetFake Budget() => new();
        public static void SendRecord() => RegistryFakes.Trace.Add("send-record"); public static void ClientLeaving() => RegistryFakes.Trace.Add("player-leaving");
        static string SeedFilePath(int seed) => "fixture-players.bin"; static void WriteFile(string path) => RegistryFakes.Trace.Add("write-players");
        public static void Fresh() { _pending = null; _restoredReport = null; _recs.Clear(); Last = WornLog = ""; ItemsRestored = WornRestored = Restored = Pocketed = ItemsDropped = 0; }
        public static void SeedPocket(uint net)
        {
            _recs[123] = new() { Present = true, PlayerId = 2, Items = new() { new() { Id = 70, Net = net, Rec = RegistryFakes.Codec.Encode(new itemDataClass { items = new() { new() { id = 70, prefabID = 11 } } }) } }, Worn = new() { 70 } };
        }
        public static string State() => "restore=" + (_pending != null) + ":" + string.Join(",", _restoredReport ?? new()) + ":" + ItemsRestored + ":" + Restored + ":" + Pocketed + ":" + Last + ":recs=" + string.Join(";", _recs.Select(k => k.Key + ":" + k.Value.Present + ":" + string.Join(",", k.Value.Items.Select(i => i.Id + "/" + i.Net)) + ":handed=" + k.Value.Handed.Count));
    }
}
public class save_player { public uint[] inventory; public bool heldItem, sit; public uint heldItemID; public vector3 pos; public void Load(object p) => RegistryFakes.Trace.Add("player-load"); public void LoadInventory(object p) => RegistryFakes.Trace.Add("inventory-load"); } // test stub, not game code
namespace UnityEngine { static class JsonUtility { public static T FromJson<T>(string s) where T : new() => new(); } }
