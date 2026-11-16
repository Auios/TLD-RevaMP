using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// Attaching a shared object to something — bolting a part into a car's slot, sticking an item onto another item,
    /// a POI or the world. The game describes every attachment with one record (save_attachable: what it's attached to,
    /// which collider or slot, where) and rebuilds it with save_attachable.Load — the official MP replays attaches the
    /// same way (syncScript.UpdAttachable). Here the attaching machine sends that record with its per-machine save id
    /// translated to (entity, group member); every other machine replays it through the game's loader, so a slot
    /// crafts on everyone's copy with all the game's side effects (installed mesh, mass, colliders, lights).
    ///
    /// Bolted onto someone else's car, the object changes hands to the car's owner: a display copy (kinematic) bolted
    /// onto a live car would pin it, and the car's owner is who simulates it from now on. While parented into what it's
    /// attached to, it moves with it: no pose stream either way.
    public static partial class Entities
    {
        public const byte AttachSync = 41, DetachSync = 42, AttachReq = 62;
        private static bool _applyingAttach;
        public static long AttachesSent, AttachesApplied, AttachesSame, AttachesFailed, DetachSyncsSent, DetachSyncsApplied;
        public static string AttachFails = "", UnmountTrace = "";

        /// Parented into what it's attached to: its transform follows the parent on every machine, nothing to stream.
        /// An attach or detach of an object whose copy here is in the far store (we're far from it): before v0.66.2 it was
        /// dropped ("nopart" — 15 in the 2026-10-09 playtest's client log) and the copy came back as it was before: a door
        /// bolted back on while we were away was missing, a wheel taken off still on (meetagain.py A1/B1). Now the
        /// stored records change the way the game's own save would have them: the attach record written (the game's
        /// placing loads it), the object's record moved to its parent's; a detach drops the record (DetachStored).
        /// A parent that is loaded here: the part is placed now (the game's GetOne) and attached the normal way.
        public static bool StoredAttach = true;   // A/B: false = before v0.66.2
        public static long AttachesStored, DetachesStored;

        private static bool AttachStored(Ent pe, int attachType, int indexType, int index, uint parentNet, int parentIdx, int poigen, Vector3d poiid, Vector3d lpos, Vector3d gpos)
        {
            var data = savedatascript.s != null ? savedatascript.s.data : null;
            if (data == null || data.itemData == null || pe.RootId == 0 || !savedatascript.IndexOfID(data.itemData.items, pe.RootId, out int k)) return false;
            var list = data.itemData.items;
            uint parentId = 0;
            if (attachType == 1)
            {
                if (!ByNet.TryGetValue(parentNet, out var pa)) return false;
                if (pa.Root != null)
                {
                    // the parent is here: place the part now — the caller then attaches it the normal way
                    if (itemPlaceRemoveScript.s != null && itemPlaceRemoveScript.s.GetOne(pe.RootId, out _)) { PartsPlacedForAttach++; Resolve(pe); }
                    return false;
                }
                parentId = parentIdx == 0 ? pa.RootId : pa.ItemIds != null && parentIdx - 1 < pa.ItemIds.Count ? pa.ItemIds[parentIdx - 1] : 0;
                if (parentId == 0 || !savedatascript.IndexOfID(list, parentId, out int pk)) return false;
                var pt = list[pk].transform;
                MoveRecord(list, k, pt.pos, pt.rot.Load());   // stored with its parent: placed back together
            }
            else if (attachType > 0) MoveRecord(list, k, gpos, list[k].transform.rot.Load());
            if (CopyLockSafe) { DropFromLockRecords(data.itemData, pe.RootId); pe.Ip = new PoseInterpolator(); }   // bolted on: in no container, no stale pose
            data.itemData.attachable.RemoveAll(a => a != null && a.id == pe.RootId);
            _boltIdx.Invalidate();
            data.itemData.attachable.Add(new save_attachable { id = pe.RootId, attachType = attachType, indexType = indexType, index = index, parentid = parentId,
                                                               poigenid = poigen, poiid = poiid, lpos = lpos, gpos = gpos });
            AttachesStored++;
            AttachesApplied++;
            SessionLog.Event("attach-applied", pe.NetId, parentNet, item: parentIdx, slot: index, to: MyId, result: "far store");
            return true;
        }
        public static long PartsPlacedForAttach, LocksOpenedForAttach;

        /// A stored object's container records (save_physicslock: what a container holds, placed and stored with it)
        /// without this item.
        private static void DropFromLockRecords(itemDataClass d, uint id)
        {
            if (d == null || d.physLocks == null) return;
            foreach (var rec in d.physLocks)
            {
                if (rec == null || rec.IDs == null) continue;
                int i = System.Array.IndexOf(rec.IDs, id);
                if (i < 0) continue;
                var ids = new List<uint>(rec.IDs); ids.RemoveAt(i); rec.IDs = ids.ToArray();
                if (rec.poss != null && i < rec.poss.Length) { var l = new List<vector3>(rec.poss); l.RemoveAt(i); rec.poss = l.ToArray(); }
                if (rec.rots != null && i < rec.rots.Length) { var l = new List<vector3>(rec.rots); l.RemoveAt(i); rec.rots = l.ToArray(); }
                LockRecordsDropped++;
            }
        }

        public static bool ClearHeldOnAttach = true;   // A/B: false = before v0.66.2 (heldattach.py)
        public static long HeldClearedOnAttach;

        internal static bool RidesParent(Ent e)
        {
            var at = e.Root != null ? e.Root.attachable : null;
            return at != null && at.attached && at.parentAtAttach && e.Root.transform.parent != null;
        }
        public static long BoltedGroupSkips;
        internal static bool RidesBolted(Ent e)
        {
            if (!RidesParent(e)) return false;
            if (e.PartIndex >= 0 || e.Items.Count <= 1) return true;
            BoltedGroupSkips++;
            return true;
        }

        /// The entity this object is the root of (its own shared object), or null.
        /// The shared object whose root is this item. In the placed objects (Entities.Placed), by reference: it runs in the
        /// game's item streaming for every item in range every second (the freeze hooks, Entities.Proxy), and walking every
        /// shared object with Unity's == was ~1 ms an item in a world of ~12,000 — a 150-300 ms hitch every second in the
        /// DevTest session of 2026-11-10. Every object only when one was shared or bound since the list was made.
        internal static Ent OwnEntity(tosaveitemscript ts)
        {
            // null: no object (it matched the first stored-away one, whose Root is null too, after a walk of every object)
            if (ReferenceEquals(ts, null) || ts == null) return null;
            if (LoopPlacedOnly) return PlacedOf(ts);
            foreach (var e in ByNet.Values) if (e.Root == ts) return e;
            return null;
        }

        public static bool AttachReqBecomesSync = true;   // A/B: false = relayed as a request every copy ignores (before)
        public static bool StaleDetachDirect = true;    // A/B: false = through the reach and rate checks of ServerDetachReq
        public static bool StaleOwnerForwards = true;   // A/B: false = a bolt or an unbolt from a player who just lost the object is dropped (before)
        public static long StaleAttachesForwarded, StaleDetachesForwarded;
        public static long AttachReqsSent, AttachReqsApplied, AttachReqsRefused;

        private static void SendAttach(Ent pe, attachablescript at, byte type = AttachSync)
        {
            var rec = new save_attachable(at, at.tosave.idInSave);
            if (rec.attachType == 0) return;
            uint parentNet = 0; int parentIdx = 0;
            if (rec.attachType == 1)
            {
                var pt = Fixes.SlotOwnerSave.Owner(at);   // a slot outside its owner's hierarchy (Bus01's rear wheels)
                Ent pa = null;
                if (pt != null)
                    foreach (var e in LookupSet())
                    {
                        if (e == pe || e.Root == null) continue;
                        if (e.Root == pt) { pa = e; parentIdx = 0; break; }
                        int i = e.Items.IndexOf(pt);
                        if (i >= 0 && pa == null) { pa = e; parentIdx = i + 1; }
                    }
                if (pa == null) { AttachesFailed++; AttachFails += "unshared-parent(" + (pt != null ? pt.name : "?") + ") "; return; }
                parentNet = pa.NetId;
            }
            if (at.utanfuto != null && type == AttachSync && parentNet != 0) pe.HitchAnnounced.Add(parentNet);
            W.Reset(); W.U8(type); W.U32(pe.NetId); W.U8((byte)rec.attachType); W.U8((byte)rec.indexType); W.VarU32((uint)(rec.index + 1));
            W.U32(parentNet); W.VarU32((uint)parentIdx); W.VarU32((uint)(rec.poigenid + 1));
            W.F64(rec.poiid.x); W.F64(rec.poiid.y); W.F64(rec.poiid.z);
            W.F64(rec.lpos.x); W.F64(rec.lpos.y); W.F64(rec.lpos.z);
            W.F64(rec.gpos.x); W.F64(rec.gpos.y); W.F64(rec.gpos.z);
            WriteAttachPose(W, at);
            ToServer(W, true);
            if (type == AttachReq) AttachReqsSent++; else AttachesSent++;
            SessionLog.Event(type == AttachReq ? "attach-request" : "attach-sent", pe.NetId, parentNet, item: parentIdx, slot: rec.index, from: MyId, epoch: pe.Epoch);
        }

        public static long SlotPosesSkipped;

        /// Server: a player bolted a part it doesn't own — to the part's owner (who may be the requester by now: its
        /// claim went through first — then it is a plain attach).
        internal static void ServerAttachReq(int from, NetReader r)
        {
            int start = r.Pos;
            uint partNet = r.U32();
            if (r.Bad || !Server.TryGetValue(partNet, out var se)) return;
            r.Pos = start;
            if (se.OwnerId == from) { ServerAttach(from, r); return; }
            WS.Reset(); WS.Bytes(r.Buf, 0, r.End);   // the whole message, its type byte included
            ServerSendTo(se.OwnerId, WS, true);
        }

        /// The owner runs the requested attach and tells everyone (AttachSync, as its own). It can't (the part in its
        /// player's hands, the slot taken, the parent not here): it tells everyone the part is off (DetachSync) — the
        /// requester's local bolt is undone instead of staying different from everyone else's.
        internal static void ApplyAttachReq(NetReader r)
        {
            int start = r.Pos;
            uint partNet = r.U32();
            if (r.Bad || !ByNet.TryGetValue(partNet, out var pe) || !Resolve(pe) || pe.Root == null || pe.Proxy || pe.Root.attachable == null) return;
            r.Pos = start;
            if (!IsHeld(pe.Root))
            {
                long failed = AttachesFailed;
                ApplyAttachSync(r);
                if (AttachesFailed == failed && pe.Root != null && pe.Root.attachable.attached)
                {
                    SendAttach(pe, pe.Root.attachable);
                    AttachReqsApplied++;
                    return;
                }
            }
            AttachReqsRefused++;
            SessionLog.Event("attach-refused", partNet, to: MyId, result: "held or attach failed");
            if (pe.Root != null && !pe.Root.attachable.attached) SendDetachSync(pe);
        }

        /// Server: everyone else replays it; bolted onto another player's object, it becomes theirs.
        internal static void ServerAttach(int from, NetReader r)
        {
            uint partNet = r.U32(); byte attachType = r.U8(); r.U8(); r.VarU32(); uint parentNet = r.U32();
            if (r.Bad || !Server.TryGetValue(partNet, out var se)) return;
            if (se.OwnerId != from)
            {
                // the sender bolted it before it heard it no longer owns it: bolted on its screen, off on every other one
                // for good (itemchaos door-stale). The owner does it now, as for an AttachReq.
                if (!StaleOwnerForwards) return;
                StaleAttachesForwarded++;
                SessionLog.Event("attach-forwarded", partNet, parentNet, from: from, to: se.OwnerId, result: "sender no longer owns it");
                WS.Reset(); WS.U8(AttachReq); WS.Bytes(r.Buf, 1, r.End - 1);
                ServerSendTo(se.OwnerId, WS, true);
                return;
            }
            SetLiveRecordFloor(new UsableKey { Net = partNet, Epoch = se.Epoch, Item = 0 }, SessionLog.St);
            r.Pos = 0; int len = r.End;
            // its claim went through first (ServerAttachReq): for everyone else, and for later joiners, an attach, not a request
            if (AttachReqBecomesSync && r.Buf[0] == AttachReq) r.Buf[0] = AttachSync;
            WS.Reset(); WS.Bytes(r.Buf, 0, len);
            ServerSendAll(WS, true, from);
            // kept for whoever gets these objects later: a joiner gets the part's own record (taken when it came off:
            // loose, where it came off) and its states, which leave out parents in other groups (per-machine ids) — the
            // hubcap bolted back by another player lay loose 22 m from the car for a player who joined later (buglist2m 9d)
            if (HitchTrailer(se) && attachType == 1) se.Hitch = true;
            KeepAttach(se, attachType == 1 ? parentNet : 0, r.Buf, len);
            if (se.HitchCar != 0 && Server.TryGetValue(se.HitchCar, out var tow) && tow.OwnerId != se.OwnerId)
            { BeginHitchTransfer(se, tow.OwnerId); return; }
            // bolted on: in nobody's hands. The owner sends no more states for it (it rides its parent), so the "held" of
            // its last one stayed — and the server refused everyone's claims on it from then on (playtest 2026-10-09:
            // parts locked for both players)
            if (ClearHeldOnAttach && (se.Held || se.Stored)) { se.Held = false; se.Stored = false; HeldClearedOnAttach++; }
            if (attachType == 1 && Server.TryGetValue(parentNet, out var parent) && parent.OwnerId != se.OwnerId)
            {
                OwnerLog(se, parent.OwnerId, "attached onto net " + parentNet);
                se.OwnerId = parent.OwnerId; se.Epoch++;
                SetLiveRecordFloor(new UsableKey { Net = partNet, Epoch = se.Epoch, Item = 0 }, SessionLog.St);
                W.Reset(); W.U8(Owner); W.U32(partNet); W.VarU32((uint)se.OwnerId); W.U32(se.Epoch);
                ServerSendAll(W, true, -1);
                CascadeOwner(se);
            }
        }

        internal static void ApplyAttachSync(NetReader r)
        {
            uint partNet = r.U32(); int attachType = r.U8(), indexType = r.U8(), index = (int)r.VarU32() - 1;
            uint parentNet = r.U32(); int parentIdx = (int)r.VarU32(), poigen = (int)r.VarU32() - 1;
            var poiid = new Vector3d(r.F64(), r.F64(), r.F64());
            var lpos = new Vector3d(r.F64(), r.F64(), r.F64());
            var gpos = new Vector3d(r.F64(), r.F64(), r.F64());
            bool hasPose = ReadAttachPose(r, out var poseLp, out var poseLq, out var framePath, out var pointLp);
            if (r.Bad) return;
            if (framePath != null) EntityDoors.KeepDoorAttach(partNet, framePath, pointLp, poseLp, poseLq);
            else EntityDoors.ForgetStoredDoorAttach(partNet);
            SessionLog.Event("attach-received", partNet, parentNet, item: parentIdx, slot: index, to: MyId);
            if (hasPose && indexType == 2) { hasPose = false; SlotPosesSkipped++; }   // an older sender's, a kept one
            if (ByNet.TryGetValue(partNet, out var pd) && !Resolve(pd) && StoredAttach && AttachStored(pd, attachType, indexType, index, parentNet, parentIdx, poigen, poiid, lpos, gpos))
                return;
            if (!ByNet.TryGetValue(partNet, out var pe) || !Resolve(pe) || pe.Root == null || pe.Root.attachable == null)
            { SessionLog.Event("attach-refused", partNet, parentNet, slot: index, result: "part absent"); AttachesFailed++; if (AttachFails.Length < 400) AttachFails += "nopart" + partNet + " "; return; }
            var at = pe.Root.attachable;
            if (at.utanfuto != null) HitchRemoveJoint(at);
            uint parentId = 0;
            tosaveitemscript pt = null;
            if (attachType == 1)
            {
                if (ByNet.TryGetValue(parentNet, out var pa) && Resolve(pa) && pa.Root != null)
                    pt = parentIdx == 0 ? pa.Root : parentIdx - 1 < pa.Items.Count ? pa.Items[parentIdx - 1] : null;
                if (pt == null) { SessionLog.Event("attach-refused", partNet, parentNet, item: parentIdx, slot: index, result: "parent absent"); AttachesFailed++; AttachFails += "noparent" + parentNet + "/" + parentIdx + " "; return; }
                parentId = pt.idInSave;
                bool sameSlot = indexType != 2 || (at.slot != null && pt.partslotscripts.IndexOf(at.slot) == index);
                if (at.utanfuto == null && at.attached && Fixes.SlotOwnerSave.Owner(at) == pt && sameSlot)
                {
                    LiveUsablesChanged(pe.Root);
                    // already on it here (our own requested attach coming back, a replay on arrival): only the pose
                    if (hasPose && EntityDoors.RestoreAttachFrame(at, framePath, pointLp)) ApplyAttachPose(pe, at, poseLp, poseLq);
                    SessionLog.Event("attach-applied", partNet, parentNet, item: parentIdx, slot: index, result: "already attached");
                    AttachesSame++; return;
                }
            }
            // a copy a container here holds: out of it first (its body back), then attached the normal way — attached in
            // the container it kept the lock, and the release rule later took it off the car (partsfar.py near)
            if (CopyLockSafe && pe.Proxy && pe.Root.P != null && pe.Root.P.physlock != null) { pe.Root.P.UnPhysicsLock(); LocksOpenedForAttach++; }
            if (at.attached)
            {
                _applyingDetach = true;
                try { Unmount(at, true); } finally { _applyingDetach = false; }
            }
            if (attachType == 1 && indexType == 2) ClearSlotFor(at, pt, index);
            _applyingAttach = true;
            try { save_attachable.Load(at, indexType, attachType, parentId, poigen, poiid, index, lpos, gpos); }
            finally { _applyingAttach = false; }
            if (!at.attached) { SessionLog.Event("attach-refused", partNet, parentNet, slot: index, result: "loader did not attach"); AttachesFailed++; AttachFails += "load" + partNet + " "; return; }
            if (hasPose && EntityDoors.RestoreAttachFrame(at, framePath, pointLp)) ApplyAttachPose(pe, at, poseLp, poseLq);
            if (pe.Proxy) { ApplyProxyBodies(pe); pe.Ip = new PoseInterpolator(); }
            if (ClearHeldOnAttach && pe.Held) ShowHeld(pe, false);   // bolted on: no more states come — its copy collides again
            if (ClearHeldOnAttach && pe.Stored) ShowStored(pe, false);
            AttachesApplied++;
            SessionLog.Event("attach-applied", partNet, parentNet, item: parentIdx, slot: index, to: MyId, epoch: pe.Epoch);
        }

        /// A part arrives for a slot that still shows another one here. The player who bolted it saw that slot empty: the
        /// part it holds was off on their screen and on on this one. The game's loader adds the new part beside the old
        /// one and neither machine lets go of its version: two doors in one slot, for good, and in the save (2026-10-29:
        /// "double doors": the other player saw no door and put one on). The newest bolt wins, the old part comes off:
        /// the owner of the old part takes it off for everyone, a copy lets it fall here.
        public static bool SlotReplaces = true;   // A/B: false = the loader adds the second part to the slot (before)
        public static long SlotPartsReplaced;
        private static void ClearSlotFor(attachablescript at, tosaveitemscript parent, int index)
        {
            if (!SlotReplaces || at.utanfuto != null || parent == null || parent.partslotscripts == null || index < 0 || index >= parent.partslotscripts.Count) return;
            var slot = parent.partslotscripts[index];
            if (slot == null || slot.attachTarget || !slot.hasPart()) return;
            foreach (var old in new List<attachablescript>(slot.parts))
            {
                if (old == null || old == at) continue;
                bool copy = PartIsProxy(old);
                _applyingDetach = copy;
                try { Unmount(old, true); } finally { _applyingDetach = false; }
                SlotPartsReplaced++;
                SessionLog.Event("slot-part-replaced", 0, slot: index, result: old.name + " by " + at.name + (copy ? " (copy)" : ""));
                Plugin.Log.LogWarning($"slot {index} of {parent.name} still had {old.name} here when {at.name} was bolted into it: {old.name} comes off");
            }
        }

        /// Server: an object's owner changed — what is bolted onto it goes along, all the way down (a hubcap on a wheel on
        /// a car). Without it, a part bolted on while another player held the car (a claim, a hand-off) stayed that
        /// player's when the car went back: its owner, 850 m away with the car stored, answered nothing when the car's
        /// owner took it off — loose on one machine, bolted on the other (playround.py R3, 2026-10-09).
        public static bool OwnerCascade = true;   // A/B: false = before v0.66.5
        public static long OwnerCascaded;
        private static void CascadeOwner(SEnt se, int depth = 0)
        {
            CascadeHitches(se);
            if (!OwnerCascade || se.AttachKids == null || depth > 8) return;
            foreach (var kid in new List<uint>(se.AttachKids))
            {
                if (kid == se.NetId || !Server.TryGetValue(kid, out var k) || k.AttachParent != se.NetId || k.HitchCar != 0 || k.OwnerId == se.OwnerId || k.Held || k.Stored) continue;
                OwnerLog(k, se.OwnerId, "with net " + se.NetId + " (bolted onto it)");
                k.OwnerId = se.OwnerId; k.Epoch++; k.CrashReturnTo = -1;
                W.Reset(); W.U8(Owner); W.U32(k.NetId); W.VarU32((uint)k.OwnerId); W.U32(k.Epoch);
                ServerSendAll(W, true, -1);
                OwnerCascaded++;
                CascadeOwner(k, depth + 1);
            }
        }

        private static void KeepAttach(SEnt se, uint parentNet, byte[] msg, int len)
        {
            DropAttach(se);
            se.LastAttach = new byte[len]; System.Buffer.BlockCopy(msg, 0, se.LastAttach, 0, len);
            se.AttachParent = parentNet;
            if (se.Hitch) se.HitchCar = parentNet;
            if (parentNet != 0 && Server.TryGetValue(parentNet, out var pa)) (pa.AttachKids ?? (pa.AttachKids = new System.Collections.Generic.HashSet<uint>())).Add(se.NetId);
            AttachesKept++;
        }

        private static void DropAttach(SEnt se)
        {
            se.HitchCar = 0;
            if (se.AttachParent != 0 && Server.TryGetValue(se.AttachParent, out var pa) && pa.AttachKids != null) pa.AttachKids.Remove(se.NetId);
            se.LastAttach = null; se.AttachParent = 0;
        }

        public static long AttachesKept, AttachesReplayed;

        /// SendObject: the object's last attach once what it's attached to is known to that player, and the last attaches
        /// of objects attached onto it that the player knows (after their Add/part messages: the joiner's copy exists).
        /// And its parts' own: one bolted onto something the player was sent before (AttachReplay).
        public static bool ReplayPartAttaches = true;   // A/B: false = a part's attach is replayed only as its parent's kid
        private static readonly System.Collections.Generic.List<uint> _replay = new System.Collections.Generic.List<uint>();
        private static void ReplayAttaches(int playerId, SEnt se)
        {
            AttachReplay.Pick(se.NetId, se.LastAttach != null, se.AttachParent, se.AttachKids, ReplayPartAttaches ? se.Parts : null,
                n => Server.TryGetValue(n, out var k) && k.LastAttach != null, n => Server.TryGetValue(n, out var k) ? k.AttachParent : 0,
                n => KnownTo(playerId, n), _replay);
            foreach (var n in _replay) SendKept(playerId, n == se.NetId ? se : Server[n]);
        }

        /// Known to the player: sent itself, or a part that came off an object it was sent (WritePartDetached).
        private static bool KnownTo(int playerId, uint net) =>
            KnownBy(playerId).Contains(net) || (Server.TryGetValue(net, out var s) && s.ParentNet != 0 && KnownBy(playerId).Contains(s.ParentNet));

        private static void SendKept(int playerId, SEnt se)
        {
            WS.Reset(); WS.Bytes(se.LastAttach, 0, se.LastAttach.Length);
            ServerSendTo(playerId, WS, true);
            AttachesReplayed++;
        }

        internal static void ServerDetachSync(int from, NetReader r)
        {
            uint net = r.U32(), epoch = r.U32();
            if (r.Bad || !Server.TryGetValue(net, out var se)) return;
            if (se.OwnerId != from)
            {
                // the same for taking it off: no door on that player's screen, still on on the other one; a second door
                // bolted on top of it followed (double doors, 2026-10-29; itemchaos door-stale)
                if (!StaleOwnerForwards) return;
                StaleDetachesForwarded++;
                SessionLog.Event("detach-forwarded", net, from: from, to: se.OwnerId, result: "sender no longer owns it");
                // not through the checks of a player's request: it has already happened on their screen, a refusal is this bug again
                if (StaleDetachDirect && se.PrevOwnerId == from && Time.realtimeSinceStartup - se.PrevOwnerAt <= HandoverAcceptS)
                {
                    W.Reset(); W.U8(DetachReq); W.U32(net); W.VarU32(0u);
                    ServerSendTo(se.OwnerId, W, true);
                }
                else ServerDetachReq(from, net, 0);
                return;
            }
            if (!ClaimEpochOk(se, from, epoch)) return;
            SetLiveRecordFloor(new UsableKey { Net = net, Epoch = se.Epoch, Item = 0 }, SessionLog.St);
            DropAttach(se);
            r.Pos = 0; int len = r.End;
            WS.Reset(); WS.Bytes(r.Buf, 0, len);
            ServerSendAll(WS, true, from);
        }

        public static string AttachStats() => "{\"attachesStored\":" + AttachesStored + ",\"detachesStored\":" + DetachesStored + ",\"placedForAttach\":" + PartsPlacedForAttach + ",\"heldClearedOnAttach\":" + HeldClearedOnAttach + ",\"sent\":" + AttachesSent + ",\"applied\":" + AttachesApplied + ",\"same\":" + AttachesSame + ",\"failed\":" + AttachesFailed
                                              + ",\"slotPosesSkipped\":" + SlotPosesSkipped + ",\"boltedGroupSkips\":" + BoltedGroupSkips +",\"posesSent\":" + AttachPosesSent + ",\"posesApplied\":" + AttachPosesApplied + ",\"posesBad\":" + AttachPosesBad
                                              + ",\"poseOffLast\":" + Json.Num(AttachPoseOffLast) + ",\"poseOffMax\":" + Json.Num(AttachPoseOffMax) + ",\"poseAngLast\":" + Json.Num(AttachPoseAngLast)
                                              + ",\"fails\":" + Json.Str(AttachFails) + ",\"detachSent\":" + DetachSyncsSent + ",\"detachApplied\":" + DetachSyncsApplied + ",\"unmountTrace\":" + Json.Str(UnmountTrace) + "}";
    }

    internal static partial class EntityAttachments
    {
        public static float AttachPoseOffLast, AttachPoseOffMax, AttachPoseAngLast;   // copy vs. the attacher's pose when it arrived (m, °)

        public static long AttachPosesSent, AttachPosesApplied, AttachPosesBad;

        public static bool AttachRequests = true;   // A/B: false = before v0.66.4 (a copy bolted by the player stays local)

        [HarmonyLib.HarmonyPatch(typeof(attachablescript), nameof(attachablescript.Attach), new[] { typeof(Transform) })]
        private static class AttachHook
        {
            [HarmonyLib.HarmonyPrefix, HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Normal + 1)]
            private static void Prefix(attachablescript __instance, Transform Ppoint)
            {
                if (Entities.InSession && __instance.slot == null && Ppoint != null && EntityDoors.DoorAttachFrame(Ppoint.parent)) __instance.parentAtAttach = true;
            }

            /// Every attach overload and slot Craft ends here: the local player (or the game on its behalf) attached
            /// a shared object this machine simulates → tell everyone.
            [HarmonyLib.HarmonyPostfix, HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Normal + 1)]
            private static void Postfix(attachablescript __instance)
            {
                if (Entities.InSession && __instance.attached) Entities.AttachmentUsablesChanged(__instance.tosave);
                if (Entities.AttachmentApplying || !Entities.InSession || __instance.tosave == null) return;
                var pe = Entities.AttachmentRoot(__instance.tosave);
                if (pe == null) return;
                if (pe.Proxy)
                {
                    // the local player bolted a part this machine doesn't simulate (a pickup claim still on its way, or
                    // overtaken: another player's car claimed it as it was set down): the owner does it — a local-only
                    // attach stayed bolted here and loose everywhere else (buglist2m 3b, full sequence)
                    if (AttachRequests && Entities.AttachmentPlayerActing) { Entities.SendAttachment(pe, __instance, Entities.AttachReq); Entities.RefreshAttachmentBodies(pe); pe.Ip = new PoseInterpolator(); }
                    return;
                }
                Entities.SendAttachment(pe, __instance);
            }
        }

        internal static Transform AttachFrame(attachablescript at) => at.point == null ? null : at.point.parent != null ? at.point.parent : at.point;

        internal static void WriteAttachPose(NetWriter w, attachablescript at)
        {
            var f = AttachFrame(at);
            if (f == null || at.slot != null) { w.U8(0); return; }
            var t = at.transform; var iq = Quaternion.Inverse(f.rotation);
            var lp = iq * (t.position - f.position); var lq = iq * t.rotation;
            var path = EntityDoors.DoorAttachFrame(f) ? EntityDoors.AttachFramePath(at, f) : null;
            w.U8(path != null ? (byte)2 : (byte)1); w.VarI32(EntityCodec.Mm(lp.x)); w.VarI32(EntityCodec.Mm(lp.y)); w.VarI32(EntityCodec.Mm(lp.z)); EntityCodec.WriteRot(w, lq);
            if (path != null)
            {
                w.VarU32((uint)path.Length);
                foreach (int index in path) w.VarU32((uint)index);
                var point = f.InverseTransformPoint(at.point.position);
                w.VarI32(EntityCodec.Mm(point.x)); w.VarI32(EntityCodec.Mm(point.y)); w.VarI32(EntityCodec.Mm(point.z));
                var e = Entities.AttachmentRoot(at.tosave);
                if (e != null) EntityDoors.KeepDoorAttach(e.NetId, path, point, lp, lq, at.point);
            }
            AttachPosesSent++;
        }

        internal static bool ReadAttachPose(NetReader r, out Vector3 lp, out Quaternion lq, out int[] path, out Vector3 point)
        {
            lp = point = Vector3.zero; lq = Quaternion.identity; path = null;
            byte kind = r.U8();
            if (kind == 0) return false;
            if (kind != 1 && kind != 2) { r.Bad = true; return false; }
            lp = new Vector3(r.VarI32() / 1000f, r.VarI32() / 1000f, r.VarI32() / 1000f);
            lq = EntityCodec.ReadRot(r);
            if (kind == 2)
            {
                uint count = r.VarU32();
                if (count > 64) { r.Bad = true; return false; }
                path = new int[count];
                for (int i = 0; i < path.Length; i++) path[i] = (int)r.VarU32();
                point = new Vector3(r.VarI32() / 1000f, r.VarI32() / 1000f, r.VarI32() / 1000f);
            }
            if (r.Bad || lp.sqrMagnitude > 1e6f || !Entities.IsFinite(lp) || point.sqrMagnitude > 1e6f || !Entities.IsFinite(point)) { r.Bad = true; AttachPosesBad++; return false; }
            return true;
        }

        internal static void ApplyAttachPose(Entities.Ent pe, attachablescript at, Vector3 lp, Quaternion lq)
        {
            var f = AttachFrame(at);
            if (f == null) return;
            var t = at.transform;
            Vector3 p = f.position + f.rotation * lp; Quaternion q = f.rotation * lq;
            AttachPoseOffLast = (t.position - p).magnitude; AttachPoseAngLast = Quaternion.Angle(t.rotation, q);
            if (AttachPoseOffLast > AttachPoseOffMax) AttachPoseOffMax = AttachPoseOffLast;
            t.SetPositionAndRotation(p, q);
            EntityDoors.BindStoredDoorAttach(pe.NetId, at.point);
            AttachPosesApplied++;
        }
    }
}
