using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityCarSignalsTools
    {

        /// Test hooks (bridge `set`): what the driver's hands do in the car the local player drives - brake pedal, horn,
        /// handbrake lever, ignition - entered where the keyboard's input lands (GetPlayerInput). No gear hook: the
        /// automatic gearbox shifts over it every frame (carsigtest run 1); gears are tested by what the gearbox does.
        /// TestRoll: the car gets this forward speed once (m/s) - a car rolling, to brake from.
        public static float TestBrake = default, TestRoll; public static bool TestHorn = default;
        public static int TestHandbrake = -1, TestIgnition = -1;

        [HarmonyLib.HarmonyPatch(typeof(carscript), nameof(carscript.GetPlayerInput))]
        internal static class SignalsTestInput
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(carscript __instance)
            {
                var c = __instance;
                if (!c.isPlayerDriving) return;
                if (TestBrake > 0f) c.ibrake = Mathf.Max(c.ibrake, TestBrake);
                if (TestHorn) c.ihorn = true;
                if (TestRoll > 0f && c.RB != null) { c.RB.velocity = c.transform.forward * TestRoll; TestRoll = 0f; }
                if (TestHandbrake >= 0 && (c.handbrake >= 0.5f) != (TestHandbrake == 1)) c.ihandbrakeBDown = true;
                if (TestIgnition >= 0) c.ignition = TestIgnition == 1;
            }
        }

        /// Test (bridge `mp carset <net> ignition 0|1`): a car's ignition switched with nobody in it (a parked car at rest).
        public static string CarSet(uint net, string what, int v)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null || e.Root.car == null) return "{\"error\":\"no such car\"}";
            var c = e.Root.car;
            if (what == "ignition") c.ignition = v == 1;
            else return "{\"error\":\"ignition only\"}";
            return "{\"ignition\":" + (c.ignition ? "true" : "false") + ",\"key\":" + EntityCarSignals.CarSignalKey(e) + "}";
        }
    }
}
