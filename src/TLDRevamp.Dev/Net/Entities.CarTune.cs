using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityCarTuneTools
    {

        /// Diagnostics (bridge `mp tune <net>`): the car's tuning here, and what this machine (and the server) keeps.
        public static string CarTuneDiag(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || !IsTunableCar(e)) return "{\"error\":\"no such car here\"}";
            var t = CaptureTuning(e.Root.car);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>();
            foreach (var x in t.Wheels)
                rows.Add("[" + Json.Str(x.Name) + "," + x.X.ToString("F3", ci) + "," + x.Y.ToString("F3", ci) + "," + x.Z.ToString("F3", ci) + "," + x.Sus.ToString("F3", ci) + "," + x.Roles + "]");
            TLDRevamp.Net.Entities.ObservedTuneHere.TryGetValue(net, out var th);
            bool keepsTune = ObservedServerHasTune(net);
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"car\":" + Json.Str(e.Root.name) + ",\"diff\":" + t.Diff.ToString("F3", ci) +
                   ",\"key\":" + t.Key() + ",\"prefabKey\":" + PrefabTuneKey(e.Root) + ",\"known\":" + (th != null ? th.Key.ToString() : "null") +
                   ",\"serverKeeps\":" + (keepsTune ? "true" : "false") + ",\"wheels\":[" + string.Join(",", rows) + "]" +
                   ",\"sent\":" + TunesSent + ",\"resent\":" + TunesResent + ",\"applied\":" + TunesApplied + ",\"reapplied\":" + TunesReapplied + ",\"replayed\":" + TunesReplayed +
                   ",\"refused\":" + TunesRefused + ",\"kept\":" + TunesKept + ",\"last\":" + Json.Str(TuneLast) + "}";
        }
    }
}
