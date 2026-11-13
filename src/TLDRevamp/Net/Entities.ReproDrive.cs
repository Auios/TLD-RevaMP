using HarmonyLib;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static float _reproDriveNext;
        internal static void ResetDriveSample() { _reproDriveNext = 0; }
        [HarmonyPatch(typeof(carscript), nameof(carscript.GetPlayerInput))]
        private static class ReproDriveInput
        {
            private static void Postfix(carscript __instance)
            {
                if (DevHooks.Playing)
                {
                    DevHooks.ReproDriving?.Invoke(__instance); return;
                }
                if (!ReproRecorder.Recording || !__instance.isPlayerDriving) return;
                var car = __instance;
                StuckWatch(car);
                int mask = (car.ihandbrakeBDown ? 1 : 0) | (car.igrDown ? 2 : 0) | (car.ignDown ? 4 : 0) | (car.ig1Down ? 8 : 0) |
                    (car.ig2Down ? 16 : 0) | (car.ig3Down ? 32 : 0) | (car.ig4Down ? 64 : 0) | (car.ig5Down ? 128 : 0) | (car.ig6Down ? 256 : 0) |
                    (car.iupshiftDown ? 512 : 0) | (car.idownshiftDown ? 1024 : 0) | (car.iignitionDown ? 2048 : 0) | (car.iLightsBDown ? 4096 : 0);
                if (mask == 0 && Time.realtimeSinceStartup < _reproDriveNext) return;
                _reproDriveNext = Time.realtimeSinceStartup + 0.1f;
                ReproRecorder.Add(new ReproRow { Kind = ReproKind.Drive, Net = TelemetryNet(car), X = car.ithrottle, Y = car.ibrake, Z = car.isteer,
                    A = car.iclutch, B = car.ihandbrake, C = mask, D = car.iignition ? 1 : 0, E = car.ihorn ? 1 : 0 }.WithCar(ReproCarRead(car)));
            }
        }
        private static double FiniteOrZero(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : v;
        /// What the car does with the input: engine, gear, the clutch and handbrake it applies, whether the body is frozen or asleep, and
        /// what the driven wheels do. A car that does not move while the engine runs shows here which of them it is.
        private static ReproCarState.State ReproCarRead(carscript car)
        {
            var s = new ReproCarState.State { EngineOn = car.Engine != null && car.Engine.running, Driving = car.driving, HandbrakeOn = car.BhandBrake, Gear = car.gear,
                ClutchPercent = ReproCarState.Percent(car.clutch), HandbrakePercent = ReproCarState.Percent(car.handbrake), Rpm = FiniteOrZero(car.avgRpm), Speed = FiniteOrZero(car.speed), WheelsLocked = car.whlocked };
            if (car.RB != null) { s.Kinematic = car.RB.isKinematic; s.Sleeping = car.RB.IsSleeping(); s.Frozen = car.RB.constraints != RigidbodyConstraints.None; }
            if (car.WHDriven != null)
                foreach (var w in car.WHDriven)
                {
                    if (w == null) continue;
                    s.DrivenWheels++;
                    if (w.isGrounded) s.GroundedDriven++;
                    if (System.Math.Abs(w.motorTorque) > 0.01f) s.MotorTorque = true;
                    if (w.brakeTorque > 1f) s.BrakeTorque = true;
                }
            return s;
        }
    }
}

namespace TLDRevamp
{
    internal static partial class DevHooks
    {
        internal static System.Action<carscript> ReproDriving;
    }
}
