using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public partial class Collision { public Collider collider; public Rigidbody rigidbody; public Vector3 relativeVelocity; public int contactCount; public RaycastHit GetContact(int i) => new RaycastHit(); }
    public partial struct Vector3 { public static float Angle(Vector3 a, Vector3 b) => 0; }
    public partial class Transform { public void SetParent(Transform t) => SetParent(t, true); }
    public static partial class Physics
    {
        internal static RaycastHit[] Hits = Array.Empty<RaycastHit>();
        public static int RaycastNonAlloc(Vector3 p, Vector3 d, RaycastHit[] hits, float max, int mask) { Array.Copy(Hits, hits, Hits.Length); return Hits.Length; }
    }
}
public partial class newAiScript
{
    public Rigidbody rb; public Transform head, target; public tosaveitemscript tosave; public TestAIAnim anim;
    public bool sleeping, isChasing, isSeeing, isAttacking, first, targetIsSteamID; public object currentPlayer;
    public float fov = 90, range = 100, chaseTimeMin = 1, chaseTimeMax = 3, chaseStart, currentChaseTime, rayAttackDistRange = 3, damage = 10;
    public int lm = -1, Sounds; public ulong targetSteamID; public sens sensU, sensD;
    public enum aiSoundType { idle, notice, attack, hurt, die }
    public void TargetNull() { target ??= new Transform(); }
    public void TargetReset() { }
    public void SoundNotice() { Sounds++; }
    public void SoundSound(aiSoundType t, int index, float pitch, bool play) { Sounds++; DuringSound?.Invoke(); }
    public static Action DuringSound;
}
public class TestAIAnim { public float targetForward, targetTurn; public bool onGround, duck; public int Holds; public void Hold(Vector3 p, bool leftHand) { Holds++; } }
public class sens { public Collider selfCol; }
public class gamemodesettingsscript { public static gamemodesettingsscript s = new gamemodesettingsscript(); public Settings S = new Settings(); public class Settings { public int peacefulEnemies; } }
public class breakablescript : Component
{
    public bool destroyed, noShoot, decreaseAfterAttack; public float health = 10, shootHealth = 10; public aiscript AI; public newAiScript newAI; public GameObject Gib, reducedGib, spawnedGib;
    public int BreakCalls, ForceCalls, ShootCalls; public Vector3 LastDir; public float LastForce;
    public static Action<breakablescript> DuringBreak;
    public void Break() { BreakCalls++; DuringBreak?.Invoke(this); destroyed = true; }
    public void TryBreak(float force) { ForceCalls++; LastForce = force; }
    public void TryBreak(Rigidbody rb) { }
    public void TryBreakShoot(float force, Vector3 dir) { ShootCalls++; LastForce = force; LastDir = dir; }
    public void TryBreakExplosion() { }
    public void DamageFrom(Vector3 dir) { LastDir = dir; }
}
public class thumbleweedScript : Component { public bool bwind; }
public class thumbleweedMain { public tosaveitemscript nowSpawned; public int thumbleweedLimit = 3; public List<thumbleweedScript> thumbleweeds = new List<thumbleweedScript>(); public void Spawn(bool ignoreLimit) { } }
public partial class tosaveitemscript { public newAiScript npc; public thumbleweedScript thumbleweed; }
namespace TLDRevamp.Net
{
    internal static class MpServer { internal static float CarBeyondInterval = 10; }
    internal static partial class PlayerCombat
    {
        internal static long DamageTaken;
        internal static float RayDistance = 1;
        internal static float RayCapsule(Ray ray, Vector3 feet, Vector3 top, float radius) => RayDistance;
    }
    public static partial class RemotePlayers
    {
        internal static readonly List<(int id, Transform body, Transform head, Vector3 feet, Vector3 top, ulong steam)> Targets = new List<(int, Transform, Transform, Vector3, Vector3, ulong)>();
        internal static int Count => Targets.Count;
        internal static IEnumerable<(int id, Transform body, Transform head, Vector3 feet, Vector3 top, ulong steam)> AiTargets() => Targets;
    }
    public static partial class Entities
    {
        public sealed partial class Ent { public ulong[] ResyncHashes; }
        internal const byte Share = 20;
        internal static bool TestTumbleWire, StoredContentsApart;
        internal static long ShareMembersDropped, StoredContentsShared;
        internal static readonly Dictionary<uint, Ent> PendingShare = new Dictionary<uint, Ent>();
        private static List<tosaveitemscript> StoredContents(tosaveitemscript root, List<tosaveitemscript> items) => null;
        private static ulong[] StateHashes(List<tosaveitemscript> items, List<itemDataClass> records) => new ulong[items.Count];
        private static bool AttachedToItem(tosaveitemscript item) => false;
        private sealed partial class SEnt { public List<int> Broken; internal uint AiSnapshotEpoch; internal readonly Dictionary<int, byte[]> AiSnapshots = new Dictionary<int, byte[]>(); }
        internal static int AIRelays;
        private static void RelayCreature(SEnt se, int from, NetReader r) { AIRelays++; }
        private static byte[] FinalSnapshot(NetReader r, bool final) => r.Buf.Take(r.End).ToArray();
        internal static void TestAIServer(uint epoch, uint snapEpoch) { Server[301].Epoch = epoch; Server[301].AiSnapshotEpoch = snapEpoch; }
        internal static byte[] TestAISnapshot(int item) => Server[301].AiSnapshots[item];
        internal static int TestAISnapshotCount => Server[301].AiSnapshots.Count;
        internal static bool ReshareSurvivors = true, WasteFixes = true, TestAudience = true;
        internal static long StatesSkippedDeadAnimation;
        internal static readonly List<tosaveitemscript> Reshares = new List<tosaveitemscript>();
        internal static void QueueReshare(tosaveitemscript it) => Reshares.Add(it);
        private static bool OwnerAudience(Vector3d at) => TestAudience;
        internal static void SkippedByInterest(int player) { }
    }
}
namespace TLDRevamp.Net
{
    internal static partial class ItemSnapshot
    {
        internal static List<tosaveitemscript> Group(tosaveitemscript root) => new List<tosaveitemscript> { root };
        internal static itemDataClass CaptureGroup(List<tosaveitemscript> items, List<itemDataClass> records) { foreach (var item in items) records.Add(new itemDataClass()); return new itemDataClass(); }
    }
}
