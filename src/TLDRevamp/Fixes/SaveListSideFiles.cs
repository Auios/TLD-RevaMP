using System.IO;
using HarmonyLib;

namespace TLDRevamp.Fixes
{
    /// The load/save screen (newSaveScreenScript.ReadFiles) listed RevaMP's side files (TLDsaveData_<name>.tlds.leases,
    /// .players, .tune) as extra saves named "<name>.tlds" with the default picture; loading one fails, as it is no
    /// world. After the game builds its list, entries with no TLDsaveData_<name>.tlds of their own are taken out again
    /// (SaveListNames), and the count under the list follows. Autosave slots only ever take the three autosave names, so
    /// a side file never lands there.
    [HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.ReadFiles))]
    public static class SaveListSideFiles
    {
        public static bool Enabled = true;
        /// Entries taken out of the list since the game started (bridge: get TLDRevamp.Fixes.SaveListSideFiles.Hidden).
        public static int Hidden;

        [HarmonyPostfix]
        private static void Postfix(newSaveScreenScript __instance)
        {
            if (!Enabled || __instance == null || __instance.LSaveReferences == null || __instance.LSaveReferences.Count == 0) return;
            try
            {
                var list = __instance.LSaveReferences;
                var names = new string[list.Count];
                for (int i = 0; i < list.Count; i++) names[i] = list[i]?.name;
                var drop = SaveListNames.Drop(names, SaveListNames.SaveNames(Directory.GetFiles(pathscript.SavePath())));
                if (drop.Count == 0) return;
                for (int k = drop.Count - 1; k >= 0; k--)
                {
                    var r = list[drop[k]];
                    if (r != null && r.sprite != null && r.sprite != __instance.spriteNoImage) UnityEngine.Object.Destroy(r.sprite);
                    list.RemoveAt(drop[k]);
                }
                Hidden += drop.Count;
                __instance.saveCount = list.Count;
                if (__instance.TextCount != null) __instance.TextCount.text = ls.l.g(ls.w.mcount) + ": " + __instance.saveCount;
            }
            catch (System.Exception e) { Diagnostics.QuietErrors.Once("SaveListSideFiles", e); }
        }
    }
}
