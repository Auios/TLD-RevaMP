using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Diagnostics.ErrorLog;

namespace TLDRevamp.Diagnostics
{
    internal static class ErrorLogTools
    {

        /// `raw`: the texts as logged, for a test on this machine (bridge `errors raw`). They can hold private data: never in a report.
        public static string Json(bool reset, bool raw = false)
        {
            var state = ErrorLog.Observe(reset);
            var rows = new List<string>();
            for (int i = 0; i < state.Keys.Length; i++)
            {
                var key = state.Keys[i];
                rows.Add("{\"n\":" + state.Counts[i] + ",\"what\":" + (raw ? TLDRevamp.Json.Str(key) + ",\"stack\":" + TLDRevamp.Json.Str(state.Stacks[i]) : TelemetryPrivacy.Str(TelemetryPrivacy.FreeText(key))) + "}");
            }
            return "{\"errors\":" + state.Errors + ",\"exceptions\":" + state.Exceptions + ",\"kinds\":[" + string.Join(",", rows) + "]}";
        }
    }
}
