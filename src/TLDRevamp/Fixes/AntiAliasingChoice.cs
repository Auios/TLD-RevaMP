using HarmonyLib;
using UnityEngine;

namespace TLDRevamp.Fixes
{
    /// Anti-aliasing as two choices that combine: MSAA (off, 2x, 4x, 8x) and a filter on the finished picture (none,
    /// FXAA, SMAA, TAA tuned, TAA with the game's tuning). The game's one switch (Settings > Graphics > Antialiasing)
    /// still decides whether there is any anti-aliasing: when it is off, both are off, as the game does it. What the switch
    /// turns on by itself is 8x MSAA (settingsscript.RefreshInMain: QualitySettings.antiAliasing = 8) plus temporal AA on
    /// every post-processing layer (pplayermelle.TurnOn, from mainscript.SettingsRefreshed and pplayermelle.FStart):
    /// "MSAA 8x" + "TAA (game's tuning)" here is exactly that.
    ///
    /// Measured 2026-11-18 on a player's saves (1080p, RTX 4070, a car carrying ~960 mounted items and a pile of glass
    /// bottles in view; FrameProbe -frameprobe-aa2/-frameprobe-flicker):
    /// - 8x MSAA costs more the more is drawn close by: 17.3 ms a frame against 6.7 ms with none. 2x/4x: 9.6/10.1 ms.
    /// - The game's TAA has a jitter spread of 0.1: the camera barely moves between frames, so there is almost nothing to
    ///   blend and edges stay jagged. With the stack's defaults (jitter 0.75, stationary 0.95, motion 0.85, sharpness
    ///   0.25) TAA smooths like 8x MSAA for ~0.1 ms, but speckled glass textures shimmer (4% of the bottles' pixels change
    ///   visibly frame to frame, 0.05% without TAA); lower jitter that stops it also stops the smoothing.
    /// - SMAA/FXAA cost ~0.1 ms and never flicker; on top of MSAA they smooth what it leaves: MSAA 2x + SMAA had about
    ///   8x's edge smoothness at 9.3 ms, 4x + SMAA was smoother than 8x at 11.3 ms. The default is MSAA 2x + SMAA.
    /// - FSR 3.1 native AA (FsrNativeAa, AMD FidelityFX, MIT) does what tuned TAA does but follows each pixel with depth
    ///   and motion vectors: +0.3 ms, the least shimmer measured (0.02%), edges smoother than 8x MSAA. MSAA is not used
    ///   with it (FSR takes the finished picture; MSAA under it would only cost time).
    /// MSAA works here (every camera renders forward with allowMSAA). Not in VR: VrPerf manages anti-aliasing there.
    public static class AntiAliasingChoice
    {
        public static readonly string[] MsaaOptions = { "Off", "2x", "4x", "8x" };
        public static readonly string[] FilterOptions = { "None", "FXAA", "SMAA", "TAA (tuned)", "TAA (game's tuning)", "FSR 3.1 native AA" };
        public const int FsrFilter = 5;
        public const int DefaultMsaa = 1, DefaultFilter = 2;   // MSAA 2x + SMAA
        public static int Msaa = DefaultMsaa, Filter = DefaultFilter;
        public static long Applied;

        /// Temporal AA tuning: the post-processing stack's defaults (jitter spread, stationary blending, motion blending,
        /// sharpness), not the game's 0.1 jitter.
        internal const float TaaJitter = .75f, TaaStationary = .95f, TaaMotion = .85f, TaaSharpness = .25f;

        public static void SetMsaa(int v) { Msaa = Mathf.Clamp(v, 0, MsaaOptions.Length - 1); Apply(); }
        public static void SetFilter(int v) { Filter = Mathf.Clamp(v, 0, FilterOptions.Length - 1); Apply(); }

        /// The two choices from the single list the setting had before (2026-11-03 .. 11-18): 0 the game's own, 1 off,
        /// 2 FXAA, 3 SMAA, 4 TAA (the game's tuning), 5/6/7 MSAA 2x/4x/8x. Test builds of 2026-11-18 had 4 = TAA tuned and
        /// 8 = MSAA 2x + TAA tuned (tunedTaa). Returns false for 0 and unknown values (keep the defaults).
        internal static bool FromOldMode(int mode, bool tunedTaa, out int msaa, out int filter)
        {
            msaa = 0; filter = 0;
            switch (mode)
            {
                case 1: return true;
                case 2: filter = 1; return true;
                case 3: filter = 2; return true;
                case 4: filter = tunedTaa ? 3 : 4; return true;
                case 5: msaa = 1; return true;
                case 6: msaa = 2; return true;
                case 7: msaa = 3; return true;
                case 8 when tunedTaa: msaa = 1; filter = 3; return true;
                default: return false;
            }
        }

        private static int Samples => Filter == FsrFilter ? 0 : Msaa == 1 ? 2 : Msaa == 2 ? 4 : Msaa == 3 ? 8 : 0;

        /// Ours is in charge: the game's switch on, not VR. (Switch off: the game's own code turns everything off.)
        private static bool Active => mainscript.s != null && !mainscript.IsVR() &&
                                      settingsscript.s != null && settingsscript.s.S != null && settingsscript.s.S.BAntialiasing;

        internal static void Apply()
        {
            if (!Active) { FsrNativeAa.DetachAll(); return; }
            QualitySettings.antiAliasing = Samples;
            if (mainscript.s.pplayers != null)
                foreach (var pl in mainscript.s.pplayers) Layer(pl);
            Applied++;
        }

        // the post-processing stack is not referenced here (as in VR.Native.VrPerf): its layer and mode by reflection
        private static readonly System.Reflection.FieldInfo LayerField = GameLookups.Field(typeof(pplayermelle), "pplayer");
        private static System.Reflection.FieldInfo _modeField;

        private static void Layer(pplayermelle pl)
        {
            var layer = pl ? LayerField?.GetValue(pl) as Component : null;
            if (!layer) return;
            var cam = layer.GetComponent<Camera>();
            if (Filter == FsrFilter) FsrNativeAa.Attach(cam); else FsrNativeAa.Detach(cam);
            if (Filter == 3) { pl.TurnOn(TaaJitter, TaaStationary, TaaMotion, TaaSharpness); return; }
            if (Filter == 4)
            {
                var ms = mainscript.s;
                pl.TurnOn(ms.AntialiasingJitterSpread, ms.AntialiasingStationaryBlending, ms.AntialiasingMotionBlending, ms.AntialiasingSharpness);
                return;
            }
            if (_modeField == null) _modeField = GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessLayer:antialiasingMode", layer.GetType());
            if (_modeField == null) return;
            string name = Filter == 1 ? "FastApproximateAntialiasing" : Filter == 2 ? "SubpixelMorphologicalAntialiasing" : "None";
            _modeField.SetValue(layer, System.Enum.Parse(_modeField.FieldType, name));
        }

        [HarmonyPatch(typeof(mainscript), nameof(mainscript.SettingsRefreshed))]
        private static class AfterSettings
        {
            [HarmonyPostfix] private static void Postfix() => Apply();
        }

        [HarmonyPatch(typeof(settingsscript), "RefreshInMain")]
        private static class AfterRefreshInMain
        {
            [HarmonyPostfix] private static void Postfix() { if (Active) QualitySettings.antiAliasing = Samples; }
        }

        [HarmonyPatch(typeof(pplayermelle), nameof(pplayermelle.FStart))]
        private static class NewLayer
        {
            [HarmonyPostfix] private static void Postfix(pplayermelle __instance) { if (Active) Layer(__instance); }
        }
    }
}
