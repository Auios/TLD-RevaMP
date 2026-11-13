using System.Linq;
using HarmonyLib;
using static TLDRevamp.TestSafety;

namespace TLDRevamp
{
    internal static class TestSafetyTools
    {

        /// Load a named save through the game's own save screen (bridge `loadsave <name>`). Autosave is blocked first so
        /// a test session can never write into the player's autosave slots.
        public static string LoadSave(string name)
        {
            var path = pathscript.SaveDataName(name);
            if (!System.IO.File.Exists(path)) return "{\"error\":" + Json.Str("no save named " + name) + "}";
            var screen = savedatascript.s != null ? savedatascript.s.saveScreen : null;
            if (screen == null) screen = UnityEngine.Object.FindObjectOfType<newSaveScreenScript>(true);
            if (screen == null) return "{\"error\":\"save screen not found\"}";
            BlockAutoSave = true;
            screen.Load(path);
            return "{\"loading\":" + Json.Str(path) + "}";
        }
        public static bool BlockAutoSave = false;
    }
}
