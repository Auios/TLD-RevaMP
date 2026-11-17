using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class itemDataClass
{
    public List<object> partconditions = new(), usable = new(), colors = new(), door_rots = new(), ammo = new(), ginlamp = new(), busdoorscript = new(), painting = new(), food = new(), rendszam = new();
    public List<save_tankscript> tanks = new();
    public List<save_attachable> attachable = new();
}
public class save_color { public float r, g, b, a; }
public partial class save_attachable
{
    public uint parentid; public int index;
    public enum attachTypeEmu { toItem = 1 }
    public save_attachable(save_attachable a) { parentid = a.parentid; index = a.index; gpos = a.gpos; lpos = a.lpos; poiid = a.poiid; indexType = a.indexType; attachType = a.attachType; poigenid = a.poigenid; }
}
public partial class mainscript
{
    public enum fluidenum { water, petrol, diesel, oil }
    public partial class fluidcontainer
    {
        public float maxC = 20;
        public Action DuringChange;
        public void ChangeOne(float amount, fluidenum type)
        {
            DuringChange?.Invoke();
            var fluid = fluids.Find(f => f.type == (int)type);
            if (fluid == null) fluids.Add(fluid = new FluidValue { type = (int)type });
            fluid.amount += amount;
        }
    }
}
namespace TLDRevamp.Net
{
    internal static partial class ItemSnapshot
    {
        internal static void Remap(itemDataClass record, Dictionary<uint, uint> map) { foreach (var a in record.attachable) if (map.TryGetValue(a.parentid, out uint id)) a.parentid = id; }
    }
    public static partial class Entities
    {
        internal const uint CanonBase = 0xF0000000u;
        public sealed partial class Ent { public bool ResyncPhased; public float ResyncAcc; public int ResyncCursor = -1; }
        private static readonly NetWriter WR = new();
        internal const int KindUsable = 1, KindPart = 4, KindColor = 8, KindFood = 16, KindAmmo = 32, KindPlate = 64, KindBusDoor = 128;
        internal static float ResyncInterval = 3, ResyncBudgetMs = 1000;
        private static double _resyncFrameMs;
        private static bool _resyncFrameWorked, _resyncDeferred;
        private static bool LiveMoving;
        internal static void BeginResyncFrame() { _resyncFrameMs = 0; _resyncFrameWorked = false; _resyncDeferred = false; }
        internal static void TestResync(Ent e, float dt) => ResyncTick(e, dt);
        internal static bool TestDeferred => _resyncDeferred;
        internal static long EditsReturned;
        internal static string TestEditValidation, TestAddsValidation;
        private static string CheckEdit(SEnt entity, int item, int kinds, byte[] record) => TestEditValidation;
        private static string CheckTankAdds(SEnt entity, int item, List<TankAdds.Add> adds) => TestAddsValidation;
        private static void SetLiveRecordFloor(UsableKey key, double stamp) { }
        internal static void TestServerEdit(int from, NetReader reader) => ServerEdit(from, reader);
        internal static void ChangeBackOwner(uint net, int owner, uint epoch) { Server[net].OwnerId = owner; Server[net].Epoch = epoch; }
        internal static long EditAnomalyTotal { get => Anomalies; set { Anomalies = value; _anomLogged = 0; } }
        internal static List<string> EditAnomalyLogs => _anomLines;


        internal static bool EditPeerKnown;
        internal static Vector3d EditPeerPosition;
    }
}
