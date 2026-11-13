using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static TLDRevamp.BugReport;

namespace TLDRevamp
{
    internal static class BugReportTools
    {

        /// `bug status` / `bug report [note]`: what the player sees (phase, bundle name) and the recorder, for the tests.
        internal static string Bridge(string arg)
        {
            arg = (arg ?? "").Trim();
            if (arg == "panel") MarkPanel.Open(true);   // the Ctrl+Insert note box; `markpanel save <note>` then takes the report
            if (arg == "report" || arg.StartsWith("report ", StringComparison.Ordinal))
            {
                if (mainscript.s == null || mainscript.s.player == null) return "{\"error\":\"no player\"}";
                if (!Request(arg.Length > 6 ? arg.Substring(7) : "")) return "{\"error\":\"a report is already being saved\"}";
            }
            bool bar = BarText(out string title, out string status, out _);
            return "{\"phase\":\"" + TLDRevamp.BugReport.ObservedPhase + "\",\"name\":" + Json.Str(TLDRevamp.BugReport.ObservedName) + ",\"banner\":" + (bar ? Json.Str(title + " " + status) : "null") + ",\"panel\":" + (MarkPanel.Opened ? "true" : "false") + ",\"auto\":" + (ReproRecorder.Auto ? "true" : "false") +
                ",\"recording\":" + (ReproRecorder.Recording ? "true" : "false") + ",\"full\":" + (ReproRecorder.Full ? "true" : "false") +
                ",\"startWorld\":" + Json.Str(ReproRecorder.StartWorldFile ?? "") + ",\"recorder\":" + ReproRecorder.State() + "}";
        }
    }
}
