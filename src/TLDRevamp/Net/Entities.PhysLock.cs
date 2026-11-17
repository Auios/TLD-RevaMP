using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    /// The physics-lock setting (Settings → Misc: which containers hold what is put in them - glovebox, baskets, racks,
    /// trunks; mountStuff.CanStore, and with any lock on, cars loaded into a trunk or passenger room). Each machine used
    /// its own, so an item in a basket was held or loose depending on which player owned it at the moment, and changed
    /// when it was handed over. The game's own MP: the host's setting rules - the host puts it in the lobby
    /// (syncScript.UpdPhysicsLockServer) and a client's CanStore reads it from there (syncScript.GetPhysicsLock).
    /// The same here: the host sends its setting when it changes and every PhysLockResendS (a player who just joined);
    /// on a client the game's checks read the host's value instead of the local one. The client's own setting is
    /// never changed - it is its setting again when the session ends.
    internal static partial class EntityLocks
    {
        private static readonly NetWriter W = new NetWriter();
        internal static float PhysLockResendS = 5f;
        internal static int HostPhysicsLock = -1;   // a client: the host's setting (-1: none received)
        internal static long PhysLockSent, PhysLockApplied, PhysLockReads;
        private static int _physLockLast = -1;
        private static float _physLockAcc;

        internal static void ResetHostSetting() { HostPhysicsLock = -1; _physLockLast = -1; }
        internal static void ResetGentleBodies()
        {
            foreach (var kv in _gentle) if (kv.Key != null) kv.Key.maxDepenetrationVelocity = kv.Value.was;
            _gentle.Clear();
        }

        internal static void PhysLockTick(float dt)
        {
            if (!Entities.IsHost || settingsscript.s == null || settingsscript.s.S == null) return;
            int v = settingsscript.s.S.IPhysicsLock;
            _physLockAcc += dt;
            if (v == _physLockLast && _physLockAcc < PhysLockResendS) return;
            _physLockLast = v; _physLockAcc = 0f;
            W.Reset(); W.U8(Entities.PhysLockSync); W.U8((byte)Mathf.Clamp(v, 0, 255));
            Entities.SendToPeers(W, true, 0);
            PhysLockSent++;
        }

        internal static void ApplyPhysLock(NetReader r)
        {
            int v = r.U8();
            if (r.Bad || Entities.IsHost) return;
            HostPhysicsLock = v;
            PhysLockApplied++;
        }

        /// What the game's checks read in place of settingsscript.s.S.IPhysicsLock.
        internal static int PhysicsLockSetting(settingsscript.setting s)
        {
            if (HostPhysicsLock >= 0 && Entities.InSession && !Entities.IsHost) { PhysLockReads++; return HostPhysicsLock; }
            return s.IPhysicsLock;
        }
        internal static long ContainerStoresRefused;
        internal static int StoreTrace;   // test: log the next N stores with their call stack
        private static bool _containerScan;
        private static mountStuff _scanning;     // the container whose scan is running (Update runs every frame: no lookups there)

        [HarmonyPatch(typeof(mountStuff), "Update")]
        private static class ContainerScan
        {
            [HarmonyPrefix]
            private static void Prefix(mountStuff __instance)
            {
                if (!Entities.InSession) return;
                _scanning = __instance;
                _containerScan = true;
            }
            [HarmonyFinalizer]
            private static System.Exception Finalizer(System.Exception __exception) { _containerScan = false; return __exception; }
        }
        internal static long CopyStartFreezesSkipped;
        internal static float CopyLockReleaseM = 5f, CopyLockReleaseS = 1f;
        internal static long CopyLocksReleased;
        internal static bool LockedAway(Ent e)
        {
            if (e.PoseLocked) return false;
            var p = e.Root != null ? e.Root.P : null;
            if (p == null || p.physlock == null || p.physlock.transform.root == e.Root.transform || e.Ip == null || !e.Ip.Ready) return false;
            return (mainscript.UnityPosFromGlobal(e.Ip.LastPos) - p.physlock.transform.position).sqrMagnitude > CopyLockReleaseM * CopyLockReleaseM;
        }
        internal static long StoredRides;
        internal static bool RidesOwnersContainer(Ent e)
        {
            if (e.PoseLocked) return false; // its authoritative frame pose, including local changes, is applied by the interpolator
            var p = e.Root != null ? e.Root.P : null;
            if (e.Held || p == null || p.physlock == null) return false;
            var cr = p.physlock.transform.root;
            if (cr == e.Root.transform) return false;
            return Entities.LockContainerOwnedBy(cr, e.OwnerId) && !OwnerHasItOut(e, p);
        }
        internal static float ContainerMarginM = 0.5f, ContainerUpM = 2f, ContainerStillMs = 0.5f;
        internal static float MountMarginM = 0.3f, MountUpM = 0.6f;
        public static long OutOfContainerSeen;
        private static Vector3 _diagLocal;
        internal static Vector3 ObservedDiagLocal => _diagLocal;
        private static Bounds _diagBox;   // the last check's numbers (mp lockdiag)
        internal static Bounds ObservedDiagBox => _diagBox;
        private static readonly Dictionary<Transform, Bounds> _containerBox = new Dictionary<Transform, Bounds>();
        private static float _containerBoxAt;
        internal static bool OwnerHasItOut(Ent e, pickupable p)
        {
            if (p == null || p.physlock == null || e.Ip == null || !e.Ip.Ready) return false;
            var root = p.physlock.transform.root;
            var rt = root.GetComponent<tosaveitemscript>();
            if (rt == null || root == e.Root.transform) return false;
            bool copy = Entities.LockContainerMotion(root, e.OwnerId, out bool sameOwner, out var velocity, out var pose);
            // a moving container: nothing is set down beside it, and the item's and the car's latest states are up to a
            // state apart (50 ms = 0.7 m at 50 km/h) - measured against the storing box the lamp left a car at speed
            // and trailed it 6.7 m (takeover.py B). The 5 m rule alone, as before.
            float still = ContainerStillMs;
            if (copy && (velocity.sqrMagnitude > still * still || e.LastVel.sqrMagnitude > still * still)) return false;
            Vector3 local;
            if (sameOwner && pose != null && pose.Ready)
            {
                var d = (Vector3)(e.Ip.LastPos - pose.LastPos);
                var sc = root.lossyScale;
                local = Quaternion.Inverse(pose.LastRot) * d;
                local = new Vector3(local.x / Mathf.Max(1e-3f, sc.x), local.y / Mathf.Max(1e-3f, sc.y), local.z / Mathf.Max(1e-3f, sc.z));
            }
            else
            {
                var rb = root.GetComponent<Rigidbody>();
                if (!copy && rb != null && !rb.isKinematic && rb.velocity.sqrMagnitude > still * still) return false;
                local = root.InverseTransformPoint(mainscript.UnityPosFromGlobal(e.Ip.LastPos));
            }
            bool inside;
            var mc = p.physlock.col;
            if (mc != null)
            {
                // the container's own storing box (mountStuff.col, as its scan places it: the game stores what overlaps it) -
                // the whole car's box took in a can set down by the doors (pallet.py C run 4: 1.8 m beside the seats)
                var cpt = mc.transform.parent != null ? mc.transform.parent : mc.transform;
                var centre = cpt.position + cpt.TransformDirection(mc.transform.localPosition + mc.center);
                var half = Vector3.Scale(mc.size, mc.transform.localScale) * 0.5f;
                var d = Quaternion.Inverse(mc.transform.rotation) * (root.TransformPoint(local) - centre);
                float mm = MountMarginM;
                inside = Mathf.Abs(d.x) <= half.x + mm && Mathf.Abs(d.z) <= half.z + mm && d.y >= -half.y - mm && d.y <= half.y + MountUpM;
                _diagLocal = d; _diagBox = new Bounds(Vector3.zero, half * 2f);
            }
            else
            {
                var b = ContainerBox(root, rt);
                float m = ContainerMarginM;
                inside = local.x >= b.min.x - m && local.x <= b.max.x + m && local.z >= b.min.z - m && local.z <= b.max.z + m
                      && local.y >= b.min.y - m && local.y <= b.max.y + ContainerUpM;
                _diagLocal = local; _diagBox = b;
            }
            if (!inside) OutOfContainerSeen++;
            return !inside;
        }

        /// The container's own box in its frame: its own colliders, not what it holds (stored items are its children) or
        /// the parts bolted on (their own items).
        private static Bounds ContainerBox(Transform root, tosaveitemscript rt)
        {
            if (Time.realtimeSinceStartup - _containerBoxAt > 30f) { _containerBox.Clear(); _containerBoxAt = Time.realtimeSinceStartup; }
            if (_containerBox.TryGetValue(root, out var b)) return b;
            bool any = false;
            foreach (var c in root.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger || c is WheelCollider || c.GetComponentInParent<tosaveitemscript>() != rt) continue;
                var wb = c.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var lp = root.InverseTransformPoint(corner);
                    if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
                }
            }
            if (!any) b = new Bounds(Vector3.zero, Vector3.one * 2f * CopyLockReleaseM);   // nothing to measure: the old rule's reach
            _containerBox[root] = b;
            return b;
        }
        internal static float ReleaseClearS = 0.5f;
        internal static long CopyLocksDroppedAttached, ReleasesPlaced, ReleaseGhostsCleared, AttachedStoresRefused, LockRecordsDropped;
        internal static void CheckCopyLock(Ent e, double now)
        {
            var p = e.Root.P;
            if (p == null || p.physlock == null) { e.LockOffSince = 0; return; }
            var at = e.Root.attachable;
            if (at != null && at.attached)
            {
                e.LockOffSince = 0;
                { DropAttachedLock(e, p); return; }
            }
            if (e.Ip == null || !e.Ip.Ready) { e.LockOffSince = 0; return; }
            var shownUnity = mainscript.UnityPosFromGlobal(e.CarriedBy != 0 ? e.CarriedGlobal : e.Ip.LastPos);
            // against the container: the copy itself may have been drawn at its owner's pose (v0.66.7 compared that)
            var held = p.physlock.transform.position;
            if (p.physlock.transform.root == e.Root.transform
                || ((shownUnity - held).sqrMagnitude <= CopyLockReleaseM * CopyLockReleaseM && !(e.Held) && !OwnerHasItOut(e, p))) { e.LockOffSince = 0; return; }
            bool inHand = e.Held;   // its owner holds it: in no container
            if (e.LockOffSince <= 0 && !inHand) { e.LockOffSince = now; return; }
            if (now - e.LockOffSince < CopyLockReleaseS && !inHand) return;
            e.LockOffSince = 0;
            string where = p.physlock.transform.root.name;
            float off = Vector3.Distance(shownUnity, held);
            p.UnPhysicsLock();
            Entities.RefreshLockBodies(e);
            PlaceReleased(e, shownUnity, e.CarriedBy != 0 ? e.CarriedGlobalRot : e.Ip.LastRot);
            CopyLocksReleased++;
            if (CopyLocksReleased <= 20) Plugin.Log.LogWarning($"copy taken out of a container here: {e.Root.name} net {e.NetId} in {where} — its owner has it {off:F0} m away");
        }

        /// A copy attached (bolted on) while a container here holds it: the lock alone goes — the part stays where the
        /// attach put it. Its last pose is stale too (no states while attached): a fresh interpolator.
        private static void DropAttachedLock(Ent e, pickupable p)
        {
            string where = p.physlock.transform.root.name;
            p.JustUnPhysicsLock();
            p.cantPickupWhileMount = false;
            e.Ip = new PoseInterpolator();
            CopyLocksDroppedAttached++;
            if (CopyLocksDroppedAttached <= 20) Plugin.Log.LogWarning($"copy bolted on while in a container here: {e.Root.name} net {e.NetId} in {where} — lock dropped, the part stays on");
        }

        /// A copy just taken out of a container: at its owner's pose now (transform and body: no sweep through the car
        /// it was in), no collisions until clear of every car.
        private static void PlaceReleased(Ent e, Vector3 pos, Quaternion rot)
        {
            var t = e.Root.transform;
            t.SetPositionAndRotation(pos, rot);
            foreach (var rb in e.Root.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.interpolation = RigidbodyInterpolation.None;
                rb.position = rb.transform.position; rb.rotation = rb.transform.rotation;
                rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                rb.detectCollisions = false;
            }
            e.ReleaseGhost = true; e.ReleaseClearSince = -1f;
            ReleasesPlaced++;
        }
        public static long GhostsEndedOnOwn;

        /// Ours now: collisions back at once. Still inside a car, it waited for clear — but simulated here with no
        /// collisions it never got clear: a can put back in a pickup bed fell through the bed and the chassis, 200-300 m
        /// down (review 2026-10-22, #18). Inside a car it comes out of the car's colliders gently instead of popping out.
        internal static float OwnGhostDepenetration = 1f;   // m/s
        internal static void EndGhostOnOwn(Ent e)
        {
            if (e.Root != null && ReleaseOverlapsCar(e))
            {
                float back = Time.realtimeSinceStartup + GentleS;
                foreach (var rb in e.Root.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                    if (!_gentle.ContainsKey(rb)) _gentle[rb] = (rb.maxDepenetrationVelocity, back);
                    rb.maxDepenetrationVelocity = OwnGhostDepenetration;
                }
                GhostsEndedInCar++;
            }
            e.ReleaseGhost = false;
            Entities.ShowLockCollisions(e);
            GhostsEndedOnOwn++;
        }
        public static long GhostsEndedInCar;
        internal static float GentleS = 3f;   // the gentle way out of the car lasts this long, then the body's own value again
        private static readonly System.Collections.Generic.Dictionary<Rigidbody, (float was, float until)> _gentle = new System.Collections.Generic.Dictionary<Rigidbody, (float, float)>();
        private static readonly System.Collections.Generic.List<Rigidbody> _gentleDone = new System.Collections.Generic.List<Rigidbody>();
        internal static void GentleTick()
        {
            if (_gentle.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            foreach (var kv in _gentle) if (kv.Key == null || now >= kv.Value.until) _gentleDone.Add(kv.Key);
            foreach (var rb in _gentleDone) { if (rb != null) rb.maxDepenetrationVelocity = _gentle[rb].was; _gentle.Remove(rb); }
            _gentleDone.Clear();
        }

        /// Per frame for a released copy: collisions back once no car's collider has overlapped it for ReleaseClearS
        /// (an object simulated here: as soon as it is clear, since it falls with no collisions meanwhile).
        internal static void ReleaseGhostTick(Ent e)
        {
            if (e.Root == null) return;
            float now = Time.realtimeSinceStartup;
            if (!e.Proxy) { EndGhostOnOwn(e); return; }
            if (ReleaseOverlapsCar(e)) { e.ReleaseClearSince = -1f; return; }
            if (e.ReleaseClearSince < 0f) { e.ReleaseClearSince = now; return; }
            if (now - e.ReleaseClearSince < ReleaseClearS) return;
            e.ReleaseGhost = false;
            Entities.ShowLockCollisions(e);
            ReleaseGhostsCleared++;
        }

        private static bool ReleaseOverlapsCar(Ent e)
        {
            var own = e.Root.transform.root;
            bool any = false; var b = default(Bounds);
            foreach (var c in e.Root.GetComponentsInChildren<Collider>(false))
            {
                if (c == null || c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any) return false;
            return Entities.LockOverlapsCar(b, own);
        }

        [HarmonyPatch(typeof(mountStuff), nameof(mountStuff.QuickFreeze))]
        private static class CopyStartFreeze
        {
            [HarmonyPrefix]
            private static bool Prefix(mountStuff __instance)
            {
                if (!Entities.InSession || !Entities.IsLockCopy(__instance)) return true;
                if (StoreTrace > 0)
                {
                    var g = mainscript.GlobalFromUnityPos(__instance.transform.root.position);
                    Plugin.Log.LogInfo("[storetrace] copy start freeze: " + __instance.transform.root.name + " at " + g.x.ToString("F1") + "," + g.y.ToString("F1") + "," + g.z.ToString("F1") + " frame " + Time.frameCount);
                }
                CopyStartFreezesSkipped++;
                return false;
            }
        }
        public static long GluedCraftedCleared;
        [HarmonyPatch(typeof(attachablescript), nameof(attachablescript.Detach))]
        private static class GluedCraftedOff
        {
            // Clear crafted before the detach broadcast captures the released part, as under Entities.
            [HarmonyPriority(Priority.Normal + 1)]
            [HarmonyPostfix]
            private static void Postfix(attachablescript __instance, bool __runOriginal)
            {
                var p = __instance.tosave != null ? __instance.tosave.P : null;
                if (!__runOriginal || p == null || !p.crafted || __instance.attached) return;
                p.RefreshCrafted(false, false);
                GluedCraftedCleared++;
            }
        }


        [HarmonyPatch(typeof(mountStuff), nameof(mountStuff.Store))]
        private static class ContainerStore
        {
            [HarmonyPrefix]
            private static bool Prefix(mountStuff __instance, pickupable _p)
            {
                if (StoreTrace > 0 && _p != null)
                {
                    StoreTrace--;
                    Plugin.Log.LogInfo("[storetrace] " + __instance.transform.root.name + " stores " + _p.name + " scan " + _containerScan + "\n" + System.Environment.StackTrace);
                }
                // attached is never stored: the game's own scan checks it, a container's saved contents don't — a copy of
                // a car placed again stored a part that its owner had bolted back on meanwhile (partsfar.py far)
                if (Entities.InSession && _p != null && _p.tosave != null && _p.tosave.attachable != null && _p.tosave.attachable.attached)
                { AttachedStoresRefused++; return false; }
                if (!_containerScan || _p == null || _scanning == null) return true;
                bool ok = Entities.LockScanAllows(_scanning, _p);
                if (ok) return true;
                ContainerStoresRefused++;
                return false;
            }
        }
    }

    public static partial class Entities
    {
        private static readonly Collider[] _clearHits = new Collider[64];
        public const byte PhysLockSync = 55;
    }
}
