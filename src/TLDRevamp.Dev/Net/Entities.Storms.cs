using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityStormsTools
    {

        // ---- diagnostics / test hooks (bridge `mp storms`, `mp storms spawn gx gz`, `mp storms clear`, `mp storms timer`)
        public static string StormTest(string[] a)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            if (a.Length > 1 && a[1] == "spawn" && mainscript.s != null && itemdatabase.s != null && itemdatabase.s.storms.Length > 0)
            {
                var p = mainscript.UnityPosFromGlobal(new Vector3d(double.Parse(a[2], ic), mainscript.GlobalFromUnityPos(mainscript.s.player.transform.position).y, double.Parse(a[3], ic)));
                // the beta has no spawner in its scene (stormprobe: mainscript.sandStormSpawn null, none loaded): the
                // storm is made as its SpawnAt makes one
                if (mainscript.s.sandStormSpawn != null) mainscript.s.sandStormSpawn.SpawnAt(p);
                else Object.Instantiate(itemdatabase.s.storms[0], p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                return "{\"spawned\":true,\"spawner\":" + (mainscript.s.sandStormSpawn != null ? "true" : "false") + "}";
            }
            if (a.Length > 1 && a[1] == "prefab" && itemdatabase.s != null)
            {
                var rows2 = new List<string>();
                foreach (var go in itemdatabase.s.storms)
                {
                    var x = go != null ? go.GetComponent<sandstormscript>() : null;
                    if (x == null) { rows2.Add("null"); continue; }
                    rows2.Add("{\"name\":" + Json.Str(go.name) + ",\"prefabid\":" + x.prefabid + ",\"biome\":" + x.biome + ",\"lifeGain\":" + x.lifeGain.ToString("F4", ic) +
                              ",\"lifeLoss\":" + x.lifeLossOnOtherBiome.ToString("F4", ic) + ",\"destroyDist\":" + x.destroyDist.ToString("F0", ic) +
                              ",\"radius\":" + x.radius.ToString("F0", ic) + ",\"windForce\":" + x.windForce.ToString("F1", ic) + ",\"life\":" + x.life.ToString("F2", ic) +
                              ",\"size\":" + x.size.ToString("F2", ic) + ",\"speed\":" + x.speed.ToString("F2", ic) + ",\"started\":" + (x.started ? "true" : "false") + "}");
                }
                return "{\"prefabs\":[" + string.Join(",", rows2) + "],\"currentBiome\":" + (napszakvaltakozas.s != null ? napszakvaltakozas.s.CurrentBiome : -1) + "}";
            }
            if (a.Length > 3 && a[1] == "biome")
                return "{\"biome\":" + Fixes.Sandstorms.BiomeAt(mainscript.UnityPosFromGlobal(new Vector3d(double.Parse(a[2], ic), 0, double.Parse(a[3], ic)))) + "}";
            if (a.Length > 1 && a[1] == "sand" && mainscript.s != null)
            {
                // the nearest sand (biome 0) to the player: rings of 250 m out to 30 km
                var me = mainscript.GlobalFromUnityPos(mainscript.s.player.transform.position);
                for (int ring = 0; ring <= 120; ring++)
                {
                    int steps = Mathf.Max(1, ring * 8);
                    for (int k = 0; k < steps; k++)
                    {
                        double ang = k * System.Math.PI * 2 / steps, rr = ring * 250.0;
                        var g = new Vector3d(me.x + System.Math.Cos(ang) * rr, me.y, me.z + System.Math.Sin(ang) * rr);
                        if (terrainGenSettings.GetFirstBiomeInt(g) != 0) continue;
                        // the middle of the sand there: well inside it (the next 2 km on from the edge all sand)
                        bool deep = true;
                        for (int m = 1; m <= 8 && deep; m++)
                            deep = terrainGenSettings.GetFirstBiomeInt(new Vector3d(me.x + System.Math.Cos(ang) * (rr + m * 250), me.y, me.z + System.Math.Sin(ang) * (rr + m * 250))) == 0;
                        if (!deep) continue;
                        var c = new Vector3d(me.x + System.Math.Cos(ang) * (rr + 1000), me.y, me.z + System.Math.Sin(ang) * (rr + 1000));
                        return "{\"gx\":" + c.x.ToString("F1", ic) + ",\"gz\":" + c.z.ToString("F1", ic) + ",\"edgeM\":" + rr.ToString("F0", ic) + "}";
                    }
                }
                return "{\"error\":\"no sand within 30 km\"}";
            }
            if (a.Length > 1 && a[1] == "clear" && napszakvaltakozas.s != null)
            {
                foreach (var st in napszakvaltakozas.s.sandStorms.ToArray()) if (st != null) Object.Destroy(st.gameObject);
                return "{\"cleared\":true}";
            }
            if (a.Length > 1 && a[1] == "timer" && (mainscript.s == null || mainscript.s.sandStormSpawn == null)) return "{\"error\":\"no spawner\"}";
            if (a.Length > 1 && a[1] == "timer")
            {
                mainscript.s.sandStormSpawn.nextTime = 0f;   // the spawner's time is up now (as if its random wait ran out)
                return "{\"due\":true}";
            }
            var rows = new List<string>();
            if (napszakvaltakozas.s != null)
                foreach (var st in napszakvaltakozas.s.sandStorms)
                {
                    if (st == null) continue;
                    var g = mainscript.GlobalFromUnityPos(st.transform.position);
                    uint id = 0;
                    if (!TLDRevamp.Net.Entities.ObservedStormIds.TryGetValue(st, out id)) foreach (var kv in TLDRevamp.Net.Entities.ObservedStormById) if (kv.Value == st) { id = kv.Key; break; }
                    rows.Add("[" + id + "," + st.prefabid + "," + g.x.ToString("F2", ic) + "," + g.z.ToString("F2", ic) + "," + st.size.ToString("F3", ic) + "," +
                             st.speed.ToString("F2", ic) + "," + st.xdir.ToString("F3", ic) + "," + st.ydir.ToString("F3", ic) + "," + st.life.ToString("F3", ic) + "," +
                             st.distanceLerp.ToString("F3", ic) + "," + (Fixes.Sandstorms.FadeDist.TryGetValue(st, out var fd) ? fd : StormDist(st)).ToString("F0", ic) + "," + st.destroyDist.ToString("F0", ic) + "]");
                }
            var sp = mainscript.s != null ? mainscript.s.sandStormSpawn : null;
            var all = Resources.FindObjectsOfTypeAll<sandStormSpawnScript>();
            var spawners = new List<string>();
            foreach (var x in all) spawners.Add(Json.Str(x.name + (x.gameObject.scene.IsValid() ? "" : "(asset)") + (x.gameObject.activeInHierarchy ? "" : "(inactive)") + (x.enabled ? "" : "(disabled)") + " under " + (x.transform.parent != null ? x.transform.parent.name : "-")));
            return "{\"sandstorms\":" + TLDRevamp.Fixes.Sandstorms.Stats + ",\"spawnerObjects\":[" + string.Join(",", spawners) + "],\"stormPrefabs\":" + (itemdatabase.s != null && itemdatabase.s.storms != null ? itemdatabase.s.storms.Length : -1) + ",\"storms\":[" + string.Join(",", rows) + "],\"sent\":" + StormSyncsSent + ",\"applied\":" + StormSyncsApplied + ",\"made\":" + StormsMade +
                   ",\"removed\":" + StormsRemoved + ",\"snaps\":" + StormSnaps +
                   (sp != null ? ",\"spawner\":{\"radius\":" + sp.radius.ToString("F0", ic) + ",\"min\":" + sp.minTime.ToString("F0", ic) + ",\"max\":" + sp.maxTime.ToString("F0", ic) +
                                 ",\"next\":" + (sp.lastTime + sp.nextTime - Time.time).ToString("F0", ic) + "}" : "") + "}";
        }

        internal static readonly AccessTools.FieldRef<sandstormscript, float> StormDist = GameLookups.FieldRefAccess<sandstormscript, float>(typeof(sandstormscript), "currentDistance");
    }
}
