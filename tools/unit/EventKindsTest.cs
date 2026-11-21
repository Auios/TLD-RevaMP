using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// Marks and bug reports carry the session's events through TelemetryPrivacy, which turns any event kind it doesn't
/// list into "[removed]" and any result it doesn't list into "[removed]" too. A new SessionLog.Event kind that nobody
/// added to the list is therefore invisible exactly where it is needed: 24 kinds were, until 2026-11-21 (the car
/// ownership history among them). This test finds every SessionLog.Event call in src/ and fails when:
///   - its kind (a string literal, or each literal of a ?: choice) is not an allowed kind;
///   - its kind is not a literal and the call is not listed in Indirect with the kinds it can produce;
///   - its result is one plain string literal that is not an allowed result (a fixed word lost for nothing).
/// Results built from names or free text are meant to be removed and are not checked.
static class EventKindsTest
{
    /// Calls whose kind comes from elsewhere: file, the kind argument as written, the kinds it can be.
    static readonly (string File, string Arg, string[] Kinds)[] Indirect =
    {
        ("TLDRevamp/Net/Session.cs", "tpa", new[] { "tpa-request-sent", "tpa-accept-sent", "tpa-refuse-sent" }),   // Tpa.CommandKind
    };

    static int _failures;
    static void Fail(string why) { _failures++; Console.WriteLine("FAIL " + why); }

    static string WithoutLineComments(string path) => string.Join("\n", File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//")));
    static IEnumerable<string> Literals(string code) => Regex.Matches(code, "\"((?:[^\"\\\\]|\\\\.)*)\"").Select(m => m.Groups[1].Value);

    static HashSet<string> Region(string code, string start, string end, bool split)
    {
        int a = code.IndexOf(start, StringComparison.Ordinal);
        if (a < 0) throw new Exception("TelemetryPrivacy.cs: '" + start + "' not found");
        int b = code.IndexOf(end, a, StringComparison.Ordinal);
        var words = Literals(code.Substring(a, b - a));
        return new HashSet<string>(split ? words.SelectMany(w => w.Split(' ', StringSplitOptions.RemoveEmptyEntries)) : words, StringComparer.Ordinal);
    }

    /// From just after "SessionLog.Event(": the arguments up to the call's closing parenthesis, split at top-level commas.
    static List<string> Arguments(string code, int from)
    {
        var args = new List<string>(); int depth = 0, start = from; bool inString = false;
        for (int i = from; i < code.Length; i++)
        {
            char c = code[i];
            if (inString) { if (c == '\\') i++; else if (c == '"') inString = false; continue; }
            if (c == '"') inString = true;
            else if (c == '(' || c == '[' || c == '{') depth++;
            else if ((c == ')' || c == ']' || c == '}') && depth > 0) depth--;
            else if (c == ',' && depth == 0) { args.Add(code.Substring(start, i - start).Trim()); start = i + 1; }
            else if (c == ')' && depth == 0) { args.Add(code.Substring(start, i - start).Trim()); return args; }
        }
        throw new Exception("unclosed SessionLog.Event call");
    }

    static void Main()
    {
        const string privacyPath = "src/TLDRevamp/TelemetryPrivacy.cs";
        if (!File.Exists(privacyPath)) throw new Exception("run from the repository root");
        string privacy = WithoutLineComments(privacyPath);
        var kinds = Region(privacy, "EventKinds = new HashSet<string>((", "StringComparer.Ordinal);", true);
        var results = Region(privacy, "Results = new HashSet<string>(new[] {", "}, StringComparer.Ordinal);", false);
        if (kinds.Count < 50 || results.Count < 50) throw new Exception("TelemetryPrivacy.cs lists look wrong: " + kinds.Count + " kinds, " + results.Count + " results");

        int calls = 0; var used = new HashSet<string>(StringComparer.Ordinal); var indirectSeen = new HashSet<string>();
        foreach (string path in Directory.GetFiles("src", "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string file = Path.GetRelativePath("src", path).Replace('\\', '/');
            if (file.Contains("/obj/") || file.Contains("/bin/")) continue;
            string code = WithoutLineComments(path);
            foreach (Match m in Regex.Matches(code, @"\bSessionLog\.Event\("))
            {
                calls++;
                var args = Arguments(code, m.Index + m.Length);
                string kindArg = args[0];
                int line = code.Take(m.Index).Count(ch => ch == '\n') + 1;
                string where = file + ":" + line;
                // a literal, or a choice between two (cond ? "a" : "b"; literals in the condition are not kinds)
                var single = Regex.Match(kindArg, "^\"([^\"]*)\"$");
                var choice = Regex.Match(kindArg, "\\?\\s*\"([^\"]*)\"\\s*:\\s*\"([^\"]*)\"$");
                var literalKinds = single.Success ? new List<string> { single.Groups[1].Value }
                    : choice.Success ? new List<string> { choice.Groups[1].Value, choice.Groups[2].Value } : new List<string>();
                if (literalKinds.Count == 0)
                {
                    var known = Indirect.FirstOrDefault(x => x.File == file && x.Arg == kindArg);
                    if (known.Kinds == null) { Fail(where + ": kind '" + kindArg + "' is not a literal; list the call in Indirect with the kinds it can be"); continue; }
                    indirectSeen.Add(file + "|" + kindArg);
                    literalKinds = known.Kinds.ToList();
                }
                foreach (string k in literalKinds)
                {
                    used.Add(k);
                    if (!kinds.Contains(k)) Fail(where + ": event kind \"" + k + "\" is not in TelemetryPrivacy.EventKinds - marks and bug reports show it as [removed]");
                }
                var result = args.Skip(1).FirstOrDefault(a => a.StartsWith("result:"));
                if (result != null)
                {
                    var plain = Regex.Match(result.Substring(7).Trim(), "^\"((?:[^\"\\\\]|\\\\.)*)\"$");
                    if (plain.Success && !results.Contains(plain.Groups[1].Value))
                        Fail(where + ": result \"" + plain.Groups[1].Value + "\" is a fixed word but not in TelemetryPrivacy.Results - it arrives as [removed]");
                }
            }
        }
        foreach (var x in Indirect)
            if (!indirectSeen.Contains(x.File + "|" + x.Arg)) Fail("stale Indirect entry: " + x.File + " " + x.Arg);
        if (calls < 100) Fail("only " + calls + " SessionLog.Event calls found: the scan is broken");
        if (_failures > 0) Environment.Exit(1);
        Console.WriteLine("PASS " + calls + " SessionLog.Event calls, " + used.Count + " kinds, every kind and fixed result allowed by TelemetryPrivacy");
        Console.WriteLine("ALL PASS (event kinds reach marks and reports)");
    }
}
