using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using static TLDRevamp.Fixes.StreamRace;

namespace TLDRevamp.Fixes
{
    internal static class StreamRaceTools
    {

        /// Test (bridge `streamrace arm` / `streamrace check <id>`): the race on demand, in the game's own streaming pass.
        /// arm: the car nearest the player gets one of its parts moved 400 m away (past itemRemoveDist) and the game's
        /// Upd runs at once — RemoveStuff stores and destroys the whole car (the part is far), PlaceStuff finds the car's
        /// fresh record near the player while the car still exists. check: a frame later, is the car's record there, and
        /// does the game place the car again?
        public static string Probe(string arg)
        {
            var parts = (arg ?? "").Split(' ');
            var s = savedatascript.s; var ips = itemPlaceRemoveScript.s; var pl = mainscript.s != null ? mainscript.s.player : null;
            if (s == null || ips == null || pl == null) return "{\"error\":\"no world\"}";
            if (parts[0] == "arm")
            {
                tosaveitemscript car = null; float best = 60f;
                if (parts.Length > 1) car = Net.Entities.RootOfNet(uint.Parse(parts[1]));   // a given shared car
                if (car == null)
                foreach (var it in s.items.Values)
                    if (it != null && it.car != null && it.transform.parent == null && !it.preDestroyed)
                    {
                        float d = (it.transform.position - pl.transform.position).magnitude;
                        if (d < best) { best = d; car = it; }
                    }
                if (car == null) return "{\"error\":\"no car within 60 m\"}";
                tosaveitemscript part = null; int members = 0;
                foreach (var c in car.GetComponentsInChildren<tosaveitemscript>())
                    if (c != car) { members++; if (part == null) part = c; }
                if (part == null) return "{\"error\":\"the car has no parts\"}";
                long kept0 = RecordsKept;
                part.transform.position += new UnityEngine.Vector3(400f, 0f, 0f);
                ips.Upd();
                bool rec = savedatascript.IndexOfID(s.data.itemData.items, car.idInSave, out _);
                return "{\"car\":" + Json.Str(car.name) + ",\"id\":" + car.idInSave + ",\"members\":" + members + ",\"part\":" + Json.Str(part.name) +
                       ",\"dist\":" + best.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",\"preDestroyed\":" + (car.preDestroyed ? "true" : "false") +
                       ",\"recordAfterUpd\":" + (rec ? "true" : "false") + ",\"recordsKept\":" + (RecordsKept - kept0) +
                       ",\"spawnDist\":" + ips.itemSpawnDist.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"removeDist\":" + ips.itemRemoveDist.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"enabled\":" + (Enabled ? "true" : "false") + "}";
            }
            if (parts[0] == "check" && parts.Length > 1 && uint.TryParse(parts[1], out uint id))
            {
                bool rec = savedatascript.IndexOfID(s.data.itemData.items, id, out _);
                bool live = s.items.TryGetValue(id, out var obj) && obj != null;
                return "{\"id\":" + id + ",\"record\":" + (rec ? "true" : "false") + ",\"live\":" + (live ? "true" : "false") +
                       (live ? ",\"name\":" + Json.Str(obj.name) + ",\"members\":" + (obj.GetComponentsInChildren<tosaveitemscript>().Length - 1) +
                               ",\"dist\":" + (obj.transform.position - pl.transform.position).magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "") +
                       ",\"recordsKept\":" + RecordsKept + ",\"idsKept\":" + IdsKept + "}";
            }
            return "{\"error\":\"streamrace arm | check <id>\"}";
        }
    }
}
