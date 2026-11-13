using static TLDRevamp.Net.EntityHealthRules;
using static TLDRevamp.Net.EntityPartWatch;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    /// Item health's checks, run by the machine itself (HealthWatchRules): the shared objects within HealthWatchRangeM,
    /// a few each HealthWatchEveryS, against what this machine keeps on them — the copy's place against its owner's poses,
    /// the body (colliders, the held layer, collisions, kinematic), and what can never be right: a position or speed that
    /// isn't a number or is beyond anything the game does, an object under the ground with nothing below it, a slot
    /// holding the same part twice or a part that names another slot. Every shared object: an owner who has left the
    /// session. A problem that lasts (HealthWatchRules.AfterS) is an event (health-problem), a log line, and at most once
    /// per AutoMarkEveryS an automatic mark. The tabs of Debugging V2 show it until it clears.
    /// Fitted parts you can't reach: Entities.PartWatch. Loose items you can't pick up: Entities.ItemWatch. A copy
    /// whose parts or state differ from its owner's: the census (Entities.Repair) — a repair that does not take is
    /// marked here too.
    internal static class EntityHealthWatch
    {
        public static bool HealthWatch = true;    // A/B: false = no background checks of shared objects
        public static bool AutoMarks = true;      // false: events only, no automatic marks (every watch)
        public static float HealthWatchEveryS = 0.5f, HealthWatchRangeM = 60f, AutoMarkEveryS = 30f;
        public static int HealthWatchPerTick = 3, HealthWatchFarPerTick = 12;
        public static long HealthWatchFlags, AutoMarksSaved;
        public static string HealthWatchLast = "";

        private static readonly List<uint> _hwCycle = new List<uint>(), _hwFar = new List<uint>();
        internal static System.Collections.Generic.IReadOnlyList<uint> ObservedHwFar => _hwFar;
        internal static System.Collections.Generic.IReadOnlyList<uint> ObservedHwCycle => _hwCycle;
        private static int _hwCursor, _hwFarCursor;
        private static float _hwAt = -99f, _autoMarkAt = -999f;
        private static readonly HealthWatchRules.Persist _hwSeen = new HealthWatchRules.Persist(), _hwOwnerSeen = new HealthWatchRules.Persist();
        private static readonly List<HealthCheck> _hwChecks = new List<HealthCheck>();
        private static readonly HashSet<string> _hwNow = new HashSet<string>(StringComparer.Ordinal), _hwOwnerNow = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<int> _hwPlayers = new HashSet<int>();
        private static readonly Dictionary<uint, string> _hwActive = new Dictionary<uint, string>();
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, string> ObservedHwActive => _hwActive;
        private static readonly Dictionary<uint, int> _repairFails = new Dictionary<uint, int>();
        private static readonly HashSet<uint> _hwAbove = new HashSet<uint>();   // seen above the ground this session

        internal static void HealthWatchTick()
        {
            if (!HealthWatch || ObservedEntities.Count == 0) return;
            AutoMarkWaitingTick();
            float now = Time.realtimeSinceStartup;
            if (now - _hwAt < HealthWatchEveryS) return;
            _hwAt = now;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (_hwCursor >= _hwCycle.Count) HealthWatchCycle(pl, now);
            for (int n = 0; n < HealthWatchPerTick && _hwCursor < _hwCycle.Count;)
            {
                uint net = _hwCycle[_hwCursor++];
                if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null || !e.Root.gameObject.activeInHierarchy) continue;
                HealthWatchOne(e, now, true); n++;
            }
            // the loaded objects farther away: only what can never be right (a flung object leaves the range at once)
            if (_hwFarCursor >= _hwFar.Count) { _hwFar.Clear(); _hwFarCursor = 0; foreach (var e in ObservedPlaced) if (!_hwCycle.Contains(e.NetId)) _hwFar.Add(e.NetId); }
            for (int n = 0; n < HealthWatchFarPerTick && _hwFarCursor < _hwFar.Count;)
            {
                uint net = _hwFar[_hwFarCursor++];
                if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null || !e.Root.gameObject.activeInHierarchy) continue;
                HealthWatchOne(e, now, false); n++;
            }
        }

        /// A new round: the objects near the player, and every shared object's owner against who is in the session.
        private static void HealthWatchCycle(fpscontroller pl, float now)
        {
            _hwCycle.Clear(); _hwCursor = 0;
            _hwOwnerNow.Clear();
            var at = pl != null ? pl.transform.position : Vector3.zero;
            foreach (var kv in ObservedEntities)
            {
                var e = kv.Value;
                if (e.OwnerId > 0 && e.OwnerId != MyId && OwnerGone(e.OwnerId)) _hwOwnerNow.Add(kv.Key + ":" + HealthWatchRules.OwnerLeft);
                if (pl != null && e.Root != null && (e.Root.transform.position - at).sqrMagnitude <= HealthWatchRangeM * HealthWatchRangeM) _hwCycle.Add(kv.Key);
            }
            foreach (var key in _hwOwnerSeen.Pass("", _hwOwnerNow, now, _ => HealthWatchRules.OwnerLeftAfterS))
            {
                uint net = uint.Parse(key.Substring(0, key.IndexOf(':')), CultureInfo.InvariantCulture);
                if (!ObservedEntities.TryGetValue(net, out var e)) continue;
                HealthWatchFound(e, HealthWatchRules.OwnerLeft, "Owner left", "Owned by player " + e.OwnerId + ", who is no longer in the session; nobody simulates it, so it hangs where it was on every screen.");
            }
        }

        /// `near`: every check; else only the sanity checks (CheckSanity, without its slots).
        private static void HealthWatchOne(Ent e, float now, bool near)
        {
            _hwChecks.Clear();
            void Add(int level, string title, string why, string how = null, string key = null) => _hwChecks.Add(new HealthCheck { Level = level, Title = title, Why = why, How = how, Key = key });
            if (near) { CheckPosition(e, null, Add); CheckBody(e, null, Add); CheckPickup(e, null, Add); }
            CheckSanity(e, Add, near);
            _hwNow.Clear();
            string scope = e.NetId + ":";
            foreach (var c in _hwChecks) if (c.Level == 2 && c.Key != null && c.Key != HealthWatchRules.OwnerLeft) _hwNow.Add(scope + c.Key);
            foreach (var key in _hwSeen.Pass(scope, _hwNow, now, HealthWatchRules.AfterOf))
                foreach (var c in _hwChecks)
                    if (scope + c.Key == key) { HealthWatchFound(e, c.Key, c.Title, c.Why); break; }
            // the tab's line: the problems reported and still there
            string line = null;
            foreach (var c in _hwChecks)
                if (c.Key != null && _hwSeen.Active(scope + c.Key)) line = (line == null ? "health: " : line + ", ") + c.Title.ToLowerInvariant();
            if (line != null) _hwActive[e.NetId] = line; else _hwActive.Remove(e.NetId);
        }

        private static void HealthWatchFound(Ent e, string key, string title, string why)
        {
            HealthWatchFlags++;
            string name = e.Root != null ? PrefabName(e.Root.id) : "net " + e.NetId;
            HealthWatchLast = name + " net " + e.NetId + ": " + title + " — " + (why.Length > 240 ? why.Substring(0, 240) + "…" : why);
            if (key == HealthWatchRules.OwnerLeft) _hwActive[e.NetId] = "health: owner left";
            SessionLog.Event("health-problem", e.NetId, from: e.OwnerId, epoch: e.Epoch, result: key);
            Plugin.Log.LogWarning("health watch: " + HealthWatchLast);
            AutoMark("auto: " + HealthWatchLast, e.NetId);
        }

        /// One automatic mark (every watch shares it): the state of this machine and, through the host, the others'.
        /// Once per object and problem in a session, and AutoMarkMax a session: a problem that comes and goes (a magazine
        /// under the ground, a glovebox's copy off its owner's) was due again each time it came back. The DevTest session
        /// of 2026-11-10: 62 marks in 25 minutes, 187 MB (each 1.5-4.6 MB, the other players' copies with them, and the
        /// comparison page written again each time), a hitch each.
        public static int AutoMarkMax = 10;
        public static long AutoMarksSkipped;
        private static readonly HashSet<string> _autoMarked = new HashSet<string>(StringComparer.Ordinal);
        private static int _autoMarksThisSession;
        internal static void ResetAutoMarks() { _autoMarked.Clear(); _autoMarksThisSession = 0; _autoWaiting.Clear(); _hwAbove.Clear(); }
        /// Finds that came within AutoMarkEveryS of the last mark: marked when the gap is over (they were dropped, and a
        /// false alarm just before could crowd out a real problem).
        private static readonly List<KeyValuePair<string, uint>> _autoWaiting = new List<KeyValuePair<string, uint>>();
        /// `note`: "auto: <subject> — <details>": the subject (what and which problem, "Magazin net 9: Under the ground")
        /// is marked once a session; `net`: the object it is about, recorded with the mark.
        internal static bool AutoMark(string note, uint net = 0)
        {
            if (!AutoMarks) return false;
            float now = Time.realtimeSinceStartup;
            string subject = TelemetryMarks.AutoSubject(note) ?? note;
            if (_autoMarksThisSession >= AutoMarkMax || _autoMarked.Contains(subject)) { AutoMarksSkipped++; return false; }
            if (now - _autoMarkAt < AutoMarkEveryS)
            {
                if (_autoWaiting.Count < 4 && !_autoWaiting.Exists(w => TelemetryMarks.AutoSubject(w.Key) == subject)) _autoWaiting.Add(new KeyValuePair<string, uint>(note, net));
                return false;
            }
            _autoMarked.Add(subject);
            _autoMarksThisSession++;
            _autoMarkAt = now;
            string res = TelemetryMarks.Local(note, net);
            if (!res.Contains("\"ok\"")) return false;
            AutoMarksSaved++; return true;
        }
        private static void AutoMarkWaitingTick()
        {
            if (_autoWaiting.Count == 0 || Time.realtimeSinceStartup - _autoMarkAt < AutoMarkEveryS) return;
            var w = _autoWaiting[0]; _autoWaiting.RemoveAt(0);
            AutoMark(w.Key, w.Value);
        }

        /// Debugging V2's tab: the problems the watch reported on this object and that are still there, or null.
        internal static string HealthWatchLine(uint net) => _hwActive.TryGetValue(net, out var s) ? s : null;

        /// The census (Entities.Repair) asked the owner, got the parts or state again, and the copy still differs.
        internal static void RepairFailedNote(Ent e, uint net, string diff)
        {
            _repairFails.TryGetValue(net, out int n); _repairFails[net] = ++n;
            string name = e != null && e.Root != null ? PrefabName(e.Root.id) : "net " + net;
            if (n == 1) { SessionLog.Event("repair-failed", net, result: diff.Length > 120 ? diff.Substring(0, 120) : diff); Plugin.Log.LogWarning("repair did not take: " + name + " net " + net + " — " + diff); }
            if (n == 3 && e != null) HealthWatchFound(e, HealthWatchRules.RepairLoop, "Repair keeps failing", "The census found this copy different from its owner's three times and the owner's resend did not fix it: " + diff);
        }

        /// What can never be right, whatever the mod does with the object.
        internal static void CheckSanity(Ent e, CheckAdd Add, bool slots = true)
        {
            var p = e.Root.transform.position;
            if (!HealthWatchRules.Finite(p.x, p.y, p.z))
            { Add(2, "Position not a number", "Its position is " + p + ": physics blew up on it (a collision inside another body, a joint to something destroyed).", null, HealthWatchRules.NonFinite); return; }
            var rb = e.Root.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                var v = rb.velocity;
                if (HealthWatchRules.IsRunaway(v.x, v.y, v.z))
                    Add(2, "Runaway speed", "Moving at " + EntityHealthRules.N(v.magnitude) + " m/s (" + EntityHealthRules.N(v.magnitude * 3.6) + " km/h): beyond anything the game does; physics blew up on it.", null, HealthWatchRules.Runaway);
            }
            double ground = GroundOver(p);
            if (!double.IsNaN(ground) && p.y < ground - HealthWatchRules.UnderGroundM && HealthWatchRules.IsUnderGround(p.y, ground, FloorBelow(e, p)))
            {
                // only a fall seen here: above the ground before this session, or falling now. Objects a save already holds
                // deep down (an earlier fall-through, saved) were marked again in every session, at the same depths
                // (DevTest 11-10: Attachable07 207 m, Drotkefe 200 m, BeerGib01 229 m: 56 of 161 automatic marks)
                bool falling = rb != null && !rb.isKinematic && rb.velocity.y < -1f;
                if (_hwAbove.Contains(e.NetId) || falling)
                    Add(2, "Under the ground", EntityHealthRules.N(ground - p.y) + " m under the ground's surface with nothing below it: it fell through the world.", null, HealthWatchRules.UnderGround);
                else Add(1, "Under the ground (from before)", EntityHealthRules.N(ground - p.y) + " m under the ground's surface with nothing below it, already when first seen this session (a save's leftover).");
            }
            else if (!double.IsNaN(ground)) _hwAbove.Add(e.NetId);
            if (e.OwnerId > 0 && e.OwnerId != MyId && OwnerGone(e.OwnerId))
                Add(2, "Owner left", "Owned by player " + e.OwnerId + ", who is no longer in the session.", null, HealthWatchRules.OwnerLeft);
            // slots: the same part twice, or a part that names another slot
            var bad = new List<string>();
            if (slots && e.Items != null)
                foreach (var it in e.Items)
                {
                    if (it == null || it.partslotscripts == null) continue;
                    foreach (var ps in it.partslotscripts)
                    {
                        if (ps == null || !ps.hasPart()) continue;
                        for (int i = 0; i < ps.parts.Count; i++)
                        {
                            var part = ps.parts[i]; if (part == null) continue;
                            if (part.slot != ps) bad.Add(PrefabName(part.tosave != null ? part.tosave.id : -1) + " is in slot " + ps.name + " but names " + (part.slot != null ? part.slot.name : "no slot"));
                            for (int j = i + 1; j < ps.parts.Count; j++)
                                if (ps.parts[j] == part || !ps.attachTarget && ps.parts[j] != null && part.tosave != null && ps.parts[j].tosave != null && ps.parts[j].tosave.id == part.tosave.id)
                                    bad.Add(PrefabName(part.tosave != null ? part.tosave.id : -1) + " twice in slot " + ps.name);
                        }
                    }
                }
            if (bad.Count > 0) Add(2, "Slot mismatch", string.Join("; ", bad.GetRange(0, Math.Min(3, bad.Count))) + (bad.Count > 3 ? "; " + (bad.Count - 3) + " more" : "") + ". The part and its slot disagree: taking it off or fitting another goes wrong.", null, HealthWatchRules.SlotMismatch);
        }

        private static float _hwPlayersAt = -99f;
        /// Not among the session's players (asked at most once a second; false outside a session).
        private static bool OwnerGone(int id)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _hwPlayersAt > 1f) { _hwPlayersAt = now; _hwPlayers.Clear(); foreach (var p in Session.Players()) _hwPlayers.Add(p.Id); }
            return _hwPlayers.Count > 0 && !_hwPlayers.Contains(id);
        }

        /// Anything under the object (not itself) within 300 m to stand on.
        private static bool FloorBelow(Ent e, Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (h.collider != null && !h.collider.transform.IsChildOf(e.Root.transform)) return true;
            return false;
        }

        internal static void ResetHealthObservations() { _hwSeen.Clear(); _hwOwnerSeen.Clear(); _hwActive.Clear(); _autoMarkAt = -999f; }
        private static int _groundMask = -1;

        /// The ground's surface over a point (NaN: no ground there, or not loaded).
        internal static double GroundOver(Vector3 p)
        {
            if (_groundMask < 0) _groundMask = LayerMask.GetMask(GroundLayers.Split(','));
            if (_groundMask == 0) return double.NaN;
            return Physics.Raycast(new Vector3(p.x, p.y + 1000f, p.z), Vector3.down, out var hit, 1000f, _groundMask, QueryTriggerInteraction.Ignore) ? hit.point.y : double.NaN;
        }

    }
}
