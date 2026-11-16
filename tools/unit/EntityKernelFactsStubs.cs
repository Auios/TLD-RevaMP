using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEngine
{
    public partial struct Vector3
    {
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 back => new Vector3(0, 0, -1);
    }
}

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public static string ShareCar(tosaveitemscript root) => ShareItem(root, null);
    }
}

// Slice 30 uses a separate world so lookup order and reflection rows do not depend on earlier feature fixtures.
namespace KernelFactsFakes
{
    internal class GameObject
    {
        internal readonly Transform transform;
        internal int layer;
        internal bool activeInHierarchy = true;
        internal GameObject() { transform = new Transform(this); }
    }
    internal class Transform
    {
        internal readonly GameObject gameObject;
        internal Transform parent;
        internal Item Item;
        internal Vector3 position;
        internal string name = "root";
        internal Transform root => parent?.root ?? this;
        internal Transform(GameObject go) { gameObject = go; }
        internal T GetComponent<T>() where T : class => Item as T;
    }
    internal sealed class Item
    {
        internal readonly GameObject gameObject = new GameObject();
        internal Transform transform => gameObject.transform;
        internal object car;
        internal List<Tank> tanks;
        internal string name;
        internal Item(string value) { name = value; transform.Item = this; }
    }
    internal sealed class Tank { internal string name; internal Tank(string value) { name = value; } }
    internal sealed class Player
    {
        internal Transform transform = new GameObject().transform;
        internal bool Sit = true;
        internal LayerMask useLayer = new LayerMask { value = 3 };
        internal float FrayRange = 2;
        internal bool CanPickupSit(Pickup p) => Sit;
    }
    internal sealed class Main { internal static Main s; internal Player player; }
    internal sealed class Save { internal static Save s; internal Dictionary<uint, Item> items = new Dictionary<uint, Item>(); }
    internal sealed class Slot { internal bool cantCraft, cantUnCraft, attachTarget; }
    internal sealed class Cover { internal bool Has; internal bool hasPart() => Has; }
    internal sealed class Part
    {
        internal string name = "part";
        internal Transform transform = new GameObject().transform;
        internal bool attached;
        internal Cover cover_CantCraftIfCovered;
        internal Collider[] Colliders = Array.Empty<Collider>();
        internal T[] GetComponentsInChildren<T>(bool all) => (T[])(object)Colliders;
    }
    internal sealed class Pickup
    {
        internal bool cantPickup, cantPickupWhileMount, crafted;
        internal bool CanPickup() => !cantPickup;
    }
    internal sealed class Layer { internal Pickup p; }
    internal sealed class Collider
    {
        internal string name;
        internal GameObject gameObject = new GameObject();
        internal Transform transform => gameObject.transform;
        internal bool enabled = true, isTrigger;
        internal Bounds bounds;
        internal Slot Slot;
        internal Layer Layer;
        internal T GetComponent<T>() where T : class => Slot as T ?? Layer as T;
    }
    internal struct LayerMask
    {
        internal int value;
        internal static string LayerToName(int layer) => "layer" + layer;
    }
    internal static class Physics
    {
        internal struct Hit { internal Collider collider; }
        internal static readonly Queue<Collider> Hits = new Queue<Collider>();
        internal static int Rays;
        internal static bool Raycast(Vector3 from, Vector3 direction, out Hit hit, float range, LayerMask mask)
        { Rays++; hit = new Hit { collider = Hits.Count > 0 ? Hits.Dequeue() : null }; return hit.collider != null; }
    }
    internal sealed class Data
    {
        public List<object> items = new List<object>();
        public List<object> rows = new List<object>();
        public List<WrongRow> wrong = new List<WrongRow>();
        public List<OtherRow> other = new List<OtherRow>();
        public int count;
    }
    internal sealed class Row { public uint id; public string value; internal Row(uint key, string text) { id = key; value = text; } }
    internal sealed class WrongRow { public int id; }
    internal sealed class OtherRow { public uint idInSave; }
    internal static class Snapshot { internal static readonly FieldInfo[] ListFields = typeof(Data).GetFields(); }
    internal static class Lookups
    {
        internal static readonly List<string> Calls = new List<string>();
        internal static FieldInfo SaveIdField(Type data, string name, Type row)
        { Calls.Add(name + ":" + (row?.Name ?? "null")); return row?.GetField(name); }
    }
    internal static partial class Kernel
    {
        internal sealed class Ent { internal uint NetId; internal Item Root; }
        internal static readonly Dictionary<uint, Ent> ByNet = new Dictionary<uint, Ent>();
        internal static bool InSession;
        internal static readonly List<string> Shares = new List<string>();
        internal static string Result = "{}";
        internal static string ShareCar(Item item) { Shares.Add("car:" + item.name); return Result; }
        internal static string ShareItem(Item item, List<Item> group) { Shares.Add("item:" + item.name); return Result; }
    }
}
