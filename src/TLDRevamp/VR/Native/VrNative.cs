using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace TLDRevamp.VR.Native
{
    /// Native VR play, on top of the game's own VR code (docs/VR-NATIVE.md).
    ///
    /// The game's VR is a thin layer over flat play: the controllers feed the walk, the look and "use", the grip slides
    /// doors and levers, and every interaction ray comes from the head. This layer makes the hands the way you play:
    /// both hands grab, hold and throw items, the trigger uses what a hand points at or what it holds, A/X attaches and
    /// detaches parts, and the flat-screen camera effects are off while the headset is on.
    ///
    /// It never changes the player's saved settings: every flat-screen setting it turns off in VR is turned off only for
    /// the frame that reads it, and comes back the moment VR is off.
    internal static class VrNative
    {
        /// Settings → Revamp → VR.
        internal static bool Comfort = true;
        internal static bool Hands = true;

        private static int _frame = -1;
        private static bool _active;

        /// VR is on and the headset is really presenting (or a test drives the hands without one, see VrSim).
        internal static bool Active
        {
            get
            {
                int f = Time.frameCount;
                if (f != _frame) { _frame = f; _active = Compute(); }
                return _active;
            }
        }

        /// VR is on and a headset is presenting: the real thing, not a simulation.
        internal static bool Headset
        {
            get
            {
                try { return GameVr() && XRSettings.isDeviceActive; }
                catch { return false; }
            }
        }

        private static bool Compute()
        {
            if ((DevHooks.VrSimulation?.Invoke() ?? false)) return true;
            return Headset;
        }

        internal static bool GameVr()
        {
            var s = settingsscript.s;
            return s != null && s.S != null && s.S.BVR;
        }

        internal static fpscontroller Player => mainscript.s != null ? mainscript.s.player : null;

        /// A menu or the pause screen owns the controllers (the game's own menu laser).
        internal static bool MenuOpen => mainscript.s == null || !mainscript.s.crsrLocked || mainscript.s.pauseMenuOpen;

        internal static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        internal static string V(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
    }

    /// The per-frame parts that run after the game's own Update: the camera height, then the laser from the hand.
    internal sealed class VrNativeRunner : MonoBehaviour
    {
        /// Main-thread time of this runner's work, per part, summed since the last reset (`vrnative cost`).
        internal static readonly double[] CostMs = new double[4];
        internal static readonly string[] CostNames = { "update", "height", "sky", "hands" };
        internal static int CostFrames;
        private static readonly System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();

        private static void Lap(int part) { CostMs[part] += _sw.Elapsed.TotalMilliseconds; _sw.Restart(); }

        private void Update()
        {
            _sw.Restart();
            try { DevHooks.VrSimulationTick?.Invoke(); } catch { }
            try { VrPerf.Tick(); } catch (Exception e) { VrHands.Fault("perf", e); }
            Lap(0);
        }

        private void LateUpdate()
        {
            _sw.Restart();
            try { VrHeight.LateTick(); } catch (Exception e) { VrHands.Fault("height", e); }
            Lap(1);
            try { VrSky.LateTick(); } catch (Exception e) { VrSky.Error = e.GetType().Name + ": " + e.Message; VrHands.Fault("sky", e); }
            Lap(2);
            try { VrHands.LateTick(); } catch (Exception e) { VrHands.Fault("late", e); }
            Lap(3);
            CostFrames++;
        }
    }
}
