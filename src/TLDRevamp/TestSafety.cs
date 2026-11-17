using HarmonyLib;

namespace TLDRevamp
{
    /// Runtime-only guards for automated tests. Nothing here is saved to disk.
    ///
    /// BlockAutoSave: test worlds must not autosave (they would replace the player's autosave slots). Earlier the test
    /// tools switched the game's own setting (settingsscript.S.BAutoSave) off instead; the game writes its settings file
    /// whenever the settings menu closes, so that "off" reached the player's settings.tldc on 2026-09-25 and turned
    /// autosave off in their real game. This flag lives only in the mod's memory and is reset on every game start.
    [HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.AutoSave))]
    public static class TestSafety
    {
        /// Multiplayer's own block (the client plays in the host's world; the host owns the save). Separate from the
        /// test flag: leaving a session cleared the shared flag, and a test world left running after `mp stop` then
        /// autosaved into the laptop's slots after the test had restored them (2026-10-01, found from the slot times).
        public static bool InHostWorld = false;
        /// Leaving the host's world (left, kicked, the host lost): the session is gone but this is still the host's world
        /// until the next scene is in — the menu load is a coroutine a frame later, and if it failed the player would
        /// play on in it with the block already off (Mp.Stop clears InHostWorld) and the next autosave would write the
        /// host's world into their own slot. Cleared when a scene has loaded.
        public static bool LeavingHostWorld = false;
        public static long Blocked;

        public static void Init()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, mode) =>
            {
                if (LeavingHostWorld) Plugin.Log.LogInfo("left the host's world: saving allowed again (" + sc.name + " loaded)");
                LeavingHostWorld = false;
                Net.PlayerSave.SceneLoaded();
            };
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!(DevHooks.SaveVeto?.Invoke() ?? false) && !InHostWorld && !LeavingHostWorld) return true;
            Blocked++;
            return false;
        }
    }
}

namespace TLDRevamp
{
    /// A joiner plays in the host's world, and the game's Save menu would write that world into the joiner's own slot
    /// (under their own save's name it replaces their world with the host's). The host owns the shared save; in a
    /// joiner's game, saving does nothing and says why. The host saves as before.
    [HarmonyPatch]
    public static class JoinerSaveBlock
    {
        public static long Blocked;
        private static bool InHostWorld => TestSafety.InHostWorld || TestSafety.LeavingHostWorld;

        /// The Save menu's button (the "saved" sign and the slot list skipped too).
        [HarmonyPrefix, HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.SaveAndRefresh))]
        private static bool SaveButton() => Allow();

        /// Every other way into a save file.
        [HarmonyPrefix, HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.Save))]
        private static bool SaveFile() => Allow();

        private static bool Allow()
        {
            if (!InHostWorld) return true;
            Blocked++;
            Plugin.Log.LogInfo("save refused: this is the host's world (JoinerSaveBlock)");
            Net.Session.Show(1, "", "You can't save in someone else's world: the host saves it. Your own saves are untouched.");
            return false;
        }
    }
}
