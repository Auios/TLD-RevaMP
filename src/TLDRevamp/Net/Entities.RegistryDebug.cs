using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        // Requested snapshots only: do not resolve, rebuild a cache, sample physics, or send anything here.
        public static string RegistryList()
        {
            int deadItems = 0, deadWheels = 0, deadEngines = 0, goneWheels = 0;
            foreach (var go in ProxyItems) if (go == null) deadItems++;
            foreach (var kv in ProxyWheelOwner)
            {
                if (kv.Key == null) deadWheels++;
                if (!ByNet.TryGetValue(kv.Value.NetId, out var now) || now != kv.Value) goneWheels++;
            }
            foreach (var kv in ProxyEngineOwner) if (kv.Key == null) deadEngines++;
            var markers = new List<string>();
            foreach (var go in ProxyItems) markers.Add("{\"id\":" + go.GetInstanceID() + ",\"dead\":" + (go == null ? "true" : "false") + "}");
            var rows = new List<string>();
            foreach (var e in ByNet.Values)
            {
                var ids = new List<string>();
                if (e.ItemIds != null) foreach (uint id in e.ItemIds) ids.Add(id.ToString());
                var objects = new List<string>();
                foreach (var it in e.Items) if (it != null) objects.Add(it.gameObject.GetInstanceID().ToString());
                int bound = 0, deadBound = 0, deadEnginesBound = 0;
                foreach (var kv in ProxyWheelOwner) if (kv.Value == e) { bound++; if (kv.Key == null) deadBound++; }
                foreach (var kv in ProxyEngineOwner) if (kv.Value == e && kv.Key == null) deadEnginesBound++;
                int wheels = 0, missing = 0, wrong = 0;
                if (e.Proxy && e.Root != null)
                {
                    var ws = e.Root.GetComponentsInChildren<wheelgraphicsscript>(true);
                    wheels = ws.Length;
                    for (int i = 0; i < ws.Length; i++)
                    {
                        if (!ProxyWheelOwner.TryGetValue(ws[i], out var owner)) missing++;
                        else if (owner != e || !ProxyWheelIndex.TryGetValue(ws[i], out int index) || index != i) wrong++;
                    }
                }
                rows.Add("{\"net\":" + e.NetId + ",\"rootId\":" + e.RootId + ",\"loaded\":" + (e.Root != null ? "true" : "false") +
                         ",\"placed\":" + (_placed.Contains(e) ? "true" : "false") + ",\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"ids\":[" + string.Join(",", ids) +
                         "],\"objects\":[" + string.Join(",", objects) + "],\"boundWheels\":" + bound + ",\"deadBoundWheels\":" + deadBound + ",\"deadBoundEngines\":" + deadEnginesBound + ",\"wheels\":" + wheels + ",\"missingWheels\":" + missing + ",\"wrongWheels\":" + wrong + "}");
            }
            rows.Sort(StringComparer.Ordinal);
            return "{\"frame\":" + Time.frameCount + ",\"cacheFrame\":" + _placedFrame + ",\"fullFrame\":" + _fullFrame +
                   ",\"dirty\":" + (_placedDirty ? "true" : "false") + ",\"nets\":" + ByNet.Count + ",\"netsSeen\":" + _netsSeen +
                   ",\"items\":" + (savedatascript.s != null ? savedatascript.s.items.Count : -1) + ",\"itemsSeen\":" + _itemsSeen + ",\"proxyItems\":" + ProxyItems.Count + ",\"deadItems\":" + deadItems + ",\"wheelOwners\":" + ProxyWheelOwner.Count +
                   ",\"wheelIndexes\":" + ProxyWheelIndex.Count + ",\"deadWheels\":" + deadWheels + ",\"goneWheels\":" + goneWheels +
                   ",\"markers\":[" + string.Join(",", markers) + "],\"engineOwners\":" + ProxyEngineOwner.Count + ",\"deadEngines\":" + deadEngines + ",\"rows\":[" + string.Join(",", rows) + "]}";
        }

        public static string RegistryServerList()
        {
            var rows = new List<string>();
            RegistrySets(rows, "known", Known);
            RegistrySets(rows, "debt", _staleEnts);
            RegistrySets(rows, "pose", _poseNear);
            RegistrySets(rows, "rabbit", _rabbitNear);
            rows.Sort(StringComparer.Ordinal);
            return "{\"server\":" + Server.Count + ",\"rows\":[" + string.Join(",", rows) + "]}";
        }

        private static void RegistrySets(List<string> rows, string name, Dictionary<int, HashSet<uint>> sets)
        {
            foreach (var kv in sets)
                foreach (uint net in kv.Value)
                    rows.Add("{\"set\":" + Json.Str(name) + ",\"player\":" + kv.Key + ",\"net\":" + net + ",\"gone\":" + (!Server.ContainsKey(net) ? "true" : "false") + "}");
        }

        // Local save ids are retained by the caller before deletion, so this also works after ByNet loses the object.
        public static string RegistryStoreList(string text)
        {
            var rows = new List<string>();
            var save = savedatascript.s;
            var data = save != null && save.data != null ? save.data.itemData : null;
            if (data == null) return "{\"error\":\"no save data\"}";
            foreach (string part in text.Split(','))
            {
                if (!uint.TryParse(part, out uint id)) continue;
                int chunks = 0, records = 0, items = 0;
                foreach (var ids in save.itemChunks.Values) foreach (uint n in ids) if (n == id) chunks++;
                foreach (var field in ItemSnapshot.ListFields)
                {
                    if (!(field.GetValue(data) is IList list)) continue;
                    foreach (var entry in list)
                    {
                        if (entry == null) continue;
                        var idf = _idField(entry);
                        if (idf == null || (uint)idf.GetValue(entry) != id) continue;
                        records++;
                        if (field.Name == "items") items++;
                    }
                }
                rows.Add("{\"id\":" + id + ",\"items\":" + items + ",\"components\":" + (records - items) + ",\"chunks\":" + chunks + "}");
            }
            return "{\"rows\":[" + string.Join(",", rows) + "]}";
        }
    }
}
