using System.Linq;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityBloodTools
    {

        /// Test (bridge `mp blood <net> [dirty|clean]`): the car's blood here, made dirty or washed the game's way.
        public static string BloodTest(uint net, string what)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null || e.Root.car == null) return "{\"error\":\"no such car here\"}";
            var w = e.Root.car.blood;
            if (w == null) return "{\"error\":\"this car has no blood\"}";
            if (what == "dirty") w.GotDirty();
            else if (what == "clean") w.Wash(true, 1, 2f, null);   // past repairValue 1: GotClean, as a sponge's last stroke
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"blood\":" + (w.ON ? "true" : "false") + ",\"cleanedOnCopy\":" + BloodCleanedOnCopy +
                   ",\"sent\":" + BloodCleanSent + ",\"applied\":" + BloodCleanApplied + "}";
        }
    }
}
