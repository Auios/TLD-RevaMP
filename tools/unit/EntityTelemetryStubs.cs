using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public class BoxCollider : Collider { public Vector3 center, size; }
    public class Joint : Component { public Rigidbody connectedBody; public float breakForce; }
    public partial class Rigidbody { public float drag, angularDrag; public bool useGravity; public Vector3 angularVelocity; }
    public struct WheelHit { public float forwardSlip, sidewaysSlip, force; public Collider collider; }
    public struct WheelFrictionCurve { public float extremumSlip, extremumValue, asymptoteSlip, asymptoteValue, stiffness; }
    public static partial class Physics
    {
        public static Collider[] Overlaps = Array.Empty<Collider>();
        public static Collider[] OverlapBox(Vector3 c, Vector3 e, Quaternion q, int mask, QueryTriggerInteraction t) => Overlaps;
        public static Collider[] OverlapSphere(Vector3 c, float r, int mask, QueryTriggerInteraction t) => Overlaps;
        public static RaycastHit[] RaycastAll(Vector3 c, Vector3 d, float r, int mask, QueryTriggerInteraction t) => Hits;
    }
    public partial struct Bounds { public void Encapsulate(Bounds b) { } }
    public class PhysicMaterial : Object { public float dynamicFriction; }
    public partial struct LayerMask { public static string LayerToName(int layer) => "layer" + layer; }
}
public partial class fpscontroller { public Component mainCam; }
public partial class carscript
{
    public float selectedgr, throttle;
    public List<BoxCollider> whColliders;
    public ParticleSystem CoolantBoil, CoolantSteam;
    public CoolantEmission CoolantSteamEmission;
    public class CoolantEmission { public bool enabled; public Rate rateOverTime; }
    public class Rate { public float constant; }
    public Coolant coolant;
    public class Coolant { public tankscript coolant; }
    public tankscript Tank;
}
public partial class mountStuff
{
    public BoxCollider col; public Collider[] cols, lastcols;
    public int sType; public bool onlyManual, noPhysicsLock;
    public bool CanStore() => true;
}
public partial class pickupable
{
    public bool dontMount, pickedUp; public float DroppedTime;
    public List<ColliderLink> cols;
    public class ColliderLink { public pickupable p; }
    public Component disableThisWhenStored;
}
public partial class tosaveitemscript { public bool mppickedup, mpequipped, mpstored, mpweared, started; }
public class syncScript { public static bool IsConnected() => true; }
public partial class PoseInterpolator
{
    public double DelayMs, JitterMs, RenderTime;
    public bool Extrapolating;
    public Vector3d LastPos;
}
namespace TLDRevamp
{
    internal sealed class MarkFocus
    {
        internal struct Target { public uint Net; public int Prefab; public string Name; public double Distance; }
        internal Target? Crosshair, Car, Held, Dragging;
        internal int Seat = -1, ClosestPlayer = -1; internal double PlayerDistance;
    }
    internal static partial class TelemetryPrivacy { internal static string ObjectName(string name) => name; }
    internal static class MotionTrace { internal const int FocusLimit = 16; }
}
namespace TLDRevamp.Net
{
    internal static class MarkWire { internal const int Limit = 16; }
    internal static class PlaceDigest { internal const int Bytes = 6; }
    public static partial class Entities
    {
        internal static long RepairsAsked, RepairsHealed, RepairsFailed, RepairsRun;
        internal static List<string> CensusTestRows = new List<string> { "(car) = car", "slot = wheel" };
        internal static List<string> RepairRows(Ent e) => CensusTestRows;
        internal static void CheckCopyPlace(Ent e, int asker, ushort x, ushort y, ushort z) { }
        internal static int FocusRefreshes; internal static bool LastFocusForced;
        private static void TelemetryFocusTick(double now, bool force = false) { FocusRefreshes++; LastFocusForced = force; }
        private static bool IsProxy(mountStuff m) => IsProxy((Component)m);
        public sealed partial class Ent
        {
            public uint ParentNet; public bool PartsColOff, SentAtRest;
            public int Frames, ExtrapFrames, Jumps, SeqOut, Recv, WhyAwake, WhyPos, WhyRot, WhyStored;
            public float MaxDevCm; public Vector3d LastSentPos;
        }
    }
}

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static bool IsStored(tosaveitemscript it) => false;
    }
}

public partial class enginescript { public float rpm, actualOutput, brakeOutput, iwrpm, gas, clutch, wrpm, drpm, rpmDifference, maxNm, modifier, temp; public bool freeRun; }
public partial class fpscontroller { public carscript Car; }
public partial class mainscript { public int mountLayerMask = -1; }
public class savedatascript
{
    public static savedatascript s; public SaveData data;
    public class SaveData { public ItemData itemData; }
    public class ItemData { public List<Record> items = new List<Record>(); }
    public class Record { public uint id; public SavedTransform transform; }
    public class SavedTransform { public Vector3d pos; }
    public static bool IndexOfID(List<Record> rows, uint id, out int k) { k = rows.FindIndex(r => r.id == id); return k >= 0; }
}
namespace UnityEngine
{
    public partial class Collider { public Rigidbody attachedRigidbody; public PhysicMaterial sharedMaterial; }
    public partial class WheelCollider
    {
        public bool enabled = true; public float mass, sprungMass, radius, rpm, wheelDampingRate;
        public Rigidbody attachedRigidbody; public WheelFrictionCurve forwardFriction, sidewaysFriction; public Spring suspensionSpring;
        public struct Spring { public float spring, damper; }
        public bool GetGroundHit(out WheelHit h) { h = default; return isGrounded; }
    }
    public static partial class Time { public static double unscaledTimeAsDouble, fixedTimeAsDouble; }
}
namespace TLDRevamp
{
    internal static partial class SessionLog
    {
        internal static void Fail(string s, Exception e) { throw e; }
        internal static partial class History
        {
            internal static partial class Trace { internal static uint[] Nets = new uint[MotionTrace.FocusLimit]; internal static uint Focus(int i) => Nets[i]; }
        }
    }
    internal static partial class GameLookups { internal static T FieldValue<T>(Type t, string name, object o) => default; }
    internal static partial class DevHooks { internal static Func<string> CommandNote; }
}
public partial class pickupable { public bool crafted; }

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static string AddRejectLast = "";
        internal static double AddRejects;
        internal static string AttachFails = "";
        internal static double AttachReapplied;
        internal static double AttachesApplied;
        internal static double AttachesFailed;
        internal static double AttachesKept;
        internal static double AttachesReplayed;
        internal static double AttachesSame;
        internal static double AttachesSent;
        internal static double AutoDetachesIgnored;
        internal static double CargoClaimed;
        internal static double CargoClaimedNear;
        internal static double CargoClaimedOnContact;
        internal static readonly List<uint> CargoGhostNets = new List<uint>();
        internal static double CargoGhostsMade;
        internal static double ContentsClaimed;
        internal static double ContentsOnOwner;
        internal static double ContentsOnPlace;
        internal static double ContentsStored;
        internal static double DetachReqsApplied;
        internal static double DetachReqsSent;
        internal static double DetachSyncsApplied;
        internal static double DetachSyncsSent;
        internal static string EditMarks = "";
        internal static double EditsApplied;
        internal static double EditsMarked;
        internal static double EditsRejected;
        internal static double FarAdds;
        internal static double FarAttachedMoved;
        internal static double FarRechunked;
        internal static double FarRecordUpdates;
        internal static double FarResyncs;
        internal static double GameFrozenReleased;
        internal static string HandoverLateWhy = "";
        internal static double HandoversApplied;
        internal static double HandoversCrashSkipped;
        internal static double HandoversLate;
        internal static double HandoversSent;
        internal static double HeldShown;
        internal static double ItemClaimedNear;
        internal static double ItemClaimedOnContact;
        internal static readonly List<string> LeaseAsked = new List<string>();
        internal static double MembersRebound;
        internal static double OrphansCleared;
        internal static double OwnerChanges;
        internal static string PartRejects = "";
        internal static double PartStatesChanged;
        internal static double PartsApplied;
        internal static double PartsAppliedStored;
        internal static double PartsReported;
        internal static double PushFrames;
        internal static double PushesApplied;
        internal static double PushesSent;
        internal static double RagdollLimbsRestored;
        internal static double Rebound;
        internal static double Reshared;
        internal static double ResyncBudgetFrames;
        internal static double ResyncBytes;
        internal static double ResyncCaptureMsMax;
        internal static double ResyncCaptureMsTotal;
        internal static double ResyncHeld;
        internal static double ResyncItemsLoaded;
        internal static double ResyncItemsSent;
        internal static double ResyncSkipped;
        internal static double ResyncSplitSkipped;
        internal static double ResyncsApplied;
        internal static double ResyncsSent;
        internal static double SilentTakeovers;
        internal static double Spawned;
        internal static double StateListsApplied;
        internal static double StateListsSame;
        internal static double StatesIn;
        internal static double StatesSent;
        internal static double StatesStale;
        internal static double SuppressedFallOff;
    }
}
