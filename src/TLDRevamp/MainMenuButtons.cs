using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLDRevamp
{
    /// Mods on the game's main menu: a full row of its own between Settings and Exit (asked for 2026-11-20; it was a
    /// small button against Settings' right edge), cloned from the game's own Settings button (same sprite, size, font
    /// and sound). It opens the mod browser over the menu. The column keeps Exit where it was and grows upwards into the
    /// free space under the language box, one even step per button. (The Revamp settings are in Settings, so there is
    /// no Revamp button here.) A menu without a Settings button is left as it is.
    [HarmonyPatch]
    public static class MainMenuButtons
    {
        public const string ModsName = "ButtonRevampMods";

        public static string Last = "not built";

        [HarmonyPatch(typeof(menuhandler), "Start")]
        [HarmonyPostfix]
        private static void AfterStart(menuhandler __instance) => TryBuild(__instance);

        [HarmonyPatch(typeof(menuhandler), nameof(menuhandler.LoadMainMenu))]
        [HarmonyPostfix]
        private static void AfterLoadMainMenu(menuhandler __instance) => TryBuild(__instance);

        public static void TryBuild(menuhandler menu)
        {
            if (menu == null) return;
            try
            {
                var settings = Find(menu.transform, "ButtonSettings");
                if (settings == null || settings.parent == null) { Last = "the main menu has no Settings button: left as it is"; return; }
                // the row: a copy of Settings, put in its place in the column by Tighten
                bool made = false;
                if (!(settings.parent.Find(ModsName) is RectTransform))
                {
                    Clone(settings, ModsName, "Mods", () => Net.ModsScreen.Open(null));
                    made = true;
                }
                Tighten(menu.transform, settings);
                Last = "built: Mods row between Settings and Exit";
                if (made) Plugin.Log.LogInfo("MainMenuButtons: " + Last);
            }
            catch (System.Exception e) { Last = "failed: " + e.Message; Plugin.Log.LogWarning("MainMenuButtons: " + e.Message); }
        }

        private static void Clone(RectTransform template, string name, string text, System.Action click)
        {
            var go = Object.Instantiate(template.gameObject, template.parent, false);
            go.name = name;
            foreach (var c in go.GetComponentsInChildren<languagetext>(true))
            {
                if (ls.l != null) { ls.l.texts.Remove(c); ls.l.tmps.Remove(c); }
                Object.DestroyImmediate(c);
            }
            // the same size, font and colour as Settings: only the word changes
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = text;
            foreach (var t in go.GetComponentsInChildren<Text>(true)) t.text = text;
            var b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();   // the Settings button's own listener goes with the clone
            b.onClick.AddListener(() => { try { click(); } catch (System.Exception e) { Plugin.Log.LogWarning("MainMenuButtons: " + e.Message); } });
        }

        /// The menu's column one even step apart (asked for 2026-11-11: "spaced out too far"; the game had 130 to 150
        /// between buttons 90 high; 2026-11-20 with the Mods row: "closer together and lower", 10 px between buttons).
        /// The lowest button (Exit) stays on the game's own spot by the bottom of the screen and the column goes up from
        /// it, and running it again changes nothing. The buttons sit under two parents, so this works in world space; the
        /// column is the buttons with Settings' left edge (the save screen has a Load button of its own elsewhere).
        public static float Step = 100f;
        public static readonly string[] Column = { "ButtonContinue", "ButtonMultiplayer", "ButtonStart", "ButtonLoad", "ButtonSettings", ModsName, "ButtonExit" };
        internal static void Tighten(Transform root, RectTransform settings)
        {
            if (Step <= 0f) return;
            var found = new System.Collections.Generic.List<RectTransform>();
            float x = settings.position.x, scale = settings.lossyScale.y;
            foreach (var name in Column)
                foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                    if (t.name == name && Mathf.Abs(t.position.x - x) < 2f * Mathf.Max(scale, 0.0001f)) { found.Add(t); break; }
            if (found.Count < 3) return;
            // already laid out (the menu is built again on every return to it): every button one step under the last
            bool even = true;
            for (int i = 1; i < found.Count && even; i++) even = Mathf.Abs(Bottom(found[i - 1]) - Bottom(found[i]) - Step * scale) < scale;
            if (even) return;
            // the lowest button (Exit) keeps the game's own spot; the rest go up from it one step each
            float step = Step * scale;
            float bottom = Bottom(found[found.Count - 1]);
            float first = bottom + (found.Count - 1) * step;   // the top button's bottom edge
            for (int i = 0; i < found.Count; i++)
            {
                var t = found[i];
                t.position += new Vector3(0f, first - i * step - Bottom(t), 0f);
            }
        }
        private static float Bottom(RectTransform t)
        {
            var c = new Vector3[4]; t.GetWorldCorners(c); return c[0].y;
        }

        private static RectTransform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true)) if (t.name == name) return t;
            return null;
        }

    }
}
