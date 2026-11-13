using System;
using System.Collections.Generic;
using static TLDRevamp.WorldGen.MainRoadOnlyRoads;

namespace TLDRevamp.WorldGen
{
    internal static class MainRoadOnlyRoadsTools
    {
        /// Every road generator of the map, plus any other roadGenScript in the scene (a generator the map does not list
        /// still makes roads if something drives it): its flags, whether it runs, its chunks and roads, and how many of
        /// its generated roads sit in chunks the game no longer keeps (those are never placed up close).
        internal static string Report(mapSettingScript map)
        {
            var gens = new List<roadGenScript>();
            if (map != null && map.roadGens != null) foreach (var g in map.roadGens) if (g != null && !gens.Contains(g)) gens.Add(g);
            int listed = gens.Count;
            foreach (var g in UnityEngine.Object.FindObjectsOfType<roadGenScript>(true)) if (g != null && !gens.Contains(g)) gens.Add(g);
            var sb = new System.Text.StringBuilder("{\"listed\":").Append(listed).Append(",\"generators\":[");
            for (int i = 0; i < gens.Count; i++)
            {
                var g = gens[i];
                int chunks = 0, roads = 0, generated = 0, placed = 0, outsideNeeded = 0, outsideGenerated = 0, offLineChunks = 0, offLineGenerated = 0;
                var offLine = new System.Text.StringBuilder();
                foreach (var kv in g.chunks)
                {
                    chunks++;
                    bool needed = g.chunksNeeded.Contains(kv.Key);
                    // a chunk off the main road's line (x != 0): in main road only mode the game never needs it, so a
                    // road generated in it is never placed, yet the distant ribbons (TerrainHorizon) would draw it
                    bool off = Math.Abs(kv.Key.x) > 0.5;
                    if (off) offLineChunks++;
                    foreach (var r in kv.Value.roads)
                    {
                        if (r == null) continue;
                        roads++;
                        if (r.generated) generated++;
                        if (!needed) { outsideNeeded++; if (r.generated) outsideGenerated++; }
                        if (off && r.generated)
                        {
                            offLineGenerated++;
                            if (offLineGenerated <= 6)
                            {
                                var a = r.start; var z = r.end;
                                if (offLine.Length > 0) offLine.Append(',');
                                offLine.Append("{\"chunk\":\"").Append(kv.Key.x.ToString("0")).Append(',').Append(kv.Key.z.ToString("0"))
                                  .Append("\",\"type\":").Append(Json.Str(r.roadType.ToString()))
                                  .Append(",\"from\":[").Append(a.x.ToString("0")).Append(',').Append(a.z.ToString("0"))
                                  .Append("],\"to\":[").Append(z.x.ToString("0")).Append(',').Append(z.z.ToString("0")).Append("]}");
                            }
                        }
                        foreach (var e in r.roadElements) if (e != null && e.obj != null) placed++;
                    }
                }
                // the roads the player can never reach: generated, so the distant ribbons draw them, but with road
                // pieces no inner chunk points at (the game only places pieces through its inner chunks), and pieces
                // in range of the player that are still not placed
                int unregistered = 0, unplacedInRange = 0;
                string unregisteredAt = "";
                foreach (var kv in g.chunks)
                {
                    if (!g.chunksNeeded.Contains(kv.Key)) continue;
                    var pointed = new HashSet<long>();
                    foreach (var ic in kv.Value.innerChunks)
                    {
                        bool inRange = false;
                        foreach (var ga in g.mapSetting.genArounds)
                        {
                            var d = ic.Key - mainscript.GlobalFromUnityPos(ga.upos); d.y = 0.0;
                            if (d.magnitude < g.roadPlaceDist) { inRange = true; break; }
                        }
                        foreach (var rc in ic.Value.roads)
                            foreach (int e in rc.roadElements)
                            {
                                pointed.Add(((long)rc.roadId << 32) | (uint)e);
                                if (inRange && rc.roadId < kv.Value.roads.Count && e < kv.Value.roads[rc.roadId].roadElements.Count
                                    && kv.Value.roads[rc.roadId].roadElements[e].obj == null) unplacedInRange++;
                            }
                    }
                    for (int ri = 0; ri < kv.Value.roads.Count; ri++)
                    {
                        var r = kv.Value.roads[ri];
                        if (r == null || !r.generated) continue;
                        for (int e = 0; e < r.roadElements.Count; e++)
                            if (!pointed.Contains(((long)ri << 32) | (uint)e))
                            {
                                unregistered++;
                                if (unregisteredAt.Length == 0 && r.roadElements[e].bonePos.Count > 0)
                                    unregisteredAt = r.roadElements[e].bonePos[0].x.ToString("0") + "," + r.roadElements[e].bonePos[0].z.ToString("0");
                            }
                    }
                }
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":").Append(Json.Str(g.gameObject.name))
                  .Append(",\"listed\":").Append(i < listed ? "true" : "false")
                  .Append(",\"active\":").Append(g.isActiveAndEnabled ? "true" : "false")
                  .Append(",\"hasMainRoad\":").Append(g.hasMainRoad ? "true" : "false")
                  .Append(",\"hasSideRoad\":").Append(g.hasSideRoad ? "true" : "false")
                  .Append(",\"chunkSize\":").Append(g.chunkSize.ToString(System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"needed\":").Append(g.chunksNeeded.Count)
                  .Append(",\"chunks\":").Append(chunks).Append(",\"roads\":").Append(roads)
                  .Append(",\"generated\":").Append(generated).Append(",\"placedElements\":").Append(placed)
                  .Append(",\"roadsOutsideNeeded\":").Append(outsideNeeded)
                  .Append(",\"generatedOutsideNeeded\":").Append(outsideGenerated)
                  .Append(",\"roadPlaceDist\":").Append(g.roadPlaceDist.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"unregisteredElements\":").Append(unregistered)
                  .Append(",\"unregisteredAt\":").Append(Json.Str(unregisteredAt))
                  .Append(",\"unplacedInRange\":").Append(unplacedInRange)
                  .Append(",\"offLineChunks\":").Append(offLineChunks)
                  .Append(",\"offLineGenerated\":").Append(offLineGenerated)
                  .Append(",\"offLine\":[").Append(offLine).Append(']')
                  .Append(",\"mainRoad\":").Append(Stats(g)).Append('}');
            }
            return sb.Append("]}").ToString();
        }
    }
}
