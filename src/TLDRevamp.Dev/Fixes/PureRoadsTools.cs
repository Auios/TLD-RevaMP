using System;
using UnityEngine;
using System.Collections.Generic;
using static TLDRevamp.Fixes.PureRoads;

namespace TLDRevamp.Fixes
{
    internal static class PureRoadsTools
    {
        public static bool Compare = false;
        internal static void CompareHeight(mapSettingScript map, Vector3d pos, List<roadGenScript.roadClass> live)
        {
            if (!Compare) return;
            try
            {
                var pure = new List<roadGenScript.roadClass>();
                foreach (var g in map.roadGens)
                {
                    if (g == null || !Usable(g)) return;
                    pure.AddRange(Near(g, pos, false, poiGenScript.alignToRoadEmu.anyRoad));
                }
                var sorted = new List<roadGenScript.roadClass>(live);
                RoadTieOrder.Sort(sorted);
                Check("height", pos, sorted, pure);
            }
            catch (Exception e) { Fail("compare height", e); }
        }

        private static void Check(string what, Vector3d pos, List<roadGenScript.roadClass> live, List<roadGenScript.roadClass> pure)
        {
            Compared++;
            var a = roadGenScript.NearestPointOnRoadBone(pos, live, out var pa);
            var b = roadGenScript.NearestPointOnRoadBone(pos, pure, out var pb);
            bool same = (a == null) == (b == null) && (a == null || (a.start.Equals(b.start) && a.end.Equals(b.end) && a.roadType == b.roadType)) && pa.Equals(pb);
            if (same) return;
            Differ++;
            var d = pa - pb; d.y = 0.0;
            double m = d.magnitude;
            if (m > WorstDifferM) WorstDifferM = m;
            LastDiffer = what + " at " + Fmt(pos) + ": live " + Desc(a) + " " + Fmt(pa) + ", pure " + Desc(b) + " " + Fmt(pb) + " (" + m.ToString("0.###") + " m)";
            if (Differ <= 20) Plugin.Log.LogInfo("PureRoads differ: " + LastDiffer);
        }

        private static string Desc(roadGenScript.roadClass r) => r == null ? "none" : r.roadType + " " + Fmt(r.start) + "-" + Fmt(r.end);

        private static string Fmt(Vector3d v) => "(" + v.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "," + v.z.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")";

    }
}
