using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public const byte Share = 20, Assigned = 21, Add = 22, State = 23, Claim = 24, Owner = 25, PartDetached = 27, RemoveItem = 35, CompactState = 98, Refetch = 58, MemberGone = 86;
        public sealed class Ent
        {
            public uint NetId, Epoch, RootId, ParentNet, CarriedBy, ReceivedStateEpoch, LockFrame, LockContainer;
            public int OwnerId, PartIndex = -1, HitchAuthorityOwner = -1, WornBy = -1, LockItem = -1, LockMount = -1, TelemetryCategory = 7;
            public tosaveitemscript Root; public List<tosaveitemscript> Items = new(); public List<uint> ItemIds; public HashSet<int> Split;
            public bool Proxy, Stored, Held, PoseLocked, ReleaseGhost, CargoGhost, HandPose, SentAtRest, PartsColOff, FarDirty, MovedByBody, Physical, HitchWaiting;
            public class RagState { public bool Has; }
            public RagState Rag;
            public PoseInterpolator Ip, HandDraw = new(); public object LockPose, QuietTail; public mountStuff PoseLockMount; public Vector3 LockLocalPos, LastVel; public Quaternion LockLocalRot;
            public List<Rigidbody> MadeKinematic = new(); public wheelgraphicsscript[] Wheels; public ulong[] ResyncHashes;
            public byte[] SettledState; public Vector3d FarPos; public Quaternion FarRot; public float FarFlushAt;
        }
        private sealed partial class SEnt { public byte[] RagdollKeyState, Tune; public uint RagdollKeyEpoch; }
        private static readonly Dictionary<uint, Ent> ByNet = new(), PendingShare = new();
        private static readonly Dictionary<uint, SEnt> Server = new();
        private static uint _nextNet = 1;
        private static readonly NetWriter W = new(), WS = new();
        private static readonly Dictionary<int, HashSet<uint>> Known = new(), _staleEnts = new(), _poseNear = new(), _rabbitNear = new();
        private static readonly Dictionary<int, long> _interestSkips = new();
        private static readonly List<KeyValuePair<int, Vector3d>> _peerPos = new();
        private static readonly HashSet<SEnt> _serverLocked = new();
        private static readonly HashSet<GameObject> ProxyItems = new();
        private static readonly Dictionary<wheelgraphicsscript, Ent> ProxyWheelOwner = new();
        private static readonly Dictionary<wheelgraphicsscript, int> ProxyWheelIndex = new();
        private static readonly Dictionary<object, Ent> ProxyEngineOwner = new();
        private static readonly Dictionary<carscript, float> DamageGraceUntil = new();
        private static readonly List<Ent> _placed = new(), _all = new();
        private static readonly Dictionary<tosaveitemscript, Ent> _byItem = new(); private static readonly Dictionary<Transform, Ent> _byOwn = new();
        private static bool _placedDirty = true; private static int _placedFrame = -1, _fullFrame = -1000, _itemsSeen = -1, _netsSeen = -1;
        private static int LockBindingRevision;
        internal static bool _applyingDetach, _tpaSending, _tpaSendFailed;
        public static bool SuppressDestroyEcho,StoredContentsApart = true,SplitPartsApart = true,FarMoveAttached = true,FarPartsByAttach = true,FarGroupLog,RemoveFarMembers = true,ReshareSurvivors = true,RefetchParts = true,RefetchLater = true,AnomalyWatch,ReplayBareDetach,LoopPlacedOnly = true,HitchReleaseOnRemove = true,OwnerCascade = true;
        public static long AttachesKept, OwnerCascaded;
        private static readonly FarBoltIndex<save_attachable> _boltIdx = new(a => a != null && a.attachType == 1 ? a.parentid : 0);
        private static readonly Queue<uint> _boltQ = new();
        private static readonly Dictionary<uint, float> _refetchedAt = new(), _refetchLater = new();
        private struct Hung { public int Prefab, Member; public Vector3 Local; }
        private sealed class LostPart { public uint Car; public int Idx; public List<Hung> Hanging; }
        public static bool LostPartMembers = true; public static long LostMembersBack, LostMembersUnmatched, PartsCameBack, MembersGoneRejected, MembersGoneApplied, MemberDeletesApplied;
        private static readonly Dictionary<uint, LostPart> _lostParts = new();
        private static readonly List<tosaveitemscript> _reshare = new();
        private static readonly HashSet<int> _hitchDeparted = new();
        private static readonly Dictionary<uint, int> _hitchOps = new(), _hitchTransfers = new();
        private static readonly List<int> _rideCars = new(), CargoGhostNets = new(), _tankAddsOut = new(), _censusFirst = new(), _censusDue = new(), _repairWait = new(), _repairCycle = new(), _repairRunAt = new(), _placeRunAt = new(), _placeWait = new();
        private static int _repairCursor;
        public static int StoreOutTrace, FullEveryFrames = 30, PlacedCount;
        public static float FarStrayM = 15, RefetchEveryS = 30;
        public static double InterestM = 3000;
        public static long Spawned,StoredContentsShared,ShareMembersDropped,StoredShown,MembersRebound,Rebound,FarRecordUpdates,FarRechunked,FarAttachedMoved,FarStraysPulled,GameFrozenReleased,RagdollLimbsRestored,CopyMembersMissing,PartsApplied,PartsAppliedStored,PartsDeduped,FarShared,FarAdds,InterestSent,FarMembersRemoved,PartsKeptOnRemove,PartsOrphaned,PartsOrphanedNoRecord,SpawnShared,DestroyTraces,PlayerDeletesSent,MemberDeletesSent,Replaced,CopiesLost,OrphansCleared,AddRejects,PlacedRebuilds,PlacedFullPasses;
        public static string PartRejects = "", UnmountTrace = "", AddRejectLast = "";
        public static bool InSession => Mp.Server != null || Mp.Client != null && Mp.Client.MyId > 0; public static bool IsHost => Mp.Server != null; public static int MyId => IsHost ? 0 : Mp.Client?.MyId ?? -1;
        public static bool ApplyingDetach => _applyingDetach;
        public static string Bytes(NetWriter w) => Convert.ToHexString(w.Buf.AsSpan(0, w.Len));
        internal static void Note(string s) => RegistryFakes.Trace.Add(s);
        static void ShareFacts(byte[] rec, SEnt e) { e.Where = RecordRootPos(rec); Note("share-facts:" + e.NetId); }
        static void ReadShareWorn(NetReader r, SEnt e, int p) { if (r.Remaining > 0) r.U8(); Note("read-share-worn:" + e.NetId); }
        static int TelemetryPrefabCategory(int id) => id == 10 ? 1 : 7;
        static bool TpaDuplicate(uint n) => false;
        static void JoinReceived(uint n, int p, bool part = false) => Note("join-received:" + n + ":" + p + ":" + part);
        static bool HitchTrailer(Ent e) => e.Root?.attachable?.utanfuto != null;
        static void ShowHeld(Ent e, bool p) { e.Held = p; Note("held:" + e.NetId + ":" + p); }
        static void ShowCarriedCollisions(Ent e) => Note("collisions:" + e.NetId);
        static void EnsurePoseLock(Ent e) => Note("ensure-lock:" + e.NetId);
        static void RecoverWorldLock(Ent e) => Note("recover-lock:" + e.NetId);
        static void AnomalyPlaced(Ent e) => Note("anomaly-placed:" + e.NetId);
        static void ClaimIfPlacedInOwnCar(Ent e) => Note("claim-placed:" + e.NetId);
        static bool RidesBolted(Ent e) => false;

        static void ServerLockedPose(SEnt e) => Note("server-locked-pose:" + e.NetId);
        static void SendTpaState(int p, SEnt e) => Note("tpa-state:" + p + ":" + e.NetId);
        static void WriteResyncAll(NetWriter w, SEnt e) { w.Reset(); w.U8(40); w.U32(e.NetId); Note("resync-all"); }
        static void SendSettledState(int p, SEnt e) => Note("settled-replay:" + p + ":" + e.NetId);
        static void SendRabbitSnapshot(int p, SEnt e) => Note("rabbit-replay:" + p + ":" + e.NetId);
        static void SendCreatureSnapshots(int p, SEnt e) => Note("creature-replay:" + p + ":" + e.NetId);
        static void SendGoneMembers(int p, SEnt e) => Note("gone-replay:" + p + ":" + e.NetId);
        static void ReplayAttaches(int p, SEnt e) => Note("attach-replay:" + p + ":" + e.NetId);
        static void SendWornTo(int p, SEnt e) => Note("worn-replay:" + p + ":" + e.NetId);
        static void SendBreaksTo(int p, SEnt e) => Note("breaks-replay:" + p + ":" + e.NetId);
        static void RemoveRecord(uint id) { RegistryFakes.Save.s.items.Remove(id); var d = RegistryFakes.Save.s.data.itemData; if (RegistryFakes.Save.IndexOfID(d.items, id, out int k)) d.items.RemoveAt(k); Note("remove-record:" + id); }
        static void ReleaseHitch(SEnt e, string why) { Note("release-hitch:" + e.NetId + ":" + why); DropAttach(e); }
        static void LiveUsablesChanged(tosaveitemscript it) => Note("usables-changed:" + (it?.name ?? "null"));
        static void HandOver(tosaveitemscript it) => Note("handover:" + it.name);
        static void NoteMemberDestroyed(tosaveitemscript it) => Note("member-destroyed:" + it.name);
        static void AnomalyStoreOut(Ent e, tosaveitemscript it) => Note("anomaly-storeout:" + e.NetId);
        static List<Hung> HangingOn(uint n, int i, tosaveitemscript it) => new();
        static void WriteLostPart(NetWriter w, uint n) => w.Bool(false);
        static void OwnerLog(SEnt e, int p, string why) => Note("owner-log:" + e.NetId + ":" + p + ":" + why);
         static void EditCheckForget(int p) => Note("edit-forget:" + p);
        static void BeginHitchTransfer(SEnt e, int p) => Note("hitch-transfer:" + e.NetId + ":" + p);
        static void ServerPoiUsablesTo(int p) => Note("poi-join:" + p);
        static void ResetTpaSync() => Note("tpa-sync-reset");  static void PoiUsablesReset() => Note("poi-reset"); static void LiveUsablesReset(bool keepPoiSequences = false) => Note("usables-reset:" + keepPoiSequences); static void EditCheckReset() => Note("edit-reset");
        enum ClaimReason : byte { None = 0 }
        static string ClaimName(ClaimReason r) => r.ToString();
        public static void DestroyHook(tosaveitemscript it) => ItemDestroyed.PostfixForTest(it);

        static readonly List<string> Rows = new();
        static string Keys<T>(Dictionary<uint, T> d) => string.Join(",", d.Keys);
        static string Index(Dictionary<int, HashSet<uint>> d) => string.Join(";", d.Select(k => k.Key + ":" + string.Join(",", k.Value)));
        static string Local(Ent e) => $"{e.NetId}:{e.OwnerId}:{e.Epoch}:root={e.Root?.name ?? "null"}/{e.RootId}:items={string.Join(",", e.Items.Select(i => i?.name ?? "null"))}:ids={string.Join(",", e.ItemIds ?? new())}:split={string.Join(",", e.Split ?? new())}:proxy={e.Proxy}:ip={e.Ip != null}:stored={e.Stored}:far={e.FarPos}/{e.FarDirty}:hitch={e.HitchAuthorityOwner}/{e.HitchWaiting}:lock={e.PoseLocked}/{e.LockFrame}/{e.ReceivedStateEpoch}:physical={e.Physical}/{e.MovedByBody}:made={string.Join(",", e.MadeKinematic.Select(RegistryFakes.Label))}";
        static string Authority(SEnt e) => $"{e.NetId}:{e.OwnerId}:{e.Epoch}:prev={e.PrevOwnerId}/{e.PrevOwnerAt}/{e.HandoverOpen}:key={e.RagdollKeyState != null}/{e.RagdollKeyEpoch}:part={e.ParentNet}/{e.PartIndex}:parts={string.Join(",", e.Parts)}:split={string.Join(",", e.SplitOff)}:stored={e.Stored}:attach={e.AttachParent}/{e.LastAttach != null}/{string.Join(",", e.AttachKids ?? new())}:hitch={e.HitchCar}:record={Convert.ToHexString(e.Record ?? Array.Empty<byte>())}";
        static System.Collections.Generic.IEnumerable<System.Reflection.FieldInfo> SnapshotCounters() =>
            typeof(Entities).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                .SelectMany(f => f.Name == "PartsDeduped" ? new[] { f, typeof(EntityAttachments).GetField("CarriedOffApplied", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic) }
                    : f.Name == "OrphansCleared" ? new[] { f, typeof(EntityPhysics).GetField("CrashLeasesKept", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic) } : new[] { f });

        static string Snapshot()
        {
            var s = RegistryFakes.Save.s;
            string counters = string.Join(",", SnapshotCounters().Where(f => f.FieldType == typeof(long)).Select(f => f.Name + "=" + f.GetValue(null)));
            return "by=[" + string.Join(";", ByNet.Select(k => k.Key + "=" + Local(k.Value))) + "] server=[" + string.Join(";", Server.Select(k => k.Key + "=" + Authority(k.Value))) + "] pending=[" + string.Join(";", PendingShare.Select(k => k.Key + "=" + Local(k.Value))) + "] next=" + _nextNet +
                " placed=" + string.Join(",", _placed.Select(e => e.NetId)) + " all=" + string.Join(",", _all.Select(e => e.NetId)) + " item=" + string.Join(",", _byItem.Select(k => k.Key.name + ":" + k.Value.NetId)) + " own=" + string.Join(",", _byOwn.Select(k => k.Key.gameObject.name + ":" + k.Value.NetId)) + " dirty=" + _placedDirty + " frames=" + _placedFrame + "," + _fullFrame + "," + _itemsSeen + "," + _netsSeen +
                " proxy=" + string.Join(",", ProxyItems.Select(g => g.name)) + " wheelOwner=" + string.Join(",", ProxyWheelOwner.Select(k => k.Key.gameObject.name + ":" + k.Value.NetId)) + " wheelIndex=" + string.Join(",", ProxyWheelIndex.Select(k => k.Key.gameObject.name + ":" + k.Value)) + " engine=" + string.Join(",", ProxyEngineOwner.Values.Select(e => e.NetId)) + " locks=" + LockBindingRevision + " serverLocked=" + string.Join(",", _serverLocked.Select(e => e.NetId)) +
                " contacts=" + string.Join(",", s.items.Values.SelectMany(i => i.gameObject.Components.OfType<EntityPhysics.ContactRelay>().Where(c => !c.Dead).Select(c => i.name + ":" + c.NetId))) + " rag=" + string.Join(",", EntityRagdolls.RagdollCopies.Select(e => e.NetId)) + " limbs=" + string.Join(",", EntityRagdolls.RagdollLimbOwner.Select(k => k.Key.name + ":" + k.Value.NetId)) +
                " known=" + Index(Known) + " stale=" + Index(_staleEnts) + " poseNear=" + Index(_poseNear) + " rabbitNear=" + Index(_rabbitNear) + " hitchOps=" + Keys(_hitchOps) + " hitchTransfers=" + Keys(_hitchTransfers) + " departed=" + string.Join(",", _hitchDeparted) + " lost=" + Keys(_lostParts) + " refetch=" + Keys(_refetchedAt) + " later=" + Keys(_refetchLater) + " reshare=" + string.Join(",", _reshare.Select(i => i.name)) +
                " loaded=" + string.Join(",", s.items.Keys) + " records=" + string.Join(";", s.data.itemData.items.Select(i => i.id + ":" + i.transform.pos)) + " attaches=" + string.Join(",", s.data.itemData.attachable.Select(a => a.id + ":" + a.parentid)) + " chunks=" + string.Join(";", s.itemChunks.Select(c => c.Key + ":" + string.Join(",", c.Value))) +
                " flags=" + SuppressDestroyEcho + "," + _applyingDetach + "," + _tpaSending + "," + _tpaSendFailed + " W=" + Bytes(W) + " WS=" + Bytes(WS) + " queued=" + string.Join(",", RegistryFakes.Deferred.Select(RegistryFakes.Label)) + " counters=" + counters + " player=" + PlayerSave.State();
        }
        static void Row(string name, Action action)
        {
            action(); Rows.Add(name + "|" + Snapshot() + "|trace=" + string.Join(" > ", RegistryFakes.Trace).Replace("\r\n", "\\n").Replace("\n", "\\n")); RegistryFakes.Trace.Clear();
        }
        static void Fresh(bool host = false)
        {
            foreach (var f in typeof(Entities).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
            {
                if (f.Name == nameof(Rows)) continue;
                if (f.FieldType == typeof(long)) f.SetValue(null, 0L);
                var value = f.GetValue(null); if (value != null && value is not string && value is not NetWriter && value is not Array) value.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(value, null);
            }
            RegistryFakes.Save.s = new(); RegistryFakes.Main.s = null; RegistryFakes.Deferred.Clear(); RegistryFakes.Trace.Clear();
            EntityAttachments.CarriedOffApplied = EntityPhysics.CrashLeasesKept = 0;
            PlayerSave.Fresh(); Mp.PendingSceneStop = false;
            EntityRagdolls.RagdollCopies.Clear(); EntityRagdolls.RagdollLimbOwner.Clear(); EntityLifetimeStreaming.Deleted.Clear();
            Mp.Server = host ? new() : null; Mp.Client = host ? null : new(); Mp.SceneTearingDown = false; EntityTeleportRefresh.Deferred = false;
            _nextNet = 100; _placedDirty = true; _placedFrame = -1; _fullFrame = -1000; _itemsSeen = _netsSeen = -1; LockBindingRevision = 0;
            SuppressDestroyEcho = _applyingDetach = _tpaSending = _tpaSendFailed = false; W.Reset(); WS.Reset();
            StoredContentsApart = SplitPartsApart = FarMoveAttached = FarPartsByAttach = RemoveFarMembers = ReshareSurvivors = RefetchParts = RefetchLater = EntityPhysics.LodKeepsPartsOff = EntityLocks.GhostEndsOnOwn = EntityPhysics.CrashLeaveKeep = LoopPlacedOnly = HitchReleaseOnRemove = OwnerCascade = true;
            FarGroupLog = AnomalyWatch = ReplayBareDetach = false; StoreOutTrace = 0; PartRejects = UnmountTrace = AddRejectLast = "";
            Time.frameCount = 1; Time.realtimeSinceStartup = Time.time = 10;
        }
        static Ent Seed(uint net = 7, int owner = 2, bool proxy = false, bool car = true)
        {
            var root = RegistryFakes.Make(net * 10, "root-" + net, car); var member = RegistryFakes.Make(net * 10 + 1, "member-" + net);
            root.Group.Add(member); var e = new Ent { NetId = net, OwnerId = owner, Epoch = 3, Root = root, Items = new() { root, member } }; ByNet[net] = e; Bind(e); if (proxy) SetProxy(e, true); RegistryFakes.Trace.Clear(); return e;
        }
        static SEnt ServerSeed(uint net = 7, int owner = 2)
        {
            var s = new SEnt { NetId = net, OwnerId = owner, Epoch = 3, Record = RegistryFakes.Codec.Encode(new itemDataClass { items = new() { new() { id = 70, prefabID = 10 } } }) }; Server[net] = s; return s;
        }
        static void Read(byte type, Action<NetWriter> write)
        {
            var w = new NetWriter(); write(w); ClientReceive(type, new() { Buf = w.Buf, End = w.Len });
        }
        static void AssignedPacket(uint local, uint net = 7, uint epoch = 4) => Read(Assigned, w => { w.U32(local); w.U32(net); w.U32(epoch); });
        static itemDataClass Data(double x = 0, bool missing = false) => new() { items = new() { new() { id = 1, prefabID = 10, transform = new(new(x, 0, 0)) }, new() { id = 2, prefabID = missing ? -1 : 11, transform = new(new(x + 1, 0, 0)) } } };
        static void AddPacket(uint net, int owner = 3, bool state = false, double x = 0, bool missing = false)
        {
            var bytes = RegistryFakes.Codec.Encode(Data(x, missing)); Read(Add, w => { w.U32(net); w.VarU32((uint)owner); w.U32(4); w.Bool(state); if (state) { w.F64(x + 200); w.F64(2); w.F64(3); w.F32(0); w.F32(0); w.F32(0); w.F32(1); } w.VarU32((uint)bytes.Length); w.Bytes(bytes, 0, bytes.Length); });
        }
        static void PartPacket(uint part = 8, int owner = 3, int index = 1, uint carrier = 0) => Read(PartDetached, w => { w.U32(7); w.VarU32((uint)index); w.U32(part); w.VarU32((uint)owner); w.U32(4); w.F64(8); w.F64(0); w.F64(9); w.F32(0); w.F32(0); w.F32(0); w.F32(1); if (carrier != 0) { w.U32(carrier); w.VarU32(0); } });
        static void Store(uint id, double x = 0, uint parent = 0)
        {
            var s = RegistryFakes.Save.s; var rec = new save_item { id = id, prefabID = 10, transform = new(new(x, 0, 0)) }; s.data.itemData.items.Add(rec); s.AddToChunk(rec.transform.pos, id); if (parent != 0) s.data.itemData.attachable.Add(new() { id = id, parentid = parent, attachType = 1 });
        }
        static void RegistryScenarios()
        {
            Fresh(); Row("assigned-missing", () => AssignedPacket(70));
            var root = RegistryFakes.Make(70, "pending"); PendingShare[70] = new() { Root = root, Items = new() { root, null }, Stored = true };
            Row("assigned-consumes", () => AssignedPacket(70)); Row("assigned-repeat", () => AssignedPacket(70)); Row("assigned-placed", () => Placed());
            Fresh(); var old = Seed(); root = RegistryFakes.Make(72, "replacement"); PendingShare[72] = new() { Root = root, Items = new() { root } };
            Row("assigned-overwrites-without-destroy", () => AssignedPacket(72));
            Fresh(); Row("add-proxy", () => AddPacket(7)); Row("add-deferred-contacts", RegistryFakes.Flush); Row("add-placed", () => Placed()); Row("add-replacement", () => AddPacket(7));
            Fresh(); Row("add-owner-state-missing-slot", () => AddPacket(7, 2, true, 0, true));
            Fresh(); Row("add-short", () => Read(Add, w => { w.U32(7); w.VarU32(3); w.U32(4); w.Bool(false); w.VarU32(99); }));
            Row("add-undecodable", () => Read(Add, w => { w.U32(7); w.VarU32(3); w.U32(4); w.Bool(false); w.VarU32(0); }));
            Fresh(); EntityTeleportRefresh.Deferred = true; Row("add-tpa-deferred", () => AddPacket(7));
            Fresh(); Row("far-add-no-state", () => AddPacket(7, 3, false, 4000));
            Row("far-add-replacement-state", () => AddPacket(7, 3, true, 5000));
            Fresh(); var recoveryCar = Seed(); _lostParts[8] = new() { Car = 7, Idx = 1, Hanging = new() }; Row("add-lost-part-back-before-bind", () => AddPacket(8));
            Fresh(); recoveryCar = Seed(); _lostParts[8] = new() { Car = 7, Idx = 1, Hanging = new() }; Row("far-add-lost-part-back-after-store", () => AddPacket(8, 3, false, 5000));
            Fresh(true); Store(70, 5000); Store(71, 5001, 70); Store(72, 5100); Store(73, 5200, 99); RegistryFakes.Make(99, "loaded-parent");
            Row("share-far-group-and-skip-loaded-parent", () => ShareFarStore()); Row("share-far-covered", () => ShareFarStore());
            Fresh(); var car = Seed(); Row("split-owner", () => PartPacket(8, 2)); Row("split-already-present", () => PartPacket(8, 2));
            Fresh(); car = Seed(owner: 3, proxy: true); car.Items[1].attachable = car.Items[1].gameObject.AddComponent<RegistryFakes.Attach>(); car.Items[1].attachable.attached = true;
            Row("split-proxy-detach-guard", () => PartPacket()); Row("split-flush", RegistryFakes.Flush);
            Fresh(); car = Seed(owner: 3, proxy: true); var detached = RegistryFakes.Make(80, "already-detached"); ByNet[8] = new() { NetId = 8, Root = detached, Items = new() { detached }, OwnerId = 3 };
            Row("split-dedups-twin", () => PartPacket()); Row("split-twin-destroy-deferred", RegistryFakes.Flush);
            Fresh(); car = Seed(owner: 3); car.Items[1] = null; Store(71, 5000, 70); Row("split-far", () => PartPacket());
            Fresh(); Row("split-missing-car", () => PartPacket()); Row("split-carrier-no-item", () => PartPacket(carrier: 9));
            Fresh(); car = Seed(); Row("split-invalid-index", () => PartPacket(index: 9));
            Fresh(true); var se = ServerSeed(); KnownBy(2).Add(7); KnownBy(3); se.ItemState[1] = new byte[] { 99 }; se.RepairOff = new() { 1 };
            Row("server-split-known-propagation", () => SplitPart(se, 1, 2, new(8, 0, 9), Quaternion.identity, 0, -1));
            Fresh(); var e = Seed(); e.RootId = 999; e.ItemIds = new() { 3, 4, 5 }; e.Split = new() { 1 }; Row("bind-keeps-split-and-slot-zero", () => { e.Items[1] = null; Bind(e); });
            Fresh(); e = Seed(owner: 3, proxy: true); var oldMember = e.Items[1]; oldMember.Dead = true; var rebound = RegistryFakes.Make(oldMember.idInSave, "rebound-member");
            Row("rebind-proxy-member", () => RebindMembers(e)); Row("rebind-no-change", () => RebindMembers(e));
            Fresh(); e = Seed(owner: 3, proxy: true); e.Items[1].Dead = true; e.Split = new() { 1 }; RegistryFakes.Make(e.ItemIds[1], "split-member"); Row("rebind-split-skips-proxy", () => RebindMembers(e));
            Fresh(); e = Seed(owner: 3, proxy: true); e.Root = null; e.Items.Clear(); e.FarDirty = true; e.SettledState = new byte[] { State }; e.ReceivedStateEpoch = e.Epoch;
            Row("resolve-streamed-proxy", () => Resolve(e)); Row("resolve-existing-keeps-revision", () => Resolve(e));
            Fresh(); e = Seed(); e.Root = null; e.ItemIds = null; e.FarDirty = true; Row("resolve-owner-contact", () => Resolve(e));
            Fresh(); e = new() { NetId = 7, RootId = 999 }; ByNet[7] = e; Row("resolve-missing", () => Resolve(e));
            Fresh(); e = Seed(owner: 3, proxy: true); Row("proxy-same-noop", () => SetProxy(e, true)); Row("proxy-promote", () => SetProxy(e, false)); Row("proxy-promote-flush", RegistryFakes.Flush);
            Fresh(); e = new() { NetId = 7, RootId = 999, OwnerId = 3 }; ByNet[7] = e; Row("proxy-dormant", () => SetProxy(e, true)); Row("proxy-dormant-promote", () => SetProxy(e, false));
            Fresh(); Mp.Client = null; root = RegistryFakes.Make(70, "share"); Row("share-offline", () => Note(ShareItem(root, null))); Mp.Client = new();
            Row("share-null", () => Note(ShareItem(null, null))); Row("share-car-refuses-item", () => Note(ShareCar(root))); Row("share-client-pending-before-send", () => Note(ShareItem(root, null))); Row("share-pending-dedup", () => Note(ShareItem(root, null))); Row("share-client-assignment", () => AssignedPacket(70)); Row("share-reference-dedup", () => Note(ShareItem(root, null)));
            Fresh(true); root = RegistryFakes.Make(70, "host-share", true); var bad = RegistryFakes.Make(71, "unsavable"); bad.Savable = false; root.Group.Add(bad);
            Row("share-host-reentry-and-alignment", () => Note(ShareCar(root)));
            Fresh(); root = RegistryFakes.Make(70, "box"); var child = RegistryFakes.Make(71, "contents"); child.P = child.gameObject.AddComponent<pickupable>(); child.P.physlock = new(); root.Group.Add(child);
            Row("share-contents-apart", () => Note(ShareItem(root, null)));
            Fresh(); root = RegistryFakes.Make(70, "spawn", true); Row("spawn-share", () => ShareSpawn(root.gameObject)); Row("spawn-pending-still-counts", () => ShareSpawn(root.gameObject)); Row("spawn-assigned", () => AssignedPacket(70)); Row("spawn-dedup", () => ShareSpawn(root.gameObject));
            Fresh(); root = RegistryFakes.Make(70, "child"); root.transform.parent = RegistryFakes.Make(71, "parent").transform; Row("spawn-parent-refused", () => ShareSpawn(root.gameObject)); Mp.SceneTearingDown = true; Row("spawn-scene-refused", () => ShareSpawn(root.gameObject));
            RemovalScenarios(); ResetScenarios(); RestoreScenarios();
        }
        static void RemovalScenarios()
        {
            Fresh(); var e = Seed(owner: 3, proxy: true); Row("remove-received", () => ApplyRemoveItem(7)); Row("remove-received-flush", RegistryFakes.Flush); Row("remove-missing", () => ApplyRemoveItem(7));
            Fresh(); e = Seed(owner: 3, proxy: true); e.Root = null; e.Split = new() { 1 }; Store(70); Store(71); Store(72); e.ItemIds.Add(72); Row("remove-far-keeps-split", () => ApplyRemoveItem(7));
            Fresh(); e = Seed(); e.Root = null; Store(70); Store(71); RemoveFarMembers = false; Row("remove-far-switch-off", () => ApplyRemoveItem(7));
            Fresh(true); e = Seed(owner: 3, proxy: true); var se = ServerSeed(owner: 3); KnownBy(2).Add(7); _staleEnts[2] = new() { 7 }; _serverLocked.Add(se); var part = ServerSeed(8, 3); part.ParentNet = 7; part.PartIndex = 1; se.Parts.Add(8);
            Row("server-remove-retains-known-debt", () => ServerRemoveItem(2, 7)); Row("server-remove-flush", RegistryFakes.Flush);
            Fresh(); e = Seed(); Row("destroy-owner", () => DestroyHook(e.Root)); Row("destroy-owner-survivors-queued", RegistryFakes.Flush);
            Fresh(); e = Seed(owner: 3, proxy: true); EntityLifetimeStreaming.Deleted.Add(e.Root); Row("destroy-player-copy", () => DestroyHook(e.Root));
            Fresh(); e = Seed(owner: 3, proxy: true); Row("destroy-copy-refetch", () => DestroyHook(e.Root)); ByNet[7] = e; Row("destroy-copy-refetch-later", () => DestroyHook(e.Root));
            Fresh(); e = Seed(); var dead = e.Root; RegistryFakes.Make(dead.idInSave, "same-frame-replacement"); Row("destroy-replacement", () => DestroyHook(dead)); Row("replacement-resolve", () => Resolve(e));
            Fresh(); e = Seed(); Store(e.RootId); Row("destroy-streamout-no-echo", () => DestroyHook(e.Root));
            Fresh(); e = Seed(); SuppressDestroyEcho = true; Row("destroy-suppressed", () => DestroyHook(e.Root)); SuppressDestroyEcho = false; Mp.SceneTearingDown = true; Row("destroy-scene-suppressed", () => DestroyHook(e.Root));
            Fresh(); e = Seed(owner: 3, proxy: true); Row("corpse-forget-only", () => ForgetDeathCorpse(7));
            Fresh(); e = Seed(owner: 3, proxy: true); Row("member-gone-keeps-slot", () => { var w = new NetWriter(); w.U32(7); w.VarU32(1); ApplyMemberGone(new() { Buf = w.Buf, End = w.Len }); }); Row("member-gone-flush", RegistryFakes.Flush);
            Fresh(); e = Seed(owner: 3, proxy: true); e.Root = null; Store(71); Row("member-gone-far-record", () => { var w = new NetWriter(); w.U32(7); w.VarU32(1); ApplyMemberGone(new() { Buf = w.Buf, End = w.Len }); });
            Fresh(); var se2 = ServerSeed(); var parent = ServerSeed(9, 2); Row("attach-keep", () => KeepAttach(se2, 9, new byte[] { 55, 66 }, 2)); Row("attach-drop", () => DropAttach(se2));
            Fresh(); e = Seed(owner: 3, proxy: true); Store(70, 0); Store(71, 1, 70); Store(72, 800, 71); e.FarPos = new(200, 0, 0); e.FarRot = Quaternion.identity; Row("far-record-attachment-and-chunk", () => FlushFarRecords(e)); Row("far-detach-invalidate", () => DetachStored(71, new(400, 0, 0), Quaternion.identity));
        }
        static void ResetScenarios()
        {
            void SeedReset(bool host)
            {
                Fresh(host); var e = Seed(owner: 3, proxy: true); ServerSeed(); PendingShare[999] = new() { RootId = 999 }; KnownBy(2).Add(7); _staleEnts[2] = new() { 7 }; _poseNear[2] = new() { 7 }; _rabbitNear[2] = new() { 7 }; _serverLocked.Add(Server[7]); _hitchOps[7] = 1; _hitchTransfers[7] = 2; _hitchDeparted.Add(3);
                EntityRagdolls.RagdollCopies.Add(e); EntityRagdolls.RagdollLimbOwner[e.Root.gameObject] = e; e.Rag = new() { Has = true }; _placed.Add(e); _byItem[e.Root] = e; _byOwn[e.Root.transform] = e; RegistryFakes.Trace.Clear();
            }
            SeedReset(true); Row("reset-host", () => Reset(true)); Row("reset-host-deferred-flush", RegistryFakes.Flush);
            SeedReset(false); Row("reset-client", () => Reset(false)); Row("reset-client-deferred-flush", RegistryFakes.Flush);
            SeedReset(false); Row("scene-world-reload", ClientWorldReload); Row("reload-old-object-destroy", RegistryFakes.Flush);
            SeedReset(true); Server[7].Stored = true; PlayerSave.SeedPocket(7); Row("stop-host-pockets-before-dispose-and-reset", () => Mp.Stop()); Row("stop-host-deferred-flush", RegistryFakes.Flush);
            SeedReset(false); Row("stop-client-leave-before-dispose-and-reset", () => Mp.Stop()); Row("stop-client-deferred-flush", RegistryFakes.Flush);
            SeedReset(true); Mp.PendingSceneStop = true; Row("stop-scene-host-skips-save", Mp.SceneStopForTest); Row("stop-scene-flush", RegistryFakes.Flush);
            Fresh(true); var a = ServerSeed(7, 2); a.RagdollKeyState = new byte[] { 1 }; a.RagdollKeyEpoch = 3; var attached = ServerSeed(8, 3); KeepAttach(attached, 7, new byte[] { 9 }, 1); KnownBy(2).Add(7); _staleEnts[2] = new() { 7 }; _poseNear[2] = new() { 7 }; _rabbitNear[2] = new() { 7 };
            Row("server-leave-owner-setter-and-cascade", () => ServerOnLeave(2)); Row("server-join-order-and-known-replace", () => ServerOnJoin(2));
            Row("join-manifest-new", () => ServerJoinLoading(2, default)); Row("join-manifest-known", () => ServerJoinLoading(2, default));
            Row("server-owner-same-value", () => { a.OwnerId = 0; a.Epoch = a.Epoch; });
            _tpaSending = true; Mp.Server.Sent = false; Row("target-send-failure", () => { W.Reset(); W.U8(99); ServerSendTo(2, W, true); });
        }
        static void RestoreScenarios()
        {
            NetReader Packet(bool dead = false)
            {
                var w = new NetWriter(); w.Str(""); Protocol.WritePos(w, new(50, 0, 60)); w.U8((byte)(dead ? 1 : 0)); w.VarU32(1); w.U32(70); var bytes = RegistryFakes.Codec.Encode(new() { items = new() { new() { id = 70, prefabID = 11 } } }); w.VarU32((uint)bytes.Length); w.Bytes(bytes, 0, bytes.Length); w.VarU32(0); return new() { Buf = w.Buf, End = w.Len };
            }
            Fresh(); Mp.Client.Ready = false; Row("restore-queued-before-ready", () => PlayerSave.ClientRestoreIn(Packet())); Row("restore-no-player-keeps-pending", PlayerSave.ClientReady);
            var playerGo = new GameObject("player"); RegistryFakes.Main.s = new() { player = playerGo.AddComponent<RegistryFakes.Player>() };
            Row("restore-ready-share", PlayerSave.ClientReady); Row("restore-assignment", () => AssignedPacket(1000));
            Fresh(); Mp.Client.Ready = false; Row("restore-cancel-pending", () => PlayerSave.ClientRestoreIn(Packet())); Row("restore-session-ended", PlayerSave.ClientSessionEnded);
            Fresh(); RegistryFakes.Main.s = new() { player = new GameObject("player").AddComponent<RegistryFakes.Player>() }; Row("restore-dead-on-ground", () => PlayerSave.ClientRestoreIn(Packet(true)));
            Fresh(true); var s = ServerSeed(); s.Stored = true; PlayerSave.SeedPocket(7); Row("leave-pocket-removal-before-promotion", () => { PlayerSave.ServerOnLeave(2, 123); ServerOnLeave(2); });
            Row("join-return-pocket-message", () => PlayerSave.ServerSendRestore(2, 123, w => Note("restore-send:" + Bytes(w))));
        }
        public static void Checks()
        {
            RegistryScenarios();
            string path = "tools/unit/EntityRegistryRows.txt";
            GoldenRows.UpdateText(path, string.Join("\r\n", Rows) + "\r\n");
            var expected = File.ReadAllLines(path);
            if (!Rows.SequenceEqual(expected))
            {
                File.WriteAllText("tools/unit/obj/EntityRegistryTest/actual-rows.txt", string.Join("\r\n", Rows) + "\r\n");
                int i = Enumerable.Range(0, Math.Min(Rows.Count, expected.Length)).FirstOrDefault(i => Rows[i] != expected[i], -1);
                throw new Exception("registry expected rows differ at row " + i + " " + (i < 0 ? "count" : Rows[i].Split('|')[0]));
            }
            Console.WriteLine(Rows.Count + " registry rows match expected rows");
        }
    }
    internal static partial class EntityPhysics
    {
        internal static bool LodKeepsPartsOff = true,CrashLeaveKeep = true;
        internal static long CrashLeasesKept;
    }


    internal static partial class EntityLocks
    {
        internal static bool GhostEndsOnOwn = true;
    }


    internal static partial class EntityAttachments
    {
        internal static long CarriedOffApplied;
    }

    internal static partial class EntityHands
    {
        internal static bool WornSync = true;
    }


    internal static partial class EntityAuthorityInterest
    {
        internal static bool ShareAllowed(int p, int n) => n > 0;
        internal static void ShareQuotaForget(int p) => Entities.Note("quota-forget:" + p);
    }


    internal static partial class EntityResync
    {
        internal static ulong[] StateHashes(List<tosaveitemscript> items, List<itemDataClass> per) => items.Select(i => (ulong)(i?.idInSave ?? 0)).ToArray();
    }


    internal static partial class EntityRabbits
    {
        internal static void RabbitBeforeShare(tosaveitemscript it) => Entities.Note("rabbit-before:" + it.name);
    }


    internal static partial class EntityLifetimeStreaming
    {
        internal static string PrefabName(int id) => "prefab-" + id;
        internal static bool FarFromHere(Vector3d p) => Math.Abs(p.x) > 3000;
        internal static void Unmount(attachablescript a, bool fall) { Entities.Note("unmount:" + a.gameObject.name + ":" + Entities._applyingDetach); a.attached = false; }
        internal static System.Reflection.FieldInfo _idField(object o) => o.GetType().GetField("id");
    }


    internal static partial class EntityPhysics
    {
        internal static void DropPhysical(Entities.Ent e) { e.Physical = false; Entities.Note("drop-physical:" + e.NetId); }
        internal static void StopBodyMotion(Entities.Ent e) { e.MovedByBody = false; Entities.Note("stop-motion:" + e.NetId); }
        internal static void SetPartColliders(Entities.Ent e, bool on) { e.PartsColOff = false; Entities.Note("part-colliders:" + e.NetId + ":" + on); }
    }


    internal static partial class EntityProxyPresentation
    {
        internal static wheelgraphicsscript[] WheelsOf(Entities.Ent e) => e.Wheels ??= e.Root.GetComponentsInChildren<wheelgraphicsscript>(true);
        internal static bool AttachedCarPart(Entities.Ent e) => false;
        internal static void ReadEntityHead(NetReader r, bool compact, out uint n, out uint ep, out double t, out Vector3d p, out Quaternion q, out Vector3 v, out bool driven, out bool stored, out bool held, out bool spray) { n = ep = 0; t = 0; p = new(5, 0, 6); q = Quaternion.identity; v = default; driven = stored = held = spray = false; Entities.Note("state-head"); }
        internal static void ReadWheels(NetReader r, Entities.Ent e) => Entities.Note("wheels-read");
        internal static void ReadEngine(NetReader r, Entities.Ent e) => Entities.Note("engine-read");
    }


    internal static partial class EntityHitches
    {
        internal static IEnumerable<Rigidbody> HitchBodies(Entities.Ent e) => e.Root.GetComponentsInChildren<Rigidbody>(true);
    }


    internal static partial class EntityLocks
    {
        internal static void EndGhostOnOwn(Entities.Ent e) { e.ReleaseGhost = false; Entities.Note("end-ghost:" + e.NetId); }
        internal static bool RidesOwnersContainer(Entities.Ent e) => false;
        internal static bool LockedAway(Entities.Ent e) => false;
    }


    internal static partial class EntityAttachments
    {
        internal static tosaveitemscript CarriedItem(uint n, int s, int p) => null;
    }


    internal static partial class EntityRepair
    {
        internal static void ResetPoiCensus() => Entities.Note("poi-census-reset");
    }

}
static class EntityRegistryTest
{
    static void Main() { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; TLDRevamp.Net.Entities.Checks(); Console.WriteLine("EntityRegistryTest PASS"); }
}
