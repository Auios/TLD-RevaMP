using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// Players install only the core plugin, so a game patch that lives in the developer plugin never reaches them.
/// The Dev split once moved a gameplay patch (the host's physics lock on containers) into Dev and players lost it
/// for a week without any test noticing. This test lists every Harmony patch in src/TLDRevamp.Dev and fails on
/// any patch that is not listed in Rows below with the reason it is developer-only. A patch that changes how the
/// game plays for players belongs in src/TLDRevamp instead.
///
/// It also refuses outright what Dev must never do: rewrite game code (a transpiler), and skip game code with a
/// prefix outside the labs that are allowed to (MaySkip).
static class DevPatchesTest
{
    const string DevRoot = "src/TLDRevamp.Dev";

    /// One row per (file, patched game method): how many patches that file puts on it, and why they are Dev-only.
    /// "new Harmony" counts the Harmony instances a file makes for patches it applies at runtime (the script profiler
    /// patches whatever methods it is told to measure through its own instances).
    /// "TargetMethods" counts patch classes that pick their game methods in code.
    static readonly (string File, string Target, int Count, string Why)[] Rows =
    {
        ("DevPlugin.cs", "new Harmony", 1, "applies the attribute patches of this list"),
        ("ScriptProfiler.cs", "new Harmony", 2, "bridge profiler: times the methods it is told to, removed when it stops"),
        ("CrashLab.cs", "carscript.DamageStuff", 1, "crash recorder: notes the game's crash-damage input while recording"),
        ("Diagnostics/AttachLog.cs", "attachablescript.Detach", 1, "log"),
        ("Diagnostics/AttachLog.cs", "save_attachable.Load", 1, "log"),
        ("Diagnostics/CrashCause.cs", "carscript.DamageStuff", 1, "log"),
        ("Diagnostics/CrashCause.cs", "carscript.OnCollisionEnter", 1, "log"),
        ("Diagnostics/DeathLog.cs", "fpscontroller.Death", 1, "log"),
        ("Diagnostics/DropLog.cs", "fpscontroller.Drop", 1, "log"),
        ("Diagnostics/DropLog.cs", "fpscontroller.InvStore", 1, "log"),
        ("Diagnostics/DropLog.cs", "fpscontroller.RightHandGrip", 1, "log"),
        ("Diagnostics/PoiAlignLog.cs", "poiGenScript.AlignPosToRoad", 1, "log"),
        ("Diagnostics/SceneChangeLog.cs", "menuhandler.LoadScene", 1, "log"),
        ("Diagnostics/SlotPartDestroyLog.cs", "tosaveitemscript.OnDestroy", 1, "log"),
        ("Fixes/LoadWatch.cs", "fpscontroller.Update", 1, "load diagnostic"),
        ("DriveLab.cs", "fpscontroller.Update", 1, "scripted test drive: holds the driver's view"),
        ("DriveLab.cs", "inputscript.Update", 1, "scripted test drive: steering and pedals"),
        ("Net/Entities.CarSignals.cs", "carscript.GetPlayerInput", 1, "test input: brake, horn, handbrake, ignition"),
        ("Net/Entities.ContactCensus.cs", "carscript.OnCollisionEnter", 1, "contact counter"),
        ("Net/Entities.ContactCensus.cs", "mainscript.PlayClipAtPoint", 1, "contact counter"),
        ("Net/Entities.ContactCensus.cs", "pickupable.OnCollisionEnter", 1, "contact counter"),
        ("Net/Entities.FakeInput.cs", "inputscript.Update", 1, "test input"),
        ("Net/Entities.ItemCheck.cs", "fpscontroller.Drop", 1, "item check: measures the drop"),
        ("Net/Entities.ItemCheck.cs", "fpscontroller.Update", 1, "item check: holds the test item"),
        ("Net/Entities.ItemCheck.cs", "inputscript.Update", 1, "item check: holds the test item"),
        ("Net/Entities.MountLab.cs", "attachablescript.Attach", 1, "mount watch: notes what happens to a watched item"),
        ("Net/Entities.MountLab.cs", "attachablescript.Detach", 1, "mount watch"),
        ("Net/Entities.MountLab.cs", "fpscontroller.Drop", 1, "mount watch"),
        ("Net/Entities.MountLab.cs", "fpscontroller.FallDamage", 1, "death watch: notes fall damage"),
        ("Net/Entities.MountLab.cs", "new Harmony", 1, "mount watch: patches applied only while watching"),
        ("Net/Entities.MountLab.cs", "save_attachable.Load", 1, "mount watch"),
        ("Net/Entities.MountLab.cs", "set Transform.parent", 1, "mount watch"),
        ("Net/Entities.MountLab.cs", "tosaveitemscript.StartUnparent", 1, "mount watch"),
        ("Net/Entities.MountLab.cs", "Transform.SetParent", 1, "mount watch"),
        ("Net/Entities.PlayerSounds.cs", "inputscript.Update", 1, "test input: walks for the footstep test"),
        ("Net/Entities.PushGrip.cs", "inputscript.Update", 1, "test input: walks while push-gripping"),
        ("Net/FillLab.cs", "inputscript.Update", 2, "fill test: walks to and holds a cap"),
        ("Net/PlayerActs.cs", "inputscript.Update", 1, "test input: hum, pee and sleep keys"),
        ("RebaseProfiler.cs", "mainscript.VisszaRakas", 2, "world-rebase timer"),
        ("RebaseProfiler.cs", "visszarako.Visszarakas", 2, "world-rebase timer"),
        ("WorldGenLab.cs", "objGenScript.Upd", 1, "world generation lab: can switch generation steps off"),
        ("WorldGenLab.cs", "poiGenScript.AlignPosToRoad", 2, "world generation lab: timer"),
        ("WorldGenLab.cs", "poiGenScript.Upd", 1, "world generation lab: can switch generation steps off"),
        ("WorldGenLab.cs", "roadGenScript.ConnectDots", 1, "world generation lab: timer"),
        ("WorldGenLab.cs", "roadGenScript.NeighborDots", 1, "world generation lab: timer"),
        ("WorldGenLab.cs", "roadGenScript.RoadsNear", 1, "world generation lab: counts roads"),
        ("WorldGenLab.cs", "roadGenScript.Upd", 1, "world generation lab: can switch generation steps off"),
        ("WorldGenLab.cs", "roadGenScript.UpdDotChunk", 1, "world generation lab: timer"),
        ("WorldGenLab.cs", "terrainGenScript.Upd", 1, "world generation lab: can switch generation steps off"),
    };

    /// Files allowed to have a prefix that returns bool (it can skip the game's method) or that writes __result.
    static readonly (string File, string Why)[] MaySkip =
    {
        ("WorldGenLab.cs", "switches world generation steps off to time the others; all on unless the bridge says so"),
    };

    static readonly Regex AttributeTarget = new Regex(@"HarmonyPatch\(\s*typeof\(\s*([\w.]+)\s*\)\s*,\s*(?:nameof\(\s*[\w.]+?\.(\w+)\s*\)|""(\w+)"")");
    static readonly Regex AttributeWithArgs = new Regex(@"HarmonyPatch\(");
    static readonly Regex RuntimeTarget = new Regex(@"\.Patch\(\s*GameLookups\.(Method|PropertySetter|PropertyGetter)\(\s*typeof\(\s*([\w.]+)\s*\)\s*,\s*nameof\(\s*[\w.]+?\.(\w+)\s*\)");
    static readonly Regex RuntimeWithArgs = new Regex(@"\.Patch\(\s*[^)\s]");
    static readonly Regex NewHarmony = new Regex(@"new\s+(?:HarmonyLib\.)?Harmony\(");
    static readonly Regex TargetMethods = new Regex(@"\bIEnumerable<[\w.]*MethodBase>\s+TargetMethods\s*\(|\bMethodBase\s+TargetMethod\s*\(");
    static readonly Regex Transpiler = new Regex(@"HarmonyTranspiler|\bTranspiler\s*\(|\bCodeInstruction\b|\btranspiler\s*:");
    static readonly Regex SkippingPrefix = new Regex(@"\[(?:HarmonyLib\.)?HarmonyPrefix\][^;{}]*?\bbool\s+\w+\s*\(|\bbool\s+Prefix\s*\(|\bref\s+[\w.<>\[\]]+\s+__result\b|\b__result\s*=[^=]", RegexOptions.Singleline);

    static int _failures;
    static void Fail(string why) { _failures++; Console.WriteLine("FAIL " + why); }

    /// The file's code without whole-line comments (doc comments often name the game methods they talk about).
    static string Code(string path) => string.Join("\n", File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//")));

    static void Main()
    {
        if (!Directory.Exists(DevRoot)) throw new Exception("run from the repository root: " + DevRoot + " not found");
        var found = new SortedDictionary<(string File, string Target), int>();
        void Add(string file, string target) => found[(file, target)] = found.TryGetValue((file, target), out int n) ? n + 1 : 1;

        foreach (string path in Directory.GetFiles(DevRoot, "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string file = Path.GetRelativePath(DevRoot, path).Replace('\\', '/');
            if (file.StartsWith("obj/") || file.StartsWith("bin/")) continue;
            string code = Code(path);

            var attributes = AttributeTarget.Matches(code);
            foreach (Match m in attributes) Add(file, m.Groups[1].Value + "." + (m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value));
            int unread = AttributeWithArgs.Matches(code).Count - attributes.Count;
            if (unread != 0) Fail(file + ": " + unread + " [HarmonyPatch(...)] attribute(s) this test can't read; teach AttributeTarget the new form");

            var runtime = RuntimeTarget.Matches(code);
            foreach (Match m in runtime) Add(file, (m.Groups[1].Value == "PropertySetter" ? "set " : m.Groups[1].Value == "PropertyGetter" ? "get " : "") + m.Groups[2].Value + "." + m.Groups[3].Value);
            unread = RuntimeWithArgs.Matches(code).Count - runtime.Count;
            if (unread != 0) Fail(file + ": " + unread + " harmony.Patch(...) call(s) this test can't read; teach RuntimeTarget the new form");

            foreach (Match _ in NewHarmony.Matches(code)) Add(file, "new Harmony");
            foreach (Match _ in TargetMethods.Matches(code)) Add(file, "TargetMethods");

            if (Transpiler.IsMatch(code)) Fail(file + ": a transpiler rewrites game code for everyone; game changes belong in src/TLDRevamp, not Dev");
            if (SkippingPrefix.IsMatch(code) && !MaySkip.Any(s => s.File == file))
                Fail(file + ": a prefix that returns bool or a write to __result changes what the game does; move it to src/TLDRevamp, or add the file to MaySkip with the reason");
        }

        var listed = Rows.ToDictionary(r => (r.File, r.Target), r => r);
        foreach (var r in Rows)
            if (string.IsNullOrWhiteSpace(r.Why)) Fail(r.File + " " + r.Target + ": the row needs a reason");
        foreach (var kv in found)
        {
            if (!listed.TryGetValue(kv.Key, out var row)) Fail("unlisted Dev patch: " + Line(kv.Key, kv.Value) + "  <- if players need it, move it to src/TLDRevamp");
            else if (row.Count != kv.Value) Fail(kv.Key.File + " " + kv.Key.Target + ": " + kv.Value + " patch(es) in the file, the row says " + row.Count);
        }
        foreach (var r in Rows)
            if (!found.ContainsKey((r.File, r.Target))) Fail("stale row (no such patch any more): " + r.File + " " + r.Target);
        foreach (var s in MaySkip)
            if (!File.Exists(Path.Combine(DevRoot, s.File))) Fail("stale MaySkip entry: " + s.File);
            else if (!SkippingPrefix.IsMatch(Code(Path.Combine(DevRoot, s.File)))) Fail("stale MaySkip entry (it skips nothing any more): " + s.File);

        if (_failures > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Every Harmony patch in " + DevRoot + " as rows (fill in the reason):");
            foreach (var kv in found) Console.WriteLine("        " + Line(kv.Key, kv.Value));
            Environment.Exit(1);
        }
        Console.WriteLine("PASS " + found.Values.Sum() + " Dev patch sites in " + found.Select(k => k.Key.File).Distinct().Count() + " files, each listed with a reason");
        Console.WriteLine("ALL PASS (Dev patches are developer-only)");
    }

    static string Line((string File, string Target) key, int count) => "(\"" + key.File + "\", \"" + key.Target + "\", " + count + ", \"\"),";
}
