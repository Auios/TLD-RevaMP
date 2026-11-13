using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using static TLDRevamp.ScriptProfiler;

namespace TLDRevamp
{
    internal static class ScriptProfilerTools
    {
        private static Harmony _profileHarmony;
        internal static bool Running => _profileHarmony != null;
        public static float SlowFrameMs = 25f;
        private static long _slowFrames, _frames;
        private static readonly long[] Ticks = new long[Capacity], Calls = new long[Capacity], Alloc = new long[Capacity], MaxTicks = new long[Capacity], SlowTicks = new long[Capacity];
        private static readonly long[] Over5 = new long[Capacity];
        internal static void Install()
        {
            DevHooks.ProfileRunning = () => Running;
            DevHooks.ProfileSlowMs = () => SlowFrameMs;
            DevHooks.ProfileSlowCall = slot => Over5[slot]++;
            DevHooks.ProfileSlotCleared = slot => Ticks[slot] = Calls[slot] = Alloc[slot] = MaxTicks[slot] = SlowTicks[slot] = Over5[slot] = 0;
            DevHooks.ProfileFrame = (counted, slow) => { if (counted && Running) { _frames++; if (slow) _slowFrames++; } };
            DevHooks.ProfileSample = (slot, ticks, calls, alloc, max, slow) =>
            {
                Ticks[slot] += ticks; Calls[slot] += calls; Alloc[slot] += alloc;
                if (max > MaxTicks[slot]) MaxTicks[slot] = max;
                if (slow) SlowTicks[slot] += ticks;
            };
        }



        /// Bridge `il Type.Method`: the original IL of a game method (writing exact transpilers).
        public static string Il(string spec)
        {
            var sb = new StringBuilder();
            foreach (var m in Resolve(spec))
            {
                sb.Append("// ").Append(m).Append('\n');
                int i = 0;
                foreach (var ins in PatchProcessor.ReadMethodBody(m)) sb.Append(i++).Append(": ").Append(ins.Key.Name).Append(' ').Append(ins.Value is MethodBase mb ? mb.DeclaringType?.Name + "::" + mb.Name : ins.Value?.ToString()).Append('\n');
            }
            return "{\"il\":" + Json.Str(sb.ToString()) + "}";
        }

        // ---- on-demand profiler ----

        /// methods == null: every Update/LateUpdate/FixedUpdate/OnGUI in the game. Otherwise "Type.Method" names (every
        /// overload), "all" or "coroutines"; a leading "alloc" also estimates allocations (see the class comment).
        public static string Start(string[] methods)
        {
            if (Running) return "{\"error\":\"already running\"}";
            bool alloc = methods != null && methods.Length > 0 && methods[0] == "alloc";
            if (alloc) methods = methods.Skip(1).ToArray();
            if (methods != null && methods.Length == 0) methods = null;
            _slowFrames = _frames = 0;
            ResetProfileClock();
            _profileHarmony = new Harmony(Plugin.Guid + ".profiler");
            int missing = 0;
            var (patched, failed) = ProbeAll(_profileHarmony, methods == null ? AllUpdates() : Specs(methods, _ => { missing++; return true; }), SetProfile, alloc);
            _wall = Stopwatch.StartNew();
            return "{\"patched\":" + patched + ",\"failed\":" + (failed + missing) + ",\"alloc\":" + (alloc ? "true" : "false") + "}";
        }

        public static string Stop(int top)
        {
            if (!Running) return "{\"error\":\"not running\"}";
            _profileHarmony.UnpatchSelf();
            _profileHarmony = null;
            double seconds = _wall.Elapsed.TotalSeconds;
            var live = Enumerable.Range(0, Capacity).Where(s => ObservedOwner[s] == SetProfile && Calls[s] > 0).ToList();

            string List(IEnumerable<int> rows) =>
                string.Join(",", rows.Take(top).Select(s =>
                    "{\"m\":" + Json.Str(ObservedLabel[s] + "(" + string.Join(",", ObservedMethod[s].GetParameters().Select(p => p.ParameterType.Name)) + ")") +
                    ",\"msPerSec\":" + (Ticks[s] * TickMs / seconds).ToString("F3") +
                    ",\"kbPerSec\":" + (Alloc[s] / 1024.0 / seconds).ToString("F1") +
                    ",\"callsPerSec\":" + (Calls[s] / seconds).ToString("F0") +
                    ",\"msPerFrame\":" + (Ticks[s] * TickMs / Math.Max(1, _frames)).ToString("F4") +
                    ",\"maxMs\":" + (MaxTicks[s] * TickMs).ToString("F2") +
                    ",\"over5ms\":" + Over5[s] + "}"));

            var sb = new StringBuilder();
            sb.Append("{\"seconds\":").Append(seconds.ToString("F1"))
              .Append(",\"totalMsPerSec\":").Append((live.Sum(s => Ticks[s]) * TickMs / seconds).ToString("F2"))
              .Append(",\"totalKbPerSec\":").Append((live.Sum(s => Alloc[s]) / 1024.0 / seconds).ToString("F1"))
              .Append(",\"byTime\":[").Append(List(live.OrderByDescending(s => Ticks[s]))).Append(']')
              .Append(",\"byAlloc\":[").Append(List(live.OrderByDescending(s => Alloc[s]))).Append(']')
              .Append(",\"bySpike\":[").Append(List(live.OrderByDescending(s => MaxTicks[s]))).Append(']')
              .Append(",\"frames\":").Append(_frames).Append(",\"slowFrames\":").Append(_slowFrames).Append(",\"slowFrameMs\":").Append(SlowFrameMs)
              .Append(",\"bySlowFrames\":[").Append(string.Join(",", live.Where(s => SlowTicks[s] > 0).OrderByDescending(s => SlowTicks[s]).Take(top).Select(s =>
                  "{\"m\":" + Json.Str(ObservedLabel[s]) +
                  ",\"msPerSlowFrame\":" + (SlowTicks[s] * TickMs / Math.Max(1, _slowFrames)).ToString("F3") +
                  ",\"msPerFrameAll\":" + (Ticks[s] * TickMs / Math.Max(1, _frames)).ToString("F3") + "}"))).Append("]}");
            Release(SetProfile);
            return sb.ToString();
        }

        /// Probe overhead: an empty method called 200k times, unprobed vs probed (and probed with alloc estimate).
        public static string Calibrate()
        {
            const int N = 200000;
            var target = typeof(ScriptProfilerTools).GetMethod(nameof(Empty), BindingFlags.Static | BindingFlags.NonPublic);
            double Run() { var sw = Stopwatch.StartNew(); for (int i = 0; i < N; i++) Empty(i); return sw.Elapsed.TotalMilliseconds * 1e6 / N; }
            double baseNs = Math.Min(Run(), Run());
            var h = new Harmony(Plugin.Guid + ".calibrate");
            string result;
            try
            {
                if (!Probe(h, target, SetProfile + 8, false)) return "{\"error\":\"patch failed\"}";
                double timedNs = Math.Min(Run(), Run());
                h.UnpatchSelf(); Release(SetProfile + 8);
                if (!Probe(h, target, SetProfile + 8, true)) return "{\"error\":\"patch failed\"}";
                long heap = GC.GetTotalMemory(false);
                double allocNs = Math.Min(Run(), Run());
                long grew = GC.GetTotalMemory(false) - heap;
                result = "{\"calls\":" + N + ",\"baseNs\":" + Json.Num(baseNs, "F1") + ",\"probedNs\":" + Json.Num(timedNs, "F1") + ",\"overheadNs\":" + Json.Num(timedNs - baseNs, "F1") +
                    ",\"allocProbeOverheadNs\":" + Json.Num(allocNs - baseNs, "F1") + ",\"heapGrowthBytes\":" + grew + "}";
            }
            finally { h.UnpatchSelf(); Release(SetProfile + 8); }
            return result;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static int Empty(int i) => i + 1;

        /// Bridge `timers on [alloc] [Type.Method ...|all|default] | off | show [n] | calibrate`
        public static string Bridge(string arg)
        {
            var a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) return "{\"on\":" + (TimersOn ? "true" : "false") + "}";
            switch (a[0])
            {
                case "coarse":
                    if (a.Length > 1 && a[1] == "off") CoarseStop();
                    else if (a.Length > 1 && a[1] == "on") CoarseStart();
                    return "{\"coarseTimers\":" + (CoarseOn ? "true" : "false") + ",\"callLimit\":" + CoarseClock.CallLimit + "}";
                case "on":
                    bool alloc = a.Length > 1 && a[1] == "alloc";
                    var r = TimersStart(a.Skip(alloc ? 2 : 1).ToArray(), alloc);
                    SessionLog.TimerColumnsChanged();
                    return r;
                case "off": { var r2 = TimersStop(); SessionLog.TimerColumnsChanged(); return r2; }
                case "show": return TimersInterval(a.Length > 1 ? int.Parse(a[1]) : 15, false);
                case "calibrate": return Calibrate();
            }
            return "{\"error\":\"timers on [alloc] [Type.Method ...|all|default] | off | show [n] | calibrate\"}";
        }
        internal static Stopwatch _wall;
    }
}
