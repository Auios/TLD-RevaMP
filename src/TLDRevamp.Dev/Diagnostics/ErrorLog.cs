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
            var state = Observe(reset);
            var rows = new List<string>();
            for (int i = 0; i < state.Keys.Length; i++)
            {
                var key = state.Keys[i];
                rows.Add("{\"n\":" + state.Counts[i] + ",\"what\":" + (raw ? TLDRevamp.Json.Str(key) + ",\"stack\":" + TLDRevamp.Json.Str(state.Stacks[i]) : TelemetryPrivacy.Str(TelemetryPrivacy.FreeText(key))) + "}");
            }
            return "{\"errors\":" + state.Errors + ",\"exceptions\":" + state.Exceptions + ",\"kinds\":[" + string.Join(",", rows) + "]}";
        }
        internal readonly struct Observation
        {
            internal readonly long Errors, Exceptions;
            internal readonly string[] Keys, Stacks;
            internal readonly int[] Counts;
            internal Observation(long errors, long exceptions, string[] keys, string[] stacks, int[] counts)
            { Errors = errors; Exceptions = exceptions; Keys = keys; Stacks = stacks; Counts = counts; }
        }

        internal static Observation Observe(bool reset)
        {
            lock (Lock)
            {
                var keys = Order.ToArray(); var stacks = new string[keys.Length]; var counts = new int[keys.Length];
                for (int i = 0; i < keys.Length; i++) { counts[i] = Kinds[keys[i]]; stacks[i] = Stacks[keys[i]]; }
                var result = new Observation(Errors, Exceptions, keys, stacks, counts);
                if (reset) { Kinds.Clear(); Order.Clear(); Stacks.Clear(); Errors = Exceptions = 0; }
                return result;
            }
        }

        private static readonly Dictionary<string, int> Kinds = new Dictionary<string, int>();

        private static readonly List<string> Order = new List<string>();

        private static readonly Dictionary<string, string> Stacks = new Dictionary<string, string>();   // `errors raw`

        public static long Errors, Exceptions;

        private const int MaxKinds = 60;

        private static readonly object Lock = new object();

        internal static void OnLog(string msg, string stack, LogType type)
        {
            string first = "";
            if (!string.IsNullOrEmpty(stack)) { int nl = stack.IndexOf('\n'); first = (nl > 0 ? stack.Substring(0, nl) : stack).Trim(); }
            string key = (msg ?? "").Trim();
            if (key.Length > 200) key = key.Substring(0, 200);
            if (first.Length > 0) key += " @ " + (first.Length > 160 ? first.Substring(0, 160) : first);
            lock (Lock)
            {
                if (type == LogType.Exception) Exceptions++; else Errors++;
                if (Kinds.TryGetValue(key, out int n)) Kinds[key] = n + 1;
                else if (Kinds.Count < MaxKinds) { Kinds[key] = 1; Order.Add(key); Stacks[key] = stack != null && stack.Length > 800 ? stack.Substring(0, 800) : stack ?? ""; }
            }
        }

    }
}
