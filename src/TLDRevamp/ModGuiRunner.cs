using UnityEngine;

namespace TLDRevamp
{
    /// The Loader mods' IMGUI in a pass of its own, IMGUI's layout pass on: the Runner skips it (the overlay needs none), and
    /// a mod's GUILayout then got no Layout event (a tool mod's menu threw "Getting control 0's position", a gauge mod's box
    /// did too). A/B: ModHost.GuiOwnPass = false is before 2026-11-24 (drawn from the Runner's pass).
    public class ModGuiRunner : MonoBehaviour
    {
        private void OnGUI()
        {
            if (ModHost.GuiOwnPass && !Net.JoinLoading.Showing) Draw();
        }

        internal static void Draw()
        {
            ModHost.Gui();
            ModHost.RefreshConfigScreen();   // is a settings screen open right now?
            ModHost.DrawConfigs();   // the Loader mods' own settings windows (IMGUI)
        }
    }
}
