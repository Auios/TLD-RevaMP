using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// Sandstorms in a session (the storms themselves: Fixes.Sandstorms). A storm is a local object (sandstormscript):
    /// it drifts, grows near players, darkens sky and fog for a player inside it, and its wind pushes loose bodies about.
    /// Each machine ran its own, so one player stood in a storm the other could not see, and its wind moved things on
    /// one machine only. Now the storms are the host's: its spawner alone runs (around any player) and a storm fades by
    /// the player nearest it (Fixes.Sandstorms); the host sends every storm (place, size, direction, speed, strength)
    /// when one comes up or goes and every StormResendS. A client makes, moves and removes its storms to match; how it
    /// looks and blows near a client is the game's own.
    internal static class EntityWeather
    {
        private static readonly NetWriter W = new NetWriter();
        public static float StormResendS = 2f;
        public static long StormSyncsSent, StormSyncsApplied, StormsMade, StormsRemoved, StormSpawnsSkipped, StormSnaps;

        private static readonly Dictionary<sandstormscript, uint> StormIds = new Dictionary<sandstormscript, uint>();
        internal static System.Collections.Generic.IReadOnlyDictionary<sandstormscript, uint> ObservedStormIds => StormIds;
        private static readonly Dictionary<uint, sandstormscript> StormById = new Dictionary<uint, sandstormscript>();
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, sandstormscript> ObservedStormById => StormById;
        private static uint _stormNext = 1;
        private static float _stormAcc;
        private static int _stormLastCount = -1;

        // ---- the host
        internal static void StormTick(float dt)
        {
            if (napszakvaltakozas.s == null) return;
            var list = napszakvaltakozas.s.sandStorms;
            if (Entities.IsHost)
            {
                bool changed = list.Count != _stormLastCount;
                foreach (var st in list) if (st != null && !StormIds.ContainsKey(st)) { StormIds[st] = _stormNext++; changed = true; }
                _stormAcc += dt;
                if (!changed && _stormAcc < StormResendS) return;
                _stormAcc = 0f; _stormLastCount = list.Count;
                PruneStormIds();
                W.Reset(); W.U8(Entities.StormSync);
                int n = 0; foreach (var st in list) if (st != null) n++;
                W.U8((byte)Mathf.Min(n, 255));
                int k = 0;
                foreach (var st in list)
                {
                    if (st == null || k++ >= 255) continue;
                    var g = mainscript.GlobalFromUnityPos(st.transform.position);
                    W.U32(StormIds[st]); W.U8((byte)st.prefabid);
                    W.F64(g.x); W.F64(g.z); W.F32(st.transform.eulerAngles.y);
                    W.F32(st.size); W.F32(st.xdir); W.F32(st.ydir); W.F32(st.speed); W.F32(st.life);
                }
                Entities.SendToPeers(W, true, 0);
                StormSyncsSent++;
            }
        }

        private static readonly List<sandstormscript> _stormGone = new List<sandstormscript>();
        private static void PruneStormIds()
        {
            _stormGone.Clear();
            foreach (var kv in StormIds) if (kv.Key == null) _stormGone.Add(kv.Key);
            foreach (var st in _stormGone) StormIds.Remove(st);
            Fixes.Sandstorms.Prune();
        }

        // ---- a client
        internal static void ApplyStorms(NetReader r)
        {
            int n = r.U8();
            var seen = new HashSet<uint>();
            var rows = new List<(uint id, int pf, double x, double z, float yaw, float size, float xd, float yd, float sp, float life)>();
            for (int i = 0; i < n; i++)
                rows.Add((r.U32(), r.U8(), r.F64(), r.F64(), r.F32(), r.F32(), r.F32(), r.F32(), r.F32(), r.F32()));
            if (r.Bad || Entities.IsHost || napszakvaltakozas.s == null || mainscript.s == null || itemdatabase.s == null) return;
            foreach (var row in rows)
            {
                if (!Entities.IsFinite(row.x) || !Entities.IsFinite(row.z) || !Entities.IsFinite(row.yaw) || !Entities.IsFinite(row.size) || !Entities.IsFinite(row.xd) || !Entities.IsFinite(row.yd) || !Entities.IsFinite(row.sp) || !Entities.IsFinite(row.life)) continue;
                seen.Add(row.id);
                StormById.TryGetValue(row.id, out var st);
                var pos = mainscript.UnityPosFromGlobal(new Vector3d(row.x, mainscript.GlobalFromUnityPos(Vector3.zero).y, row.z));
                pos.y = (float)mainscript.s.visszarakva.y;
                if (st == null)
                {
                    if (row.pf >= itemdatabase.s.storms.Length || itemdatabase.s.storms[row.pf] == null) continue;
                    var go = Object.Instantiate(itemdatabase.s.storms[row.pf], pos, Quaternion.Euler(0f, row.yaw, 0f));
                    st = go.GetComponent<sandstormscript>();
                    if (st == null) { Object.Destroy(go); continue; }
                    st.started = true;   // the host's size, direction, speed - not a new roll (Start: FStart only when not started)
                    StormById[row.id] = st;
                    StormsMade++;
                }
                st.size = row.size; st.xdir = row.xd; st.ydir = row.yd; st.speed = row.sp; st.life = row.life;
                var off = st.transform.position - pos; off.y = 0f;
                if (off.sqrMagnitude > 0.01f)
                {
                    if (off.sqrMagnitude > 25f) { st.transform.position = pos; StormSnaps++; }   // > 5 m: put it there
                    else st.transform.position = Vector3.Lerp(st.transform.position, pos, 0.5f);
                }
            }
            // storms the host no longer has, and this machine's own (loaded with its save before joining)
            _stormGone.Clear();
            foreach (var st in napszakvaltakozas.s.sandStorms)
            {
                if (st == null) continue;
                bool known = false;
                foreach (var kv in StormById) if (kv.Value == st) { known = seen.Contains(kv.Key); break; }
                if (!known) _stormGone.Add(st);
            }
            foreach (var st in _stormGone) { Object.Destroy(st.gameObject); StormsRemoved++; }
            var dead = new List<uint>();
            foreach (var kv in StormById) if (kv.Value == null || !seen.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) StormById.Remove(id);
            StormSyncsApplied++;
        }

        internal static void StormReset()
        {
            StormIds.Clear(); StormById.Clear(); _stormLastCount = -1; _stormAcc = 0f;
        }
    }
}
