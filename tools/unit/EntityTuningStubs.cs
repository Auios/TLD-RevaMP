using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class Gear { public float ratio; }
public partial class carscript
{
    public float differentialRatio;
    public List<WheelCollider> WHDriven, WHSteer, WHInvSteer, WHBrakeOnly, WHHandBrake;
    public washtriggerscript blood;
    public T[] GetComponentsInChildren<T>(bool all) => Children.OfType<T>().ToArray();
}
namespace UnityEngine
{
    public partial class WheelCollider : Component { public bool isGrounded; public float brakeTorque; public float suspensionDistance, motorTorque; public Vector3 center; }
    public partial class Transform
    {
        public Vector3 localPosition; public Quaternion localRotation = Quaternion.identity;
        public bool IsChildOf(Transform root) => this == root || parent != null && parent.IsChildOf(root);
        public int GetSiblingIndex() => parent == null ? 0 : parent.Children.IndexOf(this);
    }
    public partial struct Quaternion { public Vector3 eulerAngles => Vector3.zero; public static Vector3 operator *(Quaternion q, Vector3 p) => p; }
}
public partial class mainscript
{
    public partial class fluidcontainer
    {
        public List<FluidValue> fluids = new List<FluidValue>();
        public float GetAmount() => fluids.Sum(f => f.amount);
        public void ResetSync() { }
        public void ChangeWithoutMix(float amount) { }
        public void ChangeWithoutMix(float amount, bool sync) { }
    }
    public class FluidValue { public int type; public float amount; }
}
public partial class tankscript { public void FStart() { } public void Upd() { } }
public partial class tosaveitemscript
{
    public uint idInSave; public int id; public bool loaded = true;
    public busdoorscripts busdoorscript; public attachablescript attachable;
}
public class save_tankscript
{
    public uint id; public int ind; public int[] type; public float[] amount;
    public save_tankscript() { }
    public save_tankscript(save_tankscript r) { id = r.id; ind = r.ind; type = r.type?.ToArray(); amount = r.amount?.ToArray(); }
    public save_tankscript(tankscript t, uint root, int index) { id = root; ind = index; type = t.F.fluids.Select(f => f.type).ToArray(); amount = t.F.fluids.Select(f => f.amount).ToArray(); }
    public void Load(tankscript t) { t.F.fluids = type.Select((v, i) => new mainscript.FluidValue { type = v, amount = amount[i] }).ToList(); }
}
public partial class itemDataClass { public List<int> items = new List<int> { 1 }; public List<save_carscript> car; }
public class save_carscript { public bool blood; }
public class washtriggerscript : Component { public bool ON, destroyOnRepair; public void GotClean() { ON = false; } }
public class busdoorscripts : Component
{
    public class Door { public usablescript[] UBoth, Uopen, Uclose, Usign; public Leaf[] doors; }
    public class Leaf : Component { }
    public Door[] doors; public bool noMultiSync, somethingChanged, Powered = true; public tosaveitemscript tosave;
    public bool Electronics() => Powered;
}
public partial class usablescript
{
    public bool slideAble, rotateAble, turnable; public Transform TDoor, RT1, RT2;
    public float maxX, minX, maxY, minY, xRot, yRot, slideValue;
}
public class inputconnect : Component { public Component Sdoor_rot, Sdoor_move; public usablescript turnOverride; public usablescript[] IUsableV0To1, IUsableV1To1; }
public class partconditionscript : Component { }
public class ediblescript : Component { public GameObject[] parts; public int index; public float hp; public bool continous, moreParts, destroyOnEnd; }
public partial class aiscript : Component { public bool fleeing; public float flightFight; public Senses senses; public class Senses { public List<int> stimuls = new List<int>(); } }
public partial class attachablescript : Component
{
    public tosaveitemscript tosave, Target; public Component Poi; public partslotscript slot; public Transform point; public bool parentAtAttach, attached;
    public tosaveitemscript AttachedToTosave() => Target;
    public Component AttachedToPOI() => Poi;
    public void Attach(Transform p) { point = p; attached = true; }
    public void Detach() { attached = false; }
}
namespace TLDRevamp
{
    internal static partial class SessionLog { internal static readonly List<string> Events = new List<string>(); internal static double St = 123.5; internal static void Event(string what, uint net = 0, int item = 0, int slot = 0, int to = 0, uint epoch = 0, int count = 0, int from = 0, string result = "") { Events.Add(what + ":" + result); } }
    internal static partial class GameLookups { internal static MethodInfo Method(Type t, string n, Type[] args = null) => args == null ? t.GetMethod(n) : t.GetMethod(n, args); }
}
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public sealed partial class Ent { public uint RootId, Epoch; public int OwnerId; public List<uint> ItemIds; public HashSet<int> Split; public Dictionary<int, float> EditHoldUntil; }
        private sealed partial class SEnt { public uint NetId; }
        internal static void TestServer(uint net, int owner) => Server[net] = new SEnt { NetId = net, OwnerId = owner };
        internal static void ClearServer() => Server.Clear();
        internal static List<Ent> Placed() => ByNet.Values.ToList();
        internal static int MyId;
        internal static bool _applyingState, _applyingAttach, _applyingDetach;
        internal static bool ApplyingState => _applyingState;
        internal static readonly Dictionary<tankscript, float> _tankFilledAt = new Dictionary<tankscript, float>();
        internal static readonly Dictionary<tosaveitemscript, float> EditDirty = new Dictionary<tosaveitemscript, float>();
        internal const byte Edit = 32;
        internal const int KindAll = 65535, KindBlood = 256, KindTank = 2;
        internal static readonly Dictionary<tosaveitemscript, int> EditKinds = new Dictionary<tosaveitemscript, int>();
        private static readonly List<tosaveitemscript> _editFlushed = new List<tosaveitemscript>();
        private static float _editFlushAt; private const float EditHold = 1.5f;
        internal static long EditsSent, TankAddsSent;
        private static readonly List<TankAdds.Add> _tankAdds = new List<TankAdds.Add>();
        private static readonly HashSet<(uint, int, double)> _tankAddsOut = new HashSet<(uint, int, double)>();
        private sealed class OneGroup { internal Ent[] Set(Ent e) => new[] { e }; }
        private static readonly OneGroup _oneGroup = new OneGroup();
        internal static bool TestTankChanges, TestWithCar;
        private static bool LiveItemMoving(tosaveitemscript it) => LiveMoving;
        private static Ent EditEntityOf(tosaveitemscript it) => SharedGroupOf(it);
        private static bool TankChangesNow(tosaveitemscript it, List<TankAdds.Add> into)
        {
            into.Clear(); if (TestTankChanges) into.Add(new TankAdds.Add { Tank = 0, Type = 2, Litres = -1.25f }); return TestTankChanges;
        }
        internal static string LastEdit;
        internal static void MarkEdit(tosaveitemscript it, bool fromTank = false, string kind = "") => LastEdit = kind;
        internal static Ent SharedGroupOf(tosaveitemscript it) => ByNet.Values.FirstOrDefault(e => e.Items.Contains(it));
        internal static Ent OwnEntity(tosaveitemscript it) => ByNet.Values.FirstOrDefault(e => e.Root == it);
        internal static void TelemetryRender() { }
    }
    internal static partial class ItemSnapshot { internal static itemDataClass Capture(tosaveitemscript it) => new itemDataClass { car = new List<save_carscript> { new save_carscript() } }; }
    internal static partial class Protocol { internal const byte LiveUsable = 93; internal const int StateHz = 20; }
}
