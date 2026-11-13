using static TLDRevamp.Net.Entities;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal static partial class EntityReproPlayback
    {
        internal static string ReproNotOwner() => DevHooks.Playing ? "{\"ok\":true,\"skipped\":true}" : "{\"error\":\"requires local owner\"}";
        private static carscript _reproDrive;
        private static ReproRow _reproDriveInput;
        private static float _reproDriveUntil;
        internal static void ReproDriveEnd() { if (_reproDrive != null) _reproDrive.ResetInput(); _reproDrive = null; ResetDriveSample(); }
        internal static string ReproDriveAction(ReproRow r)
        {
            var it = ReproTarget(r.Net);
            if (it == null || it.car == null || !ObservedEntities.TryGetValue(r.Net, out var e)) return "{\"error\":\"drive target absent\"}";
            if (r.X < 0 || r.X > 1 || r.Y < 0 || r.Y > 1 || r.Z < -1 || r.Z > 1 || r.A < 0 || r.A > 1 || r.B < 0 || r.B > 1 || r.C < 0 || r.C > 8191)
                return "{\"error\":\"invalid drive input\"}";
            if (!it.car.isPlayerDriving || e.Proxy || e.OwnerId != MyId) return ReproNotOwner();
            _reproDrive = it.car; _reproDriveInput = r; _reproDriveUntil = Time.realtimeSinceStartup + 0.35f; return "{\"ok\":true}";
        }
        internal static void ApplyDriveInput(carscript __instance)
        {
            if (__instance != _reproDrive || !__instance.isPlayerDriving || Time.realtimeSinceStartup > _reproDriveUntil) return;
            var c = __instance; var r = _reproDriveInput; int bits = (int)r.C;
            c.ithrottle = (float)r.X; c.ibrake = (float)r.Y; c.isteer = (float)r.Z; c.iclutch = (float)r.A; c.ihandbrake = (float)r.B;
            c.iignition = r.D != 0; c.ihorn = r.E != 0;
            c.ihandbrakeBDown = (bits & 1) != 0; c.igrDown = (bits & 2) != 0; c.ignDown = (bits & 4) != 0;
            c.ig1Down = (bits & 8) != 0; c.ig2Down = (bits & 16) != 0; c.ig3Down = (bits & 32) != 0;
            c.ig4Down = (bits & 64) != 0; c.ig5Down = (bits & 128) != 0; c.ig6Down = (bits & 256) != 0;
            c.iupshiftDown = (bits & 512) != 0; c.idownshiftDown = (bits & 1024) != 0; c.iignitionDown = (bits & 2048) != 0; c.iLightsBDown = (bits & 4096) != 0;
            _reproDriveInput.C = 0; return;
        }
    }
}
