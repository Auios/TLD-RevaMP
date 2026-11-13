using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        /// A/B: share the corpse that the game creates in fpscontroller.Death2. False leaves it local as before; the
        /// limb transport itself is already implemented in Entities.Ragdoll.
        public static bool DeathRagdollShare = true;
        public static long DeathCorpsesShared, DeathCorpseShareMissed;
        /// How many corpses were un-shared again (the corpse of a death that ended: the game's own RagdolReset
        /// replaces the corpse object but never destroys the old one, so without this every death leaked one shared
        /// entity and one orphan body per machine).
        public static long DeathCorpsesReleased;
        /// A/B: a player's corpse stays in the world after the respawn and after the session, a shared object like any
        /// other body, as the game itself leaves it in single player (its RagdolReset makes a new corpse object for the
        /// next death and leaves the old one lying). False is the old behaviour: the respawn removed the corpse on every
        /// machine (DeathCorpsesReleased).
        public static bool DeathCorpseStays = true;
        /// How many corpses were left lying at a respawn or a session's end (DeathCorpseStays).
        public static long DeathCorpsesKept;
        /// How many times the game's own Death was called again for a death that already happened: the corpse was
        /// already shared, so it is the same corpse (not a second one).
        public static long DeathCorpseDuplicates;

        /// The corpse of the last death this machine made, with the exact ids a test needs: the game's own save id
        /// (the id the host and the joiner agree on) and the network id (the id the share got).
        public static uint DeathCorpseSaveId, DeathCorpseNetId;
        /// The save ids of the things the death spawned and shared (the test's own items), in order. The save id
        /// is the id every machine agrees on; the network id comes later (the host answers the share), so it is
        /// read when the diagnostics are made, not when the thing is spawned.
        public static readonly System.Collections.Generic.List<uint> DeathItemSaveIds = new System.Collections.Generic.List<uint>();
        /// The network ids of the same things, in the same order (0 while the share is still pending).
        public static readonly System.Collections.Generic.List<uint> DeathItemNetIds = new System.Collections.Generic.List<uint>();

        /// The corpse object of the death that is currently open on this machine (the one this machine shared). It is
        /// released when the death ends, so the next death does not leave the previous body behind.
        private static GameObject _deathCorpse;
        private static uint _deathCorpseNet;

        /// The network id of a shared object by its save id (0 while the share is still pending).
        internal static uint NetIdOfSaveId(uint saveId)
        {
            if (saveId == 0) return 0;
            foreach (var e in ByNet.Values) if (e.RootId == saveId) return e.NetId;
            return 0;
        }

        /// The diagnostics of the last death this machine made (bridge `mp deathinfo`): the exact ids of the corpse
        /// and of the things this death spawned, and the counters that say what happened. A test asserts these
        /// exact numbers, not "an item nearby".
        public static string DeathInfo()
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"corpseSaveId\":").Append(DeathCorpseSaveId).Append(",\"corpseNetId\":").Append(DeathCorpseNetId);
            sb.Append(",\"corpsesShared\":").Append(DeathCorpsesShared).Append(",\"corpsesMissed\":").Append(DeathCorpseShareMissed);
            sb.Append(",\"corpsesReleased\":").Append(DeathCorpsesReleased).Append(",\"corpsesKept\":").Append(DeathCorpsesKept);
            sb.Append(",\"corpsesDuplicated\":").Append(DeathCorpseDuplicates);
            sb.Append(",\"itemIds\":[");
            for (int i = 0; i < DeathItemSaveIds.Count; i++)
            {
                if (i > 0) sb.Append(',');
                // the network id is resolved now: the host answers the share a moment after the spawn
                uint net = NetIdOfSaveId(DeathItemSaveIds[i]);
                if (net == 0 && i < DeathItemNetIds.Count) net = DeathItemNetIds[i];
                sb.Append('[').Append(DeathItemSaveIds[i]).Append(',').Append(net).Append(']');
            }
            sb.Append("],\"death\":").Append(SessionRules.DeathStats());
            sb.Append('}');
            return sb.ToString();
        }

        /// A new session: the death's ids belong to the session that made them. The corpse of the last death stays
        /// like the session's other objects (the host keeps them in its world); with DeathCorpseStays off it is
        /// released, as before.
        internal static void DeathReset()
        {
            EndDeathCorpse();
            DeathCorpseSaveId = 0; DeathCorpseNetId = 0;
            DeathItemSaveIds.Clear(); DeathItemNetIds.Clear();
            _deathCorpseNet = 0;
        }

        /// The death is over (the respawn, the session's end): its corpse stays where it lies (DeathCorpseStays), and
        /// only this machine's note of "the open death's corpse" goes; or, with the switch off, it is released.
        private static void EndDeathCorpse()
        {
            if (!DeathCorpseStays) { ReleaseDeathCorpse(); return; }
            if (_deathCorpse != null) DeathCorpsesKept++;
            _deathCorpse = null; _deathCorpseNet = 0;
        }

        /// Release the corpse of the death that just ended. The game's own Revive calls RagdolReset, which replaces
        /// the corpse object with a fresh one and never destroys the old one: without this the world gained one
        /// orphan body and one shared entity per death, and the death-screen camera of the next death was inside the
        /// previous corpse's collider.
        private static void ReleaseDeathCorpse()
        {
            if (_deathCorpse == null) { _deathCorpseNet = 0; return; }
            var go = _deathCorpse;
            _deathCorpse = null;
            _deathCorpseNet = 0;
            // the shared entity goes first: the others' copies of the corpse go with it (the RemoveItem broadcast).
            uint net = 0;
            foreach (var e in ByNet.Values)
                if (e.Root != null && (e.Root.transform == go.transform || e.Root.transform.IsChildOf(go.transform))) { net = e.NetId; break; }
            if (net != 0)
            {
                try { DeleteShared(net); }
                catch (System.Exception e) { Plugin.Log.LogWarning("death corpse: release failed: " + e.Message); }
                // the entity may be dormant here (the body streamed out): the removal still has to go out, or the
                // others keep the corpse for good.
                if (ByNet.ContainsKey(net))
                {
                    try
                    {
                        W.Reset(); W.U8(RemoveItem); W.U32(net);
                        ToServer(W, true);
                        ByNet.Remove(net);
                    }
                    catch (System.Exception e) { Plugin.Log.LogWarning("death corpse: removal failed: " + e.Message); }
                }
            }
            // the local body: the game's own object, destroyed so it stops lying there and stops being counted.
            try { UnityEngine.Object.Destroy(go); } catch { }
            DeathCorpsesReleased++;
        }

        /// A/B: share the corpse that the game creates in fpscontroller.Death2. False leaves it local as before; the
        /// limb transport itself is already implemented in Entities.Ragdoll.
        [HarmonyLib.HarmonyPatch(typeof(fpscontroller), nameof(fpscontroller.Death))]
        private static class ShareRealDeathRagdoll
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(fpscontroller __instance)
            {
                if (!DeathRagdollShare || !InSession || __instance == null || mainscript.s == null || __instance != mainscript.s.player) return;
                var rag = __instance.spawnedRagdol;
                if (rag == null || !__instance.died || rag.GetComponent<tosaveitemscript>() == null)
                {
                    if (__instance.died) DeathCorpseShareMissed++;
                    return;
                }
                var ts = rag.GetComponent<tosaveitemscript>();
                long before = SpawnShared;
                ShareSpawn(rag);
                if (SpawnShared > before)
                {
                    DeathCorpsesShared++;
                    DeathCorpseSaveId = ts != null ? ts.idInSave : 0;
                    DeathCorpseNetId = NetIdOf(rag);   // 0 while the host has not answered the share yet
                    _deathCorpse = rag;
                    _deathCorpseNet = DeathCorpseNetId;
                }
                else
                {
                    // The corpse was already shared. That is the normal case for a DUPLICATE death: the game's own
                    // SpawnRagdol re-uses the corpse object (only RagdolReset makes a new one), so the second Death
                    // call shares the same corpse and the share is refused as "already shared". The corpse's ids are
                    // kept (they are the same corpse's), and the duplicate is counted as such.
                    bool known = false;
                    foreach (var e in ByNet.Values) if (e.Root != null && (e.Root.transform == rag.transform || e.Root.transform.IsChildOf(rag.transform))) { known = true; break; }
                    if (!known) DeathCorpseShareMissed++;
                    else
                    {
                        DeathCorpseSaveId = ts != null ? ts.idInSave : 0;
                        DeathCorpseNetId = NetIdOf(rag);
                        _deathCorpse = rag;
                        _deathCorpseNet = DeathCorpseNetId;
                        DeathCorpseDuplicates++;   // the same corpse, shared once: a duplicate death, not a second corpse
                    }
                }
            }
        }

        /// The respawn ended the death: its corpse stays (DeathCorpseStays; before, it went away on every machine). The
        /// game's own RagdolReset, which ran just before, has made the corpse object of the next death already.
        [HarmonyLib.HarmonyPatch(typeof(fpscontroller), nameof(fpscontroller.Revive))]
        private static class ReleaseCorpseOnRevive
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(fpscontroller __instance)
            {
                if (!InSession || __instance == null || mainscript.s == null || __instance != mainscript.s.player) return;
                EndDeathCorpse();
            }
        }
    }
}
