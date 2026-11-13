using System;
using System.Text;
using UnityEngine;
using static TLDRevamp.Cinematic.CinematicCamera;

namespace TLDRevamp.Cinematic
{
    internal static class CinematicCameraTools
    {
        private static readonly CinematicPlayback Playback = new CinematicPlayback();

        private static CinematicShot _shot;

        private static string _shotError = "";

        /// The next cut time in the take being played; double.MaxValue when the take has no more cuts.
        private static double _nextCut = double.MaxValue;

        /// The take currently loaded, if any.
        public static CinematicShot CurrentShot => _shot;

        /// Loads a shot list from text. The text is parsed by the pure codec; nothing is executed.
        public static string LoadShot(string text)
        {
            string error;
            var shot = CinematicShotCodec.Parse(text, out error);
            if (shot == null) { _shotError = error; return "{\"error\":" + Json.Str(error) + "}"; }
            _shot = shot;
            _shotError = "";
            Playback.Time = 0;
            Playback.Playing = true;
            Playback.Speed = 1f;
            _nextCut = shot.NextCut(0.0) ?? double.MaxValue;
            LastNotice = "shot list loaded (" + shot.Steps.Count + " steps)";
            return ShotStatus();
        }

        public static string UnloadShot()
        {
            _shot = null;
            Playback.Playing = false;
            Playback.Time = 0;
            _nextCut = double.MaxValue;
            return ShotStatus();
        }

        /// Applies the take's state at the playhead.
        ///
        /// The positions in a shot list are **global** coordinates - the ones the game's own map and coordinates
        /// show, and the ones that stay the same when the world shifts by two kilometres. They are converted to the
        /// game's internal coordinates here, so a take written today still frames the same place tomorrow.
        private static void ApplyShot(CinematicShot shot, double now)
        {
            var move = shot.LastOf("move", now);
            if (move != null && move.Values.Length >= 6)
            {
                CinematicCamera.ApplyPose(ToUnity(move.Values[0], move.Values[1], move.Values[2]),
                    (float)move.Values[3], (float)move.Values[4], (float)move.Values[5]);
            }
            var target = shot.LastOf("target", now);
            if (target != null && target.Values.Length >= 3)
            {
                var unity = ToUnity(target.Values[0], target.Values[1], target.Values[2]);
                CinematicCamera.ApplyTarget(unity);
            }
            var speed = shot.LastOf("speed", now);
            if (speed != null && speed.Values.Length >= 1) Playback.Speed = (float)speed.Values[0];
        }

        /// A global position (the world's own coordinates) in the game's internal ones.
        private static Vector3 ToUnity(double x, double y, double z)
        {
            try
            {
                if (mainscript.s == null) return new Vector3((float)x, (float)y, (float)z);
                return mainscript.UnityPosFromGlobal(new Vector3d(x, y, z));
            }
            catch { return new Vector3((float)x, (float)y, (float)z); }
        }

        /// Where the camera's own screenshots go: a local folder, never in a report, never shared.
        /// The repo's telemetry deliberately holds no screenshots (docs/TELEMETRY.md), so the camera writes
        /// them somewhere of its own, and only when the player asks for one.
        public static string ShotFolder => System.IO.Path.Combine(
            BepInEx.Paths.BepInExRootPath, "tldrevamp-cinematic");

        /// Takes a screenshot of the cinematic camera's view. The file is written where the player can find it,
        /// and the path is returned so a tool can pick it up.
        ///
        /// The name is made of the camera's own state, never of anything the player typed: a screenshot's file
        /// name ends up in a folder listing, and a name typed by a player would carry whatever they typed.
        public static string Screenshot()
        {
            try
            {
                System.IO.Directory.CreateDirectory(ShotFolder);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                string name = "shot-" + stamp + ".png";
                string path = System.IO.Path.Combine(ShotFolder, name);
                UnityEngine.ScreenCapture.CaptureScreenshot(path);
                LastNotice = "screenshot saved: " + name;
                return "{\"path\":" + Json.Str(path) + ",\"name\":" + Json.Str(name) + "}";
            }
            catch (Exception e)
            {
                return "{\"error\":" + Json.Str("the screenshot could not be saved: " + e.Message) + "}";
            }
        }

        /// The shot list's state, as JSON.
        public static string ShotStatus()
        {
            var sb = new StringBuilder();
            sb.Append("{\"loaded\":").Append(_shot != null ? "true" : "false");
            sb.Append(",\"steps\":").Append(_shot != null ? _shot.Steps.Count : 0);
            sb.Append(",\"duration\":").Append(_shot != null ? _shot.Duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "0");
            sb.Append(",\"playing\":").Append(Playback.Playing ? "true" : "false");
            sb.Append(",\"time\":").Append(Playback.Time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"speed\":").Append(Playback.Speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"version\":").Append(_shot != null ? _shot.Version : CinematicShotFormat.CurrentVersion);
            sb.Append(",\"error\":").Append(Json.Str(_shotError ?? ""));
            sb.Append('}');
            return sb.ToString();
        }

        internal static void Reset(bool starting)
        {
            Playback.Playing = false;
            if (starting) Playback.Time = 0;
        }

        internal static void Tick(float dt)
        {
            if (Playback.Playing && CurrentShot != null)
            {
                Playback.Advance(dt, CurrentShot, out bool ended);
                // A cut is a hard jump in the take: the playhead moves to the cut and the shot at that moment
                // takes over. The cut itself carries the time it happens at, so the take's own state at the
                // cut is what the camera shows.
                if (Playback.Time >= _nextCut)
                {
                    _nextCut = CurrentShot.NextCut(Playback.Time) ?? double.MaxValue;
                    Playback.JumpToNextCut(CurrentShot, Playback.Time - 0.0001);
                }
                ApplyShot(CurrentShot, Playback.Time);
                if (ended) LastNotice = "the take has ended";
            }
        }
    }
}
