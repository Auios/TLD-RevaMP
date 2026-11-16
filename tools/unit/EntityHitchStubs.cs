#if HITCH_RUNTIME
using HitchOp = TLDRevamp.Net.EntityHitches.HitchOp;
using System;
using System.Linq;
using System.Text.Json;
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static System.Collections.Generic.Dictionary<uint, HitchOp> _hitchCommands => (System.Collections.Generic.Dictionary<uint, HitchOp>)typeof(EntityHitches).GetField("_hitchCommands", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        internal static bool IsFinite(Vector3d v) => Finite(v);
        internal static bool IsFinite(UnityEngine.Vector3 v) => Finite(v);
        internal static bool IsFinite(UnityEngine.Quaternion v) => Finite(v);
        internal static void HitchFixedChecks()
        {
            for (int bits = 0; bits < 256; bits++)
                EntityHitchChecks.Row("refusal:" + bits + ":" + HitchAuthority.Refusal((bits & 1) != 0, (bits & 2) != 0,
                    (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0, (bits & 32) != 0, (bits & 64) != 0, (bits & 128) != 0));
            for (int scenario = 0; scenario < 12; scenario++)
            {
                RecoverySetup(0, 1, false); Near = scenario != 2;
                var op = new HitchOp { Trailer = 20, Car = 10, TrailerEpoch = 1, CarEpoch = 1, Slot = 0, Connect = true };
                if (scenario == 0) Server.Remove(20);
                if (scenario == 1) { Server[20].Hitch = false; Server[10].Car = false; }
                if (scenario == 3) op.TrailerEpoch = 0;
                if (scenario == 4) Server[20].Stored = true;
                if (scenario == 5) Server[20].HitchCar = 10;
                if (scenario == 6) op.Slot = -1;
                if (scenario == 7) op.Slot = 4097;
                if (scenario == 8) op.Connect = false;
                if (scenario == 9) _hitchOps[20] = op;
                if (scenario == 10) { Server[20].Stored = true; Server[20].HitchCar = 10; op.Slot = -1; op.TrailerEpoch = 0; Near = false; }
                var w = new NetWriter(); w.U8(HitchRequest); EntityHitches.WriteHitch(w, op); ServerPacket(Copy(w), 2);
                EntityHitchChecks.Capture("request-refusal:" + scenario);
            }
            Near = true;
        }
        internal static string HitchRow(string why) => JsonSerializer.Serialize(new {
            why, MyId, InSession, PlayerActing, _applyingAttach, _applyingDetach, _hitchApplying,
            HitchTransferTimeout, HitchEnsureKnown, HitchReleaseOnRemove, EntityHitches.ProxyGripsMoved, _hitchToken,
            time = UnityEngine.Time.time, realtime = UnityEngine.Time.realtimeSinceStartup,
            server = Server.OrderBy(k => k.Key).Select(k => new { k.Key, k.Value.OwnerId, k.Value.Epoch,
                k.Value.HitchCar, k.Value.HitchSlot, k.Value.AttachParent, k.Value.CrashReturnTo, k.Value.Held, k.Value.Stored }),
            local = ByNet.OrderBy(k => k.Key).Select(k => new { k.Key, k.Value.OwnerId, k.Value.Epoch,
                k.Value.HitchAuthorityOwner, k.Value.Proxy, k.Value.HitchWaiting, k.Value.HitchUntil, k.Value.SentAtRest,
                frozen = k.Value.HitchBodies.Count, body = k.Value.Root.RB.isKinematic,
                attached = k.Value.Root.attachable != null && k.Value.Root.attachable.attached,
                joint = k.Value.Root.attachable != null && k.Value.Root.attachable.J != null }),
            ops = _hitchOps.OrderBy(k => k.Key).Select(k => OpRow(k.Key, k.Value)),
            commands = _hitchCommands.OrderBy(k => k.Key).Select(k => OpRow(k.Key, k.Value)),
            transfers = _hitchTransfers.OrderBy(k => k.Key).Select(k => new { k.Key, k.Value.From, k.Value.To,
                k.Value.Epoch, k.Value.Token, k.Value.Until }),
            departed = _hitchDeparted.OrderBy(n => n),
            packets = Packets.Select(p => p.target + ":" + Convert.ToHexString(p.data)),
            requests = Requests.Select(Convert.ToHexString)
        });
        private static object OpRow(uint net, HitchOp op) => new { net, op.Car, op.Trailer, op.CarEpoch,
            op.TrailerEpoch, op.Slot, op.From, op.Connect, op.ReleaseBrake, op.Until };
    }
}
#else
using System.Collections.Generic;
using UnityEngine;
public class HitchTrailerBody
{
    public usablescript brake;
    public void RefreshCenterOffMass(bool attached, rearlights lights) { }
}
public class attachgripscript : Component { public Transform GripPos; }
public partial class attachablescript
{
    public Joint J;
    public void Attach(partslotscript target) { slot = target; attached = true; }
}
public partial class partslotscript
{
    public tosaveitemscript tosaveitem;
    public Transform T => transform;
    public List<attachablescript> parts = new List<attachablescript>();
    public bool hasPart() => parts.Count > 0;
    public bool CanCraft(attachablescript at) => true;
    public void Craft(attachablescript at) { }
}
public partial class fpscontroller { public void Drop() { pickedUp = null; } }
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private const float HitchTimeoutS = 5f;
        public static bool HitchTransferTimeout = true;
        private static readonly Dictionary<carscript, float> DamageGraceUntil = new Dictionary<carscript, float>();
        public sealed partial class Ent
        {
            public int HitchAuthorityOwner;
            public bool HitchWaiting, HitchHasVelocity;
            public float HitchUntil;

            public readonly List<Rigidbody> HitchBodies = new List<Rigidbody>();
        }
        internal static bool HitchTrailer(Ent e) => e != null && e.Root != null && e.Root.attachable != null && e.Root.attachable.utanfuto != null;
        internal static Ent HitchCar(attachablescript at) => at != null && at.slot != null ? EditEntityOf(at.slot.tosaveitem) : null;
        private static bool HitchCanJoin(Ent tr, Ent car) => tr.HitchAuthorityOwner == tr.OwnerId && car.HitchAuthorityOwner == car.OwnerId &&
            HitchAuthority.CanJoin(car.OwnerId, tr.OwnerId, MyId, tr.HitchWaiting, car.Proxy, tr.Proxy);
        private static void ApplyProxyBodies(Ent entity) { }
        private static void SendDetachSync(Ent entity) { }
    }
}
namespace UnityEngine { public partial class Object { public static void DestroyImmediate(Object value) { } } }
namespace TLDRevamp
{
    internal static partial class SessionLog
    {
        internal static void Event(string kind, uint net, uint other, int from = -1, int to = -1, int slot = -1,
            uint epoch = 0, int count = 0, string result = null) { }
    }
}
#endif
