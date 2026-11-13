using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityTumbleweedsTools
    {

        // ---- diagnostics / test hooks (bridge `mp tumble`, `mp tumble spawn` (the limit applies), `mp tumble due`)
        public static string TumbleTest(string[] a)
        {
            var m = thumbleweedMain.s;
            if (m == null) return "{\"error\":\"no tumbleweed spawner on this map\"}";
            string did = "null";
            if (a.Length > 1 && a[1] == "spawn") { var was = m.nowSpawned; m.Spawn(false); did = m.nowSpawned != was ? "\"spawned\"" : "\"none\""; }
            if (a.Length > 1 && a[1] == "due") { m.spawnTime = 0f; did = "\"due\""; }
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>();
            var all = new HashSet<thumbleweedScript>(Object.FindObjectsOfType<thumbleweedScript>());
            foreach (var e in ObservedEntities.Values) if (e.Root != null && e.Root.thumbleweed != null) all.Add(e.Root.thumbleweed);
            foreach (var t in all)
            {
                var it = t.GetComponent<tosaveitemscript>();
                uint net = 0; int owner = -1;
                foreach (var e in ObservedEntities.Values) if (e.Root == it) { net = e.NetId; owner = e.OwnerId; break; }
                var g = mainscript.GlobalFromUnityPos(t.transform.position);
                var v = t.rb != null ? t.rb.velocity : Vector3.zero;
                rows.Add("[" + net + "," + owner + "," + g.x.ToString("F2", ic) + "," + g.y.ToString("F2", ic) + "," + g.z.ToString("F2", ic) + "," +
                         v.magnitude.ToString("F2", ic) + "," + (t.bwind ? "true" : "false") + "," + (IsProxy(t) ? "true" : "false") + "," +
                         (t.gameObject.activeInHierarchy ? "true" : "false") + "]");
            }
            var srv = new List<string>();
            var peerPos = new List<KeyValuePair<int, Vector3d>>();
            if (IsHost)
                foreach (var e0 in ObservedEntities.Values)
                {
                    if (e0.Root == null || e0.Root.thumbleweed == null || !ObserveTumbleServer(e0.NetId, out var se)) continue;
                    var d = RecordCodec.Decode(se.Record ?? new byte[0]);
                    var known = new List<string>();
                    if (Mp.Server != null) { Mp.Server.ReadyPeers(peerPos); foreach (var kv in peerPos) known.Add("[" + kv.Key + "," + (KnownBy(kv.Key).Contains(se.NetId) ? 1 : 0) + "," + kv.Value.x.ToString("F0", ic) + "," + kv.Value.z.ToString("F0", ic) + "]"); }
                    srv.Add("{\"net\":" + se.NetId + ",\"owner\":" + se.OwnerId + ",\"where\":[" + se.Where.x.ToString("F0", ic) + "," + se.Where.z.ToString("F0", ic) + "],\"hasState\":" + (se.HasState ? "true" : "false") +
                            ",\"records\":" + (d != null ? d.items.Count : -1) + ",\"recId\":" + (d != null && d.items.Count > 0 ? d.items[0].id : -1) + ",\"recBytes\":" + (se.Record != null ? se.Record.Length : -1) + ",\"peers\":[" + string.Join(",", known) + "]}");
                }
            return "{\"server\":[" + string.Join(",", srv) + "],\"did\":" + did + ",\"tumbleweeds\":[" + string.Join(",", rows) + "],\"listed\":" + m.thumbleweeds.Count + ",\"near\":" + TumbleweedsNear(m) +
                   ",\"limit\":" + m.thumbleweedLimit + ",\"dist\":[" + m.spawnMinDist.ToString("F0", ic) + "," + m.spawnMaxDist.ToString("F0", ic) + "]" +
                   ",\"next\":" + (m.spawnTime - Time.time).ToString("F0", ic) + ",\"shared\":" + TumbleShared + ",\"limited\":" + TumbleLimited +
                   ",\"biome\":" + (mainscript.s != null ? mainscript.s.GetCurrentBiome() : -1) + "}";
        }
    }
}
