using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using static TLDRevamp.Fixes.FluidTypeGuard;

namespace TLDRevamp.Fixes
{
    internal static class FluidTypeGuardTools
    {

        /// Test (bridge `guardlab fluid`): the nearest tank within 10 m gets a record with an unknown type loaded, then an
        /// unknown type straight into its contents and its colour read — what threw, what was dropped.
        internal static string Test()
        {
            var pl = mainscript.s.player.transform.position;
            tankscript tank = null; float best = 100f;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<tankscript>())
            {
                float d = (t.transform.position - pl).sqrMagnitude;
                if (d < best && t.F != null && t.transform.root != mainscript.s.player.transform.root) { best = d; tank = t; }   // not the player's own
            }
            if (tank == null) return "{\"error\":\"no tank within 10 m\"}";
            long d0 = Dropped; string loadEx = "", readEx = "";
            var rec = new save_tankscript(tank, 0, 0);
            rec.type = new List<int>(rec.type ?? new int[0]) { 99 }.ToArray();
            rec.amount = new List<float>(rec.amount ?? new float[0]) { 1f }.ToArray();
            try { rec.Load(tank); } catch (System.Exception ex) { loadEx = ex.GetType().Name; }
            long d1 = Dropped;
            tank.F.fluids.Add(new mainscript.fluid((mainscript.fluidenum)99, 1f));
            try { tank.F.GetColor(); } catch (System.Exception ex) { readEx = ex.GetType().Name; }
            int left = 0; foreach (var f in tank.F.fluids) if ((int)f.type >= mainscript.s.fluids.Length) left++;
            tank.F.fluids.RemoveAll(f => (int)f.type >= mainscript.s.fluids.Length);   // the test's own leftovers
            return "{\"tank\":" + Json.Str(tank.transform.root.name) +
                   ",\"loadException\":" + Json.Str(loadEx) + ",\"droppedOnLoad\":" + (d1 - d0) +
                   ",\"readException\":" + Json.Str(readEx) + ",\"droppedOnRead\":" + (Dropped - d1) + ",\"unknownLeft\":" + left + "}";
        }
    }
}
