using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityHitchTools
    {

        public static string TestHitch(uint carNet, uint trailerNet, bool connect)
        {
            if (!ObservedEntities.TryGetValue(carNet, out var car) || !ObservedEntities.TryGetValue(trailerNet, out var tr) || !Resolve(car) || !Resolve(tr) || !HitchTrailer(tr)) return "{\"error\":\"no car or trailer\"}";
            var at = tr.Root.attachable;
            if (connect && at.attached) return "{\"already\":true}";
            partslotscript slot = at.attached ? at.slot : null;
            if (connect) foreach (var s in car.Root.partslotscripts) if (s != null && s.T != null && !s.hasPart() && s.CanCraft(at)) { slot = s; break; }
            if (slot == null) return "{\"error\":\"no hitch slot\"}";
            RequestHitch(tr, car, slot, connect, releaseBrake: true);
            if (car.OwnerId == MyId && tr.OwnerId == MyId && !tr.HitchWaiting) HitchCommandTick();
            if (connect && at.attached) return "{\"requested\":true,\"hitched\":" + Json.Str(slot.name) + "}";
            return "{\"requested\":true}";
        }
        public static string TestHitchState(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var tr) || !Resolve(tr) || !HitchTrailer(tr)) return "{\"error\":\"no trailer\"}";
            var at = tr.Root.attachable; var car = HitchCar(at);
            return "{\"attached\":" + (at.attached ? "true" : "false") + ",\"car\":" + (car != null ? car.NetId : 0) + ",\"owner\":" + tr.OwnerId +
                ",\"epoch\":" + tr.Epoch + ",\"waiting\":" + (tr.HitchWaiting ? "true" : "false") + ",\"joint\":" + (at.J != null ? "true" : "false") +
                ",\"trailerProxy\":" + (tr.Proxy ? "true" : "false") + ",\"carProxy\":" + (car == null || car.Proxy ? "true" : "false") + "}";
        }
    }
}
