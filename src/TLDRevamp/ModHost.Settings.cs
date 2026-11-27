using System;
using UnityEngine;

namespace TLDRevamp
{
    /// A Loader mod's settings window, drawn as TLD Loader 3.0.3's mod list draws it (ModList.RenderSettings, read from its
    /// decompile; RevaMP's own code): a draggable window 500 wide and 70 % of the screen tall in the middle, titled "Mod
    /// Config for <Name>", an x at its top right, and under its 40 px title a scroll view as tall as the mod's
    /// ConfigScrollHeight, in which the mod's Config() draws at its own positions (SettingAPI's controls, plain GUI or
    /// GUILayout) in the default skin, sliders on a white track. A mod's Config() thus sees what it saw under TLD Loader:
    /// the same window, the same coordinates, Event.current set.
    public static partial class ModHost
    {
        private const int ConfigWindowId = 0x7D1D0;   // not 0: a mod's own window (GUI.Window(0, ...)) keeps its id
        private static Rect _configRect;
        private static Vector2 _configScroll;
        private static Texture2D _sliderTrack, _windowFill;
        private static GUIStyle _windowStyle;

        static partial void ConfigWindowReset() { _configRect = default; _configScroll = Vector2.zero; }

        static partial void DrawConfigWindow(Loaded l)
        {
            if (_configRect == default)
            {
                float h = Screen.height * 0.7f;
                _configRect = new Rect(Screen.width / 2f - 250f, Screen.height / 2f - h / 2f, 500f, h);
            }
            if (_sliderTrack == null)
            {
                _sliderTrack = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _sliderTrack.SetPixel(0, 0, Color.white); _sliderTrack.Apply();
            }
            if (_windowStyle == null)
            {
                // a solid window, as TLD Loader's themed one (Unity's default window is see-through: the mods list read through it)
                _windowFill = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _windowFill.SetPixel(0, 0, new Color(0.11f, 0.12f, 0.13f, 0.98f)); _windowFill.Apply();
                _windowStyle = new GUIStyle(GUI.skin.window);
                _windowStyle.normal.background = _windowStyle.onNormal.background = _windowFill;
                _windowStyle.normal.textColor = _windowStyle.onNormal.textColor = Color.white;
            }
            var track = GUI.skin.horizontalSlider.normal.background;
            GUI.skin.horizontalSlider.normal.background = _sliderTrack;   // TLD Loader's; put back after, the game's skin is shared
            try { _configRect = GUI.Window(ConfigWindowId, _configRect, id => ConfigWindow(l), "Mod Config for " + l.Name, _windowStyle); }
            finally { GUI.skin.horizontalSlider.normal.background = track; }
        }

        private static void ConfigWindow(Loaded l)
        {
            if (GUI.Button(new Rect(_configRect.width - 40f, -10f, 40f, 40f), "<b>x</b>")) { _configId = null; return; }
            float height = 2000f;
            try { height = Convert.ToSingle(l.Mod.GetType().GetProperty("ConfigScrollHeight")?.GetValue(l.Mod, null) ?? 2000f); }
            catch { }
            _configScroll = GUI.BeginScrollView(new Rect(0f, 40f, _configRect.width - 10f, _configRect.height - 40f), _configScroll,
                new Rect(0f, 0f, _configRect.width - 25f, height));
            try { Call(l, l.Config, "Config"); }
            finally { GUI.EndScrollView(); }
            GUI.DragWindow(new Rect(0f, 0f, _configRect.width, 40f));
        }
    }
}
