using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;
using RF = RepairFakes;

static partial class EntitySplitTest
{
    static void RepairChecks()
    {
        var culture = CultureInfo.CurrentCulture;
        long anomalies = Entities.EditAnomalyTotal;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        var type = typeof(EntityRepair);
        object Call(string name, params object[] args) => type.GetMethod(name, Static).Invoke(null, args);
        string State(tosaveitemscript item) => (string)Call("RepairState", item);
        string Map(params string[] input) => string.Join(";", ((Dictionary<string, string>)Call("RowMap", (object)input)).Select(kv => kv.Key + ":" + kv.Value));
        Row("rows-empty", Map());
        Row("rows-malformed-duplicate-order", Map("missing", "=empty-key", "m1=off", "self=on2:0#4", "m1=none", "s0=c1=2", "g2="));
        var item = new tosaveitemscript();
        Row("state-empty", State(item));
        item.partconditions = new List<RepairCondition> { null, new RepairCondition { state = 1, state2 = -2, state3 = 3 }, new RepairCondition { state = 4, state2 = 5, state3 = 6 } };
        item.usables.AddRange(new[] { null, new usablescript { turnable = false, currentTurnState = 7 }, new usablescript { turnable = true, currentTurnState = -8 } });
        item.tanks = new List<tankscript> { null, new tankscript { F = null }, new tankscript { F = new fluid() } };
        item.tanks[2].F.fluids.Add(new mainscript.FluidValue { amount = 2.5f });
        Row("state-condition-turn-litre-order", State(item));
        item.tanks[2].F.fluids[0].amount = 3.5f; Row("state-round-even", State(item));
        var at = new attachablescript();
        Row("glue-no-frame", Call("GluedPlace", at) ?? "null");
        at.point = new Transform { position = new Vector3(10, 20, 30) };
        at.transform.position = new Vector3(10.1f, 19.7f, 30.5f);
        Row("glue-frame-relative-quantized", Call("GluedPlace", at));
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        Row("glue-other-culture", Call("GluedPlace", at));
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        EntityRepair.ResetPoiCensus();
        EntityRepair.PoiCensusSent = EntityRepair.PoiCensusIn = EntityRepair.PoiDiffers = 0;
        EntityRepair.PoiCensusOn = true; EntityRepair.PoiCensusEveryS = 5; EntityRepair.PoiCensusRadiusM = 250; EntityRepair.PoiCensusNearM = 120;
        Entities.Packets.Clear(); Entities.IsHost = false;
        RF.Menu.s = null; RF.Main.s = null; RF.Settings.s = null;
        void Tick(float now) { Time.realtimeSinceStartup = now; Call("PoiCensusTick"); }
        string Sent() => Entities.Packets.Count + ":" + EntityRepair.PoiCensusSent + ":" + Field(type, "_poiCensusAt");
        Tick(4.999f); Row("tick-before-interval", Sent());
        Tick(5); Row("tick-unready-consumes-turn", Sent());
        RF.Menu.s = new RF.Menu { currentMainMap = new RF.Map() };
        RF.Main.s = new RF.Main { player = new fpscontroller() };
        RF.Main.s.player.transform.position = new Vector3(1, 2, 3);
        RF.Settings.s = new RF.Settings { S = new RF.Settings.Values { Draw = .75f, Distant = true } };
        Tick(9.999f); Row("tick-ready-waits", Sent());
        Tick(10); Row("tick-empty-map", Sent());
        Row("wire-empty", Convert.ToHexString(Entities.Packets.Last().Bytes));
        Row("wire-reliable-peer", Entities.Packets.Last().Reliable + ":" + Entities.Packets.Last().Peer);
        var generator = new RF.Generator { lodDistance = 600, removeDistance = 700 };
        var chunk = new RF.Chunk(); generator.chunks[0] = chunk;
        RF.Menu.s.currentMainMap.poiGens.Add(null); RF.Menu.s.currentMainMap.poiGens.Add(generator);
        chunk.pois[0] = null;
        chunk.pois[1] = new RF.Poi { pos = new Vector3d(251, 2, 3), posID = new Vector3d(7, 8, 9), id = 4, pobj = new Component() };
        chunk.pois[2] = new RF.Poi { pos = new Vector3d(251.001, 2, 3), posID = new Vector3d(11, 12, 13), id = 5 };
        chunk.pois[3] = new RF.Poi { pos = new Vector3d(1, 900, 3), posID = new Vector3d(21, 22, 23), id = 6 };
        Tick(15); Row("wire-radius-xz-key-order-first-generator-null", Convert.ToHexString(Entities.Packets.Last().Bytes));
        RF.Menu.s.currentMainMap.poiGens.RemoveAt(0);
        Tick(20); Row("wire-first-generator-distances", Convert.ToHexString(Entities.Packets.Last().Bytes));
        EntityRepair.PoiCensusOn = false; Tick(30); Row("tick-disabled-keeps-time", Sent()); EntityRepair.PoiCensusOn = true;
        RF.Menu.s.currentMainMap.poiGens.Clear();
        for (int g = 0; g < 2; g++)
        {
            var gen = new RF.Generator();
            for (int c = 0; c < 2; c++)
            {
                var ch = new RF.Chunk(); gen.chunks[c] = ch;
                for (int p = 0; p < 205; p++) ch.pois[p] = new RF.Poi { pos = new Vector3d(1, 0, 3), posID = new Vector3d(p, 0, c), id = g };
            }
            RF.Menu.s.currentMainMap.poiGens.Add(gen);
        }
        Tick(30); var many = Reader(Entities.Packets.Last().Bytes); many.F64(); many.F64(); many.F32(); many.Bool(); many.F32(); many.F32();
        Row("wire-count-cap-breaks-only-inner-loop", many.U16() + ":" + many.Remaining);

        RF.Database.s = new RF.Database(); RF.Database.s.Buildings[4] = new GameObject { name = "station" };
        byte[] Report(double px, double pz, params (double X, double Z, int Type, bool Shown)[] pois)
        {
            var w = new NetWriter(); w.U8(84); w.F64(px); w.F64(pz); w.F32(.5f); w.Bool(false); w.F32(400); w.F32(500); w.U16((ushort)pois.Length);
            foreach (var p in pois) { w.F64(p.X); w.F64(p.Z); w.I32(p.Type); w.Bool(p.Shown); }
            return w.Buf.Take(w.Len).ToArray();
        }
        string Census() => EntityRepair.PoiCensusIn + ":" + EntityRepair.PoiDiffers + ":" + ((IDictionary)Field(type, "_poiLists")).Count + ":" + string.Join("|", Entities.EditAnomalyLogs);
        void Reset() { EntityRepair.ResetPoiCensus(); EntityRepair.PoiCensusIn = EntityRepair.PoiDiffers = 0; Entities.EditAnomalyLogs.Clear(); }
        void Receive(int peer, byte[] bytes) => EntityRepair.ServerPoiCensus(peer, Reader(bytes));
        Reset(); Tick(40); Receive(1, Report(0, 0, (10, 20, 4, true))); Receive(2, Report(0, 0, (10, 20, 4, true))); Row("compare-same", Census());
        foreach (var scenario in new[] { "missing", "not-shown", "same-offset", "other-type", "same-type-precedes-closer-other", "unshown-source", "outside-near" })
        {
            Reset(); Time.realtimeSinceStartup = 50;
            Receive(1, Report(0, 0, (10, 20, 4, scenario != "unshown-source")));
            var pois = scenario switch
            {
                "not-shown" => new[] { (10d, 20d, 4, false) },
                "same-offset" => new[] { (25d, 20d, 4, true) },
                "other-type" => new[] { (11d, 20d, 5, true) },
                "same-type-precedes-closer-other" => new[] { (11d, 20d, 5, true), (100d, 20d, 4, true) },
                _ => Array.Empty<(double, double, int, bool)>()
            };
            Receive(2, Report(scenario == "outside-near" ? 131 : 0, 0, pois)); Row("compare-" + scenario, Census());
            Receive(2, Report(0, 0, pois)); Row("compare-dedup-" + scenario, Census());
        }
        Reset(); Receive(1, Report(0, 0, (0, 0, 4, true))); Time.realtimeSinceStartup = 65; Receive(2, Report(120, 0)); Row("compare-exact-stale-and-near-boundary", Census());
        Reset(); Time.realtimeSinceStartup = 50; Receive(1, Report(0, 0, (0, 0, 4, true))); Time.realtimeSinceStartup = 65.001f; Receive(2, Report(0, 0)); Row("compare-too-stale", Census());
        Reset(); Receive(1, Report(double.NaN, 0)); Receive(2, Report(double.PositiveInfinity, 0)); Row("receive-nonfinite-player", Census());
        var valid = Report(0, 0, (10, 20, 4, true));
        for (int i = 1; i < valid.Length; i++) { Reset(); Receive(1, valid.Take(i).ToArray()); Check(EntityRepair.PoiCensusIn == 0, "POI truncated payload " + i); }
        Reset(); RF.Database.s.Throws = true; Receive(1, Report(0, 0, (0, 0, 4, true))); Receive(2, Report(0, 0)); Row("building-name-throws-fallback", Census()); RF.Database.s.Throws = false;
        Reset(); Entities.IsHost = true; RF.Menu.s.currentMainMap.poiGens.Clear();
        Entities.HostReceive = (kind, reader) => { Row("host-reentry-kind", kind); EntityRepair.ServerPoiCensus(0, reader); };
        Tick(80); Entities.HostReceive = null; Row("host-reentry", Census());
        long sent = EntityRepair.PoiCensusSent, received = EntityRepair.PoiCensusIn;
        EntityRepair.ResetPoiCensus(); Row("reset-keeps-counters-settings", (sent == EntityRepair.PoiCensusSent) + ":" + (received == EntityRepair.PoiCensusIn) + ":" + EntityRepair.PoiCensusEveryS + ":" + Field(type, "_poiCensusAt") + ":" + ((IDictionary)Field(type, "_poiLists")).Count);
        var expectedPath = "tools/unit/EntityRepairRows.txt";
        if (Environment.GetEnvironmentVariable("REPAIR_CAPTURE") == "1")
        {
            Check(Environment.GetEnvironmentVariable("REPAIR_BASELINE") == "1", "repair capture explicitly uses the HEAD baseline");
            File.WriteAllText(expectedPath, string.Join("\r\n", rows) + "\r\n");
        }
        else
        {
            var expected = File.ReadAllLines(expectedPath); Check(expected.Length == rows.Count, "repair HEAD row count");
            for (int i = 0; i < rows.Count; i++) Check(expected[i] == rows[i], "repair HEAD row " + i + ": " + rows[i]);
        }
        if (Environment.GetEnvironmentVariable("REPAIR_BASELINE") != "1") RepairBoundaryChecks();
        EntityRepair.ResetPoiCensus(); Entities.EditAnomalyLogs.Clear(); Entities.Packets.Clear(); Entities.IsHost = false;
        Entities.EditAnomalyTotal = anomalies;
        Time.realtimeSinceStartup = 0; RF.Menu.s = null; RF.Main.s = null; RF.Settings.s = null; RF.Database.s = null;
        CultureInfo.CurrentCulture = culture;
    }

    static void RepairBoundaryChecks()
    {
        Check(File.ReadAllLines("tools/unit/EntityRepairSourceRows.txt").SequenceEqual(File.ReadAllLines("tools/unit/obj/EntityRepair/source-actual.txt")), "repair moved helpers and retained transactions equal HEAD after named substitutions");
        var owner = typeof(EntityRepair);
        Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, "repair owner is internal static");
        CoreMembers.Register();
        foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
        {
            bool retained = typeof(Entities).GetMember(member.Name, Static).Length > 0;
            foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." }) Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "repair alias " + prefix + member.Name);
        }
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.PoiCensusEveryS", "7") == "7" && EntityRepair.PoiCensusEveryS == 7, "POI historical setting writes sole owner");
        EntityRepair.PoiCensusEveryS = 5;
        Check(BridgeReflect.Get("Entities._poiLists.Count") == "0", "POI private state historical alias");
        foreach (string part in new[] { "Repair", "PlaceCheck", "PoiCensus" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Split("    public static partial class Entities")[0];
            source = Regex.Replace(source, @"//[^\r\n]*", "");
            Check(!source.Contains("using static") && !Regex.IsMatch(source, @"\b(?:ByNet|Server|SEnt|Ent|WS)\b"), part + " repair feature has no entity maps, records or static imports");
            foreach (Match call in Regex.Matches(source, @"Entities\.(\w+)")) Check(new[] { "RepairAttachFrame", "RepairAnomaly", "PoiCensus", "IsFinite", "SendToServer" }.Contains(call.Groups[1].Value), "narrow repair call " + call.Value);
            Check(!source.Contains("HarmonyPatch"), part + " repair extraction moves no Harmony classes");
        }
        Check(!ReferenceEquals(Field(owner, "W"), Field(typeof(EntityRadio), "W")) && !ReferenceEquals(Field(owner, "W"), Field(typeof(Entities), "W")), "POI owns its packet writer");
        Check(typeof(Entities).GetField("_poiLists", Static) == null && typeof(Entities).GetField("PoiCensusSent", Static) == null, "POI state has one owner");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs"), features = File.ReadAllText("src/TLDRevamp/Net/Entities.Features.cs");
        Check(kernel.Contains("case CensusRepair: ServerCensusRepair(from, r);") && kernel.Contains("case CensusRepair: ApplyCensusRepair(r);"), "CensusRepair dispatch stays at original kernel slots");
        Check(features.Contains("private static void PoiCensusTick() => EntityRepair.PoiCensusTick();") && features.Contains("internal static void ResetPoiCensus() => EntityRepair.ResetPoiCensus();"), "POI forwards preserve frozen tick and reset calls");
        foreach (string facade in new[] {
            "private static string RepairState(tosaveitemscript it) => EntityRepair.RepairState(it);",
            "private static System.Collections.Generic.Dictionary<string, string> RowMap(System.Collections.Generic.IEnumerable<string> rows) => EntityRepair.RowMap(rows);",
            "private static string GluedPlace(attachablescript at) => EntityRepair.GluedPlace(at);",
            "internal static Transform RepairAttachFrame(attachablescript at) => AttachFrame(at);",
            "internal static void RepairAnomaly(string what) => Anomaly(what);" }) Check(features.Contains(facade), "repair retains the exact narrow forward " + facade);
    }
}
