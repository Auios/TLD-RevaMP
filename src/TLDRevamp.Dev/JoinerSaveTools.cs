using HarmonyLib;
using static TLDRevamp.JoinerSaveBlock;

namespace TLDRevamp
{
    internal static class JoinerSaveTools
    {

        /// Test (bridge `mp trysave <name>`): the Save menu's button with this name typed in. Did a file appear? One that
        /// did (the block off) is deleted again, and the game's "last save" goes back to what it was.
        public static string Try(string name)
        {
            var sc = savedatascript.s != null ? savedatascript.s.saveScreen : null;
            if (sc == null) sc = UnityEngine.Object.FindObjectOfType<newSaveScreenScript>(true);
            if (sc == null || sc.TextSaveNameInput == null) return "{\"error\":\"save screen not found\"}";
            var path = pathscript.SaveDataName(name);
            if (System.IO.File.Exists(path)) return "{\"error\":" + Json.Str("a save named " + name + " exists") + "}";
            string lastPath = UnityEngine.PlayerPrefs.GetString("LastSavePath"), lastName = UnityEngine.PlayerPrefs.GetString("LastSaveName"),
                   lastVer = UnityEngine.PlayerPrefs.GetString("LastSaveVersion");
            GameLookups.ReflectionProperty(typeof(TMPro.TextMeshProUGUI), "text", runtimeType: sc.TextSaveNameInput.GetType()).SetValue(sc.TextSaveNameInput, name);
            long b0 = Blocked;
            string err = "";
            try { sc.SaveAndRefresh(); } catch (System.Exception e) { err = e.GetType().Name + ": " + e.Message; }
            bool written = System.IO.File.Exists(path);
            if (written)
            {
                pathscript.Delete(path); pathscript.Delete(pathscript.SavePropertiesName(name)); pathscript.Delete(pathscript.SaveThumbnailName(name));
                UnityEngine.PlayerPrefs.SetString("LastSavePath", lastPath); UnityEngine.PlayerPrefs.SetString("LastSaveName", lastName);
                UnityEngine.PlayerPrefs.SetString("LastSaveVersion", lastVer); UnityEngine.PlayerPrefs.Save();
            }
            return "{\"written\":" + (written ? "true" : "false") + ",\"blocked\":" + (Blocked - b0) + ",\"inHostWorld\":" + ((TestSafety.InHostWorld || TestSafety.LeavingHostWorld) ? "true" : "false") +
                   ",\"fault\":" + Json.Str(err) + "}";
        }
    }
}
