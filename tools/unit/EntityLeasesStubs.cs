using System;
using System.Collections.Generic;
using System.Linq;
using TLDRevamp.Net;
using UnityEngine;

namespace HarmonyLib { public static partial class Priority { public const int Last = 0; } }

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static int ShareRoots(List<tosaveitemscript> items) => items.Count;
        private static List<GameObject> DisableBuiltIns(poiScript poi) => new List<GameObject>();
    }
}

namespace LeaseFakes
{
    internal static class Kernel
    {
        internal const byte LeaseReq = 28, LeaseGrant = 29, LeaseTaken = 30, LeaseDone = 33, LeaseFreed = 34;
        internal static bool InSession = true, IsHost;
        internal static readonly List<string> Packets = new List<string>();
        internal static readonly List<string> Anomalies = new List<string>();
        internal static readonly List<tosaveitemscript> Shared = new List<tosaveitemscript>();
        internal static Action<NetWriter> Request;
        internal static Action<int, byte[]> Peer;
        internal static void SendLeaseRequest(NetWriter w, bool reliable) { Packet(-2, w, reliable); Request?.Invoke(w); }
        internal static void SendToPeer(int peer, NetWriter w, bool reliable)
        { Packet(peer, w, reliable); Peer?.Invoke(peer, w.Buf.Take(w.Len).ToArray()); }
        internal static void SendToPeers(NetWriter w, bool reliable, int except) => Packet(-1, w, reliable);
        private static void Packet(int peer, NetWriter w, bool reliable) => Packets.Add(peer + ":" + reliable + ":" + Convert.ToHexString(w.Buf.AsSpan(0, w.Len)));
        internal static void LeaseAnomaly(string text) => Anomalies.Add(text);
        internal static bool IsFinite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        internal static bool IsFinite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        internal static bool IsFinite(Vector3d n) => IsFinite(n.x) && IsFinite(n.y) && IsFinite(n.z);
        internal static bool TryLeasePlayerPosition(int from, out Vector3d pos)
        {
            pos = default;
            if (Transport.Server == null || from < 0) return false;
            if (from != 0) return Transport.Server.PeerPos(from, out pos) && IsFinite(pos);
            var player = Main.s != null ? Main.s.player : null;
            if (player == null) return false;
            pos = Main.GlobalFromUnityPos(player.transform.position);
            return IsFinite(pos);
        }
        internal static int ShareLeaseRoots(List<tosaveitemscript> items) { Shared.AddRange(items); return items.Count; }
        internal static List<GameObject> DisableLeaseBuiltIns(PoiObject poi)
        {
            var list = new List<GameObject>();
            foreach (var it in poi.Items)
                if (!Shared.Contains(it) && it.gameObject.activeSelf) { it.gameObject.SetActive(false); list.Add(it.gameObject); EntityWorldLeases.BuiltInDisabled++; }
            return list;
        }
    }
    internal static class Transport { internal static Server Server; }
    internal sealed class Server
    {
        internal Dictionary<int, Vector3d> Positions = new Dictionary<int, Vector3d>();
        internal bool PeerPos(int id, out Vector3d pos) => Positions.TryGetValue(id, out pos);
    }
    internal sealed class MenuData { public void ResetStuff() { } }
    internal sealed class SaveScreen { public void ReadSave() { } public void Save() { } public void AutoSave() { } }
    internal sealed class Main
    {
        internal static Main s;
        internal int Scene;
        internal fpscontroller player = new fpscontroller();
        internal int GetInstanceID() => Scene;
        internal static string LastSave;
        internal static string GetLastSaveName() => LastSave;
        internal static Vector3d GlobalFromUnityPos(Vector3 p) => new Vector3d(p.x, p.y, p.z);
    }
    internal static class Paths
    {
        internal static string autosaveName1, autosaveName2, autosaveName3;
        internal static string SaveDataName(string name) => name;
        internal static string UnDuckStringBecauseItIsDuckedByDefault(string name) => name;
    }
    internal static class Players { internal static bool WithSaveFile = true; }
    internal sealed class Menu { internal static Menu s; internal Map currentMainMap = new Map(); }
    internal sealed class Map { internal List<Generator> poiGens = new List<Generator>(); }
    internal sealed class Generator
    {
        internal float chunkSize;
        internal Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        internal sealed class poiClass
        {
            internal Vector3d posID;
            internal int id;
            internal string poiName;
            internal bool spawnedItems, spawnedFixed;
            internal PoiObject pobj;
        }
    }
    internal sealed class Chunk { internal Dictionary<int, Generator.poiClass> pois = new Dictionary<int, Generator.poiClass>(); }
    internal sealed class PoiObject : Component
    {
        internal List<tosaveitemscript> Items = new List<tosaveitemscript>();
        internal Action<Generator.poiClass> Spawn;
        public new T[] GetComponentsInChildren<T>(bool all) where T : class => Items.OfType<T>().ToArray();
        internal void SpawnStuff(Generator.poiClass p) { p.spawnedItems = p.spawnedFixed = true; Spawn?.Invoke(p); }
    }
    internal sealed class Saved { internal static Saved s; internal Dictionary<uint, tosaveitemscript> items = new Dictionary<uint, tosaveitemscript>(); }
    internal sealed class Database
    {
        internal static Database s;
        internal Dictionary<int, GameObject> Buildings = new Dictionary<int, GameObject>();
        internal GameObject Building(int id) => Buildings[id];
    }
}
