using System;
using System.Collections.Generic;
using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;
using DoorRef = TLDRevamp.Net.Entities.DoorRef;

namespace TLDRevamp.Net
{
    /// Test: a car's doors (hood, trunk, doors — door_rot / door_move driven by inputconnect from a usable). A player
    /// opens one through its usable (a click turns it, a drag slides it); the usable sync carries it to the others.
    internal static partial class EntityDoors
    {
        /// Plugin LateUpdate: the car-frame positions `mp relpos` asked for, as they will be drawn this frame.
        internal static void LateSample()
        {
            RestoreStoredDoorAttaches();
            Entities.TelemetryRender();
            DevHooks.EntityLateSample?.Invoke();
        }

        internal static List<DoorRef> DoorsOf(Ent e)
        {
            var list = new List<DoorRef>();
            if (e == null || e.Root == null) return list;
            // car doors, hood, trunk: a slide usable that swings its TDoor between two poses (usablescript.Slide)
            // (most cars: a rotatable usable on the hinge, dragged by the player — usablescript.Rot turns its RT1)
            foreach (var u in e.Root.GetComponentsInChildren<usablescript>(true))
            {
                if (u.slideAble && u.TDoor != null) list.Add(new DoorRef { U = u, T = u.TDoor, Name = u.TDoor.name });
                else if (u.rotateAble && u.RT1 != null && (u.maxX - u.minX > 20f || u.maxY - u.minY > 20f))
                    list.Add(new DoorRef { U = u, T = u.RT1, Name = u.RT1.name });
            }
            foreach (var ic in e.Root.GetComponentsInChildren<inputconnect>(true))
            {
                if (ic.Sdoor_rot == null && ic.Sdoor_move == null) continue;
                usablescript u = ic.turnOverride;
                if (u == null && ic.IUsableV0To1 != null) foreach (var x in ic.IUsableV0To1) if (x != null) { u = x; break; }
                if (u == null && ic.IUsableV1To1 != null) foreach (var x in ic.IUsableV1To1) if (x != null) { u = x; break; }
                if (u != null) list.Add(new DoorRef { Ic = ic, U = u, T = ic.Sdoor_rot != null ? ic.Sdoor_rot.transform : ic.Sdoor_move.transform, Name = ic.name });
            }
            return list;
        }

        /// `mp cardoors <net>`: each door — its name, the usable's state (turn state, slide value) and the door's.
        internal static string CarDoors(uint net)
        {
            if (!Entities.TryGetEntity(net, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder("{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"doors\":[");
            int n = 0;
            foreach (var d in DoorsOf(e))
            {
                // what a player sees: the door's pose relative to the car (degrees off its closed pose here)
                var lr = d.T.localRotation; var lp = d.T.localPosition;
                if (n++ > 0) sb.Append(',');
                sb.Append("{\"name\":" + Json.Str(d.Name) + ",\"x\":" + d.U.xRot.ToString("F1", ci) + ",\"y\":" + d.U.yRot.ToString("F1", ci) + ",\"turn\":" + (d.U.turnable ? d.U.currentTurnState : -1) +
                          ",\"slide\":" + d.U.slideValue.ToString("F2", ci) +
                          ",\"rot\":[" + lr.eulerAngles.x.ToString("F1", ci) + "," + lr.eulerAngles.y.ToString("F1", ci) + "," + lr.eulerAngles.z.ToString("F1", ci) + "]" +
                          ",\"pos\":[" + lp.x.ToString("F3", ci) + "," + lp.y.ToString("F3", ci) + "," + lp.z.ToString("F3", ci) + "]}");
            }
            sb.Append("],\"usables\":[");
            n = 0;
            foreach (var u in e.Root.GetComponentsInChildren<usablescript>(true))
            {
                if (n++ > 0) sb.Append(',');
                sb.Append(Json.Str(u.name + (u.slideAble ? " slide" : "") + (u.TDoor != null ? " tdoor" : "") + (u.rotateAble ? " rot" : "") + (u.turnable ? " turn" : "") + (u.RT1 != null ? " rt1" : "")));
            }
            sb.Append("],\"parts\":" + e.Root.GetComponentsInChildren<partconditionscript>(true).Length);
            return sb.Append("}").ToString();
        }

        internal static string Food(uint net)
        {
            if (!Entities.TryGetEntity(net, out var e) || e.Root == null) return "{\"gone\":true}";
            var ed = e.Root.GetComponentInChildren<ediblescript>(true);
            if (ed == null) return "{\"error\":\"not food\"}";
            int shown = 0, all = 0;
            if (ed.parts != null) foreach (var p in ed.parts) { all++; if (p != null && p.activeSelf) shown++; }
            return "{\"gone\":false,\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"name\":" + Json.Str(e.Root.name) + ",\"index\":" + ed.index +
                   ",\"hp\":" + ed.hp.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"parts\":[" + shown + "," + all + "],\"continous\":" +
                   (ed.continous ? "true" : "false") + ",\"more\":" + (ed.moreParts ? "true" : "false") + ",\"destroyOnEnd\":" + (ed.destroyOnEnd ? "true" : "false") + "}";
        }

        /// Test: a rabbit (aiscript). `mp rabbit <net>`: fleeing?, its stimulus balance, global position and facing.
        internal static string Rabbit(uint net)
        {
            if (!Entities.TryGetEntity(net, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            var a = e.Root.GetComponentInChildren<aiscript>(true);
            if (a == null) return "{\"error\":\"no aiscript\"}";
            if (EntityRabbits.NyOf(e) != null) return EntityRabbits.RabbitJson(e);   // fleeing, ff, stimuli, pos, fwd are in it (rabbitflee.py)
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var g = mainscript.GlobalFromUnityPos(a.transform.position); var f = a.transform.forward;
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"fleeing\":" + (a.fleeing ? "true" : "false") + ",\"ff\":" + a.flightFight.ToString("F2", ci) +
                   ",\"stimuli\":" + (a.senses != null ? a.senses.stimuls.Count : -1) +
                   ",\"pos\":[" + g.x.ToString("F2", ci) + "," + g.y.ToString("F2", ci) + "," + g.z.ToString("F2", ci) + "],\"fwd\":[" + f.x.ToString("F2", ci) + "," + f.z.ToString("F2", ci) + "]}";
        }
    }

    public static partial class Entities
    {
        internal sealed class DoorRef { public inputconnect Ic; public usablescript U; public Transform T; public string Name; }
    }
}
