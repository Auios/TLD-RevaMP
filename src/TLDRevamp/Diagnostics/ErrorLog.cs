using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Diagnostics
{
    /// Every error and exception the game logs (Unity's own log: the game's code, ours, patched methods), counted by kind —
    /// the message and the first stack line — so a test can read what went wrong on each machine during its run (bridge
    /// `errors [reset]`). BepInEx logs are overwritten at each launch; a two-machine run restarts both games.
    public static class ErrorLog
    {
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
        private static readonly object Lock = new object();
        private static readonly Dictionary<string, int> Kinds = new Dictionary<string, int>();
        private static readonly List<string> Order = new List<string>();
        private static readonly Dictionary<string, string> Stacks = new Dictionary<string, string>();   // `errors raw`
        public static long Errors, Exceptions;
        private const int MaxKinds = 60;
        private static readonly WarningCounter All = new WarningCounter(), Interval = new WarningCounter();
        private static long _warningsAll, _errorsAll, _exceptionsAll;

        public static void Install() => Application.logMessageReceivedThreaded += OnLog;

        private static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string kind = type == LogType.Warning ? "warning" : type == LogType.Exception ? "exception" : "error";
            string safeMsg = TelemetryPrivacy.Clean(msg), safeStack = TelemetryPrivacy.Clean(stack);
            lock (Lock)
            {
                All.Add(kind, safeMsg, safeStack); Interval.Add(kind, safeMsg, safeStack);
                if (kind == "warning") _warningsAll++; else if (kind == "exception") _exceptionsAll++; else _errorsAll++;
            }
            if (type == LogType.Warning) return;
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

        internal static string IntervalJson()
        {
            lock (Lock) { string json = Interval.Json(10, TelemetryPrivacy.Clean); Interval.Clear(); return json; }
        }
        internal static System.Func<string> ReportSnapshot()
        {
            lock (Lock)
            {
                long warnings = _warningsAll, errors = _errorsAll, exceptions = _exceptionsAll;
                var all = All.Snapshot();
                return () => "{\"warnings\":" + warnings + ",\"errors\":" + errors + ",\"exceptions\":" + exceptions +
                    ",\"droppedKinds\":" + all.Dropped + ",\"kinds\":" + all.Json(redact: TelemetryPrivacy.Clean) + "}";
            }
        }
    }
}
