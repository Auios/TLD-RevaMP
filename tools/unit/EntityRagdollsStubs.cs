using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public partial class GameObject { public void SetActive(bool on) { activeSelf = activeInHierarchy = on; } public T[] GetComponentsInChildren<T>(bool all) where T : class => Part == null ? Array.Empty<T>() : Part.GetComponentsInChildren<T>(all); }
    public static partial class Mathf { public static float Exp(float x) => (float)Math.Exp(x); }
    public static partial class Time { public static int frameCount; }
    public partial struct Vector3 { public static Vector3 operator /(Vector3 p, float n) => p * (1 / n); }
    public partial struct Vector3d { public double sqrMagnitude => x * x + y * y + z * z; }
    public class Renderer : Component { public bool enabled = true; public Material[] sharedMaterials = Array.Empty<Material>(); }
    public class Material { public bool HasProperty(string name) => true; public float GetFloat(string name) => 0; }
    public static class ColorUtility { public static string ToHtmlStringRGB(Color c) => "FFFFFF"; }
}
public class ragdollactivatescript : Component { public bool ragdoll; public Rigidbody[] RBs; public Rigidbody RB; public void SetRagdol(bool on) { ragdoll = on; } }
public partial class newAiScript : Component { public bool died; public void Die(bool _playSound) { died = true; } }
public partial class nyulscript : Component { }
public partial class aiscript { public bool alive = true; public void Die(bool _playSound) { alive = false; } }
public class TestLimbSet { public Component[] joints; public Transform[] childs; public pickupable[] ps; }
public partial class tosaveitemscript { public TestLimbSet ragdollPos; }
public partial class fpscontroller
{
    public GameObject spawnedRagdol; public bool died; public playermodeloutfitscript outfit;
    public void Death() { } public void Revive() { }
}
public class save_outfit
{
    public int selectedCharacter; public save_outfitsegment[] segments;
    public void Save(playermodeloutfitscript script, uint id) { } public void Load(playermodeloutfitscript script) { }
}
public class save_outfitsegment { public bool enabled; public float r, g, b; }
public class playermodeloutfitscript : Component
{
    public int selectedCharacter, Resets; public Character[] characters;
    public class Character { public Model ch = new Model(); }
    public class Model { public Segment[] segments; public Renderer[] renderers = Array.Empty<Renderer>(); }
    public class Segment { public bool BEnabled; public Color Ccolor; }
    public void ReSetDistance() { Resets++; }
    public void GetData(out int sel, out bool[] en, out Color[] col) { sel = selectedCharacter; en = characters[sel].ch.segments.Select(s => s.BEnabled).ToArray(); col = characters[sel].ch.segments.Select(s => s.Ccolor).ToArray(); }
}
public class bulgingeyesScript : Component { public float current; public bool disabled, enabled; public void Refresh() { } }
namespace TLDRevamp.Multiplayer
{
    internal static class MpApi
    {
        internal static GameObject Root; internal static Transform[] Limbs; internal static Func<bool> Limp;
        internal static bool TryGetRagdoll(GameObject root, out Transform[] limbs, out Func<bool> limp) { limbs = root == Root ? Limbs : null; limp = root == Root ? Limp : null; return limbs != null; }
    }
}
namespace TLDRevamp.Net
{
    internal static class SessionRules { internal static string DeathStats() => "{}"; }
    internal static class PlayerLook { internal static uint OutfitHash(int sel, bool[] en, Color[] col) => (uint)sel; }
    public static partial class Entities
    {
        public sealed partial class Ent { public Rag Rag; public bool ReleaseGhost, CargoGhost; public List<Rigidbody> MadeKinematic = new List<Rigidbody>(); }
        public const byte RemoveItem = 35, CompactState = 98;
        internal static readonly HashSet<GameObject> ProxyItems = new HashSet<GameObject>();
        internal static long SpawnShared;
        internal static bool TestDeleteDormant, TestShareMiss, TestDeleteThrow, TestSharePendingNet;
        internal static Transform RagdollFrame(Ent e) => e.Root.transform;
        internal static bool LimbsByBody(Ent e) => e.MovedByBody && e.Root.car != null;
        internal static void ShareSpawn(GameObject go) { if (TestTumbleWire) { TestTumbleShareSpawn(go); return; } if (TestShareMiss || ByNet.Values.Any(e => e.Root?.gameObject == go)) return; SpawnShared++; var ts = go.GetComponent<tosaveitemscript>(); ByNet[301] = new Ent { NetId = 301, RootId = ts.idInSave, Root = ts }; }
        internal static uint NetIdOf(GameObject go) => TestSharePendingNet ? 0 : ByNet.Values.FirstOrDefault(e => e.Root?.gameObject == go)?.NetId ?? 0;
        internal static string DeleteShared(uint net) { if (TestDeleteThrow) throw new Exception("fixture"); if (!TestDeleteDormant) { W.Reset(); W.U8(RemoveItem); W.U32(net); ToServer(W, true); ByNet.Remove(net); } return "ok"; }
    }
}
