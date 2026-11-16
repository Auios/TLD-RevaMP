using System;
using System.Collections.Generic;
using UnityEngine;

public partial class tosaveitemscript { public List<usablescript> usables = new List<usablescript>(); }
public partial class usablescript
{
    public float tunedFloatMin, tunedFloatMax = 1;
    public Action DuringRefresh;
    public void Refresh(float slide, float x, float y, int turn)
    { DuringRefresh?.Invoke(); slideValue = slide; xRot = x; yRot = y; currentTurnState = turn; }
}
public class poiScript : Component
{
    public int poigenid; public Vector3d posID; public bool started;
    public List<usablescript> usables = new List<usablescript>();
    public void FStart() { }
}
public class save_poi { public void Load(poiScript p) { } }
public class menuhandler // test stub, not game code
{
    public static menuhandler s;
    public Map currentMainMap;
    public class Map { public List<Gen> poiGens = new List<Gen>(); }
    public class Gen { public float removeDistance; public int chunkSize; public Dictionary<Vector3d, Chunk> chunks = new Dictionary<Vector3d, Chunk>(); }
    public class Chunk { public Dictionary<Vector3d, Poi> pois = new Dictionary<Vector3d, Poi>(); }
    public class Poi { public poiScript pobj; }
}
public partial class mainscript { public static Vector3d GetChunkPos(Vector3d id, int size) => default; }
namespace UnityEngine { public static partial class Mathf { public static float InverseLerp(float a, float b, float v) => a == b ? 0 : Clamp01((v - a) / (b - a)); } }

namespace TLDRevamp.Net
{
    internal static class Tpa
    {
        internal static bool RefreshingObjects, WaitingWorld;
        internal static List<uint> Applied = new List<uint>();
        internal static void ObjectApplied(uint net) => Applied.Add(net);
    }
    public static partial class Entities
    {
        internal const byte Add = 22, Resync = 31, PartDetached = 27, AttachSync = 41, MemberGone = 86,
            HandoverState = 59, Refetch = 58;
        internal static readonly Dictionary<PoiKey, PoiState> KnownPoiUsables = new Dictionary<PoiKey, PoiState>();
        internal static readonly List<string> TpaRows = new List<string>();
        internal static bool TpaReplayThrows;
        internal static int MarkedPoi;
        private static bool MarkLiveUsable(usablescript u) { MarkedPoi++; return true; }
        private static void WriteLostPart(NetWriter w, uint net) { }
        private static void ClientReceive(byte type, NetReader r)
        { if (TpaReplayThrows) throw new Exception("replay"); if (!EntityTeleportRefresh.DeferTpa(type, r)) TpaRows.Add(type + ":" + Convert.ToHexString(r.Buf.AsSpan(r.Pos, r.Remaining))); }
    }
}
