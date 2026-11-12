using HarmonyLib;

namespace TLDRevamp.Fixes
{
    /// mainscript.Update calls SetCursorVisible(!crsrLocked) every frame: in the world it locks the cursor to the screen's
    /// centre. Our own screens over the world (the join screen, the mark panel) free it again every frame after that, so
    /// the cursor was snapped back to the centre each frame and hardly moved. While one of them is open the game's own
    /// call frees it too, so nothing locks it in between.
    ///
    [HarmonyPatch(typeof(mainscript), nameof(mainscript.SetCursorVisible))]
    public static class FreeCursorScreens
    {

        [HarmonyPrefix]
        private static void Prefix(ref bool _state)
        {
            if (Net.JoinLoading.Visible || MarkPanel.Opened || DebugV2.Editing) _state = true;
        }
    }
}
