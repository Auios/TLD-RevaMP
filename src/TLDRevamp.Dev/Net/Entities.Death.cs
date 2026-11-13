using System.Linq;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityDeathTools
    {

        /// The game's own Death, called again for a death that already happened (the duplicate path). The game's
        /// Death is not idempotent, so the mod must not treat the second call as a new death: no second anchor, no
        /// second corpse, no second record. The test calls this while the death screen is open.
        public static string DeathAgain()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            if (!InSession) return "{\"error\":\"join a multiplayer world first\"}";
            if (!pl.died && (mainscript.s == null || !mainscript.s.died)) return "{\"error\":\"not dead\"}";
            string beforeStats = SessionRules.DeathStats();   // the stats are the observable, not a copy of it
            long corpsesBefore = DeathCorpsesShared;
            try { pl.Death(); }
            catch (System.Exception e) { return "{\"error\":\"" + e.Message + "\"}"; }
            return "{\"died\":true,\"corpsesBefore\":" + corpsesBefore + ",\"corpsesAfter\":" + DeathCorpsesShared +
                   ",\"statsBefore\":" + beforeStats + ",\"statsAfter\":" + SessionRules.DeathStats() + "}";
        }

        /// Deterministic two-machine test: optional item prefab ids are spawned at the player and picked
        /// up through the game's own Pickup before the player's body is put into a known fling. Then the game's public
        /// Death runs, and the normal hooks capture the anchor and share the corpse.
        public static string DeathFling(float vx, float vy, float vz, int[] prefabs = null)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            if (!InSession) return "{\"error\":\"join a multiplayer world first\"}";
            if (pl.died || (mainscript.s != null && mainscript.s.died)) return "{\"error\":\"already dead\"}";
            try
            {
                if (kaposztaleves.s != null) { kaposztaleves.s.settings.god = false; kaposztaleves.s.settings.fly = false; }
            }
            catch { }
            pl.godModeTillGrounded = false;
            var v = new Vector3(vx, vy, vz);
            if (!Finite(v)) return "{\"error\":\"non-finite velocity\"}";
            var before = mainscript.GlobalFromUnityPos(pl.transform.position);
            var spawned = new System.Collections.Generic.List<int>();
            // this death's own things: the ids a test asserts on, not "any item nearby"
            DeathItemSaveIds.Clear(); DeathItemNetIds.Clear();
            if (prefabs != null)
                foreach (int id in prefabs)
                {
                    if (id < 0 || itemdatabase.s == null || id >= itemdatabase.s.items.Length || itemdatabase.s.items[id] == null) continue;
                    var go = UnityEngine.Object.Instantiate(itemdatabase.s.items[id], pl.transform.position + pl.transform.forward * 0.8f + Vector3.up, pl.transform.rotation);
                    var ts = go.GetComponent<tosaveitemscript>();
                    if (ts != null) { ts.FStart(); ShareSpawn(go); DeathItemSaveIds.Add(ts.idInSave); DeathItemNetIds.Add(NetIdOf(go)); }
                    var pk = go.GetComponent<pickupable>();
                    if (pk != null) Carry(pl, pk);
                    spawned.Add(id);
                }
            // A seated player has no rigidbody of their own (the game's GetIn destroys it and parents the player to
            // the seat), so a fling would silently do nothing. The game's own Respawn gets the player out of the
            // seat; here the death itself is what the test asks for, so the fling is only applied when there is a
            // body to fling. The report says what really happened.
            bool flung = false;
            if (pl.RB != null) { pl.RB.isKinematic = false; pl.RB.velocity = v; pl.RB.angularVelocity = Vector3.zero; pl.RB.WakeUp(); flung = true; }
            pl.Death();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var ids = new System.Text.StringBuilder();
            // [save id, network id] pairs, as `mp deathinfo` writes them (the old "save:net" was not JSON: every reply
            // with carried things failed to parse)
            for (int i = 0; i < DeathItemSaveIds.Count; i++) { if (i > 0) ids.Append(','); ids.Append('[').Append(DeathItemSaveIds[i]).Append(',').Append(DeathItemNetIds[i]).Append(']'); }
            return "{\"died\":" + (pl.died ? "true" : "false") + ",\"flung\":" + (flung ? "true" : "false") + ",\"seated\":" + (pl.Bsitting ? "true" : "false") +
                   ",\"velocity\":[" + v.x.ToString("F3", ic) + "," + v.y.ToString("F3", ic) + "," + v.z.ToString("F3", ic) +
                   "],\"before\":[" + before.x.ToString("F3", ic) + "," + before.y.ToString("F3", ic) + "," + before.z.ToString("F3", ic) + "],\"spawned\":[" + string.Join(",", spawned) +
                   "],\"itemIds\":[" + ids + "],\"corpseSaveId\":" + DeathCorpseSaveId + ",\"corpseNetId\":" + DeathCorpseNetId +
                   "," + SessionRules.DeathStats().TrimStart('{');
        }

        /// Put one thing in the player's hands and the rest in the inventory slots, the way the game does. The
        /// game's own Pickup only ever takes the first thing into the hands (pl.pickedUp is single), so a test
        /// that spawned two things and called Pickup twice really carried one and left the other lying on the
        /// ground: the "the carried things drop at the anchor" assertion then passed for the wrong reason.
        internal static void Carry(fpscontroller pl, pickupable pk)
        {
            if (pl.pickedUp == null) { pl.Pickup(pk); return; }
            if (pl.inventory == null) return;
            for (int i = 0; i < pl.inventory.Count; i++)
            {
                if (pl.inventory[i] != null) continue;
                if (!pk.canInventory) return;   // it cannot be carried: it stays where it is (and the test says so)
                pl.inventory[i] = pk;
                pk.transform.parent = pl.transform;
                try { pk.SyncInventory(); } catch { }
                return;
            }
        }
    }
}
