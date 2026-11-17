using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// Server: what a player's edit of someone else's object (Entities.Edits) and a building usable change
    /// (Entities.PoiUsables) may do. Before v0.69.2 the server checked only that the sender stood at the object: the
    /// record itself went to the owner unread — an edit naming every kind (KindAll) loaded the car's controls and
    /// re-attached parts, a "tank fill" could put 10,000 L into a jerry can or NaN into a fuel tank, a colour edit
    /// could repair every part, and a door change could come from anywhere in the world, as often as the sender liked.
    /// Now the server decodes each edit and refuses it when it names kinds edits never carry, when it changes something
    /// its kinds don't cover, or when a value is out of range (non-finite, a tank over capacity, a part look index
    /// nobody has); and each player gets a rate limit for edits and for building usables. Refusals count as anomalies
    /// (Entities.Anomaly), the first 5 of each reason are logged. The host's own edits are trusted (its world is the save).
    internal static class EntityEditCheck
    {
        /// An item's edits leave at most every 0.15 s (EditTick): a door dragged is ~7 a second, pouring the same.
        internal static float EditRatePerS = 20f, EditBurst = 40f;
        /// Live building controls send up to 60/s each; allow linked gates and reliable settling records.
        internal static float PoiRatePerS = 180f, PoiBurst = 240f;
        /// How far from a building's position (its poiid: the building's own spot) a player may be to move one of its
        /// usables — buildings are big; their usables are within ~60 m of it (gas station, the big sites).
        internal static float PoiReachM = 150f;
        internal static float UnknownTankL = 1000f;   // a tank whose capacity the server can't look up (no copy, no prefab)
        public static long EditRefusedRate, EditRefusedKinds, EditRefusedValues, PoiRefusedReach, PoiRefusedRate, PoiRefusedValues;
        public static string EditRefusedLast = "";

        private struct RateBucket { public float Tokens, At; }
        private static readonly Dictionary<int, RateBucket> _editBuckets = new Dictionary<int, RateBucket>(), _poiBuckets = new Dictionary<int, RateBucket>();
        private static readonly Dictionary<string, int> _refusalsLogged = new Dictionary<string, int>();

        private static bool TakeToken(Dictionary<int, RateBucket> d, int player, float rate, float burst)
        {
            float now = Time.realtimeSinceStartup;
            if (!d.TryGetValue(player, out var b)) b = new RateBucket { Tokens = burst, At = now };
            b.Tokens = Mathf.Min(burst, b.Tokens + (now - b.At) * rate); b.At = now;
            bool ok = b.Tokens >= 1f;
            if (ok) b.Tokens -= 1f;
            d[player] = b;
            return ok;
        }

        /// One refusal: its own counter and the anomaly count; the first 5 of each reason logged as [anomaly] lines.
        internal static void Refuse(ref long counter, string reason, string what)
        {
            counter++;
            EditRefusedLast = what;
            _refusalsLogged.TryGetValue(reason, out int n);
            _refusalsLogged[reason] = n + 1;
            Entities.RecordEditRefusal(what, n < 5);
        }

        internal static bool Sane(float v) => Entities.IsFinite(v) && Math.Abs(v) <= 1e5f;

        internal static bool SaneColor(save_color c) => c == null || (Sane(c.r) && Sane(c.g) && Sane(c.b) && Sane(c.a) && Math.Abs(c.r) <= 100 && Math.Abs(c.g) <= 100 && Math.Abs(c.b) <= 100 && Math.Abs(c.a) <= 100);

        internal static bool Ind(int i) => i >= 0 && i < 1024;

        /// Server: a building usable change — from a player at that building, within the rate, with sane values.
        internal static bool CheckPoiUsable(int from, Entities.PoiKey k, Entities.PoiState st)
        {
            if (from == 0) return true;
            if (!TakeToken(_poiBuckets, from, PoiRatePerS, PoiBurst)) { Refuse(ref PoiRefusedRate, "poi rate", $"building usable from player {from}: over {PoiRatePerS:F0} a second"); return false; }
            if (k.Gen < 0 || k.Gen > 255 || k.U < 0 || k.U > 4096 || !Entities.IsFinite(k.X) || !Entities.IsFinite(k.Y) || !Entities.IsFinite(k.Z) ||
                !Sane(st.XRot) || !Sane(st.YRot) || !Sane(st.Slide) || !Sane(st.Tuned) || st.Turn < -1 || st.Turn > 1024)
            { Refuse(ref PoiRefusedValues, "poi value", $"building usable from player {from}: values out of range (gen {k.Gen} u {k.U})"); return false; }
            if (!Entities.TryEditPeerPosition(from, out var p)) { Refuse(ref PoiRefusedReach, "poi reach", $"building usable from player {from}: their position isn't known"); return false; }
            double dx = p.x - k.X, dz = p.z - k.Z;
            if (dx * dx + dz * dz > (double)PoiReachM * PoiReachM)
            {
                Refuse(ref PoiRefusedReach, "poi reach", $"building usable from player {from}: {Math.Sqrt(dx * dx + dz * dz):F0} m from the building (gen {k.Gen} at {k.X:F0},{k.Z:F0})");
                return false;
            }
            return true;
        }

        internal static string EditCheckJson() =>
            "{\"refused\":{\"outOfReach\":" + Entities.EditReachRefusals + ",\"rate\":" + EditRefusedRate + ",\"kinds\":" + EditRefusedKinds +
            ",\"values\":" + EditRefusedValues + ",\"poiReach\":" + PoiRefusedReach + ",\"poiRate\":" + PoiRefusedRate + ",\"poiValues\":" + PoiRefusedValues + "},\"last\":" + Json.Str(EditRefusedLast) +
            ",\"anomalies\":" + Entities.EditAnomalyCount + "}";
        internal static bool TakeEditToken(int player) => TakeToken(_editBuckets, player, EditRatePerS, EditBurst);
        internal static void ForgetEditRate(int player) => _editBuckets.Remove(player);
        internal static void ForgetPoiRate(int player) => _poiBuckets.Remove(player);
        internal static void ResetRates() { _editBuckets.Clear(); _poiBuckets.Clear(); _refusalsLogged.Clear(); }
    }

    public static partial class Entities
    {
        public const int KindsKnown = KindUsable | KindTank | KindPart | KindColor | KindFood | KindAmmo | KindPlate | KindBusDoor | KindBlood;
        private static readonly Dictionary<uint, KeyValuePair<byte[], itemDataClass>> _groupRecords = new Dictionary<uint, KeyValuePair<byte[], itemDataClass>>();
        private static readonly int FluidKinds = Enum.GetValues(typeof(mainscript.fluidenum)).Length;

        private static void EditCheckForget(int player) { EntityEditCheck.ForgetEditRate(player); _liveServerIn.Remove(player); EntityEditCheck.ForgetPoiRate(player); }
        private static void EditCheckReset() { EntityEditCheck.ResetRates(); _groupRecords.Clear(); }

        /// The object's record as shared (decoded once per object and record).
        private static itemDataClass GroupRecord(SEnt se)
        {
            if (se.Record == null) return null;
            if (_groupRecords.TryGetValue(se.NetId, out var c) && ReferenceEquals(c.Key, se.Record)) return c.Value;
            if (_groupRecords.Count > 512) _groupRecords.Clear();
            var d = RecordCodec.Decode(se.Record);
            _groupRecords[se.NetId] = new KeyValuePair<byte[], itemDataClass>(se.Record, d);
            return d;
        }

        /// The owner's latest state of group member idx: its last resync record, else its part of the shared record
        /// (an item unchanged since the share never resyncs). Group members are in the record's items in group order.
        private static itemDataClass Baseline(SEnt se, int idx)
        {
            if (se.ItemState.TryGetValue(idx, out var b)) return RecordCodec.Decode(b);
            var g = GroupRecord(se);
            if (g == null || idx >= g.items.Count || g.items[idx] == null) return null;
            uint id = g.items[idx].id;
            var o = new itemDataClass();
            foreach (var f in ItemSnapshot.ListFields)
            {
                if (!(f.GetValue(g) is IList src) || !(f.GetValue(o) is IList dst)) continue;
                foreach (var x in src)
                {
                    var idf = x != null ? EntityLifetimeStreaming._idField(x) : null;
                    if (idf != null && idf.FieldType == typeof(uint) && (uint)idf.GetValue(x) == id) dst.Add(x);
                }
            }
            return o;
        }

        /// Litres tank `ind` of group member idx holds: the host's copy if it has one placed, else the item's prefab.
        private static float TankCapacity(SEnt se, int idx, int ind)
        {
            if (ByNet.TryGetValue(se.NetId, out var e) && e.Items != null && idx < e.Items.Count && e.Items[idx] != null)
            {
                var c = MaxC(e.Items[idx], ind);
                if (c > 0) return c;
            }
            var g = GroupRecord(se);
            if (g != null && idx < g.items.Count && g.items[idx] != null && itemdatabase.s != null)
            {
                var go = itemdatabase.s.Item(g.items[idx].prefabID);
                var c = go != null ? MaxC(go.GetComponent<tosaveitemscript>(), ind) : 0f;
                if (c > 0) return c;
            }
            return EntityEditCheck.UnknownTankL;
        }

        private static float MaxC(tosaveitemscript it, int ind)
        {
            if (it == null || it.tanks == null || ind < 0 || ind >= it.tanks.Count || it.tanks[ind] == null || it.tanks[ind].F == null) return 0f;
            return it.tanks[ind].F.maxC;
        }

        /// Server: is this edit's record something its kinds may do? null = yes, else why not.
        private static string CheckTankAdds(SEnt se, int idx, List<TankAdds.Add> adds)
        {
            foreach (var a in adds)
            {
                if (a.Type >= FluidKinds) return "fluid type " + a.Type;
                float cap = TankCapacity(se, idx, a.Tank);
                if (System.Math.Abs(a.Litres) > cap * 1.01f + 0.01f) return $"tank change: {a.Litres:F2} of {cap:F2} L";
            }
            return null;
        }

        private static string CheckEdit(SEnt se, int idx, int kinds, byte[] rec)
        {
            if (kinds == 0 || (kinds & ~KindsKnown) != 0) return "kinds " + kinds;   // KindAll: the car's controls and its attachments — never another player's edit
            if (idx > 4096) return "index " + idx;
            var d = RecordCodec.Decode(rec);
            if (d == null) return "record unreadable";
            bool K(int bit) => (kinds & bit) != 0;
            if (K(KindUsable))
            {
                foreach (var u in d.usable)
                    if (u == null || !EntityEditCheck.Ind(u.ind) || !EntityEditCheck.Sane(u.xRot) || !EntityEditCheck.Sane(u.yRot) || !EntityEditCheck.Sane(u.slideValue) || !EntityEditCheck.Sane(u.tunedFloat) || u.currentTurnState < -1 || u.currentTurnState > 1024) return "usable value";
                foreach (var x in d.door_rots) if (x == null || !EntityEditCheck.Ind(x.ind) || !EntityEditCheck.Sane(x.state)) return "door value";
                foreach (var x in d.ginlamp) if (x == null || !EntityEditCheck.Sane(x.value)) return "lamp value";
            }
            if (K(KindTank))
                foreach (var t in d.tanks)
                {
                    if (t == null || !EntityEditCheck.Ind(t.ind)) return "tank";
                    int n = t.amount != null ? t.amount.Length : 0;
                    if ((t.type != null ? t.type.Length : 0) != n || n > 16) return "tank fluids";
                    float sum = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        if (!EntityNumbers.Finite(t.amount[i]) || t.amount[i] < 0f) return "tank amount " + t.amount[i];
                        if (t.type[i] < 0 || t.type[i] >= FluidKinds) return "fluid type " + t.type[i];
                        sum += t.amount[i];
                    }
                    float cap = TankCapacity(se, idx, t.ind);
                    if (sum > cap * 1.01f + 0.01f) return $"tank over capacity: {sum:F2} of {cap:F2} L";
                }
            if (K(KindPart | KindColor))
            {
                foreach (var p in d.partconditions)
                    if (p == null || !EntityEditCheck.Ind(p.ind) || p.state < 0 || p.state > 16 || p.state2 < 0 || p.state2 > 4096 || p.state3 < 0 || p.state3 > 4096 ||
                        p.slectedRandomTipus < 0 || p.slectedRandomTipus > 256 || !EntityEditCheck.SaneColor(p.color)) return "part value";
                // a paint job carries the part conditions too (one list): it may change their colour, never their condition
                if (!K(KindPart) && d.partconditions.Count > 0)
                {
                    var b = Baseline(se, idx);
                    if (b == null) return "colour edit, the item's part conditions unknown";
                    foreach (var p in d.partconditions)
                    {
                        var q = b.partconditions.Find(x => x != null && x.ind == p.ind);
                        if (q == null || q.state != p.state || q.state2 != p.state2 || q.state3 != p.state3 || q.slectedRandomTipus != p.slectedRandomTipus)
                            return "colour edit changes part " + p.ind + "'s condition";
                    }
                }
            }
            if (K(KindColor))
            {
                foreach (var c in d.colors) if (c == null || !EntityEditCheck.Ind(c.ind) || !EntityEditCheck.SaneColor(c)) return "colour value";
                foreach (var p in d.painting) if (p == null || p.index < 0 || p.index > 4096) return "painting";
            }
            if (K(KindFood)) foreach (var f in d.food) if (f == null || !EntityEditCheck.Sane(f.hp) || f.hp < 0f || f.index < 0 || f.index > 4096) return "food value";
            if (K(KindAmmo)) foreach (var a in d.ammo) if (a == null || a.count < 0 || a.count > 10000 || !EntityEditCheck.Sane(a.fammo) || a.fammo < 0f) return "ammo value";
            if (K(KindPlate))
                foreach (var p in d.rendszam)
                {
                    if (p == null || (p.S != null && p.S.Length > 32)) return "plate";
                    if (p.S != null) foreach (char ch in p.S) if (ch < ' ') return "plate";
                }
            if (K(KindBusDoor))
                foreach (var b in d.busdoorscript)
                    if (b == null || (b.Bopened != null && b.Bopened.Length > 64) || (b.Bsigned != null && b.Bsigned.Length > 64)) return "bus doors";
            // a wash (Entities.Blood): the car record, of which the owner takes "clean" only — one record, and clean
            if (K(KindBlood) && (d.car.Count != 1 || d.car[0] == null || d.car[0].blood)) return "blood: an edit only washes it off";
            return null;
        }

    }
}
