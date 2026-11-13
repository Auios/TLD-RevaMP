using System.Collections.Generic;
using HarmonyLib;

namespace TLDRevamp.Fixes
{
    /// The game's item streaming (itemPlaceRemoveScript.Upd, once a second) runs RemoveStuff and then PlaceStuff in the
    /// same call. RemoveStuff stores an item (tosaveitemscript.Remove: its far-store record) and Destroys it — but a
    /// Destroy takes effect at the end of the frame, so the object is still registered in savedatascript.items when
    /// PlaceStuff runs. If the fresh record lies within itemSpawnDist of a generation centre, PlaceStuff finds "a stored
    /// record AND a live object with that id" and deletes the record as a stale duplicate (the !mapSpawned branch): the
    /// object goes at the end of the frame, its record went with it — the item is lost for good, in single player too.
    /// A whole car goes this way when any item under it is past itemRemoveDist (RemoveStuff then stores the whole
    /// hierarchy): the car vanished from the road (2026-10-03 reports, bug 10: "the game would delete my car at times").
    ///
    /// Second half of the same race: tosaveitemscript.OnDestroy removes savedatascript.items[id] whoever holds that id
    /// now. An object destroyed after its record was placed again (a new object with the same id) unregisters the NEW
    /// object: never saved, streamed or synced again — gone on the next load.
    ///
    /// Fix, exact otherwise: a record PlaceStuff dropped while its object was on its way out (preDestroyed) is put back;
    /// a dying object leaves the id to the object that holds it now.
    ///
    /// Runtime switch (bridge `set`): TLDRevamp.Fixes.StreamRace.Enabled
    [HarmonyPatch]
    public static class StreamRace
    {
        public static bool Enabled = true;
        public static long RecordsKept, IdsKept;
        private static readonly List<KeyValuePair<uint, save_item>> _dying = new List<KeyValuePair<uint, save_item>>();

        [HarmonyPatch(typeof(itemPlaceRemoveScript), nameof(itemPlaceRemoveScript.PlaceStuff))]
        [HarmonyPrefix]
        private static void BeforePlace()
        {
            _dying.Clear();
            var s = savedatascript.s;
            if (!Enabled || s == null || s.data == null || s.data.itemData == null) return;
            var list = s.data.itemData.items;
            foreach (var kv in s.items)
            {
                var it = kv.Value;
                if (it == null || !it.preDestroyed || it.mapSpawned) continue;
                if (savedatascript.IndexOfID(list, kv.Key, out int k)) _dying.Add(new KeyValuePair<uint, save_item>(kv.Key, list[k]));
            }
        }

        [HarmonyPatch(typeof(itemPlaceRemoveScript), nameof(itemPlaceRemoveScript.PlaceStuff))]
        [HarmonyPostfix]
        private static void AfterPlace()
        {
            if (_dying.Count == 0) return;
            var list = savedatascript.s.data.itemData.items;
            foreach (var kv in _dying)
                if (!savedatascript.IndexOfID(list, kv.Key, out _)) { list.Add(kv.Value); RecordsKept++; }   // its chunk entry was never removed
            _dying.Clear();
        }

        /// The holder of the id when the object dies; restored after the game's OnDestroy took it off the list.
        [HarmonyPatch(typeof(tosaveitemscript), "OnDestroy")]
        [HarmonyPrefix]
        private static void BeforeDestroy(tosaveitemscript __instance, out tosaveitemscript __state)
        {
            __state = null;
            if (!Enabled || savedatascript.s == null) return;
            if (savedatascript.s.items.TryGetValue(__instance.idInSave, out var now) && now != null && now != __instance) __state = now;
        }

        [HarmonyPatch(typeof(tosaveitemscript), "OnDestroy")]
        [HarmonyPostfix, HarmonyPriority(Priority.First)]   // before Entities' deletion check, which reads the holder
        private static void AfterDestroy(tosaveitemscript __instance, tosaveitemscript __state)
        {
            if (__state == null) return;
            if (!savedatascript.s.items.TryGetValue(__instance.idInSave, out var now) || now != __state) { savedatascript.s.items[__instance.idInSave] = __state; IdsKept++; }
        }
    }
}
