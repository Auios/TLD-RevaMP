using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;
using static TLDRevamp.SessionLog;

namespace TLDRevamp
{
    internal static class SessionLogTools
    {
        /// Bridge `telemetrytest [n]`: the per-frame sampling path (FrameBreakdown.Tick, the column copy and the black-box
        /// ring) run n times on scratch objects. Time per frame from Stopwatch; allocations from heap growth over the
        /// whole loop (best of three, so worker threads' allocations don't count), since this runtime has no per-thread counter.
        internal static string SelfTest(int n)
        {
            var bd = new FrameBreakdown();
            var ring = new FrameRing(32768, TLDRevamp.SessionLog.ObservedRing.Width); var cols = new float[TLDRevamp.SessionLog.ObservedRing.Width];
            var trace = new MotionTrace(); var peers = new PeerTrafficTable(); var types = new TrafficCount[256];
            var focus = new uint[MotionTrace.FocusLimit]; for (int i = 0; i < focus.Length; i++) focus[i] = (uint)i + 1; trace.SetFocus(focus, 0, 100);
            double bestMs = double.MaxValue; long bestBytes = long.MaxValue;
            for (int run = 0; run < 3; run++)
            {
                long heap = GC.GetTotalMemory(false), t0 = Stopwatch.GetTimestamp();
                for (int i = 0; i < n; i++)
                {
                    bd.Tick(5f);
                    var row = new TelemetryFrame { St = i, Local = i * 0.001, Ms = 5f, Speed = Speed(out _), Flags = TelemetryHooks.Previous.Flags, Gen = GenTotal };
                    row.ObserveGc(bd.GcMs);
                    int a = bd.LastMs.Length, x = bd.Extra.Length;
                    Array.Copy(bd.LastMs, 0, cols, 0, a); Array.Copy(bd.Extra, 0, cols, a, x);
                    Array.Copy(ScriptProfiler.Coarse.Last, 0, cols, a + x, ScriptProfiler.CoarseNames.Length);
                    for (int c = BaseWidth; c < cols.Length; c++) cols[c] = c - BaseWidth < ScriptProfiler.ColumnCount ? ScriptProfiler.Column(c - BaseWidth) : 0;
                    ring.Add(row, cols, 40);
                    for (int k = 0; k < focus.Length; k++)
                    {
                        trace.Add(new MotionRow { Net = (uint)k + 1, Local = i / 60.0, St = i / 60.0 + 100, Kind = 2 });
                        if (i % 3 == 0) { trace.Add(new MotionRow { Net = (uint)k + 1, Local = i / 60.0, Kind = 0 }); trace.Add(new MotionRow { Net = (uint)k + 1, Local = i / 60.0, Kind = 1 }); }
                    }
                    Array.Clear(types, 0, types.Length);
                    for (int k = 0; k < 128; k++) { types[k & 31].Received++; types[k & 31].InBytes += 60; peers.Add((uint)(k & 7), false, 60); }
                }
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                long grew = GC.GetTotalMemory(false) - heap;
                bestMs = Math.Min(bestMs, ms); bestBytes = Math.Min(bestBytes, Math.Max(0, grew));
            }
            bd.Dispose();
            return "{\"frames\":" + n + ",\"msPerFrame\":" + Json.Num(bestMs / n, "F6") + ",\"heapGrowthBytes\":" + bestBytes +
                ",\"bytesPerFrame\":" + Json.Num((double)bestBytes / n, "F3") + ",\"columns\":" + TLDRevamp.SessionLog.ObservedRing.Width + ",\"live\":" + Stats() + "}";
        }
    }
}
