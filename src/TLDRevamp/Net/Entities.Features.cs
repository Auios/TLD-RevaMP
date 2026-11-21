using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        // Diagnostic walks read records in the same ByNet.Values order, without a snapshot.
        internal static void VisitEnts(System.Action<Ent> see)
        {
            foreach (var e in ByNet.Values) see(e);
        }
        internal static Ent PartWatchGroup(tosaveitemscript item) => EditEntityOf(item);
        internal static uint NetOfRoot(Transform root)
        {
            foreach (var e in ByNet.Values) if (e.Root != null && e.Root.transform == root) return e.NetId;
            return 0;
        }
        internal static bool StatusHeld(tosaveitemscript root) => IsHeld(root);
        internal static bool StatusStored(tosaveitemscript root) => IsStored(root);
        // End diagnostic boundary.

        // Repair helpers and the POI census use facts and messages without sharing entity tables.
        public const byte PoiCensus = 84;   // c→s: f64 gx, f64 gz, f32 drawLerp, u8 distant terrain, f32 shown within, f32 removed beyond, u16 n × (f64 x, f64 z, i32 type, u8 shown)
        internal static Transform RepairAttachFrame(attachablescript at) => EntityAttachments.AttachFrame(at);
        internal static void RepairAnomaly(string what) => Anomaly(what);

        // Game adapters ask for one identity fact; all table walks stay here.
        internal static bool TryModRootNet(tosaveitemscript item, out uint net)
        {
            foreach (var e in ByNet.Values) if (e.Root == item) { net = e.NetId; return true; }
            net = 0; return false;
        }
        internal static GameObject ModObject(uint net) => ByNet.TryGetValue(net, out var e) && e.Root != null ? e.Root.gameObject : null;
        internal static string ModShareRoot(tosaveitemscript top) => top.car != null ? ShareCar(top) : ShareItem(top, null);
        internal static uint MountRootNet(Transform root)
        {
            uint partNet = 0; foreach (var x in ByNet.Values) if (x.Root != null && x.Root.transform == root) { partNet = x.NetId; break; }
            return partNet;
        }

        // Physics boundary. Scheduling, map walks and server ownership stay in the kernel.
        internal static Ent PhysicsRoot(Transform root) => PlacedOfTransform(root);
        internal static bool PhysicsNetOf(Transform root, out uint net, out bool proxy) => NetOf(root, out net, out proxy);
        internal static bool PhysicsTrailer(Ent entity) => HitchTrailer(entity);
        internal static wheelgraphicsscript[] PhysicsWheels(Ent entity) => EntityProxyPresentation.WheelsOf(entity);
        internal static bool PhysicsPartCameOff(Ent entity, int item) => CameOff(entity, item);
        internal static Transform[] PhysicsLimbs(Ent entity) => EntityRagdolls.Limbs(entity);
        internal static bool PhysicsIsProxy(Component body) => IsProxy(body);
        internal static void PhysicsSnap(Ent entity, Vector3 target, Vector3 from) => TelemetryProxySnap(entity, target, from);
        internal static void SetCrashProxy(Ent entity, bool proxy, bool keepMotion) => SetProxy(entity, proxy, keepMotion);
        internal static void SendPhysicsRequest(NetWriter writer, bool reliable) => ToServer(writer, reliable);
        internal static bool TryPhysicsContact(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static bool ResolvePhysics(Ent entity) => Resolve(entity);
        internal static uint PhysicsFootingNet => _telemetryFooting;
        internal static bool TryPhysicsStand(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static void PhysicsContacts(uint net, Collision collision) { EntityCargoInteraction.CargoContact(net, collision); EntityRabbits.RabbitContact(net, collision); EntityCargoInteraction.ItemContact(net, collision); }
        // End physics boundary.
        // Copy presentation boundary. Role transitions, indexes and map walks stay in the kernel.
        internal static bool IsPresentationCopy(Component item) => IsProxy(item);
        internal static Ent PresentationEntity(tosaveitemscript item) => OwnEntity(item);
        internal static bool PresentationHasDamageGrace => DamageGraceUntil.Count > 0;
        internal static bool TryPresentationDamageGrace(carscript car, out float until) => DamageGraceUntil.TryGetValue(car, out until);
        internal static void RemovePresentationDamageGrace(carscript car) => DamageGraceUntil.Remove(car);
        internal static bool PresentationNeedsTorques(Ent entity) => NearOtherPlayersCar(entity);
        internal static void BindPresentationEngine(Ent entity) => BindProxyEngine(entity);
        internal static bool TryPresentationEngine(enginescript engine, out Ent entity)
        {
            entity = null;
            return ProxyEngineOwner.Count != 0 && ProxyEngineOwner.TryGetValue(engine, out entity);
        }
        internal static bool TryPresentationWheel(wheelgraphicsscript wheel, out Ent entity)
        {
            entity = null;
            return ProxyWheelOwner.Count != 0 && ProxyWheelOwner.TryGetValue(wheel, out entity);
        }
        internal static int PresentationWheelIndex(wheelgraphicsscript wheel) => ProxyWheelIndex[wheel];
        internal static System.Collections.Generic.IEnumerable<carscript> PresentationCars() => CopyCars();
        internal static Transform[] PresentationLimbs(Ent entity) => EntityRagdolls.Limbs(entity);
        internal static bool PresentationLimp(Ent entity) => EntityRagdolls.Limp(entity);
        // End copy presentation boundary.
        internal static bool LifetimeInSession => InSession;
        internal static long LifetimeSpawnCount => SpawnShared;
        internal static void ShareLifetimeSpawn(GameObject go) => ShareSpawn(go);
        internal static bool LifetimePartIsProxy(attachablescript part) => PartIsProxy(part);
        internal static void RecordLifetimeFallOff() => SuppressedFallOff++;
        internal static Ent ProxyForLinkedDelay(Transform root) => ProxyOf(root);

        private static void RemoveRecord(uint id) => RemoveSavedRecord(id);

        // Cargo boundary. Map walks and server authority stay in the kernel.
        internal static bool CargoInSession => InSession;
        internal static int CargoLocalId => MyId;
        internal static float CargoItemMinSpeed => ItemMinSpeed;
        internal static byte PushMessage => PushIn;
        internal static Ent CargoCopyOf(Transform root) => ProxyOf(root);
        internal static bool IsCargoBody(Ent entity) => EntityProxyPresentation.CargoBody(entity);
        internal static bool CargoRidesParent(Ent entity) => RidesParent(entity);
        internal static Transform[] CargoLimbs(Ent entity) => EntityRagdolls.Limbs(entity);
        internal static bool CargoLimp(Ent entity) => EntityRagdolls.Limp(entity);
        internal static void SetCargoProxy(Ent entity, bool proxy) => SetProxy(entity, proxy);
        internal static void WriteCargoClaim(NetWriter writer, uint net, ClaimReason reason) => WriteClaim(writer, net, reason);
        internal static void SendCargoRequest(NetWriter writer, bool reliable) => ToServer(writer, reliable);
        internal static void CargoClaimSent() => ClaimsSent++;
        internal static void CargoContactClaimed() => CargoClaimedOnContact++;
        internal static void ItemContactClaimed() => ItemClaimedOnContact++;
        internal static carscript CargoOverlappedCar(Ent entity) => OverlappedCar(entity);
        internal static Bounds CargoCarBounds(carscript car) => CarLocalBounds(car);
        internal static bool CargoLockedAway(Ent entity) => EntityLocks.LockedAway(entity);
        internal static bool IsPushCopy(Component pushable) => IsProxy(pushable);
        internal static Ent PushEntityOf(tosaveitemscript item) => EditEntityOf(item);
        internal static Ent GripEntityOf(Transform root) => PlacedOfTransform(root);
        internal static nyulscript CargoRabbit(Ent entity) => EntityRabbits.NyOf(entity);
        internal static Ent CargoCarEntity(carscript car) => CarEnt(car);
        internal static bool CargoItemHeld(tosaveitemscript item) => IsHeld(item);
        internal static bool TryCargoContactCar(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static bool TryItemContactSource(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static bool TryPushTarget(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static bool AttachmentApplying => _applyingAttach || _applyingState;
        internal static bool AttachmentApplyingDetach => _applyingDetach;
        internal static bool AttachmentPlayerActing => PlayerActing;
        internal static Ent AttachmentRoot(tosaveitemscript item) => OwnEntity(item);
        internal static Ent AttachmentGroup(tosaveitemscript item) => EditEntityOf(item);
        internal static bool TryAttachmentCarrier(uint net, out Ent carrier) => ByNet.TryGetValue(net, out carrier) && Resolve(carrier);
        internal static void AttachmentUsablesChanged(tosaveitemscript item) => LiveUsablesChanged(item);
        internal static void RefreshAttachmentBodies(Ent entity) => ApplyProxyBodies(entity);
        internal static void SendAttachment(Ent entity, attachablescript at, byte type = AttachSync) => SendAttach(entity, at, type);
        internal static void SendAttachmentMessage(NetWriter writer, bool reliable) => ToServer(writer, reliable);
        internal static void TellAttachmentRefused(int player, string notice) => Mp.Server?.Tell(player, notice);
        // End attachment boundary.
        // Authority and interest boundary. Records, map walks and pose replay stay in the kernel.
        private static void RelayResync(SEnt se, int from, NetWriter msg) => EntityAuthorityInterest.RelayResync(se.NetId, se.Where, from, msg);
        internal static int InterestCatchUpLimit => InterestPerTick;
        internal static void RecordInterestSkip(int player) => SkippedByInterest(player);
        internal static void DeliverInterestMessage(NetWriter writer) => Deliver(writer);
        internal static void MarkInterestStale(int player, uint net) => StaleOf(_staleEnts, player).Add(net);
        internal static bool TakeInterestStale(int player, uint net) => StaleOf(_staleEnts, player).Remove(net);
        internal static int CountInterestStale()
        {
            int count = 0;
            foreach (var stale in _staleEnts.Values) count += stale.Count;
            return count;
        }
        internal static void WriteInterestResync(NetWriter writer, uint net) => WriteResyncAll(writer, Server[net]);
        private static bool NearInterestRecord(Vector3d viewer, Vector3d at)
        {
            double dx = viewer.x - at.x, dz = viewer.z - at.z;
            return dx * dx + dz * dz <= EntityAuthorityInterest.StateInterestM * EntityAuthorityInterest.StateInterestM;
        }
        internal static void CatchUpInterestRecords(int player, Vector3d viewer, NetWriter writer)
        {
            if (_staleEnts.TryGetValue(player, out var ents) && ents.Count > 0)
            {
                _dueEnts.Clear();
                foreach (var net in ents)
                    if (!Server.TryGetValue(net, out var se) || NearInterestRecord(viewer, se.Where)) { _dueEnts.Add(net); if (_dueEnts.Count >= InterestPerTick) break; }
                foreach (var net in _dueEnts)
                {
                    ents.Remove(net);
                    if (!Server.TryGetValue(net, out var se) || se.ItemState.Count == 0) continue;
                    WriteResyncAll(writer, se); Mp.Server.SendToPlayer(player, writer, true); EntityAuthorityInterest.ResyncCaughtUp++;
                }
            }
        }
        internal static bool HasInterestPoi(PoiKey key) => ServerPoiUsables.ContainsKey(key);
        internal static bool TryInterestPoi(PoiKey key, out byte[] bytes) => ServerPoiUsables.TryGetValue(key, out bytes);
        // End authority and interest boundary.
        // Lock boundary. Copy and ownership decisions stay in the kernel.
        internal static bool IsLockCopy(Component item) => IsProxy(item);
        internal static bool LockRidesParent(Ent entity) => RidesParent(entity);
        internal static void RefreshLockBodies(Ent entity) => ApplyProxyBodies(entity);
        internal static void ShowLockCollisions(Ent entity) => ShowCarriedCollisions(entity);
        internal static bool LockContainerOwnedBy(Transform root, int owner)
        {
            var container = ProxyOf(root);
            return container != null && container.OwnerId == owner;
        }
        internal static bool LockContainerMotion(Transform root, int owner, out bool sameOwner, out Vector3 velocity, out PoseInterpolator pose)
        {
            var container = ProxyOf(root);
            sameOwner = container != null && container.OwnerId == owner;
            velocity = container != null ? container.LastVel : default;
            pose = container != null ? container.Ip : null;
            return container != null;
        }
        internal static bool LockScanAllows(mountStuff scanning, pickupable item)
        {
            var ce = IsProxy(scanning) ? EntOfRoot(scanning.transform.root) : null;
            int _scanCarOwner = ce != null ? ce.OwnerId : -1;
            var pe = IsProxy(item) ? EntOfRoot(item.transform.root) : null;
            bool ok = _scanCarOwner < 0 ? pe == null                          // our container: our own things only
                                    : pe != null && pe.OwnerId == _scanCarOwner;  // a copy's: the car owner's things only
            // ... and only what lies there: what the owner carries in a hand or an inventory, sitting in or walking through
            // the car, was held by the copy of the car on this screen only (itemchaos 2026-10-31)
            if (ok && pe != null && (pe.Held || pe.Stored)) ok = false;
            return ok;
        }
        internal static bool LockOverlapsCar(Bounds b, Transform own)
        {
            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * 0.1f, _clearHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var r = _clearHits[i].transform.root;
                if (r == own) continue;
                if (r.GetComponent<carscript>() != null) return true;
            }
            return false;
        }
        // End lock boundary.
        // World lease boundary. Entity map walks and ownership decisions stay in the kernel.
        public const byte LeaseReq = 28, LeaseGrant = 29, LeaseTaken = 30, LeaseDone = 33, LeaseFreed = 34;
        internal static void SendLeaseRequest(NetWriter writer, bool reliable) => ToServer(writer, reliable);
        internal static int ShareLeaseRoots(System.Collections.Generic.List<tosaveitemscript> items) => ShareRoots(items);
        internal static System.Collections.Generic.List<GameObject> DisableLeaseBuiltIns(poiScript poi) => DisableBuiltIns(poi);
        internal static void LeaseAnomaly(string message) => Anomaly(message);
        internal static bool TryLeasePlayerPosition(int from, out Vector3d pos)
        {
            pos = default;
            if (Mp.Server == null || from < 0) return false;
            if (from != 0) return Mp.Server.PeerPos(from, out pos) && EntityNumbers.Finite(pos);
            var player = mainscript.s != null ? mainscript.s.player : null;
            if (player == null) return false;
            pos = mainscript.GlobalFromUnityPos(player.transform.position);
            return EntityNumbers.Finite(pos);
        }
        // End world lease boundary.

        internal static float EditBackHold => EditHold;
        internal static bool RequestEditBack(uint net, uint item)
        {
            if (!Server.TryGetValue(net, out var se)) return false;
            WS.Reset(); WS.U8(CensusRepair); WS.U32(se.NetId); WS.U32(se.Epoch); WS.U8(8);
            WS.VarU32(1); WS.VarU32(item);
            WS.VarU32(0);
            ServerSendTo(se.OwnerId, WS, true);
            return true;
        }
        internal static void RecordEditRefusal(string what, bool log)
        {
            if (log) Anomaly(what + " (refused)"); else Anomalies++;
        }
        internal static long EditReachRefusals => EditsOutOfReach;
        internal static long EditAnomalyCount => Anomalies;
        internal static bool TryEditPeerPosition(int player, out Vector3d position)
        {
            position = default;
            return Mp.Server != null && Mp.Server.PeerPos(player, out position);
        }
        // Hitch client boundary. Server records and map walks stay in the kernel.
        public const byte HitchRequest = 99, HitchCommand = 100, HitchFreeze = 101, HitchFrozen = 102, HitchResult = 103;
        internal static bool HitchInSession => InSession;
        internal static int HitchLocalId => MyId;
        internal static bool HitchPlayerActing => PlayerActing;
        internal static bool HitchApplyingAttach { get => _applyingAttach; set => _applyingAttach = value; }
        internal static bool HitchApplyingDetach { get => _applyingDetach; set => _applyingDetach = value; }
        private static bool _hitchApplying => EntityHitches.Applying;
        internal static float HitchCommandTimeoutS => HitchTimeoutS;
        internal static bool TryHitchEntity(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static bool TryHitchFreezeTarget(uint net, uint epoch, out Ent entity) => ByNet.TryGetValue(net, out entity) && entity.Epoch == epoch && entity.OwnerId == MyId;
        internal static bool TryHitchFrozenTarget(uint net, uint epoch, out Ent entity) => ByNet.TryGetValue(net, out entity) && !entity.Proxy && entity.Epoch == epoch;
        internal static bool IsHitchTrailer(Ent entity) => HitchTrailer(entity);
        internal static Ent HitchTowEntity(attachablescript at) => HitchCar(at);
        internal static Ent HitchRootEntity(tosaveitemscript item) => OwnEntity(item);
        internal static Ent HitchGroupEntity(tosaveitemscript item) => EditEntityOf(item);
        internal static bool ResolveHitch(Ent entity) => Resolve(entity);
        internal static bool HitchMayJoin(Ent trailer, Ent car) => HitchCanJoin(trailer, car);
        internal static void FreezeHitchProxy(Ent entity) => ApplyProxyBodies(entity);
        internal static bool UnmountHitch(attachablescript at, bool fallOff) => EntityLifetimeStreaming.Unmount(at, fallOff);
        internal static void SyncHitchDetach(Ent entity) => SendDetachSync(entity);
        internal static void SendHitchRequest(NetWriter writer, bool reliable) => ToServer(writer, reliable);
        internal static void HitchDamageGrace(Ent car, float until) => DamageGraceUntil[car.Root.car] = until;
        // End hitch client boundary.

        internal static bool HasTpaCopy(uint net) => ByNet.ContainsKey(net);
        internal static void RequestTpaCopy(uint net) { W.Reset(); W.U8(Refetch); W.U32(net); WriteLostPart(W, net); ToServer(W, true); }
        internal static void ReplayTpaMessage(byte type, NetReader reader) => ClientReceive(type, reader);
        internal static bool UsablePoiApplying => EntityUsables.ApplyingPoi;
        internal static bool HasKnownPoiUsables => KnownPoiUsables.Count != 0;
        internal static bool TryKnownPoiUsable(PoiKey key, out PoiState state) => KnownPoiUsables.TryGetValue(key, out state);
        internal static bool MarkPoiUsable(usablescript usable) => MarkLiveUsable(usable);

        internal static uint DiagnosticNet(Component component) => TelemetryNet(component);
        internal static bool DiagnosticMine(Ent entity) => entity.OwnerId == MyId;
        internal static bool DiagnosticProxy(mountStuff mount) => IsProxy(mount);
        internal static long DiagnosticStoreRefusals(bool attached) => attached ? EntityLocks.AttachedStoresRefused : EntityLocks.ContainerStoresRefused;
        internal static void RefreshMarkFocus() => TelemetryFocusTick(Time.realtimeSinceStartupAsDouble, true);
        internal static bool MarkIsLocal(int player) => player == MyId;
        internal static uint MarkItemNet(tosaveitemscript item)
        {
            var entity = item != null ? SharedGroupOf(item) : null;
            return entity != null ? entity.NetId : 0;
        }

        internal static Ent ReproEntityOf(tosaveitemscript it) => EditEntityOf(it);
        internal static bool ReproOwns(Ent e) => e != null && !e.Proxy && e.OwnerId == MyId;
        internal static Ent ReproAttachEntity(uint net)
        {
            if (!PlayerActing || !ByNet.TryGetValue(net, out var e) || e.Root == null || e.Root.attachable == null || _hitchApplying) return null;
            return e;
        }
        internal static bool ReproUsableAllowed => PlayerActing && !_applyingLive && !_applyingState && !UsablePoiApplying;
        internal static bool ReproDetachAllowed => PlayerActing && !_applyingDetach && !_applyingState && !_hitchApplying;
        internal static tosaveitemscript ReproUsableItem(usablescript u) => ItemOf(u, u.tosaveid, u.setid);
        internal static void SampleReproCar(carscript car) => EntityDiag.StuckWatch(car);

        public const byte RabbitState = 90, RabbitFx = 91;
        private static readonly bool _rabbitLookupsReady = EntityRabbits.InitializeRabbitLookups();
        internal sealed class RabCharge { public Vector3d At; public float Speed, Until, OwnSpeed; }
        internal static bool RabbitHandoverActive(Ent e) => Time.realtimeSinceStartup - e.OwnerAt <= HandoverWindowS;
        internal static byte[] CaptureRabbitSnapshot(NetReader r) => EntityProxyPresentation.FinalSnapshot(r, true);
        internal static Ent RabbitOfItem(tosaveitemscript it)
        {
            foreach (var e in ByNet.Values) if (e.Root == it && e.PartIndex < 0) return e;
            return null;
        }
        internal static void CollectRabbits(System.Collections.Generic.List<Ent> into)
        {
            foreach (var e in ByNet.Values) if (EntityRabbits.NyOf(e) != null) into.Add(e);
        }
        internal static System.Collections.Generic.IEnumerable<Ent> KnownRabbits()
        {
            foreach (var e in ByNet.Values) if (EntityRabbits.NyOf(e) != null) yield return e;
        }
        internal static bool HasRabbitCars => _localCars.Count != 0;
        internal static System.Collections.Generic.IEnumerable<carscript> LocalRabbitCars()
        {
            foreach (var car in _localCars) yield return car;
        }
        internal static bool ClaimRabbitObject(Ent e, ClaimReason reason) => EntityCargoInteraction.ClaimCargo(e, reason: reason);
        internal static Bounds RabbitCarBounds(carscript car) => CarLocalBounds(car);
        internal static Ent RabbitProxyOf(Transform root) => ProxyOf(root);

        /// Server: nearest ready player or moving car, with the original hold and switch distances.
        internal static void ChooseRabbitOwner(float dt, ref float elapsed, System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, Vector3d>> anchors)
        {
            if (Mp.Server == null) return;
            elapsed += dt;
            if (elapsed < 0.25f) return;
            elapsed = 0f;
            bool any = false;
            foreach (var se in Server.Values) if (se.Rabbit) { any = true; break; }
            if (!any) return;
            Mp.Server.ReadyPeers(anchors);
            if (!DedicatedServer.Enabled && mainscript.s != null && mainscript.s.player != null)
                anchors.Add(new System.Collections.Generic.KeyValuePair<int, Vector3d>(0, mainscript.GlobalFromUnityPos(mainscript.s.player.transform.position)));
            float rt = Time.realtimeSinceStartup;
            foreach (var c in Server.Values)
                if (c.Car && c.PartIndex < 0 && c.HasState && (Busy(c, BusyKind.Driven, rt) != BusyKind.None || c.Speed > EntityRabbits.RabbitCarMoveMps))
                    anchors.Add(new System.Collections.Generic.KeyValuePair<int, Vector3d>(c.OwnerId, c.Where));
            if (anchors.Count < 2) return;   // one machine: nothing to decide
            double near2 = EntityRabbits.RabbitNearM * (double)EntityRabbits.RabbitNearM;
            foreach (var se in Server.Values)
            {
                // locked in a container (a dead rabbit in a car's bed): it goes with the container's owner (contents claims),
                // and this rule took it back every ~2 s — an owner change on every machine each time
                if (!se.Rabbit || Busy(se, RabbitBusy) != BusyKind.None || rt < se.RabbitHoldUntil) continue;
                int best = -1; double bestD = double.MaxValue, curD = double.MaxValue;
                foreach (var p in anchors)
                {
                    double d = (p.Value - se.Where).sqrMagnitude;
                    if (p.Key == se.OwnerId) { if (d < curD) curD = d; continue; }
                    if (d < bestD && (p.Key == 0 || KnownBy(p.Key).Contains(se.NetId))) { bestD = d; best = p.Key; }
                }
                if (best < 0 || bestD > near2) continue;
                double bd = System.Math.Sqrt(bestD), cd = curD == double.MaxValue ? double.MaxValue : System.Math.Sqrt(curD);
                if (cd <= bd + EntityRabbits.RabbitSwitchM) continue;
                EntityRabbits.RabbitLastMove = "net " + se.NetId + ": " + se.OwnerId + " -> " + best + " (" + bd.ToString("F1") + " m against " + (cd == double.MaxValue ? "none" : cd.ToString("F1")) + ")";
                ServerSetOwner(se, best, OwnerChange.RabbitNearest, CrashReturnRule.Keep, W,
                    "rabbit: nearest player / moving car", beforeSend: static (changed, to) => EntityRabbits.RabbitOwnerMoves++);
                CascadeOwner(se);
            }
        }

        /// A provisional local claim uses the shared claim writer before the next physics step.
        internal static bool ClaimNow(Ent e, ClaimReason reason)
        {
            float now = Time.realtimeSinceStartup;
            if (now - e.OwnerAt < EntityCargoInteraction.CargoClaimCooldownS) return false;
            e.OwnerAt = now;
            SetProxy(e, false);
            e.OwnerId = MyId;
            W.Reset(); WriteClaim(W, e.NetId, reason); ToServer(W, true);
            ClaimsSent++;
            return true;
        }

        /// A newly claimed rabbit stays with its claimer for the existing hold interval.
        internal static void RabHold(uint net) { if (Server.TryGetValue(net, out var se) && se.Rabbit) se.RabbitHoldUntil = Time.realtimeSinceStartup + EntityRabbits.RabbitHoldS; }

        /// Server, at Share: record position and root kind.
        private static void ShareFacts(byte[] rec, SEnt se)
        {
            var d = RecordCodec.Decode(rec);
            if (d == null || d.items.Count == 0) return;
            se.Where = d.items[0].transform.pos;
            se.TelemetryCategory = TelemetryPrefabCategory(d.items[0].prefabID);
            uint id = d.items[0].id;
            foreach (var n in d.nyul) if (n != null && n.id == id) { se.Rabbit = true; break; }
            foreach (var c in d.car) if (c != null && c.id == id) { se.Car = true; break; }
        }

        private static void ServerRabbitState(int from, NetReader r)
        {
            var s = RabbitStateCodec.Read(r);
            if (r.Bad || !Server.TryGetValue(s.Net, out var se) || se.Epoch != s.Epoch)
            { EntityRabbits.RabbitStatesStale++; return; }
            if (se.OwnerId != from)
            {
                // One final target from the departing owner, using the existing handover epoch/window.
                if (se.PrevOwnerId != from || Time.realtimeSinceStartup - se.PrevOwnerAt > HandoverAcceptS || se.RabbitHandoverEpoch == s.Epoch)
                { EntityRabbits.RabbitStatesStale++; return; }
                se.RabbitHandoverEpoch = s.Epoch;
            }
            se.RabbitSnapshot = EntityProxyPresentation.FinalSnapshot(r, true);
            // The host keeps the durable snapshot; returning known copies get it from StateInterestCatchUp.
            WS.Reset(); WS.Bytes(r.Buf, 0, r.End);
            if (se.OwnerId != from) ServerSendTo(se.OwnerId, WS, true);
            foreach (var kv in Known)
                if (kv.Key != from && kv.Key != se.OwnerId && kv.Value.Contains(s.Net) && RabbitReceiverNear(kv.Key, se)) ServerSendTo(kv.Key, WS, true);
        }

        private static void SendRabbitSnapshot(int playerId, SEnt se)
        {
            if (se.RabbitSnapshot == null) return;
            var r = new NetReader { Buf = se.RabbitSnapshot, Pos = 1, End = se.RabbitSnapshot.Length };
            var s = RabbitStateCodec.Read(r);
            if (r.Bad || s.Epoch != se.Epoch) return;
            WS.Reset(); WS.Bytes(se.RabbitSnapshot, 0, se.RabbitSnapshot.Length); ServerSendTo(playerId, WS, true);
        }

        public const byte BreakHit = 48, BreakFx = 49, AiState = 50, AiSound = 51, WorldBreak = 112;
        private static readonly bool _aiLookupsReady = EntityAI.InitializeAiLookups();

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
            var ny = a == null && ByNet.TryGetValue(net, out var re) && Resolve(re) ? EntityRabbits.NyOf(re) : null;
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
        internal static bool RagdollRabbitBodyGone(aiscript rab) { bool gone = false; EntityRabbits.RabBodyCheck(rab, ref gone); return gone; }
        /// The network id of a shared object by its save id (0 while the share is still pending).
        internal static uint NetIdOfSaveId(uint saveId)
        {
            if (saveId == 0) return 0;
            foreach (var e in ByNet.Values) if (e.RootId == saveId) return e.NetId;
            return 0;
        }


        public const byte Worn = 85;   // c to s: net, epoch, slot and pose; s to c: net, wearer, slot and pose

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
        internal static bool LooseCarriedRelease(uint previousCar, uint car, bool held, bool stored) => EntityProxyPresentation.LooseRelease(previousCar, car, held, stored);
        internal static bool ClaimReleasedCarried(Ent e) => EntityCargoInteraction.ClaimCargo(e, released: true, reason: ClaimReason.CargoRelease);
        internal static bool TryLockedHandWorld(Ent e, out Vector3d pos, out Quaternion rot) => LockedWorld(e, out pos, out rot);
        internal static void ExitHandPhysical(Ent e, Rigidbody body) => EntityPhysics.ExitPhysical(e, body);
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
            if (slot != 255 && (slot > 15 || !EntityNumbers.Finite(lp) || lp.sqrMagnitude > 4f)) return;   // on the head: within 2 m of it
            if (se.OwnerId != from && !WornTakesBack(se, from, slot)) return;
            se.WornBy = slot == 255 ? -1 : from; se.WornSlot = slot == 255 ? -1 : slot; se.WornLp = lp; se.WornLq = lq;
            EntityHands.RelayWorn(from, se.NetId, ServerWornNow(se), se.WornBy, se.WornSlot, se.WornLp, se.WornLq);
        }
        private static bool WornTakesBack(SEnt se, int from, byte slot)
        {
            if (slot == 255) return false;   // taken off: the new owner's game has it off already
            if (se.PrevOwnerId != from || Time.realtimeSinceStartup - se.PrevOwnerAt > HandoverAcceptS ||
                Busy(se, WornTakeBackBusy) != BusyKind.None || Busy(se, BusyKind.Hitched) != BusyKind.None)
            {
                EntityHands.WornTooLate++;
                SessionLog.Event("worn-refused", se.NetId, from: from, to: se.OwnerId, result: "no longer theirs");
                return false;
            }
            se.ClaimedBy = from; se.ClaimedAt = Time.realtimeSinceStartup; se.ClaimedFromEpoch = se.Epoch;
            ServerSetOwner(se, from, OwnerChange.WornTakeBack, CrashReturnRule.Keep, W, "on their head before the claim got here");
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
        internal static bool TakeKeyToken(int from) => EntityEditCheck.TakeEditToken(from);
        internal static bool KeyReachAllowed(int from, uint net) => AtObject(from, Server[net]);
        internal static void RefuseKeyReach(int from, uint net) => EntityEditCheck.Refuse(ref EditsOutOfReach, "key reach", $"ignition key from player {from}: they aren't at net {net}");
        internal static bool TryPassengerControlOwner(int from, uint net, out int owner)
        {
            owner = 0;
            if (!Server.TryGetValue(net, out var se) || se.OwnerId == from) return false;
            if (!HitCheck.AllowOnObject(from, $"passenger controls of car {net}", se.HasState ? se.Pos : se.Where, HitCheck.ObjSeatM, 10f)) return false;
            owner = se.OwnerId;
            return true;
        }

        internal static bool ApplyingDoorAttach { get => _applyingAttach; set => _applyingAttach = value; }
        internal static bool ApplyingDoorDetach => _applyingDetach;
        internal static void ApplyDoorAttachPose(Ent e, attachablescript at, Vector3 position, Quaternion rotation) => EntityAttachments.ApplyAttachPose(e, at, position, rotation);

        public const byte CarTune = 113;   // both: u32 net, CarTuning
        public const int MaxTuneBytes = 2048;

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

        public const byte WorldClock = 97;   // f64 st, f32 world seconds, f32 rate, u16 sleep epoch, bool snap

        public const byte StormSync = 56;


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
        private static readonly bool _effectsLookupsReady = EntityEffects.InitializeLookups();

        internal static bool IsFinite(Vector3d value) => EntityNumbers.Finite(value);
        internal static bool IsFinite(Quaternion value) => EntityNumbers.Finite(value);
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

        internal static bool TryExplosionRequestOwner(int from, uint net, out int owner)
        {
            owner = 0;
            if (!Server.TryGetValue(net, out var se) || se.OwnerId == from) return false;
            if (!HitCheck.AllowOnObject(from, $"explosive or tank on object {net}", se.HasState ? se.Pos : se.Where, HitCheck.ObjShotM, HitCheck.ObjSizeM)) return false;
            owner = se.OwnerId;
            return true;
        }

        internal static bool TryGetEntity(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static bool IsFinite(Vector3 value) => EntityNumbers.Finite(value);
        internal static bool IsFinite(float value) => EntityNumbers.Finite(value);
        internal static bool IsFinite(double value) => EntityNumbers.Finite(value);
        internal static void SendToPeer(int playerId, NetWriter w, bool reliable) => ServerSendTo(playerId, w, reliable);
        internal static void SendToPeers(NetWriter w, bool reliable, int except, Vector3d? near = null, double range = 0) => ServerSendAll(w, reliable, except, near, range);
    }
}
