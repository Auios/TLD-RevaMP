using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityRepairTools
    {

        /// `mp censusnow <net>`: this copy's check now, not when its turn comes.
        public static string CensusNow(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !e.Proxy || !Resolve(e) || e.Root == null) return "{\"error\":\"no copy of it placed here\"}";
            ScheduleCensus(net, restartRepair: true);
            return "{\"rows\":" + RepairRows(e).Count + "}";
        }

        /// `mp repairrows <net>`: the rows this machine would hash, to compare two machines.
        public static void TryRows(uint net, out string json)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null) { json = "{\"error\":\"no such object here\"}"; return; }
            var rows = new List<string>(); foreach (var row in RepairRows(e)) rows.Add(Json.Str(row));
            json = "{\"rows\":[" + string.Join(",", rows) + "],\"msMax\":" + Json.Num((float)RepairRowsMsMax) + ",\"asked\":" + RepairsAsked + ",\"healed\":" + RepairsHealed + ",\"failed\":" + RepairsFailed + ",\"run\":" + RepairsRun + "}";
        }
    }
}
