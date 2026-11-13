using UnityEngine;

namespace TLDRevamp.Diagnostics
{
    /// Player error telemetry, with an optional raw-message observer for developer tools.
    public static class ErrorLog
    {
        internal static System.Action<string, string, LogType> DiagnosticMessage;
        private static readonly object Lock = new object();
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
            DiagnosticMessage?.Invoke(msg, stack, type);
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
