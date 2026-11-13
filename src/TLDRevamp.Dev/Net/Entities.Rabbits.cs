using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityRabbitsTools
    {

        /// Bridge `mp rabbitdo <net> roar|attack|evil|emp|eyes|ears|size <s>|push <vx> <vy> <vz>|aim <gx> <gz>`: the owner's
        /// rabbit does it the game's way (test staging).
        public static string RabbitDo(string[] a)
        {
            if (a.Length < 3 || !uint.TryParse(a[1], out uint net) || !ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || NyOf(e) == null) return "{\"error\":\"no rabbit\"}";
            var ny = NyOf(e);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            switch (a[2])
            {
                case "roar": ny.Roar(); break;
                case "attack": ny.Attack(); break;
                case "evil": ny.GetEvil(natural: true); break;
                case "emp": ny.GetEvil(natural: false); break;
                case "eyes": ny.senses.LooseEyes(); break;
                case "ears": ny.senses.LooseEars(); break;
                case "size": ny.started = false; ny.SetBasedOnSize(float.Parse(a[3], ci)); break;
                case "push": { var rb = e.Root.GetComponent<Rigidbody>(); if (rb != null) rb.velocity = new Vector3(float.Parse(a[3], ci), float.Parse(a[4], ci), float.Parse(a[5], ci)); break; }
                case "charge":
                {
                    var m = ny.move;
                    if (m == null) return "{\"error\":\"no move\"}";
                    TLDRevamp.Net.Entities.ObservedRabCharges.TryGetValue(m, out var was);
                    SetRabbitCharge(m, new RabCharge { At = new Vector3d(double.Parse(a[3], ci), 0, double.Parse(a[4], ci)), Speed = float.Parse(a[5], ci),
                                                    Until = Time.time + float.Parse(a[6], ci), OwnSpeed = was != null ? was.OwnSpeed : m.speed });
                    break;
                }
                case "aim": if (ny.AI != null) ny.AI.interestPos = mainscript.UnityPosFromGlobal(new Vector3d(double.Parse(a[3], ci), mainscript.GlobalFromUnityPos(ny.transform.position).y, double.Parse(a[4], ci))); break;
                default: return "{\"error\":\"what?\"}";
            }
            return RabbitJson(e);
        }

        /// Bridge `mp body <net>`: the object's root body here - for crash checks (velocity, spin, which way is up).
        public static string BodyState(uint net, string[] a = null)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null) return "{\"error\":\"no such object here\"}";
            // `mp body <net> launch <m/s>`: the owner sends it off forwards at that speed (a car rolling into something)
            if (a != null && a.Length > 3 && a[2] == "launch")
            {
                var b = e.Root.GetComponent<Rigidbody>();
                if (e.Proxy || b == null || b.isKinematic) return "{\"error\":\"not simulated here\"}";
                var fw = Vector3.ProjectOnPlane(e.Root.transform.forward, Vector3.up).normalized;
                b.velocity = fw * float.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture);
            }
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var rb = e.Root.GetComponent<Rigidbody>(); var t = e.Root.transform;
            var g = mainscript.GlobalFromUnityPos(t.position);
            Vector3 v = rb != null ? (rb.isKinematic ? e.LastVel : rb.velocity) : Vector3.zero, w = rb != null && !rb.isKinematic ? rb.angularVelocity : Vector3.zero;
            System.Func<Vector3, string> V = x => "[" + x.x.ToString("F3", ci) + "," + x.y.ToString("F3", ci) + "," + x.z.ToString("F3", ci) + "]";
            return "{\"net\":" + net + ",\"owner\":" + e.OwnerId + ",\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"kin\":" + (rb != null && rb.isKinematic ? "true" : "false") +
                   ",\"mass\":" + (rb != null ? rb.mass : 0f).ToString("F1", ci) + ",\"pos\":[" + g.x.ToString("F3", ci) + "," + g.y.ToString("F3", ci) + "," + g.z.ToString("F3", ci) + "]" +
                   ",\"vel\":" + V(v) + ",\"spin\":" + V(w) + ",\"up\":" + V(t.up) + ",\"fwd\":" + V(t.forward) + ",\"t\":" + Time.realtimeSinceStartup.ToString("F3", ci) + "}";
        }
    }
}
