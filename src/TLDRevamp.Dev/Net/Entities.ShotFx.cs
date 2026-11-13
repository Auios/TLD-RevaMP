using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityShotFxTools
    {

        /// Test: the bullet holes on object `net` on this machine (root-local positions), or within `r` m of a global
        /// point when net is 0, and tank holes on it.
        public static string Decals(uint net, double gx, double gy, double gz, float rad)
        {
            var names = new HashSet<string>();
            foreach (var d in mainscript.s.ShootDecals) if (d != null) names.Add(d.name + "(Clone)");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>(); int holes = 0;
            if (net != 0)
            {
                if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null) return "{\"error\":\"no such object here\"}";
                var root = e.Root.transform.root;
                foreach (var t in root.GetComponentsInChildren<Transform>())
                {
                    if (names.Contains(t.name)) { var lp = root.InverseTransformPoint(t.position); rows.Add("{\"name\":" + Json.Str(t.name) + ",\"on\":" + Json.Str(t.parent != null ? t.parent.name : "") + ",\"p\":[" + lp.x.ToString("F3", ci) + "," + lp.y.ToString("F3", ci) + "," + lp.z.ToString("F3", ci) + "]}"); }
                    var cap = t.GetComponent<tankcapscript>();
                    if (cap != null && cap.disableCollider) holes++;
                }
            }
            else
            {
                var at = mainscript.UnityPosFromGlobal(new Vector3d(gx, gy, gz));
                foreach (var t in Object.FindObjectsOfType<Transform>())
                {
                    if (!names.Contains(t.name) || (t.position - at).magnitude > rad) continue;
                    var g = mainscript.GlobalFromUnityPos(t.position);
                    rows.Add("{\"name\":" + Json.Str(t.name) + ",\"on\":" + Json.Str(t.parent != null ? t.parent.name : "") + ",\"p\":[" + g.x.ToString("F3", ci) + "," + g.y.ToString("F3", ci) + "," + g.z.ToString("F3", ci) + "]}");
                }
            }
            return "{\"decals\":[" + string.Join(",", rows) + "],\"holes\":" + holes + "}";
        }
    }
}
