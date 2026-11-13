using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// Tumbleweeds. An item (thumbleweedScript) that gusts of wind push about while its machine applies the wind (bwind).
    /// The map's spawner (thumbleweedMain.Upd, a random wait) makes one around the player on sand, up to its limit; once
    /// far from the player the world's streaming drops it (PreDestroy: not stored). The game's MP: the host spawns and
    /// blows them, the others get them as items. Here every machine spawned its own around its own player, unshared:
    /// each player had tumbleweeds the other could not see.
    /// Now every machine still spawns around its own player - it has the ground there for them to roll on - and the new
    /// one is shared (the others get it as an item; its owner moves it). The limit counts every tumbleweed around the
    /// player, the others' too: players together do not get twice as many. The wind blows on the owner's machine only
    /// (a copy is moved by its owner).
    public static partial class Entities
    {
        public static long TumbleShared, TumbleLimited;
        public static float TumbleNearM = 300f;   // the others' tumbleweeds this near the player count for its limit

        [HarmonyPatch(typeof(thumbleweedMain), nameof(thumbleweedMain.Spawn))]
        private static class TumbleSpawn
        {
            [HarmonyPrefix]
            private static bool Prefix(thumbleweedMain __instance, bool ignoreLimit, out tosaveitemscript __state)
            {
                __state = __instance.nowSpawned;
                if (!InSession || ignoreLimit) return true;
                if (TumbleweedsNear(__instance) >= __instance.thumbleweedLimit) { TumbleLimited++; return false; }
                return true;
            }

            [HarmonyPostfix]
            private static void Postfix(thumbleweedMain __instance, tosaveitemscript __state)
            {
                var it = __instance.nowSpawned;
                if (!InSession || it == null || it == __state) return;
                long before = SpawnShared;
                ShareSpawn(it.gameObject);
                if (SpawnShared > before) TumbleShared++;
            }
        }

        private static readonly HashSet<thumbleweedScript> _tumbles = new HashSet<thumbleweedScript>();
        /// This machine's own tumbleweeds count wherever they are (the game's count), the others' (copies: in the
        /// game's list too, and every shared one this machine knows of) only within TumbleNearM: a player 2 km off with
        /// a full set of its own must not stop them coming up here.
        internal static int TumbleweedsNear(thumbleweedMain m)
        {
            _tumbles.Clear();
            var pl = mainscript.s != null ? mainscript.s.player : null;
            foreach (var t in m.thumbleweeds) if (t != null) Count(t, pl);
            foreach (var e in ByNet.Values) if (e.Root != null && e.Root.thumbleweed != null) Count(e.Root.thumbleweed, pl);
            return _tumbles.Count;
        }

        private static void Count(thumbleweedScript t, fpscontroller pl)
        {
            if (!IsProxy(t)) { _tumbles.Add(t); return; }
            if (pl != null && (t.transform.position - pl.transform.position).sqrMagnitude <= TumbleNearM * TumbleNearM) _tumbles.Add(t);
        }

        /// The wind blows where the tumbleweed is simulated: its owner's machine.
        [HarmonyPatch(typeof(thumbleweedScript), "FixedUpdate")]
        private static class TumbleWind
        {
            [HarmonyPrefix]
            private static void Prefix(thumbleweedScript __instance) { if (InSession) __instance.bwind = !IsProxy(__instance); }
        }
    }
}
