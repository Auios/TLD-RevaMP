using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public class CharacterController : Collider { }
    public class CapsuleCollider : Collider { }
    public enum RigidbodyInterpolation { None, Interpolate }
    public partial class Rigidbody : Component { public bool isKinematic; public RigidbodyInterpolation interpolation; public Vector3 position, velocity; public Quaternion rotation = Quaternion.identity; }
    public struct Pose { public Vector3 position; public Quaternion rotation; public Pose(Vector3 p, Quaternion q) { position = p; rotation = q; } }
    public partial class Transform
    {
        public Vector3 up = Vector3.up, right = new Vector3(1, 0, 0);
        public void SetPositionAndRotation(Vector3 p, Quaternion q) { position = p; rotation = q; }
    }
}
public class seatscript : Component
{
    public carscript Car; public bool driverSeat0, driverSeat1, Free = true; public seatscript mainseat; public Transform sitPos;
    public bool FreeCanSit() => Free;
}
public partial class mountStuff : Component { }
public partial class tosaveitemscript { public List<partslotscript> partslotscripts; }
public partial class fpscontroller
{
    public bool Bsitting; public seatscript seat; public Component Cam;
    public void GetIn(seatscript value) { seat = value; Bsitting = true; }
    public void GetOut(Vector3 pos, bool forced) { Bsitting = false; }
    public void RayStuff() { }
}
public partial class carscript
{
    public bool isPlayerDriving, driving2, multiControlling, iignition, iLightsBDown, ihandbrakeBDown, iignitionDown;
    public bool igrDown, ignDown, ig1Down, ig2Down, ig3Down, ig4Down, ig5Down, ig6Down, iupshiftDown, idownshiftDown;
    public float ihandbrake, startingTime = 1; public usablescript ignitionUsable;
    public int LightCalls, HandbrakeCalls;
    public void GetPlayerInput() { }
    public void ControlStuff() { }
    public void IgnitionStuff() { }
    public void InteractSwitchLights() { LightCalls++; }
    public void InteractHandbrake() { HandbrakeCalls++; }
}
public partial class usablescript { public bool Held; public float lastHoldTime; public bool Holding() => Held; }
namespace TLDRevamp
{
    internal static class DedicatedServer { internal static bool Enabled; }
    internal static class Session { internal static void Show(int n, string title, string message) { } }
}
namespace TLDRevamp.Net
{
    internal static class Mp
    {
        internal static Vector3d LocalGlobal() => mainscript.GlobalFromUnityPos(mainscript.s.player.transform.position);
        internal static bool SceneTearingDown;
        internal static bool RemoteTrace;
        internal static SeatServer Server = new SeatServer();
        internal class SeatServer
        {
            internal void Tell(int player, string notice) => Entities.AttachmentNotices.Add((player, notice));
            internal readonly List<KeyValuePair<int, int>> Seats = new List<KeyValuePair<int, int>>();
            internal readonly List<KeyValuePair<int, Vector3d>> Peers = new List<KeyValuePair<int, Vector3d>>();
            internal bool PeerPos(int player, out Vector3d position) { position = Entities.EditPeerPosition; return Entities.EditPeerKnown; }
            internal void ReadyPeers(List<KeyValuePair<int, Vector3d>> into) { into.Clear(); into.AddRange(Peers); }
            internal bool SendToPlayer(int player, NetWriter writer, bool reliable) => true;
            internal void SeatedIn(uint net, List<KeyValuePair<int, int>> into) { into.Clear(); into.AddRange(Seats); }
        }
    }
    public static partial class RemotePlayers
    {
        internal static bool Occupied;
        internal static readonly List<(int, string, int)> Players = new List<(int, string, int)>();
        internal static uint SeatNet;
        internal static int SeatIdx;
        internal static bool AnyRemoteInSeat(seatscript seat) => Occupied;
        internal static string NameOf(int id) => "driver";
        internal static IEnumerable<(int, string, int)> Each() => Players;
        internal static uint SeatNetOf(int id) => SeatNet;
        internal static int SeatIdxOf(int id) => SeatIdx;
    }
    public static partial class Entities
    {
        public sealed partial class Ent { public bool Driven; }
        private sealed partial class SEnt
        {
            public bool Car = true, Held, Stored, Driven, Rabbit;
            public int PartIndex = -1, ClaimedBy = -1, CrashReturnTo = -1;
            public uint Epoch, ClaimedFromEpoch, HitchCar, ParentNet;
            public float ClaimedAt, DrivenAt, RabbitHoldUntil;
        }
        internal enum ClaimReason : byte { Driver = 1, DriverRetry, Pickup, CargoNear, CargoContact, CargoRelease, Contents, ContentsPlaced, CarCargo, RabbitNear, RabbitContact, RabbitObjectNear, RabbitObjectContact, PushGrip, Wear, BodyNear, BodyContact, Debug, ItemNear, ItemContact }
        public const byte Claim = 24, Owner = 25;
        private static float _lastDriveClaimTry;
        internal static bool DriveClaimRootOnly = true;
        internal static int Cascades, Shares;
        internal static string ClaimLog;
        internal static long ClaimsSent, ClaimsRefused, RefusedCrash, RefusedHeld, RefusedStored, RefusedDriven;
        private static readonly Dictionary<uint, int> _refuseLogged = new Dictionary<uint, int>();
        private static string ClaimName(ClaimReason reason) => "claim: " + reason;
        private static void OwnerLog(SEnt se, int from, string why) { ClaimLog = why; }
        private static void CascadeOwner(SEnt se) { Cascades++; }
        private static string ShareItem(tosaveitemscript root, object value) { Shares++; return "{}"; }
        private static uint _claimedFor;
        internal static bool SeatToken = true, SeatNear = true;
        internal static long EditsOutOfReach;
        private static bool TakeKeyTokenForFixture(int from) => SeatToken;
        private static bool AtObject(int from, SEnt se) => SeatNear;
        internal static void TestSeatClaim(uint net, int by, float at) { Server[net].ClaimedBy = by; Server[net].ClaimedAt = at; }
        internal static object TestSeatServer(uint net) => Server[net];
    }
}
