using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityIgnitionKeyTools
    {
        public static string KeyTest(uint net, float secs)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null || e.Root.car == null) return "{\"error\":\"no such car here\"}";
            var c = e.Root.car;
            if (secs > 0f) { ObservedKeyTestCar = c; ObservedKeyTestUntil = Time.time + secs; }
            var eng = c.Engine;
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"ignition\":" + (c.ignition ? "true" : "false") + ",\"running\":" + (eng != null && eng.running ? "true" : "false") +
                   ",\"key\":" + (c.ignitionUsable != null ? "true" : "false") + ",\"sent\":" + EntitySeats.KeyHoldsSent + ",\"applied\":" + EntitySeats.KeyHoldsApplied + ",\"refused\":" + EntitySeats.KeyHoldsRefused + "}";
        }

        /// Test (bridge `mp key <net> <seconds>`): the local player holds car <net>'s ignition key for that long, as
        /// fpscontroller does while the use button is down on it (`mp key <net>`: the car's ignition and engine here).
        internal static float _keyTestUntil;
        internal static float ObservedKeyTestUntil { get => _keyTestUntil; set => _keyTestUntil = value; }
        internal static carscript _keyTestCar;
        internal static carscript ObservedKeyTestCar { get => _keyTestCar; set => _keyTestCar = value; }

        internal static void EndKeyTest(bool expire = false)
        {
            if (expire) _keyTestUntil = Time.time - 1;
            _keyTestCar = null;
        }
        internal static void KeyTestTick()
        {
            if (_keyTestCar == null) return;
            if (Time.time > _keyTestUntil || _keyTestCar.ignitionUsable == null) { _keyTestCar = null; return; }
            _keyTestCar.ignitionUsable.lastHoldTime = Time.time;
        }
    }
}
