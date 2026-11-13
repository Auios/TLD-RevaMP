using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Text.RegularExpressions;
// Every path in src/TLDRevamp/Switches.cs names a static field that still exists, once each: the one list of runtime
// switches can't go stale when a switch is renamed or removed. Run from the checkout root (or pass it).
static class P {
  static int fails = 0;
  static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }
  static int Main(string[] args) {
    string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
    string src = Path.Combine(root, "src", "TLDRevamp");
    string list = File.ReadAllText(Path.Combine(src, "Switches.cs"));
    var paths = Regex.Matches(list, "\\(\"(TLDRevamp\\.[A-Za-z0-9_.]+)\", Kind\\.(Test|OptIn|Config|Other)").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
    Check("the list has switches", paths.Count > 100);
    Check("each switch once", paths.Distinct().Count() == paths.Count);
    var files = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Concat(Directory.GetFiles(Path.Combine(root, "src/TLDRevamp.Dev"), "*.cs", SearchOption.AllDirectories))
      .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
      .Select(File.ReadAllText).ToList();
    var missing = new List<string>();
    foreach (var p in paths) {
      int dot = p.LastIndexOf('.');
      string type = p.Substring(0, dot), field = p.Substring(dot + 1), cls = type.Substring(type.LastIndexOf('.') + 1);
      // Dev registers the original member paths through Reflect.RegisterMovedMembers.
      string owners = cls == "Entities" ? "Entities|EntityDebug|EntityMountLab|EntityReproPlayback|EntityItemCheck|EntityItemWatch|EntityContactCensus|EntityFakeInput|EntityTrack"
        : cls == "ReplayRecorder" ? "ReplayRecorder|ReplayTruthCapture" : Regex.Escape(cls);
      var classRe = new Regex(@"\b(class|struct)\s+(?:" + owners + @")\b");
      var fieldRe = new Regex(@"\bstatic\b[^;{(]*\b" + Regex.Escape(field) + @"\b\s*(=|;|,|\{)");
      if (!files.Any(t => classRe.IsMatch(t) && fieldRe.IsMatch(t))) missing.Add(p);
    }
    Check("every listed switch exists" + (missing.Count > 0 ? ": missing " + string.Join(", ", missing.Take(10)) : ""), missing.Count == 0);
    Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
    return fails == 0 ? 0 : 1;
  }
}
