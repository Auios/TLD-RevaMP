using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityShotgunTools
    {
        internal static bool TakeLights() { bool lights = TestLights; TestLights = false; return lights; }
        public static float TestSteer = default;   // test hook (bridge `set`): the passenger's steering input
        public static bool TestLights;   // test hook: one press of the light switch
    }
}
