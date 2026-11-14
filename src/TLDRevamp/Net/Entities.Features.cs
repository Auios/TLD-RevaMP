using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public const byte BreakHit = 48, BreakFx = 49, AiState = 50, AiSound = 51, WorldBreak = 112;
        private static readonly bool _aiLookupsReady = EntityAI.InitializeAiLookups();
        public static string AiStats() => EntityAI.AiStats();
        internal static bool BreakRef(breakablescript br, out uint net, out int bi) => EntityAI.BreakRef(br, out net, out bi);
        internal static float BodyR => EntityAI.BodyR;
        internal static float SeenAt(newAiScript a, Vector3 from, Vector3 dir, float maxDist, Vector3 feet, Vector3 top, bool skipSelf) => EntityAI.SeenAt(a, from, dir, maxDist, feet, top, skipSelf);
        internal static int TumbleweedsNear(thumbleweedMain m) => EntityAI.TumbleweedsNear(m);
        public static long TumbleShared { get => EntityAI.TumbleShared; set => EntityAI.TumbleShared = value; }
        public static long TumbleLimited { get => EntityAI.TumbleLimited; set => EntityAI.TumbleLimited = value; }
        public static bool AiStickyRemote { get => EntityAI.AiStickyRemote; set => EntityAI.AiStickyRemote = value; }
        public static int BreakTrace { get => EntityAI.BreakTrace; set => EntityAI.BreakTrace = value; }
        public static bool BreaksKept { get => EntityAI.BreaksKept; set => EntityAI.BreaksKept = value; }
        public static bool RagdollListHealed { get => EntityAI.RagdollListHealed; set => EntityAI.RagdollListHealed = value; }
        public static float TumbleNearM { get => EntityAI.TumbleNearM; set => EntityAI.TumbleNearM = value; }
        public static bool WorldBreakGibs { get => EntityAI.WorldBreakGibs; set => EntityAI.WorldBreakGibs = value; }
        public static float WorldBreakM { get => EntityAI.WorldBreakM; set => EntityAI.WorldBreakM = value; }
        public static float WorldBreakNearM { get => EntityAI.WorldBreakNearM; set => EntityAI.WorldBreakNearM = value; }
        public static bool WorldBreaksKept { get => EntityAI.WorldBreaksKept; set => EntityAI.WorldBreaksKept = value; }

        internal static Ent BreakObjectOf(tosaveitemscript it)
        {
            foreach (var e in ByNet.Values) if (e.Root == it) return e;
            foreach (var e in ByNet.Values) if (e.Items != null && e.Items.Contains(it)) return e;
            return null;
        }
        internal static void CollectCreatureAis(System.Collections.Generic.List<newAiScript> into)
        {
            foreach (var e in ByNet.Values)
                foreach (var it in e.Items) if (it != null && it.npc != null) into.Add(it.npc);
        }
        internal static System.Collections.Generic.IEnumerable<thumbleweedScript> KnownTumbleweeds()
        {
            foreach (var e in ByNet.Values) if (e.Root != null && e.Root.thumbleweed != null) yield return e.Root.thumbleweed;
        }
        internal static bool CreatureAudience(Vector3d at) => OwnerAudience(at);
        internal static void KeepBreakIndex(uint net, int bi)
        {
            if (!Server.TryGetValue(net, out var se)) return;
            if (se.Broken == null) se.Broken = new System.Collections.Generic.List<int>();
            if (se.Broken.Count < EntityAI.MaxBreaksPerObject) se.Broken.Add(bi);
        }
        private static void SendBreaksTo(int playerId, SEnt se) => EntityAI.SendBreaksTo(playerId, se.NetId, se.Broken);

        private static void ServerBreakHit(int from, NetReader r)
        {
            uint net = r.U32(); int bi = (int)r.VarU32(); byte kind = r.U8();
            if (r.Bad || !Server.TryGetValue(net, out var se) || se.OwnerId == from) return;
            // where the hit thing is: the host's own copy of it if it has one (a creature walks away from where its
            // object's record says), else the object's last position, with a shot's reach for anything
            var br = EntityAI.BreakableOf(net, bi);
            bool exact = br != null;
            var at = exact ? mainscript.GlobalFromUnityPos(br.transform.position) : se.HasState ? se.Pos : se.Where;
            double reach = kind == 1 || !exact ? HitCheck.ObjShotM : HitCheck.ObjForceM;
            if (!HitCheck.AllowOnObject(from, $"breakable {bi} of object {net} {(br != null ? br.transform.root.name : "not placed here")} ({(kind == 1 ? "shot" : kind == 3 ? "bite" : "blow")})", at, reach, exact ? 10f : HitCheck.ObjSizeM)) return;
            r.Pos = 0;
            WS.Reset(); WS.Bytes(r.Buf, 0, r.End);
            ServerSendTo(se.OwnerId, WS, true);
        }

        private static void ServerFwdAll(int from, NetReader r, bool reliable)
        {
            r.Pos = 0;
            WS.Reset(); WS.Bytes(r.Buf, 0, r.End);
            ServerSendAll(WS, reliable, from);
        }

        /// Server: a creature hit's attacker must own the creature (the shared object it belongs to); `where` is the
        /// creature as the host has it (its copy here, else the owner's last state), when it has one.
        internal static bool CreatureOf(int from, uint net, int idx, out Vector3d where, out bool known)
        {
            where = default; known = false;
            if (!Server.TryGetValue(net, out var se) || se.OwnerId != from) return false;
            var a = EntityAI.AiOf(net, idx);
            var ny = a == null && ByNet.TryGetValue(net, out var re) && Resolve(re) ? NyOf(re) : null;
            if (a != null) { where = mainscript.GlobalFromUnityPos(a.transform.position); known = true; }
            else if (ny != null) { where = mainscript.GlobalFromUnityPos(ny.transform.position); known = true; }
            else if (se.HasState) { where = se.Pos; known = true; }
            return true;
        }

        internal static System.Collections.Generic.IEnumerable<Ent> PlayerCorpses()
        {
            foreach (var e in ByNet.Values)
                if (e.Root != null && e.Root.name.IndexOf("PlayerRagdol", System.StringComparison.Ordinal) >= 0) yield return e;
        }
        internal static bool TryDeathCorpse(GameObject go, out uint net)
        {
            foreach (var e in ByNet.Values)
                if (e.Root != null && (e.Root.transform == go.transform || e.Root.transform.IsChildOf(go.transform))) { net = e.NetId; return true; }
            net = 0; return false;
        }
        internal static void ForgetDeathCorpse(uint net) => ByNet.Remove(net);
        internal static void TrackRagdollLimb(GameObject go, bool copy) { if (copy) ProxyItems.Add(go); else ProxyItems.Remove(go); }
        static partial void RabBodyCheck(aiscript rab, ref bool gone);
        internal static bool RagdollRabbitBodyGone(aiscript rab) { bool gone = false; RabBodyCheck(rab, ref gone); return gone; }
        /// The network id of a shared object by its save id (0 while the share is still pending).
        internal static uint NetIdOfSaveId(uint saveId)
        {
            if (saveId == 0) return 0;
            foreach (var e in ByNet.Values) if (e.RootId == saveId) return e.NetId;
            return 0;
        }

        internal static Transform[] Limbs(Ent e) => EntityRagdolls.Limbs(e);
        internal static bool Limp(Ent e) => EntityRagdolls.Limp(e);
        internal static System.Collections.Generic.IReadOnlyCollection<Ent> ObservedRagdollCopies => EntityRagdolls.ObservedRagdollCopies;
        public static string RagdollPrefabs() => EntityRagdolls.RagdollPrefabs();
        public static string CorpseList() => EntityRagdolls.CorpseList();
        public static string DeathInfo() => EntityRagdolls.DeathInfo();
        public static bool RagdollDelta { get => EntityRagdolls.RagdollDelta; set => EntityRagdolls.RagdollDelta = value; }
        public static bool DeathCorpseStays { get => EntityRagdolls.DeathCorpseStays; set => EntityRagdolls.DeathCorpseStays = value; }
        public static float RagdollKeyEveryS { get => EntityRagdolls.RagdollKeyEveryS; set => EntityRagdolls.RagdollKeyEveryS = value; }
        public static bool DeathRagdollShare { get => EntityRagdolls.DeathRagdollShare; set => EntityRagdolls.DeathRagdollShare = value; }
        public static bool CorpseOutfitSaved { get => EntityRagdolls.CorpseOutfitSaved; set => EntityRagdolls.CorpseOutfitSaved = value; }
        public static bool CorpseWhole { get => EntityRagdolls.CorpseWhole; set => EntityRagdolls.CorpseWhole = value; }
        public static float RagdollDeltaMaxShare { get => EntityRagdolls.RagdollDeltaMaxShare; set => EntityRagdolls.RagdollDeltaMaxShare = value; }
        public static long RagdollStatesSent { get => EntityRagdolls.RagdollStatesSent; set => EntityRagdolls.RagdollStatesSent = value; }
        public static long RagdollStatesApplied { get => EntityRagdolls.RagdollStatesApplied; set => EntityRagdolls.RagdollStatesApplied = value; }
        public static long RagdollKeysSent { get => EntityRagdolls.RagdollKeysSent; set => EntityRagdolls.RagdollKeysSent = value; }
        public static long RagdollDeltasSent { get => EntityRagdolls.RagdollDeltasSent; set => EntityRagdolls.RagdollDeltasSent = value; }
        public static long RagdollFramesSent { get => EntityRagdolls.RagdollFramesSent; set => EntityRagdolls.RagdollFramesSent = value; }
        public static long RagdollLimbsSent { get => EntityRagdolls.RagdollLimbsSent; set => EntityRagdolls.RagdollLimbsSent = value; }
        public static long RagdollBlockBytes { get => EntityRagdolls.RagdollBlockBytes; set => EntityRagdolls.RagdollBlockBytes = value; }
        public static long RagdollKeysIn { get => EntityRagdolls.RagdollKeysIn; set => EntityRagdolls.RagdollKeysIn = value; }
        public static long RagdollDeltasIn { get => EntityRagdolls.RagdollDeltasIn; set => EntityRagdolls.RagdollDeltasIn = value; }
        public static long RagdollDeltasNoKey { get => EntityRagdolls.RagdollDeltasNoKey; set => EntityRagdolls.RagdollDeltasNoKey = value; }
        public static long RagdollHashMismatch { get => EntityRagdolls.RagdollHashMismatch; set => EntityRagdolls.RagdollHashMismatch = value; }
        public static long RagdollLateKeys { get => EntityRagdolls.RagdollLateKeys; set => EntityRagdolls.RagdollLateKeys = value; }
        public static long RagdollReleasedOnNone { get => EntityRagdolls.RagdollReleasedOnNone; set => EntityRagdolls.RagdollReleasedOnNone = value; }
        public static long DeathCorpsesShared { get => EntityRagdolls.DeathCorpsesShared; set => EntityRagdolls.DeathCorpsesShared = value; }
        public static uint DeathCorpseSaveId { get => EntityRagdolls.DeathCorpseSaveId; set => EntityRagdolls.DeathCorpseSaveId = value; }
        public static uint DeathCorpseNetId { get => EntityRagdolls.DeathCorpseNetId; set => EntityRagdolls.DeathCorpseNetId = value; }
        public static System.Collections.Generic.List<uint> DeathItemSaveIds => EntityRagdolls.DeathItemSaveIds;
        public static System.Collections.Generic.List<uint> DeathItemNetIds => EntityRagdolls.DeathItemNetIds;

        public const byte Worn = 85;   // c to s: net, epoch, slot and pose; s to c: net, wearer, slot and pose
        public static float HeldStateHz { get => EntityHands.HeldStateHz; set => EntityHands.HeldStateHz = value; }
        public static float FastAfterReleaseS { get => EntityHands.FastAfterReleaseS; set => EntityHands.FastAfterReleaseS = value; }
        public static float HandItemPullM { get => EntityHands.HandItemPullM; set => EntityHands.HandItemPullM = value; }
        public static float HandItemPullDeg { get => EntityHands.HandItemPullDeg; set => EntityHands.HandItemPullDeg = value; }
        public static float WornClaimEveryS { get => EntityHands.WornClaimEveryS; set => EntityHands.WornClaimEveryS = value; }
        public static float WornOffHeadM { get => EntityHands.WornOffHeadM; set => EntityHands.WornOffHeadM = value; }
        public static bool HandItemDraw { get => EntityHands.HandItemDraw; set => EntityHands.HandItemDraw = value; }
        public static bool RidingFrames { get => EntityHands.RidingFrames; set => EntityHands.RidingFrames = value; }
        public static bool WornSync { get => EntityHands.WornSync; set => EntityHands.WornSync = value; }
        public static bool WornInShare { get => EntityHands.WornInShare; set => EntityHands.WornInShare = value; }
        public static bool WornLateOk { get => EntityHands.WornLateOk; set => EntityHands.WornLateOk = value; }
        public static int WornClaimTries { get => EntityHands.WornClaimTries; set => EntityHands.WornClaimTries = value; }
        public static void WornLate() => EntityHands.WornLate();
        public static string Wearables() => EntityHands.Wearables();
        public static string WornStatus() => EntityHands.WornStatus();
        internal static int LocalWornSlot(tosaveitemscript it) => EntityHands.LocalWornSlot(it);
        internal static bool WornRides(Ent e) => EntityHands.WornRides(e);
        internal static bool HandManaged(Ent e) => EntityHands.HandManaged(e);
        internal static Transform ItemHand(Ent e) => EntityHands.ItemHand(e);
        internal static string HandItemsJson(int owner, Transform hand) => EntityHands.HandItemsJson(owner, hand);

        internal static bool HasEntity(uint net) => ByNet.ContainsKey(net);
        internal static System.Collections.Generic.IEnumerable<Ent> PlacedHandItems()
        {
            foreach (var e in Placed()) yield return e;
        }
        internal static System.Collections.Generic.IEnumerable<Ent> HeldHandCopies(int owner)
        {
            foreach (var e in ByNet.Values) if (e.Proxy && e.OwnerId == owner && e.Held) yield return e;
        }
        internal static System.Collections.Generic.IEnumerable<Ent> WornEntities()
        {
            foreach (var e in ByNet.Values) if (e.Root != null && e.Root.P != null && e.Root.P.wear != null) yield return e;
        }
        internal static bool LooseCarriedRelease(uint previousCar, uint car, bool held, bool stored) => LooseRelease(previousCar, car, held, stored);
        internal static bool ClaimReleasedCarried(Ent e) => ClaimCargo(e, released: true, reason: ClaimReason.CargoRelease);
        internal static bool TryLockedHandWorld(Ent e, out Vector3d pos, out Quaternion rot) => LockedWorld(e, out pos, out rot);
        internal static void ExitHandPhysical(Ent e, Rigidbody body) => ExitPhysical(e, body);
        internal static void ClaimWorn(Ent e)
        {
            SetProxy(e, false); e.OwnerId = MyId; e.SentWornSlot = -1;
            W.Reset(); WriteClaim(W, e.NetId, ClaimReason.Wear); ToServer(W, true); ClaimsSent++;
        }

        private static void ReadShareWorn(NetReader r, SEnt se, int from)
        {
            if (!EntityHands.ReadShareWorn(r, out var slot, out var lp, out var lq)) return;
            se.WornBy = from; se.WornSlot = slot; se.WornLp = lp; se.WornLq = lq;
            EntityHands.SharedWorn++;
        }
        private static bool ServerWornNow(SEnt se) => se.WornBy >= 0 && se.WornBy == se.OwnerId;
        internal static void KeepWornRequest(int from, uint net, uint epoch, byte slot, Vector3 lp, Quaternion lq)
        {
            if (!Server.TryGetValue(net, out var se)) return;
            if (slot != 255 && (slot > 15 || !Finite(lp) || lp.sqrMagnitude > 4f)) return;   // on the head: within 2 m of it
            if (se.OwnerId != from && !WornTakesBack(se, from, slot)) return;
            if (!EntityHands.WornLateOk && !ClaimEpochOk(se, from, epoch)) return;
            se.WornBy = slot == 255 ? -1 : from; se.WornSlot = slot == 255 ? -1 : slot; se.WornLp = lp; se.WornLq = lq;
            EntityHands.RelayWorn(from, se.NetId, ServerWornNow(se), se.WornBy, se.WornSlot, se.WornLp, se.WornLq);
        }
        private static bool WornTakesBack(SEnt se, int from, byte slot)
        {
            if (!EntityHands.WornLateOk) return false;
            if (slot == 255) return false;   // taken off: the new owner's game has it off already
            if (se.PrevOwnerId != from || Time.realtimeSinceStartup - se.PrevOwnerAt > HandoverAcceptS || se.Held || se.Stored || ServerWornNow(se) || se.HitchCar != 0)
            {
                EntityHands.WornTooLate++;
                SessionLog.Event("worn-refused", se.NetId, from: from, to: se.OwnerId, result: "no longer theirs");
                return false;
            }
            se.ClaimedBy = from; se.ClaimedAt = Time.realtimeSinceStartup; se.ClaimedFromEpoch = se.Epoch;
            OwnerLog(se, from, "on their head before the claim got here");
            se.OwnerId = from; se.Epoch++;
            W.Reset(); W.U8(Owner); W.U32(se.NetId); W.VarU32((uint)from); W.U32(se.Epoch);
            ServerSendAll(W, true, -1);
            CascadeOwner(se);
            EntityHands.WornTakenBack++;
            return true;
        }

        private static void SendWornTo(int playerId, SEnt se)
        {
            if (!ServerWornNow(se)) return;
            EntityHands.SendWornTo(playerId, se.NetId, se.WornBy, se.WornSlot, se.WornLp, se.WornLq);
        }

        public const byte IgnitionKey = 96;   // u32 net, u8 held: a non-owner holding a car's key (reliable)
        public const byte ShotgunIn = 39;
        public static int ServerObjects => Server.Count;   // the admin console's status
        public static float DriveClaimHoldS { get => EntitySeats.DriveClaimHoldS; set => EntitySeats.DriveClaimHoldS = value; }
        public static bool SeatLeaveOnExit { get => EntitySeats.SeatLeaveOnExit; set => EntitySeats.SeatLeaveOnExit = value; }
        public static bool RearSeats { get => EntitySeats.RearSeats; set => EntitySeats.RearSeats = value; }
        public static bool ItemSeats { get => EntitySeats.ItemSeats; set => EntitySeats.ItemSeats = value; }
        public static float KeyResendS { get => EntitySeats.KeyResendS; set => EntitySeats.KeyResendS = value; }
        public static float KeyHoldTimeoutS { get => EntitySeats.KeyHoldTimeoutS; set => EntitySeats.KeyHoldTimeoutS = value; }
        public static float KeyTapMarginS { get => EntitySeats.KeyTapMarginS; set => EntitySeats.KeyTapMarginS = value; }
        public static long KeyHoldsSent { get => EntitySeats.KeyHoldsSent; set => EntitySeats.KeyHoldsSent = value; }
        public static long KeyHoldsApplied { get => EntitySeats.KeyHoldsApplied; set => EntitySeats.KeyHoldsApplied = value; }
        public static long KeyHoldsRefused { get => EntitySeats.KeyHoldsRefused; set => EntitySeats.KeyHoldsRefused = value; }
        public static long SeatLeavesLogged { get => EntitySeats.SeatLeavesLogged; set => EntitySeats.SeatLeavesLogged = value; }
        public static string SeatGuardStats() => EntitySeats.SeatGuardStats();
        internal static seatscript[] VehicleSeats(tosaveitemscript vehicle, bool always = false) => EntitySeats.VehicleSeats(vehicle, always);
        internal static void LocalSeat(out uint net, out int idx) => EntitySeats.LocalSeat(out net, out idx);
        internal static Transform SeatTransformCached(uint net, int idx, ref Transform carRoot, ref Transform seat) => EntitySeats.SeatTransformCached(net, idx, ref carRoot, ref seat);
        internal static seatscript SeatScript(uint net, int idx, out carscript car) => EntitySeats.SeatScript(net, idx, out car);
        internal static Transform SeatTransform(uint net, int idx) => EntitySeats.SeatTransform(net, idx);
        internal static string SeatWhy(uint net, int idx) => EntitySeats.SeatWhy(net, idx);
        internal static bool SeatAimFrame(out Transform drawn, out Rigidbody body) => EntitySeats.SeatAimFrame(out drawn, out body);
        internal static Vector3 SeatAimPoint(Transform drawn, Rigidbody body, Vector3 p) => EntitySeats.SeatAimPoint(drawn, body, p);

        internal static bool DriverClaimIs(uint net) => _claimedFor == net;
        internal static void ClearDriverClaim() => _claimedFor = 0;
        internal static void CollectSeatedPlayers(uint net, System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>> into) => Mp.Server?.SeatedIn(net, into);
        internal static bool HasProxySeatRoot(Transform root)
        {
            foreach (var e in LookupSet()) if (e.Root != null && e.Proxy && root == e.Root.transform.root) return true;
            return false;
        }
        internal static System.Collections.Generic.IEnumerable<Ent> PlacedSeatObjects()
        {
            foreach (var e in Placed()) if (e.Root != null && e.PartIndex < 0) yield return e;
        }
        internal static Ent ProxySeatCar(carscript car, bool rootOnly = false)
        {
            foreach (var e in ByNet.Values) if (e.Proxy && e.Root != null && e.Root.car == car && (!rootOnly || e.PartIndex < 0)) return e;
            return null;
        }
        internal static Ent SeatObjectAbove(Transform st)
        {
            Ent best = null; int bestDepth = int.MaxValue;
            foreach (var e in ByNet.Values)
            {
                if (e.Root == null || e.PartIndex >= 0 || !st.IsChildOf(e.Root.transform)) continue;
                int d = 0;
                for (var p = st; p != null && p != e.Root.transform; p = p.parent) d++;
                if (d < bestDepth) { bestDepth = d; best = e; }
            }
            return best;
        }
        /// Server, a claim of a car: null = may take it, else why not.
        private static string DriverSeatClaimRefusal(SEnt se, int from)
        {
            if (!se.Car || se.PartIndex >= 0) return null;
            int by = EntitySeats.DriverSeatTakenBy(se.NetId, from);
            if (by >= 0) return "driven (player " + by + " sits at the wheel)";
            if (se.ClaimedBy >= 0 && se.ClaimedBy != from && se.OwnerId == se.ClaimedBy && Time.realtimeSinceStartup - se.ClaimedAt < EntitySeats.DriveClaimHoldS)
                return "driven (player " + se.ClaimedBy + " just took the wheel)";
            return null;
        }

        internal static bool TryKeyOwner(uint net, int from, out int owner)
        {
            owner = 0;
            if (!Server.TryGetValue(net, out var se) || se.OwnerId == from || !se.Car) return false;
            owner = se.OwnerId;
            return true;
        }
        internal static bool TakeKeyToken(int from) => TakeToken(_editBuckets, from, EditRatePerS, EditBurst);
        internal static bool KeyReachAllowed(int from, uint net) => AtObject(from, Server[net]);
        internal static void RefuseKeyReach(int from, uint net) => Refuse(ref EditsOutOfReach, "key reach", $"ignition key from player {from}: they aren't at net {net}");
        internal static bool TryPassengerControlOwner(int from, uint net, out int owner)
        {
            owner = 0;
            if (!Server.TryGetValue(net, out var se) || se.OwnerId == from) return false;
            if (!HitCheck.AllowOnObject(from, $"passenger controls of car {net}", se.HasState ? se.Pos : se.Where, HitCheck.ObjSeatM, 10f)) return false;
            owner = se.OwnerId;
            return true;
        }

        public static void LateSample() => EntityDoors.LateSample();
        public static string CarDoors(uint net) => EntityDoors.CarDoors(net);
        public static string Food(uint net) => EntityDoors.Food(net);
        public static string Rabbit(uint net) => EntityDoors.Rabbit(net);
        internal static System.Collections.Generic.List<DoorRef> DoorsOf(Ent e) => EntityDoors.DoorsOf(e);
        internal static bool ApplyingDoorAttach { get => _applyingAttach; set => _applyingAttach = value; }
        internal static bool ApplyingDoorDetach => _applyingDetach;
        internal static void ApplyDoorAttachPose(Ent e, attachablescript at, Vector3 position, Quaternion rotation) => ApplyAttachPose(e, at, position, rotation);

        public const byte CarTune = 113;   // both: u32 net, CarTuning
        public const int MaxTuneBytes = 2048;
        public static float CarTuneEveryS { get => EntityCarTuning.CarTuneEveryS; set => EntityCarTuning.CarTuneEveryS = value; }
        public static float CarTuneResendS { get => EntityCarTuning.CarTuneResendS; set => EntityCarTuning.CarTuneResendS = value; }
        public static float CopyFluidCheckS { get => EntityCarTuning.CopyFluidCheckS; set => EntityCarTuning.CopyFluidCheckS = value; }
        public static float CopyFluidToleranceL { get => EntityCarTuning.CopyFluidToleranceL; set => EntityCarTuning.CopyFluidToleranceL = value; }
        public static long BloodCleanedOnCopy { get => EntityCarTuning.BloodCleanedOnCopy; set => EntityCarTuning.BloodCleanedOnCopy = value; }
        public static long BloodCleanSent { get => EntityCarTuning.BloodCleanSent; set => EntityCarTuning.BloodCleanSent = value; }
        public static long BloodCleanApplied { get => EntityCarTuning.BloodCleanApplied; set => EntityCarTuning.BloodCleanApplied = value; }
        public static long TunesSent { get => EntityCarTuning.TunesSent; set => EntityCarTuning.TunesSent = value; }
        public static long TunesResent { get => EntityCarTuning.TunesResent; set => EntityCarTuning.TunesResent = value; }
        public static long TunesApplied { get => EntityCarTuning.TunesApplied; set => EntityCarTuning.TunesApplied = value; }
        public static long TunesReapplied { get => EntityCarTuning.TunesReapplied; set => EntityCarTuning.TunesReapplied = value; }
        public static long TunesReplayed { get => EntityCarTuning.TunesReplayed; set => EntityCarTuning.TunesReplayed = value; }
        public static long TunesRefused { get => EntityCarTuning.TunesRefused; set => EntityCarTuning.TunesRefused = value; }
        public static long TunesKept { get => EntityCarTuning.TunesKept; set => EntityCarTuning.TunesKept = value; }
        public static string TuneLast { get => EntityCarTuning.TuneLast; set => EntityCarTuning.TuneLast = value; }
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, TuneHere> ObservedTuneHere => EntityCarTuning.ObservedTuneHere;
        internal static CarTuning CaptureTuning(carscript c) => EntityCarTuning.CaptureTuning(c);
        internal static int ApplyTuning(carscript c, CarTuning t) => EntityCarTuning.ApplyTuning(c, t);
        internal static int PrefabTuneKey(tosaveitemscript root) => EntityCarTuning.PrefabTuneKey(root);
        internal static bool IsTunableCar(Ent e) => EntityCarTuning.IsTunableCar(e);
        internal static void KnownTunings(System.Collections.Generic.Dictionary<uint, byte[]> into) => EntityCarTuning.KnownTunings(into);
        internal static void TunedRootIds(System.Collections.Generic.HashSet<uint> into) => EntityCarTuning.TunedRootIds(into);

        internal static System.Collections.Generic.IEnumerable<Ent> PlacedTuningCars()
        {
            foreach (var e in Placed()) if (!e.Proxy && EntityCarTuning.IsTunableCar(e)) yield return e;
        }
        internal static bool TuneOwnerIs(uint net, int from) => Server.TryGetValue(net, out var se) && se.OwnerId == from;
        internal static void KeepOwnedTune(uint net, byte[] bytes, int off, int len)
        {
            var se = Server[net];
            se.Tune = new byte[len]; System.Buffer.BlockCopy(bytes, off, se.Tune, 0, len);
        }
        internal static void MarkFeatureEdit(tosaveitemscript it, bool fromTank = false, string kind = "") => MarkEdit(it, fromTank, kind);
        internal static void ForgetTankEditTruth(uint id) => _tankTold.Remove(id);
        internal static bool HasPendingItemEdit(tosaveitemscript it) => EditDirty.ContainsKey(it);
        internal static bool TryTankFilledAt(tankscript tank, out float at) => _tankFilledAt.TryGetValue(tank, out at);
        internal static void SetFluidApplyingState(bool value) => _applyingState = value;
        internal static void CollectCopyFluidIds(System.Collections.Generic.HashSet<uint> into)
        {
            foreach (var e in ByNet.Values) if (e.Proxy && e.ItemIds != null) foreach (var id in e.ItemIds) into.Add(id);
        }
        internal static System.Collections.Generic.IEnumerable<Ent> CopyFluidEntities()
        {
            foreach (var e in ByNet.Values) if (e.Proxy && e.Root != null && e.Items != null) yield return e;
        }

        public const byte RadioSync = 54;
        public static float RadioResendS { get => EntityRadio.RadioResendS; set => EntityRadio.RadioResendS = value; }
        public static string RadioStatus() => EntityRadio.RadioStatus();

        public const byte WorldClock = 97;   // f64 st, f32 world seconds, f32 rate, u16 sleep epoch, bool snap
        public static float SleepClockS { get => EntityWorldClock.SleepClockS; set => EntityWorldClock.SleepClockS = value; }
        internal static void ApplyWorldTime(float wt, bool force) => EntityWorldClock.ApplyWorldTime(wt, force);

        public const byte StormSync = 56;
        public static float StormResendS { get => EntityWeather.StormResendS; set => EntityWeather.StormResendS = value; }
        public static long StormSyncsSent { get => EntityWeather.StormSyncsSent; set => EntityWeather.StormSyncsSent = value; }
        public static long StormSyncsApplied { get => EntityWeather.StormSyncsApplied; set => EntityWeather.StormSyncsApplied = value; }
        public static long StormsMade { get => EntityWeather.StormsMade; set => EntityWeather.StormsMade = value; }
        public static long StormsRemoved { get => EntityWeather.StormsRemoved; set => EntityWeather.StormsRemoved = value; }
        public static long StormSnaps { get => EntityWeather.StormSnaps; set => EntityWeather.StormSnaps = value; }
        internal static System.Collections.Generic.IReadOnlyDictionary<sandstormscript, uint> ObservedStormIds => EntityWeather.ObservedStormIds;
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, sandstormscript> ObservedStormById => EntityWeather.ObservedStormById;

        public static string CarSignals(uint net) => EntityCarSignals.CarSignals(net);
        internal static int CarSignalKey(Ent e) => EntityCarSignals.CarSignalKey(e);

        internal static Ent EntOfRoot(Transform root)
        {
            var own = PlacedOfTransform(root);
            if (own != null) return own;
            foreach (var e in LookupSet()) if (e.Root != null && e.Root.transform.root == root) return e;
            return null;
        }

        internal static bool TankRef(tankscript t, out uint net, out int ti)
        {
            net = 0; ti = -1;
            foreach (var e in ByNet.Values)
            {
                if (e.Items == null || e.Root == null) continue;
                int i = TanksOf(e).IndexOf(t);
                if (i >= 0) { net = e.NetId; ti = i; return true; }
            }
            return false;
        }

        public const byte PlayerSound = 57, ShotFx = 45;
        public static long PlayerSoundsSent { get => EntityEffects.PlayerSoundsSent; set => EntityEffects.PlayerSoundsSent = value; }
        public static long PlayerSoundsPlayed { get => EntityEffects.PlayerSoundsPlayed; set => EntityEffects.PlayerSoundsPlayed = value; }
        public static long ModelStepsMuted { get => EntityEffects.ModelStepsMuted; set => EntityEffects.ModelStepsMuted = value; }
        public static long[] SoundsSentBy { get => EntityEffects.SoundsSentBy; set => EntityEffects.SoundsSentBy = value; }
        public static long[] SoundsPlayedBy { get => EntityEffects.SoundsPlayedBy; set => EntityEffects.SoundsPlayedBy = value; }
        internal static string ObservedLastSound => EntityEffects.ObservedLastSound;
        public static bool SprayFx { get => EntityEffects.SprayFx; set => EntityEffects.SprayFx = value; }
        internal static bool IsSpraying(tosaveitemscript it) => EntityEffects.IsSpraying(it);
        internal static sprayscript HeldSpray(fpscontroller pl) => EntityEffects.HeldSpray(pl);
        public static string LampState(uint net) => EntityEffects.LampState(net);
        public static string SprayFxState(uint net) => EntityEffects.SprayFxState(net);
        public static string ShotFxStats() => EntityEffects.ShotFxStats();
        private static readonly bool _effectsLookupsReady = EntityEffects.InitializeLookups();

        internal static bool IsFinite(Vector3d value) => Finite(value);
        internal static bool IsFinite(Quaternion value) => Finite(value);
        internal static bool ResolveEntity(Ent entity) => Resolve(entity);
        internal static bool ItemRef(tosaveitemscript it, out uint net, out int idx)
        {
            net = 0; idx = -1;
            if (it == null) return false;
            foreach (var e in ByNet.Values)
            {
                if (e.Items == null) continue;
                int i = e.Items.IndexOf(it);
                if (i >= 0) { net = e.NetId; idx = i; return true; }
            }
            return false;
        }

        public const byte ExplodeReq = 46, ExplosionFx = 47;
        public static string ExplosionStats() => EntityExplosions.ExplosionStats();

        internal static bool TryExplosionRequestOwner(int from, uint net, out int owner)
        {
            owner = 0;
            if (!Server.TryGetValue(net, out var se) || se.OwnerId == from) return false;
            if (!HitCheck.AllowOnObject(from, $"explosive or tank on object {net}", se.HasState ? se.Pos : se.Where, HitCheck.ObjShotM, HitCheck.ObjSizeM)) return false;
            owner = se.OwnerId;
            return true;
        }

        internal static bool TryGetEntity(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static float HostClockRate => EntityWorldClock.HostClockRate;
        internal static bool IsFinite(Vector3 value) => Finite(value);
        internal static bool IsFinite(float value) => Finite(value);
        internal static bool IsFinite(double value) => Finite(value);
        internal static void SendToPeer(int playerId, NetWriter w, bool reliable) => ServerSendTo(playerId, w, reliable);
        internal static void SendToPeers(NetWriter w, bool reliable, int except, Vector3d? near = null, double range = 0) => ServerSendAll(w, reliable, except, near, range);
    }
}
