using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace TLDRevamp
{
    /// Per-method timing through Harmony prefix/postfix probes. Three users share one probe table:
    ///  - the on-demand profiler (bridge `profile start [Type.Method ...|coroutines]` / `profile stop [n]`): every
    ///    Update/LateUpdate/FixedUpdate/OnGUI declared in the game's assembly by default; patched only while running;
    ///  - the telemetry script timers (config [Telemetry] ScriptTimers, bridge `timers`): a fixed, configurable list of
    ///    hot methods whose ms/frame and calls go into the play log's 5 s ticks, hitch lines and black-box columns.
    ///  - bounded coarse timers: a small always-on set for attributing marks without detailed profiling.
    /// Each probed method gets a slot. Its prefix/postfix belong to a closed generic class (Slot&lt;A,B,C&gt;, one per slot)
    /// with the slot number as a static field, so a call costs two Stopwatch reads and a few array writes: no
    /// dictionary, no __originalMethod lookup, no allocation. The prefix runs first and the postfix last among all
    /// patches, so a fix's own prefix (also one that skips the original) is part of the method's time.
    /// Times are inclusive: a probed method called from another probed one counts in both.
    ///
    /// Allocations: this Mono (Boehm GC) has no per-thread allocation counter. GC.GetAllocatedBytesForCurrentThread
    /// returns 0, the shipped player has no "GC Allocated In Frame" profiler counter, and Mono's allocation profiler
    /// can only be enabled at startup. With alloc on, a probe reads GC.GetTotalMemory around the call instead: process-wide
    /// heap growth while the method ran. It moves in heap-block steps and includes what worker threads allocate
    /// meanwhile (AsyncTerrain's tile building), so it is an estimate: rank short main-thread methods, average over
    /// many calls, or compare with the workers off. It also takes the GC lock twice per call; leave it off for timing.
    public static class ScriptProfiler
    {
        internal static void CoarseStop()
        {
            _coarseHarmony?.UnpatchSelf(); _coarseHarmony = null; Release(SetCoarse); Coarse.FrameEnd(); Coarse.FrameEnd();
        }
        internal static void ResetProfileClock() => _lastFrameEnd = 0;
        internal static IReadOnlyList<int> ObservedOwner => Owner;
        internal static IReadOnlyList<string> ObservedLabel => Label;
        internal static IReadOnlyList<MethodBase> ObservedMethod => Method;
        internal static IEnumerable<MethodBase> Callees(MethodBase caller)
        {
            List<KeyValuePair<System.Reflection.Emit.OpCode, object>> code;
            try { code = PatchProcessor.ReadMethodBody(caller).ToList(); } catch { yield break; }
            foreach (var ins in code)
                // Not the framework's methods or generic types: their code is shared by every caller and instantiation
                // (probing List<T>/String members broke unrelated callers in a test)
                // Only the game's own methods: probing the framework's shared generic code broke unrelated callers, and
                // probing RevaMP's network internals this way crashed a two-copy test (2026-10-18). Time RevaMP by name.
                if (ins.Value is MethodBase m && !m.IsAbstract && !m.ContainsGenericParameters && m.DeclaringType != null && !m.DeclaringType.IsGenericType &&
                    m.DeclaringType.Assembly == typeof(mainscript).Assembly && m.GetMethodBody() != null)
                    yield return m;
        }
        internal const int Capacity = 1000; // Slot<A,B,C> with ten digit types
        internal const int SetProfile = 1, SetTimers = 2, SetCoarse = 3;
        private static readonly string[] CoarseMethods = { "carscript.Update", "mapSettingScript.Update", "fpscontroller.Update", "aiscript.Update",
            "TLDRevamp.Net.Mp.Tick", "TLDRevamp.Net.Entities.FixedTick", "TLDRevamp.Net.EntityDoors.LateSample", "TLDRevamp.Fixes.TinyRendererCull.Tick" };
        internal static readonly string[] CoarseNames = CoarseMethods.Select(m => "coarse:" + m).Concat(new[] { "coarse:truncated" }).ToArray();
        internal static readonly CoarseClock Coarse = new CoarseClock(CoarseMethods.Length);
        private static readonly int[] CoarseGroup = new int[Capacity];
        private static Harmony _coarseHarmony;
        internal static bool CoarseOn => _coarseHarmony != null;
        internal static void CoarseStart()
        {
            if (CoarseOn) return;
            _coarseHarmony = new Harmony(Plugin.Guid + ".coarse");
            for (int i = 0; i < CoarseMethods.Length; i++) foreach (var m in Resolve(CoarseMethods[i]))
            {
                int slot = Array.IndexOf(Owner, 0);
                if (slot >= 0) { CoarseGroup[slot] = i; if (!Probe(_coarseHarmony, m, SetCoarse, false)) Plugin.Log.LogWarning("Coarse timer unavailable: " + CoarseMethods[i]); }
            }
        }

        // Per slot. Frame* is the current frame window (Runner.Update to Runner.Update), rolled up by FrameEnd.
        private static readonly MethodBase[] Method = new MethodBase[Capacity];
        private static readonly string[] Label = new string[Capacity];
        private static readonly int[] Owner = new int[Capacity];
        private static readonly long[] FrameTicks = new long[Capacity], FrameCalls = new long[Capacity], FrameAlloc = new long[Capacity], FrameMax = new long[Capacity];
        private static readonly int[] Touched = new int[Capacity];
        private static int _touched;
        // On-demand profile totals
        // Telemetry interval totals (reset at every tick) and last finished frame
        private static readonly long[] IntTicks = new long[Capacity], IntCalls = new long[Capacity], IntAlloc = new long[Capacity], IntMax = new long[Capacity];
        internal static readonly float[] LastFrameMs = new float[Capacity];
        private static long _intFrames;
        private static double _intStart;

        private static Harmony _timerHarmony;
        /// Slow-frame attribution: a frame window longer than SlowFrameMs adds each method's time in that window to SlowTicks.
        private static long _lastFrameEnd;

        public static bool Running => DevHooks.ProfileRunning?.Invoke() ?? false;
        public static bool TimersOn => _timerHarmony != null;
        public static bool TimerAlloc { get; private set; }

        /// Unity's main thread; set at plugin load. Probes ignore other threads (terrain/road workers call some of the
        /// profiled game functions).
        public static int MainThreadId = -1;
        private static readonly long Over5msTicks = Stopwatch.Frequency / 200;
        internal static readonly double TickMs = 1000.0 / Stopwatch.Frequency;

        // ---- probes ----

        public struct D0 { } public struct D1 { } public struct D2 { } public struct D3 { } public struct D4 { }
        public struct D5 { } public struct D6 { } public struct D7 { } public struct D8 { } public struct D9 { }
        private static readonly Type[] Digits = { typeof(D0), typeof(D1), typeof(D2), typeof(D3), typeof(D4), typeof(D5), typeof(D6), typeof(D7), typeof(D8), typeof(D9) };
        internal static int DigitOf(Type t) => Array.IndexOf(Digits, t);

        public struct Mark { public long T, M; }

        /// One class per slot (value-type arguments: Mono compiles each instantiation separately, no shared-generic lookup).
        public static class Slot<A, B, C> where A : struct where B : struct where C : struct
        {
            private static readonly int I = DigitOf(typeof(A)) * 100 + DigitOf(typeof(B)) * 10 + DigitOf(typeof(C));
            public static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();
            public static void Postfix(long __state) => Hit(I, Stopwatch.GetTimestamp() - __state, 0);
            public static void PrefixCoarse(out long __state) => __state = System.Threading.Thread.CurrentThread.ManagedThreadId == MainThreadId ? Coarse.Start() : 0;
            public static void PostfixCoarse(long __state) => Coarse.Stop(CoarseGroup[I], __state);
            public static void PrefixAlloc(out Mark __state) { __state.M = GC.GetTotalMemory(false); __state.T = Stopwatch.GetTimestamp(); }
            public static void PostfixAlloc(Mark __state)
            {
                long t = Stopwatch.GetTimestamp() - __state.T;
                Hit(I, t, GC.GetTotalMemory(false) - __state.M);
            }
        }

        private static void Hit(int s, long ticks, long alloc)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != MainThreadId) return;
            if (FrameCalls[s]++ == 0 && _touched < Capacity) Touched[_touched++] = s;
            FrameTicks[s] += ticks;
            if (ticks > FrameMax[s]) FrameMax[s] = ticks;
            if (ticks > Over5msTicks) DevHooks.ProfileSlowCall?.Invoke(s);
            if (alloc > 0) FrameAlloc[s] += alloc; // negative = a GC ran mid-call; skip that sample
        }

        private static readonly Type SlotGeneric = typeof(Slot<,,>);
        private static Type SlotType(int s) => SlotGeneric.MakeGenericType(Digits[s / 100], Digits[s / 10 % 10], Digits[s % 10]);

        internal static bool Probe(Harmony h, MethodBase m, int owner, bool alloc)
        {
            int s = Array.IndexOf(Owner, 0);
            if (s < 0) return false;
            var type = SlotType(s);
            var pre = new HarmonyMethod(type.GetMethod(owner == SetCoarse ? "PrefixCoarse" : alloc ? "PrefixAlloc" : "Prefix")) { priority = Priority.First + 100 };
            var post = new HarmonyMethod(type.GetMethod(owner == SetCoarse ? "PostfixCoarse" : alloc ? "PostfixAlloc" : "Postfix")) { priority = Priority.Last - 100 };
            Clear(s);
            Owner[s] = owner; Method[s] = m; Label[s] = Name(m);
            try { h.Patch(m, pre, post); return true; }
            catch { Owner[s] = 0; Method[s] = null; return false; }
        }

        private static void Clear(int s)
        {
            FrameTicks[s] = FrameCalls[s] = FrameAlloc[s] = FrameMax[s] = 0;
            DevHooks.ProfileSlotCleared?.Invoke(s);
            IntTicks[s] = IntCalls[s] = IntAlloc[s] = IntMax[s] = 0; LastFrameMs[s] = 0;
        }

        internal static void Release(int owner)
        {
            for (int s = 0; s < Capacity; s++) if (Owner[s] == owner) { Owner[s] = 0; Method[s] = null; Clear(s); }
            // A released slot may still be in this frame's touched list: FrameEnd skips unowned slots.
        }

        /// Called once per frame from the mod's Runner (main thread), before telemetry samples the frame.
        public static void FrameEnd()
        {
            Coarse.FrameEnd();
            if (!CoarseOn) Coarse.Last[CoarseNames.Length - 1] = -1;
            if (!Running && !TimersOn && _touched == 0) return;
            long now = Stopwatch.GetTimestamp();
            bool counted = _lastFrameEnd != 0, slow = counted && (now - _lastFrameEnd) * TickMs > (DevHooks.ProfileSlowMs?.Invoke() ?? 25f);
            DevHooks.ProfileFrame?.Invoke(counted, slow);
            if (TimersOn)
            {
                _intFrames++;
                for (int i = 0; i < _timerSlots.Count; i++) LastFrameMs[_timerSlots[i]] = 0; // not called this frame: 0 ms
            }
            for (int i = 0; i < _touched; i++)
            {
                int s = Touched[i];
                if (Owner[s] == SetProfile && counted)
                {
                    DevHooks.ProfileSample?.Invoke(s, FrameTicks[s], FrameCalls[s], FrameAlloc[s], FrameMax[s], slow);
                }
                else if (Owner[s] == SetTimers)
                {
                    IntTicks[s] += FrameTicks[s]; IntCalls[s] += FrameCalls[s]; IntAlloc[s] += FrameAlloc[s];
                    if (FrameMax[s] > IntMax[s]) IntMax[s] = FrameMax[s];
                    LastFrameMs[s] = (float)(FrameTicks[s] * TickMs);
                }
                FrameTicks[s] = FrameCalls[s] = FrameAlloc[s] = FrameMax[s] = 0;
            }
            _touched = 0;
            _lastFrameEnd = now;
        }

        private static string Name(MethodBase m)
        {
            var t = m.DeclaringType;
            return t.DeclaringType != null && t.Name.StartsWith("<") ? t.DeclaringType.Name + "." + t.Name : t.Name + "." + m.Name;
        }

        internal static IEnumerable<MethodBase> Resolve(string spec)
        {
            int dot = spec.LastIndexOf('.');
            var type = dot > 0 ? Reflect.FindType(spec.Substring(0, dot)) : null;
            if (type == null) yield break;
            foreach (var m in GameLookups.RuntimeMethods(type, spec.Substring(dot + 1), BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (m.Name == spec.Substring(dot + 1) && !m.IsAbstract && !m.IsGenericMethodDefinition && !m.ContainsGenericParameters) yield return m;
        }

        private static readonly string[] Targets = { "Update", "LateUpdate", "FixedUpdate", "OnGUI" };
        private static readonly string[] Callbacks = { "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit", "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit" };
        internal static IEnumerable<MethodBase> AllUpdates()
        {
            foreach (var type in typeof(mainscript).Assembly.GetTypes())
            {
                if (!typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(type) || type.IsGenericTypeDefinition) continue;
                foreach (var name in Targets)
                {
                    var m = GameLookups.RuntimeMethod(type, name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, Type.EmptyTypes);
                    if (m != null && !m.IsAbstract) yield return m;
                }
                // Physics callbacks take a Collision/Collider argument
                foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (Array.IndexOf(Callbacks, m.Name) >= 0 && !m.IsAbstract && !m.ContainsGenericParameters) yield return m;
            }
        }

        private static IEnumerable<MethodBase> Coroutines()
        {
            // Compiler-generated iterator classes (<Name>d__N) implementing IEnumerator in the game
            var asms = AppDomain.CurrentDomain.GetAssemblies().Where(x => { var n = x.GetName().Name; return !n.StartsWith("System") && n != "mscorlib" && !n.StartsWith("Mono.") && !n.StartsWith("0Harmony") && !n.StartsWith("BepInEx") && !n.StartsWith("TLDRevamp") && !n.StartsWith("netstandard"); });
            foreach (var type in asms.SelectMany(x => { try { return x.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); } }))
            {
                if (!type.Name.StartsWith("<") || !typeof(System.Collections.IEnumerator).IsAssignableFrom(type) || type.IsGenericTypeDefinition || type.ContainsGenericParameters) continue;
                var m = GameLookups.RuntimeMethod(type, "MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m != null) yield return m;
            }
        }

        internal static (int patched, int failed) ProbeAll(Harmony h, IEnumerable<MethodBase> methods, int owner, bool alloc)
        {
            int patched = 0, failed = 0;
            var seen = new HashSet<MethodBase>();
            foreach (var m in methods)
            {
                if (!seen.Add(m)) continue;
                if (Probe(h, m, owner, alloc)) patched++; else failed++;
            }
            return (patched, failed);
        }

        internal static IEnumerable<MethodBase> Specs(string[] specs, Func<string, bool> missing)
        {
            foreach (var spec in specs)
            {
                if (spec == "all") { foreach (var m in AllUpdates()) yield return m; continue; }
                if (spec == "coroutines") { foreach (var m in Coroutines()) yield return m; continue; }
                if (spec.StartsWith("callees:"))
                {
                    // drill-down: every managed method the named one calls directly (engine icalls fail to patch and count as failed)
                    foreach (var caller in Resolve(spec.Substring(8)))
                        foreach (var m in Callees(caller)) yield return m;
                    continue;
                }
                bool any = false;
                foreach (var m in Resolve(spec)) { any = true; yield return m; }
                if (!any) missing(spec);
            }
        }

        // ---- telemetry script timers ----

        /// The game's costliest scripts in TLDBench's scenes (2026-10-18 profile, every Update/FixedUpdate timed), the world
        /// generators' per-frame steps (inside mapSettingScript.Update: inclusive times count in both), then RevaMP's own
        /// per-frame work. Ordered by cost in the car park: the first ScriptTimerColumns become black-box columns.
        public const string DefaultTimers =
            "carscript.Update mapSettingScript.Update wheelgraphicsscript.Update talajnezo.FixedUpdate aiscript.Update tankcapscript.Update " +
            "usablescript.Update fpscontroller.Update mainscript.Update arrayfollow.Update mountStuff.Update headlightscript.Update " +
            "inputconnect.Update electronicsscript.Update kozlampa.Update IKscript.LateUpdate " +
            "inputscript.Update seatscript.Update attachablescript.Update dinamiccolliderforbus.Update sens2.FixedUpdate movecreaturescript.Update " +
            "randomFlyer.Update fluidGraphicScript.Update tapeMeasureScript.Update SteamManager.Update SteamP2PMenuHandlerScript.Update " +
            "roadGenScript.Upd2 objGenScript.UpdPlaceRemove objGenScript.UpdGen poiGenScript.Upd terrainGenScript.Upd " +
            "TLDRevamp.Runner.Update TLDRevamp.Runner.FixedUpdate TLDRevamp.Runner.LateUpdate TLDRevamp.Runner.OnGUI " +
            "TLDRevamp.Net.Mp.Tick TLDRevamp.Net.Entities.FixedTick TLDRevamp.Net.EntityDoors.LateSample TLDRevamp.Fixes.TinyRendererCull.Tick";

        public static int MaxColumns = 16;
        /// Methods per 5 s tick line.
        public static int TickTop = 10;
        private static readonly int[] _columns = new int[Capacity];
        private static int _columnCount;
        private static readonly List<int> _timerSlots = new List<int>();
        internal static string[] ColumnNames = Array.Empty<string>();
        internal static int ColumnCount => _columnCount;
        internal static float Column(int c) => LastFrameMs[_columns[c]];
        public static string Missing { get; private set; } = "";

        /// specs: "Type.Method" names, "all" (every Update/LateUpdate/FixedUpdate/OnGUI) or "default"; alloc: estimate allocations.
        public static string TimersStart(string[] specs, bool alloc)
        {
            if (TimersOn) TimersStop();
            if (specs == null || specs.Length == 0 || (specs.Length == 1 && specs[0] == "default")) specs = DefaultTimers.Split(' ');
            _timerHarmony = new Harmony(Plugin.Guid + ".timers");
            TimerAlloc = alloc;
            var missing = new List<string>();
            var (patched, failed) = ProbeAll(_timerHarmony, Specs(specs, s => { missing.Add(s); return true; }), SetTimers, alloc);
            Missing = string.Join(" ", missing);
            _timerSlots.Clear(); _columnCount = 0;
            for (int s = 0; s < Capacity; s++) if (Owner[s] == SetTimers) { _timerSlots.Add(s); if (_columnCount < MaxColumns) _columns[_columnCount++] = s; }
            ColumnNames = Enumerable.Range(0, _columnCount).Select(c => "script:" + Label[_columns[c]]).ToArray();
            _intFrames = 0; _intStart = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (missing.Count > 0) Plugin.Log.LogInfo("Script timers: not found (skipped): " + Missing);
            return "{\"patched\":" + patched + ",\"failed\":" + failed + ",\"missing\":" + Json.Str(Missing) + ",\"columns\":" + _columnCount + ",\"alloc\":" + (alloc ? "true" : "false") + "}";
        }

        public static string TimersStop()
        {
            if (!TimersOn) return "{\"error\":\"not running\"}";
            _timerHarmony.UnpatchSelf(); _timerHarmony = null;
            Release(SetTimers); _timerSlots.Clear(); _columnCount = 0; ColumnNames = Array.Empty<string>();
            return "{\"ok\":true}";
        }

        /// Top methods of the interval since the last call (main thread; the 5 s tick). Resets the interval.
        internal static string TimersInterval(int top, bool reset = true)
        {
            if (!TimersOn) return "null";
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble, seconds = Math.Max(.001, now - _intStart);
            long frames = Math.Max(1, _intFrames);
            _timerSlots.Sort((a, b) => IntTicks[b].CompareTo(IntTicks[a]));
            var b2 = new StringBuilder("{\"frames\":").Append(_intFrames).Append(",\"totalMsPerFrame\":");
            long total = 0; foreach (int s in _timerSlots) total += IntTicks[s];
            b2.Append(Json.Num(total * TickMs / frames, "F4")).Append(",\"top\":[");
            for (int i = 0; i < Math.Min(top, _timerSlots.Count); i++)
            {
                int s = _timerSlots[i];
                if (IntCalls[s] == 0) break;
                if (i > 0) b2.Append(',');
                b2.Append("{\"m\":").Append(TelemetryPrivacy.CodeStr(Label[s])).Append(",\"msPerFrame\":").Append(Json.Num(IntTicks[s] * TickMs / frames, "F4"))
                  .Append(",\"callsPerFrame\":").Append(Json.Num((double)IntCalls[s] / frames, "F2")).Append(",\"maxMs\":").Append(Json.Num(IntMax[s] * TickMs));
                if (TimerAlloc) b2.Append(",\"kbPerS\":").Append(Json.Num(IntAlloc[s] / 1024.0 / seconds, "F1"));
                b2.Append('}');
            }
            b2.Append("]}");
            if (reset)
            {
                foreach (int s in _timerSlots) IntTicks[s] = IntCalls[s] = IntAlloc[s] = IntMax[s] = 0;
                _intFrames = 0; _intStart = now;
            }
            return b2.ToString();
        }

        /// The slowest timed methods of the last finished frame (for hitch lines), without allocating per frame.
        private static readonly int[] _top = new int[5];
        internal static string LastFrameTop()
        {
            if (!TimersOn) return null;
            int n = 0, k = _top.Length;
            for (int i = 0; i < _timerSlots.Count; i++)
            {
                int s = _timerSlots[i]; float v = LastFrameMs[s];
                if (v <= 0) continue;
                int at;
                if (n < k) at = n++;
                else if (v > LastFrameMs[_top[k - 1]]) at = k - 1;
                else continue;
                while (at > 0 && LastFrameMs[_top[at - 1]] < v) { _top[at] = _top[at - 1]; at--; }
                _top[at] = s;
            }
            var b = new StringBuilder("{");
            for (int i = 0; i < n; i++) { if (i > 0) b.Append(','); b.Append(TelemetryPrivacy.CodeStr(Label[_top[i]])).Append(':').Append(Json.Num(LastFrameMs[_top[i]])); }
            return b.Append('}').ToString();
        }
    }
}
