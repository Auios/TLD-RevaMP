using UnityEngine;

namespace TLDRevamp
{
    /// Ground placement and settling used by joins, teleports and respawns.
    internal static class GroundPlacement
    {
        /// The first STATIC collider under (x, z) that isn't the player (Unity coordinates); null if nothing is generated there.
        internal static RaycastHit? GroundHit(float x, float z)
        {
            Transform playerRoot = mainscript.s != null && mainscript.s.player != null ? mainscript.s.player.transform.root : null;
            var hits = Physics.RaycastAll(new Vector3(x, 5000f, z), Vector3.down, 10000f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (playerRoot != null && h.collider.transform.root == playerRoot) continue; // the player, held items
                if (h.collider.attachedRigidbody != null) continue;                           // cars, loose items
                return h;
            }
            return null;
        }

        // ---------------------------------------------------------------- tpground
        // It used to take the TOPMOST static surface (roofs: 'teto'; something high by the mansion gate) and, where the
        // ground wasn't generated yet, threw the player 120 m up to fall onto it (16 of 37 printed replies were 'hop';
        // aimp runs 5/6 lost the laptop to that fall before the test began). Now: the lowest surface a player can stand
        // on (room above it) at or above the terrain — a road over terrain, a floor over the ground under a house, never
        // the roof; with a height given, the floor nearest below it. Ground not generated yet: the player is HELD at the
        // spot (kinematic, no fall) and set down once the ground is there (FixedTick), up to 30 s.
        private static double _tpGx, _tpGz; private static double? _tpGy;
        private static float _tpUntil, _tpReadySince = -1; private static bool _tpWaiting;
        private static string _tpOn = ""; private static long _tpPlaced, _tpWaited;

        internal static RaycastHit? StandHit(float x, float z, float? belowY, bool vehicles = false) =>
            Net.TpaPlacement.StandHit(x, z, belowY, vehicles);
        /// The floor under (x, z) below belowY, with or without room for a player on it (is someone standing on something).
        internal static RaycastHit? FloorHit(float x, float z, float belowY) => Net.TpaPlacement.StandHit(x, z, belowY, false, false);
        internal static bool Room(Vector3 p, Transform playerRoot) => Net.TpaPlacement.Room(p, playerRoot);

        /// Production use too (a joiner placed next to the host, Mp): stand on the ground at a global (x, z), held until
        /// it is generated there. TpBusy while held.
        internal static void PlaceAt(double gx, double gz) { TpGround(gx, gz, null); }
        /// Next to someone standing at global height `nearGy` (a building's floor, a roof, a bridge): the floor nearest
        /// below that height, else the ground. The lowest surface with room was the wrong floor: beside the host in the
        /// spawn house, its roof (no room on the kitchen floor at that spot), and under a raised floor the ground beneath
        /// it (playtest 2026-10-18: "tpa sending him under the floor").
        internal static void PlaceNear(double gx, double gz, double nearGy) { TpGround(gx, gz, null, nearGy); }
        // Give the target's floor a short generation window before the ordinary placement fallback.
        internal static void PlaceTpa(double gx, double gz, double bodyGy, bool retry = false)
        {
            TpGround(gx, gz, null, bodyGy + .5, true, retry);
        }
        private static bool _tpStrict;
        private static float _tpReadyAt;
        internal static bool TpaWorldReady(double gx, double gy, double gz)
        {
            var u = mainscript.UnityPosFromGlobal(new Vector3d(gx, gy, gz));
            if (Time.realtimeSinceStartup < _tpReadyAt || u.sqrMagnitude >= 6250000f) return false;
            foreach (var o in TpAround)
            {
                var hit = StandHit(u.x + o.x, u.z + o.y, null);
                if (hit.HasValue && Net.TpaArrival.WorldFloor(new Net.TpaArrival.Candidate(hit.Value.point.y, true, hit.Value.collider.attachedRigidbody == null))) return true;
            }
            return false;
        }
        internal static void Hold(fpscontroller player) { _tpHeld = true; TpSettle(player); }
        internal static void Release() { _tpHeld = false; _tpCalmUntil = 0; }
        internal static bool TpBusy => _tpWaiting;
        internal static void CancelJoinPlacement() { _tpWaiting = _tpHeld = false; _tpCalmUntil = 0; }
        private static double? _tpNearGy;
        /// The spot, then rings of 1, 2 and 3 m around it (8 directions each).
        private static readonly Vector2[] TpAround = Ring();
        private static Vector2[] Ring()
        {
            var l = new System.Collections.Generic.List<Vector2> { Vector2.zero };
            foreach (float r in new[] { 1f, 2f, 3f })
                for (int i = 0; i < 8; i++) l.Add(new Vector2(Mathf.Cos(i * Mathf.PI / 4f), Mathf.Sin(i * Mathf.PI / 4f)) * r);
            return l.ToArray();
        }

        internal static string TpGround(double gx, double gz, double? gy, double? nearGy = null, bool strict = false, bool retry = false)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"not in game\"}";
            _tpGx = gx; _tpGz = gz; _tpGy = gy; _tpNearGy = nearGy;
            _tpStrict = strict;
            if (!retry) { _tpReadySince = -1; _tpReadyAt = Time.realtimeSinceStartup + .5f; }
            _tpWaiting = false;
            if ((!strict || retry) && TryPlace(pl)) return "{\"tpground\":true,\"on\":" + Json.Str(_tpOn) + "}";
            // not generated yet: hold the player at the spot (the world streams in around it) — no hop, no fall
            var u = mainscript.UnityPosFromGlobal(new Vector3d(gx, gy ?? nearGy ?? mainscript.GlobalFromUnityPos(pl.transform.position).y, gz));
            pl.Teleport(u);
            TpSettle(pl);
            _tpWaiting = true; _tpUntil = Time.realtimeSinceStartup + 30f; _tpWaited++;
            return "{\"tpground\":\"waiting\"}";
        }

        private static bool TryPlace(fpscontroller pl)
        {
            var u = mainscript.UnityPosFromGlobal(new Vector3d(_tpGx, _tpGy ?? 0.0, _tpGz));
            if (_tpStrict)
            {
                bool ready = TpaWorldReady(_tpGx, _tpNearGy.Value - .5, _tpGz);
                if (ready && _tpReadySince < 0) _tpReadySince = Time.realtimeSinceStartup;
                float bodyY = mainscript.UnityPosFromGlobal(new Vector3d(_tpGx, _tpNearGy.Value - .5, _tpGz)).y;
                // Prefer the target's floor across the full ring before accepting any lower floor.
                var hits = new System.Collections.Generic.List<RaycastHit?>();
                var floors = new System.Collections.Generic.List<Net.TpaArrival.Candidate>();
                foreach (var o in TpAround)
                {
                    var hit = Net.TpaPlacement.StandHit(u.x + o.x, u.z + o.y, bodyY - .4f, true, false);
                    hits.Add(hit); floors.Add(new Net.TpaArrival.Candidate(hit?.point.y ?? double.NaN, hit.HasValue && Room(hit.Value.point, pl.transform.root), hit.HasValue && hit.Value.collider.attachedRigidbody == null));
                }
                int chosen = Net.TpaArrival.ChooseFloor(floors, bodyY, ready, _tpReadySince < 0 ? 0 : Time.realtimeSinceStartup - _tpReadySince);
                if (chosen >= 0)
                {
                    var hit = hits[chosen].Value;
                    pl.godModeTillGrounded = true;
                    pl.Teleport(new Vector3(hit.point.x, hit.point.y + 1.1f, hit.point.z));
                    TpSettle(pl); _tpHeld = false;
                    _tpOn = hit.collider.name; _tpPlaced++;
                    return true;
                }
                if (chosen != -2) return false;
                _tpStrict = false;
            }
            RaycastHit? h = null;
            if (_tpNearGy.HasValue)
            {
                // the floor at their level: here, else the nearest spot around with room on it (inside a house the spot
                // beside them can be a wall or a cupboard — the first try at the spawn house found no room on the kitchen
                // floor there and put the asker on the roof); a floor lower down only if there's none at their level
                // near: their body + 0.5 m; their feet are ~0.9 m under their body. The floor they stand on first (within
                // half a metre of their feet — not the bed beside them), then anything up to a step above it.
                float near = mainscript.UnityPosFromGlobal(new Vector3d(_tpGx, _tpNearGy.Value, _tpGz)).y;
                foreach (var (below, lowest) in new[] { (near - 1.1f, near - 1.9f), (near, near - 2.5f) })
                {
                    foreach (var o in TpAround)
                    {
                        var hh = StandHit(u.x + o.x, u.z + o.y, below);
                        if (hh != null && hh.Value.point.y >= lowest) { h = hh; u.x += o.x; u.z += o.y; break; }
                    }
                    if (h != null) break;
                }
                if (h == null)
                    foreach (var o in TpAround)
                    {
                        var hh = StandHit(u.x + o.x, u.z + o.y, near);
                        if (hh != null) { h = hh; u.x += o.x; u.z += o.y; break; }
                    }
            }
            if (h == null)
                foreach (var o in TpAround)
                {
                    var hh = StandHit(u.x + o.x, u.z + o.y, _tpGy.HasValue ? u.y : (float?)null);
                    if (hh != null) { h = hh; u.x += o.x; u.z += o.y; break; }
                }
            if (h == null) return false;
            pl.godModeTillGrounded = true;
            pl.Teleport(new Vector3(u.x, h.Value.point.y + 1.1f, u.z));
            TpSettle(pl);
            _tpHeld = false;
            _tpOn = h.Value.collider.name; _tpPlaced++;
            return true;
        }

        internal readonly struct Observation
        {
            internal readonly bool Waiting;
            internal readonly string On;
            internal readonly long Placed, Waited;
            internal Observation(bool waiting, string on, long placed, long waited)
            { Waiting = waiting; On = on; Placed = placed; Waited = waited; }
        }
        internal static Observation Observe() => new Observation(_tpWaiting, _tpOn, _tpPlaced, _tpWaited);

        private static bool _tpHeld;   // tpg … hold: the next tpground settles too
        private static float _tpCalmUntil;

        /// After a long test teleport: the floating-origin shift now (same frame), physics synced, the body still and the
        /// fall-damage baseline reset for half a second, so the arrival starts clean (no fall from the old height's speed).
        /// The 2–10 million m/s launches of farworld runs 6–8 were NOT this body: copies of items attached at the spawn
        /// building were held at pre-shift coordinates (Fixes.FreeAttachPoints) and their kinematic bodies swept 201 km per
        /// physics step, carrying the player along (tpprobe trace, v0.57.97).
        internal static void TpSettle(fpscontroller pl)
        {
            if (pl.RB != null) pl.RB.position = pl.transform.position;
            if (mainscript.s.player.transform.position.sqrMagnitude > 6250000f) mainscript.s.VisszaRakas();
            if (pl.RB != null) pl.RB.position = pl.transform.position;
            Physics.SyncTransforms();
            StoreLeftBehind();
            _tpCalmUntil = Time.realtimeSinceStartup + 0.5f;
            TpCalm(pl);
        }
        public static long TpStores;

        private static void StoreLeftBehind()
        {
            var map = menuhandler.s != null ? menuhandler.s.currentMainMap : null;
            if (map == null || map.genArounds == null || itemPlaceRemoveScript.s == null || savedatascript.s == null) return;
            foreach (var ga in map.genArounds) if (ga != null && ga.t != null) ga.upos = ga.t.position;
            itemPlaceRemoveScript.s.RemoveStuff();
            TpStores++;
        }

        private static void TpCalm(fpscontroller pl)
        {
            if (pl == null) return;
            if (pl.RB != null) { pl.RB.velocity = Vector3.zero; pl.RB.angularVelocity = Vector3.zero; }
            pl.lastVelocity = 0f; pl.currentVelocity = 0f;
        }

        /// Plugin.FixedUpdate: keep the body calm for a moment after a settled teleport.
        public static void FixedTick()
        {
            if (mainscript.s == null) return;
            var pl = mainscript.s.player;
            // held (tpg … hold) until tpground/tprelease — not only the first half second: waiting for the target's
            // ground can take longer, and a 120 m fall with the game's fall damage killed the host there (farworld
            // runs 11/16: its ragdoll became an item of the site under test)
            if (_tpWaiting && pl != null)
            {
                bool placed = TryPlace(pl);
                if (placed || Time.realtimeSinceStartup > _tpUntil) { _tpWaiting = false; Net.Tpa.TelemetryGroundFinished(placed); return; }
                // held at the spot until the ground is there
                var hold = mainscript.UnityPosFromGlobal(new Vector3d(_tpGx, _tpGy ?? _tpNearGy ?? mainscript.GlobalFromUnityPos(pl.transform.position).y, _tpGz));
                if (!_tpGy.HasValue && !_tpNearGy.HasValue) hold.y = pl.transform.position.y;
                pl.transform.position = hold; if (pl.RB != null) pl.RB.position = hold;
                TpCalm(pl); pl.godModeTillGrounded = true;
                return;
            }
            if (_tpHeld && pl != null) { TpCalm(pl); pl.godModeTillGrounded = true; return; }
            if (_tpCalmUntil <= 0f) return;
            if (Time.realtimeSinceStartup < _tpCalmUntil) { TpCalm(pl); return; }
            _tpCalmUntil = 0f;
        }

    }
}
