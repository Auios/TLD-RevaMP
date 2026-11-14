using System.Collections.Generic;
using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    /// Hats, caps, helmets, glasses and masks (the game's wearable slots, wearableType; a lit cigarette goes in one too).
    /// The game wears an item by parenting it to the local player's head (fpscontroller.Wear: TWearablePos by
    /// wearable.tipus, its rigidbody destroyed, colliders off). Before v0.68.2 nothing told the other machines: the
    /// wearer's states drew the copy where the hat had been on its own (late) path — beside the head when walking, metres
    /// behind a passenger at speed — as a colliding body anyone could grab or a car could claim; and a record captured
    /// while worn (save_wear: wearable.player = the wearer's playerIndex, 1) put the hat on the receiving machine's own
    /// player when it loaded (save_wear.Load → fpscontroller.Wear).
    /// Now:
    ///  - the owner says when it is put on or taken off (Worn: slot, pose relative to its head target, anim.HHTarget — the
    ///    point the pose stream gives the remote body's head too, PlayerLook); the server keeps it for players the object
    ///    reaches later (join, interest), refuses claims and hand-offs while worn, and counts it in the wearer's pocket
    ///    (PlayerSave: it leaves with them and comes back on their head);
    ///  - copies ride the wearer's body: placed every frame after animation (LateUpdate) at that pose on the remote body's
    ///    head target, no collisions. Not parented: a seated body goes with the car model it sits in (RemotePlayers) and
    ///    would take the hat with it. No body shown here (far away, hidden with its car): the states place it as before;
    ///  - records from the network never carry a wearer (RecordCodec.StripWorn).
    internal static partial class EntityHands
    {
        private static readonly NetWriter W = new NetWriter();
        public static bool WornSync = true;   // A/B: false = before v0.68.2 (worn items drawn at their owner's states)
        public static long WornSent, WornRelayed, WornApplied, WornCleared, WornRefused, WornPlaced;
        private static float _wornCheckAt;
        private static readonly HashSet<Ent> _wornOwn = new HashSet<Ent>(), _wornCopies = new HashSet<Ent>();
        private static readonly List<Ent> _wornTmp = new List<Ent>();
        private static readonly Dictionary<tosaveitemscript, Ent> _wornEnt = new Dictionary<tosaveitemscript, Ent>();

        /// The slot this item is worn in by the local player, -1 if it isn't.
        internal static int LocalWornSlot(tosaveitemscript it)
        {
            var p = it != null ? it.P : null;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (p == null || p.wear == null || !p.wear.wearing || pl == null || pl.weared == null) return -1;
            int i = (int)p.wear.tipus;
            return i >= 0 && i < pl.weared.Length && pl.weared[i] == p ? i : -1;
        }

        private static Transform LocalHead()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return null;
            if (pl.anim != null && pl.anim.HHTarget != null) return pl.anim.HHTarget;
            return pl.Cam != null ? pl.Cam.transform : null;
        }

        /// Owner, 10 Hz: what the local player put on or took off since (any way the game does it: the wear key, a
        /// cigarette lit from its pack, death, a restored player save).
        internal static void WornTick()
        {
            if (!WornSync || Time.unscaledTime < _wornCheckAt) return;
            _wornCheckAt = Time.unscaledTime + 0.1f;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || pl.weared == null) return;
            _wornTmp.Clear(); _wornTmp.AddRange(_wornOwn);
            foreach (var e in _wornTmp)
            {
                int s = e.Proxy || e.Root == null ? -1 : LocalWornSlot(e.Root);
                if (s >= 0) continue;
                if (!e.Proxy && e.SentWornSlot >= 0 && Entities.HasEntity(e.NetId)) SendWorn(e, -1);
                Plugin.Log.LogInfo("worn: " + (e.Root != null ? e.Root.name : "?") + " net " + e.NetId + " off the local player" + (e.Proxy ? " (another machine's now)" : ""));
                e.SentWornSlot = -1;
                _wornOwn.Remove(e);
            }
            if (_wornEnt.Count > 64) _wornEnt.Clear();
            for (int i = 0; i < pl.weared.Length; i++)
            {
                var p = pl.weared[i];
                if (p == null) continue;
                var ts = p.tosave != null ? p.tosave : p.GetComponent<tosaveitemscript>();
                if (ts == null) continue;
                if (!_wornEnt.TryGetValue(ts, out var e) || e.Root != ts || !Entities.HasEntity(e.NetId))
                {
                    e = Entities.OwnEntity(ts);   // not shared yet (a cigarette just lit): next time
                    if (e == null) continue;
                    _wornEnt[ts] = e;
                }
                if (WornLateOk && WornNotMineAnymore(pl, p, e)) continue;
                if (e.Proxy || e.SentWornSlot == i) continue;
                SendWorn(e, i);
                _wornOwn.Add(e);
            }
        }

        /// On the local player's head and in the game's wear slot, whoever owns it. A copy there is a hat the other player's
        /// states carry away: it lay on the ground and the slot stayed taken, so no other hat could go on. It is claimed back
        /// (the wear key's own claim); when the claim keeps being refused it comes off here. Back as ours it can have been
        /// moved off the head meanwhile: put on again. True: nothing to send this round.
        public static int WornClaimTries = 3;
        public static float WornClaimEveryS = 1f, WornOffHeadM = 1.2f;
        public static long WornClaimedBack, WornGivenUp, WornReseated;
        private static bool WornNotMineAnymore(fpscontroller pl, pickupable p, Ent e)
        {
            if (e.Proxy)
            {
                if (e.Root == null || e.Root.car != null) return true;
                if (Time.unscaledTime - e.WornClaimAt < WornClaimEveryS) return true;
                e.WornClaimAt = Time.unscaledTime;
                if (e.WornClaims++ >= WornClaimTries)
                {
                    e.WornClaims = 0; WornGivenUp++;
                    Plugin.Log.LogWarning("worn: " + e.Root.name + " net " + e.NetId + " is another player's: off the local player");
                    pl.TakeOff(p, true);
                    return true;
                }
                Entities.ClaimWorn(e);
                WornClaimedBack++;
                return false;
            }
            e.WornClaims = 0;
            var head = LocalHead();
            if (head != null && e.Root != null && (e.Root.transform.position - head.position).sqrMagnitude > WornOffHeadM * WornOffHeadM)
            {
                pl.TakeOff(p, true);
                pl.Wear(p);
                WornReseated++;
            }
            return false;
        }

        internal static void SendWorn(Ent e, int slot)
        {
            W.Reset(); W.U8(Entities.Worn); W.U32(e.NetId); W.U32(e.Epoch); W.U8((byte)(slot < 0 ? 255 : slot));
            if (slot >= 0) WriteWornPose(W, e.Root);
            Entities.SendToServer(W, true);
            e.SentWornSlot = slot;
            WornSent++;
        }

        /// Where a worn item sits on the local player's head: metres in the head target's frame (unscaled: the two bodies'
        /// rigs need not share a scale), and its rotation there.
        private static void WriteWornPose(NetWriter w, tosaveitemscript root)
        {
            var h = LocalHead(); var t = root.transform;
            var iq = h != null ? Quaternion.Inverse(h.rotation) : Quaternion.identity;
            Vector3 lp = h != null ? iq * (t.position - h.position) : Vector3.zero;
            Quaternion lq = iq * t.rotation;
            w.F32(lp.x); w.F32(lp.y); w.F32(lp.z); EntityCodec.WriteRot(w, lq);
        }

        /// A share of something the local player already wears (a player save's hat put back on at a rejoin, a cigarette
        /// lit and then shared) says so in the same message. Before, the Worn message came only after the network id
        /// did: for that round trip the server and the others had a loose hat at head height, and the host's car took it
        /// (its contents claim) — on the joiner's screen the hat then sat on its head as the host's copy, 0.7 m below and
        /// 1 m beside it, and nobody else saw it on them (wearsync.py D, 2026-10-17).
        public static bool WornInShare = true;   // A/B: false = before v0.68.5 (the Worn message after the network id only)
        internal static void WriteShareWorn(NetWriter w, tosaveitemscript root)
        {
            int slot = WornSync && WornInShare ? LocalWornSlot(root) : -1;
            if (slot < 0) return;
            w.U8((byte)slot); WriteWornPose(w, root);
        }

        /// The server side: a share that came worn is on its sharer's head from the start (claims refused, SendObject
        /// tells everyone it reaches).

        public static long SharedWorn;

        // ------------------------------------------------------------------ server

        /// A player put it on while someone's claim on it (walking past is enough) was on its way: on their head for good,
        /// loose for everyone else, and no one can do it for them as an owner can bolt a part. A deliberate act beats a claim
        /// that overtook it: it goes back to them, unless the new owner has it in hand or on their own head.
        public static bool WornLateOk = true;   // A/B: false = a Worn from a player who just lost the object is dropped, a copy stays in the wear slot (before)
        public static long WornTakenBack, WornTooLate;

        internal static void ServerWorn(int from, NetReader r)
        {
            uint net = r.U32(), epoch = r.U32(); byte slot = r.U8();
            Vector3 lp = default; Quaternion lq = Quaternion.identity;
            if (slot != 255) { lp = new Vector3(r.F32(), r.F32(), r.F32()); lq = EntityCodec.ReadRot(r); }
            if (r.Bad) return;
            Entities.KeepWornRequest(from, net, epoch, slot, lp, lq);
        }

        internal static bool ReadShareWorn(NetReader r, out byte slot, out Vector3 lp, out Quaternion lq)
        {
            slot = 255; lp = default; lq = Quaternion.identity;
            if (r.Remaining <= 0) return false;   // an older client, or not worn
            slot = r.U8(); lp = new Vector3(r.F32(), r.F32(), r.F32()); lq = EntityCodec.ReadRot(r);
            return !r.Bad && slot <= 15 && Entities.IsFinite(lp) && lp.sqrMagnitude <= 4f;
        }

        internal static void WriteWornOut(NetWriter w, uint net, bool on, int wearer, int slot, Vector3 lp, Quaternion lq)
        {
            w.Reset(); w.U8(Entities.Worn); w.U32(net); w.VarU32((uint)(on ? wearer : 0)); w.U8((byte)(on ? slot : 255));
            if (on) { w.F32(lp.x); w.F32(lp.y); w.F32(lp.z); EntityCodec.WriteRot(w, lq); }
        }

        internal static void RelayWorn(int from, uint net, bool on, int wearer, int slot, Vector3 lp, Quaternion lq)
        {
            WriteWornOut(W, net, on, wearer, slot, lp, lq);
            Entities.SendToPeers(W, true, from);
            WornRelayed++;
        }

        internal static void SendWornTo(int playerId, uint net, int wearer, int slot, Vector3 lp, Quaternion lq)
        {
            WriteWornOut(W, net, true, wearer, slot, lp, lq); Entities.SendToPeer(playerId, W, true);
        }

        // ------------------------------------------------------------------ copies

        internal static void ApplyWorn(NetReader r)
        {
            uint net = r.U32(); int wearer = (int)r.VarU32(); byte slot = r.U8();
            Vector3 lp = default; Quaternion lq = Quaternion.identity;
            if (slot != 255) { lp = new Vector3(r.F32(), r.F32(), r.F32()); lq = EntityCodec.ReadRot(r); }
            if (r.Bad || !Entities.TryGetEntity(net, out var e)) return;
            WornApplied++;
            if (slot == 255 || !WornSync) { ClearWorn(e); return; }
            if (!e.Proxy || wearer == Entities.MyId || !Entities.IsFinite(lp)) return;   // ours: the game's own wear rules it
            e.WornBy = wearer; e.WornSlot = slot; e.WornLp = lp; e.WornLq = lq;
            _wornCopies.Add(e);
            if (e.MovedByBody) Entities.StopBodyMotion(e);
        }

        /// A copy on someone's head collides with nothing (as the worn item does on its wearer's machine).
        private static void WornBodies(Ent e, bool worn)
        {
            if (e.Root == null) return;
            foreach (var rb in e.Root.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.detectCollisions = !worn && !e.Held;
                if (worn) rb.interpolation = RigidbodyInterpolation.None;   // placed in LateUpdate: nothing to interpolate
            }
            var wr = e.Root.P != null ? e.Root.P.wear : null;
            if (wr != null && wr.c != null) foreach (var c in wr.c) if (c != null) c.enabled = !worn;
            e.WornOffFor = worn ? e.Root : null;
        }

        internal static void ClearWorn(Ent e)
        {
            if (e.WornBy < 0) return;
            e.WornBy = -1; e.WornSlot = -1;
            _wornCopies.Remove(e);
            if (e.Root != null && e.WornOffFor == e.Root) WornBodies(e, false);
            e.WornOffFor = null;
            WornCleared++;
        }

        /// The remote body's head this copy rides on, null when there is none to show it on here.
        private static Transform WornAnchor(Ent e) => e.WornBy >= 0 && e.Proxy ? RemotePlayers.WearAnchor(e.WornBy) : null;

        /// On a body here: placed by WornLate, not by its states (Tick).
        internal static bool WornRides(Ent e) => e.WornBy >= 0 && e.Proxy && e.Root != null && WornAnchor(e) != null;

        /// Every frame after animation (the runner's LateUpdate): worn copies onto their wearers' heads.
        public static void WornLate()
        {
            if (_wornCopies.Count == 0) return;
            _wornTmp.Clear(); _wornTmp.AddRange(_wornCopies);
            foreach (var e in _wornTmp)
            {
                if (!e.Proxy || !Entities.HasEntity(e.NetId)) { ClearWorn(e); continue; }
                if (e.Root == null) continue;   // in the far store here: placed again when it comes back
                var head = WornAnchor(e);
                if (head == null) continue;
                if (e.WornOffFor != e.Root) WornBodies(e, true);   // (placed again by the game's streaming: a new object)
                if (e.MovedByBody) Entities.StopBodyMotion(e);
                var q = head.rotation;
                e.Root.transform.SetPositionAndRotation(head.position + q * e.WornLp, q * e.WornLq);
                WornPlaced++;
            }
        }

        internal static void WornReset() { _wornOwn.Clear(); _wornCopies.Clear(); _wornEnt.Clear(); }

        /// `mp wearables`: the spawnable items the game lets a player wear (itemdatabase index, name, slot).
        public static string Wearables()
        {
            var rows = new List<string>();
            var db = itemdatabase.s != null ? itemdatabase.s.items : null;
            if (db != null)
                for (int k = 0; k < db.Length; k++)
                {
                    var w = db[k] != null ? db[k].GetComponentInChildren<wearable>(true) : null;
                    if (w != null) rows.Add("[" + k + "," + Json.Str(db[k].name) + "," + Json.Str(w.tipus.ToString()) + "]");
                }
            return "{\"wearables\":[" + string.Join(",", rows) + "]}";
        }

        /// `mp worn`: every shared wearable this machine knows about: who wears it (here: the local slot; a copy: the
        /// wearer it rides), where it is shown and how far from the wearer's head.
        public static string WornStatus()
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>();
            var lh = LocalHead();
            foreach (var e in Entities.WornEntities())
            {
                if (e.Root == null || e.Root.P == null || e.Root.P.wear == null) continue;
                var t = e.Root.transform;
                int local = LocalWornSlot(e.Root);
                Transform head = e.Proxy ? (e.WornBy >= 0 ? RemotePlayers.WearAnchor(e.WornBy) : null) : (local >= 0 ? lh : null);
                var g = mainscript.GlobalFromUnityPos(t.position);
                var cols = 0; foreach (var c in e.Root.GetComponentsInChildren<Collider>()) if (c.enabled && !c.isTrigger) cols++;
                var rb = e.Root.GetComponent<Rigidbody>();
                // where it is on its wearer's body: height above the body's root, distance from its vertical axis (the same
                // numbers on every machine when it sits where its wearer has it)
                var body = local >= 0 && mainscript.s != null && mainscript.s.player != null ? mainscript.s.player.transform : e.Proxy ? RemotePlayers.BodyOf(e.WornBy >= 0 ? e.WornBy : e.OwnerId) : null;
                string rel = "null";
                if (body != null)
                {
                    var d = t.position - body.position;
                    rel = "[" + d.y.ToString("F3", ic) + "," + new Vector2(d.x, d.z).magnitude.ToString("F3", ic) + "]";
                }
                rows.Add("{\"net\":" + e.NetId + ",\"name\":" + Json.Str(e.Root.name) + ",\"tipus\":" + Json.Str(e.Root.P.wear.tipus.ToString()) +
                         ",\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"owner\":" + e.OwnerId + ",\"localSlot\":" + local + ",\"wornBy\":" + e.WornBy +
                         ",\"wearing\":" + (e.Root.P.wear.wearing ? "true" : "false") + ",\"wearPlayer\":" + e.Root.P.wear.player +
                         ",\"rides\":" + (WornRides(e) ? "true" : "false") + ",\"headDist\":" + (head != null ? Vector3.Distance(head.position, t.position).ToString("F3", ic) : "-1") +
                         ",\"pos\":[" + g.x.ToString("F2", ic) + "," + g.y.ToString("F2", ic) + "," + g.z.ToString("F2", ic) + "]" +
                         ",\"parent\":" + Json.Str(t.parent != null ? t.parent.name : "") + ",\"colliders\":" + cols + ",\"rb\":" + (rb == null ? "\"none\"" : rb.isKinematic ? "\"kinematic\"" : "\"dynamic\"") +
                         ",\"onBody\":" + rel + ",\"held\":" + (e.Held ? "true" : "false") + ",\"shown\":" + (e.Root.gameObject.activeInHierarchy ? "true" : "false") + "}");
            }
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var slots = new List<string>();
            if (pl != null && pl.weared != null) foreach (var p in pl.weared) slots.Add(Json.Str(p != null ? p.name : ""));
            return "{\"enabled\":" + (WornSync ? "true" : "false") + ",\"slots\":[" + string.Join(",", slots) + "],\"sent\":" + WornSent + ",\"relayed\":" + WornRelayed +
                   ",\"applied\":" + WornApplied + ",\"cleared\":" + WornCleared + ",\"refused\":" + WornRefused + ",\"placed\":" + WornPlaced +
                   ",\"sharedWorn\":" + SharedWorn + ",\"strippedRecords\":" + RecordCodec.WornStripped + ",\"items\":[" + string.Join(",", rows) + "]}";
        }
    }
}
