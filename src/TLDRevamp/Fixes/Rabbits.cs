using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TLDRevamp.Fixes
{
    /// Rabbits, back. The beta still has the rabbit (itemdatabase item 103 "Nyul": nyulscript, aiscript, sensoryscript,
    /// movecreaturescript, a ragdoll; the big ones turn evil and kill, a car can run one over) and its spawner on the
    /// player (aispawn on the Player prefab: rabbits around the player, out of view, up to its limit) - but nothing
    /// starts the spawner (aispawn.START has no caller), so no rabbit ever comes up; only the dev menu made one.
    ///
    /// The game's own spawner is started once the player is in a world, with its own settings (chance, wait, count,
    /// distance, field of view). In a session (Net.Entities.Rabbits) every machine runs its own spawner around its own
    /// player - it has the ground there; the host does not generate around the others - and a new rabbit is shared at
    /// once. The limit counts every rabbit around the player, the others' too (players together do not get twice as
    /// many), and a rabbit never comes up near another player or where they look.
    ///
    /// Rabbits never leave on their own, and the spawner only knows the ones it made since the world loaded: every load
    /// used to add up to its limit again on top of the saved ones (a save played at one place for long had 63 around the
    /// player, 2026-11-26). So the limit counts every living rabbit within the game's item range, in solo too, and the
    /// rabbits over the limit that this machine runs are taken away one at a time, farthest first, never near a player.
    public static class Rabbits
    {
        public static long Starts, Stops, Spawned, Limited, NearOthers, Trimmed;
        public static float CountM = 0f;   // living rabbits this near the player count for its limit; 0: the game's item range (at least maxDist + 50 m)
        public static bool Trim = true;    // rabbits over the limit go, farthest first
        public static int TrimSlack = 2;   // only above the limit plus this many
        public static float TrimAwayM = 120f;   // never a rabbit nearer than this to any player
        public static float TrimEveryS = 2f;

        private static aispawn _sp;
        private static float _checkAt, _trimAt;

        /// The local player's spawner (on the Player prefab).
        internal static aispawn Spawner()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) { _sp = null; return null; }
            if (_sp == null || _sp.transform.root != pl.transform.root) _sp = pl.GetComponentInChildren<aispawn>(true);
            return _sp;
        }

        /// Once a second: the spawner runs while the switch is on and the player is in a world (a dedicated server has no
        /// player: no spawner).
        internal static void Tick()
        {
            if (Time.unscaledTime < _checkAt) return;
            _checkAt = Time.unscaledTime + 1f;
            var sp = Spawner();
            if (sp == null || sp.AIPrefab == null || !sp.isActiveAndEnabled) return;
            bool want = !mainscript.s.died;
            if (want && !sp.started) { sp.START(); Starts++; Plugin.Log.LogInfo("rabbits: the player's spawner started (max " + sp.maxAINum + ", chance " + sp.chance + ", every " + sp.minTime + "-" + sp.maxTime + " s, " + sp.minDist + "-" + sp.maxDist + " m)"); }
            else if (!want && sp.started) { sp.STOP(); sp.StopAllCoroutines(); Stops++; }
            if (want && sp.started) TrimTick(sp);
        }

        /// How near a living rabbit must be to count for the limit: CountM, else the game's item range (rabbits stay
        /// loaded that far; the spawner's own reach plus 50 m let the ones that wandered off be replaced over and over).
        internal static float CountRadius(aispawn sp)
        {
            if (CountM > 0f) return CountM;
            var ips = itemPlaceRemoveScript.s;
            return Mathf.Max(sp.maxDist + 50f, ips != null ? ips.itemRemoveDist : 0f);
        }

        private static Vector3 At(aispawn sp) => sp.bodyRot != null ? sp.bodyRot.position : sp.transform.position;
        private static bool Alive(nyulscript ny) => ny.AI == null || ny.AI.alive;

        /// Living rabbits around the local player within CountRadius: the saved ones, the spawner's and, in a session,
        /// copies of the others' too. Dead ones don't count (a body is the player's to keep).
        internal static int Around(aispawn sp)
        {
            int n = 0;
            float r = CountRadius(sp);
            var at = At(sp);
            foreach (var ny in Object.FindObjectsOfType<nyulscript>())
                if (ny != null && Alive(ny) && (ny.transform.position - at).sqrMagnitude < r * r) n++;
            return n;
        }

        /// Every TrimEveryS: while more living rabbits than the limit + TrimSlack are around, the farthest one this machine
        /// runs (not a copy of another player's), loose in the world and at least TrimAwayM from every player, goes - the
        /// game's own deletion (in a session it goes out as a removal). Old saves shrink back to the limit this way.
        private static readonly List<nyulscript> _trimBuf = new List<nyulscript>();
        private static void TrimTick(aispawn sp)
        {
            if (!Trim || Time.unscaledTime < _trimAt) return;
            _trimAt = Time.unscaledTime + TrimEveryS;
            float r = CountRadius(sp);
            var at = At(sp);
            _trimBuf.Clear();
            foreach (var ny in Object.FindObjectsOfType<nyulscript>())
                if (ny != null && Alive(ny) && (ny.transform.position - at).sqrMagnitude < r * r) _trimBuf.Add(ny);
            if (_trimBuf.Count <= sp.maxAINum + TrimSlack) return;
            nyulscript far = null; float farD = -1f;
            foreach (var ny in _trimBuf)
            {
                if (Net.Entities.InSession && Net.Entities.IsProxy(ny)) continue;   // another player's: theirs to trim
                var it = ny.GetComponentInParent<tosaveitemscript>();
                if (it == null || it.transform.parent != null) continue;            // held, carried, in something
                var p = ny.transform.position;
                float d = (p - at).sqrMagnitude;
                if (d < TrimAwayM * TrimAwayM || NearOther(p)) continue;
                if (d > farD) { farD = d; far = ny; }
            }
            _trimBuf.Clear();
            if (far == null) return;
            Net.Entities.Delete(far.GetComponentInParent<tosaveitemscript>());   // gone from the save too; in a session a removal
            Trimmed++;
        }

        private static bool NearOther(Vector3 p)
        {
            if (!Net.Entities.InSession) return false;
            foreach (var t in Net.RemotePlayers.AiTargets())
                if (t.body != null && (t.body.position - p).sqrMagnitude < TrimAwayM * TrimAwayM) return true;
            return false;
        }

        /// The limit counts every living rabbit around the player (Around), solo and in a session: the game's own check
        /// only knows the ones its spawner made since the world loaded.
        [HarmonyPatch(typeof(aispawn), nameof(aispawn.TrySpawn))]
        private static class Limit
        {
            [HarmonyPrefix]
            private static bool Prefix(aispawn __instance)
            {
                if (__instance != Spawner()) return true;
                if (Around(__instance) < __instance.maxAINum) return true;
                Limited++;
                return false;
            }
        }

        /// Never near another player or in their view: the game keeps a spawn out of the local player's field of view
        /// (halfFov) - the same for every other player (their body faces where they look), and never closer than the
        /// spawner's minDist to them.
        [HarmonyPatch(typeof(aispawn), nameof(aispawn.CanSpawn))]
        private static class NotNearOthers
        {
            [HarmonyPostfix]
            private static void Postfix(aispawn __instance, ref Vector3 __result)
            {
                if (__result == Vector3.zero || !Net.Entities.InSession) return;
                foreach (var t in Net.RemotePlayers.AiTargets())
                {
                    var d = __result - t.body.position;
                    var flat = Vector3.ProjectOnPlane(d, Vector3.up);
                    if (flat.magnitude < __instance.minDist || Vector3.Angle(Vector3.ProjectOnPlane(t.body.forward, Vector3.up), flat) <= __instance.halfFov)
                    {
                        NearOthers++;
                        __result = Vector3.zero;
                        return;
                    }
                }
            }
        }

        /// A new rabbit: shared at once in a session (aispawn instantiates the prefab itself, not through mainscript.Spawn).
        [HarmonyPatch(typeof(aispawn), nameof(aispawn.Spawn))]
        private static class Share
        {
            [HarmonyPostfix]
            private static void Postfix(aispawn __instance)
            {
                Spawned++;
                if (__instance.AIs.Count > 0) Net.EntityRabbits.RabbitSpawned(__instance.AIs[__instance.AIs.Count - 1]);
            }
        }

        public static string Stats()
        {
            var sp = Spawner();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string s = sp == null ? "null" :
                "{\"started\":" + (sp.started ? "true" : "false") + ",\"prefab\":" + Json.Str(sp.AIPrefab != null ? sp.AIPrefab.name : "") + ",\"max\":" + sp.maxAINum +
                ",\"chance\":" + sp.chance.ToString("F2", ic) + ",\"time\":[" + sp.minTime.ToString("F0", ic) + "," + sp.maxTime.ToString("F0", ic) + "]" +
                ",\"count\":[" + sp.minCount.ToString("F0", ic) + "," + sp.maxCount.ToString("F0", ic) + "],\"dist\":[" + sp.minDist.ToString("F0", ic) + "," + sp.maxDist.ToString("F0", ic) + "]" +
                ",\"halfFov\":" + sp.halfFov.ToString("F0", ic) + ",\"radius\":" + sp.radius.ToString("F2", ic) + ",\"listed\":" + sp.AIs.Count + ",\"around\":" + Around(sp) + ",\"countM\":" + CountRadius(sp).ToString("F0", ic) + "}";
            return "{\"enabled\":" + ("true") + ",\"spawner\":" + s + ",\"starts\":" + Starts + ",\"stops\":" + Stops + ",\"spawned\":" + Spawned +
                   ",\"limited\":" + Limited + ",\"nearOthers\":" + NearOthers + ",\"trim\":" + (Trim ? "true" : "false") + ",\"trimmed\":" + Trimmed + "}";
        }
    }
}
