using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// docs/MESSAGES.md against the code: every routed message has a row, the row's id and direction are what the code does,
/// and a message that changes lasting state says what heals it or is on the short list of known gaps below.
static class MessageTableTest
{
    static int failures;
    static void Check(string name, bool ok) { if (!ok) { Console.WriteLine("FAIL " + name); failures++; } }

    /// State messages nothing heals. Fixing one removes it from here and from the table; adding one has to be a decision.
    static readonly string[] KnownGaps = { "Share", "Assigned", "Add", "LeaseReq", "LeaseTaken", "ModShare", "ModRemove", "ModEvent" };

    static readonly string[] Kinds = { "state", "stream", "ask", "effect", "session" };

    class Row { public int Id; public string Name, Dir, Kind, Heal, Note; }

    static List<Row> ReadTable(string text)
    {
        var rows = new List<Row>();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (!Regex.IsMatch(line, @"^\| *\d+ *\|")) continue;
            var c = line.Trim('|').Split('|').Select(x => x.Trim()).ToArray();
            Check("a row has six columns: " + line, c.Length == 6);
            if (c.Length == 6) rows.Add(new Row { Id = int.Parse(c[0]), Name = c[1], Dir = c[2], Kind = c[3], Heal = c[4], Note = c[5] });
        }
        return rows;
    }

    /// Class -> const name -> value, over every `const byte` in the source.
    static Dictionary<string, Dictionary<string, int>> ReadConsts(Dictionary<string, string> src)
    {
        var all = new Dictionary<string, Dictionary<string, int>>();
        foreach (string text in src.Values)
        {
            var classes = Regex.Matches(text, @"\bclass (\w+)").Cast<Match>().ToList();
            foreach (Match m in Regex.Matches(text, @"const byte ([^;]+);"))
            {
                string owner = classes.LastOrDefault(k => k.Index < m.Index)?.Groups[1].Value ?? "";
                if (!all.TryGetValue(owner, out var names)) all[owner] = names = new Dictionary<string, int>();
                foreach (Match a in Regex.Matches(m.Groups[1].Value, @"(\w+)\s*=\s*(\d+)")) names[a.Groups[1].Value] = int.Parse(a.Groups[2].Value);
            }
        }
        return all;
    }

    static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string sourceRoot = Path.Combine(root, "src", "TLDRevamp");
        var src = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "src", "TLDRevamp.Dev"), "*.cs", SearchOption.AllDirectories))
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToDictionary(p => p, p => File.ReadAllText(p).Replace("\r\n", "\n"));
        string allSource = string.Join("\n", src.Values);
        var consts = ReadConsts(src);
        var rows = ReadTable(File.ReadAllText(Path.Combine(root, "docs", "MESSAGES.md")));
        Check("the table has rows", rows.Count > 0);
        Check("ids are unique", rows.Select(r => r.Id).Distinct().Count() == rows.Count);
        Check("sorted by id", rows.Select(r => r.Id).SequenceEqual(rows.Select(r => r.Id).OrderBy(i => i)));

        // what the dispatchers route: Mp.cs's two switches, then Entities' own
        string mp = File.ReadAllText(Path.Combine(sourceRoot, "Net", "Mp.cs")).Replace("\r\n", "\n");
        int serverAt = mp.IndexOf("switch (type)"), serverEnd = mp.IndexOf("void OnTransport", serverAt), clientAt = mp.IndexOf("switch (type)", serverEnd);
        Check("Mp.cs has its two dispatchers", serverAt > 0 && serverEnd > serverAt && clientAt > serverEnd);
        string Method(string owner, string name)
        {
            string bodies = string.Join("\n", src.Values.Where(t => Regex.IsMatch(t, @"\bclass\s+" + Regex.Escape(owner) + @"\b")));
            return Regex.Match(bodies, @"void " + name + @"\(.*?\n        \}\n", RegexOptions.Singleline).Value;
        }
        string server = Method("Entities", "ServerReceive"), client = Method("Entities", "ClientReceive");
        foreach (string name in new[] { "Command", "Freeze", "Frozen", "Result" })
        {
            string handler = "ApplyHitch" + name;
            Check("Hitch" + name + ": client routes to hitch owner", client.Contains("case Hitch" + name + ": EntityHitches." + handler + "(r);"));
            string body = Method("EntityHitches", handler);
            Check("Hitch" + name + ": owner reads payload", body.Contains("r.U32()") || body.Contains("ReadHitch(r)"));
        }
        foreach (string name in new[] { "Request", "Frozen", "Result" })
            Check("Hitch" + name + ": authority remains on kernel", server.Contains("case Hitch" + name + ": ServerHitch" + name + "(from, r);") && Method("Entities", "ServerHitch" + name).Contains("Server.TryGetValue("));
        var toServer = Routed(mp.Substring(serverAt, serverEnd - serverAt), "Entities.ServerReceive(", server, consts);
        var toClient = Routed(mp.Substring(clientAt), "Entities.ClientReceive(", client, consts);
        Check("the dispatchers route messages", toServer.Count > 10 && toClient.Count > 10);
        foreach (var handler in new[] { ("RabbitState", "EntityRabbits", "ApplyRabbitState"), ("RabbitFx", "EntityRabbits", "ApplyRabbitFx"), ("BreakHit", "EntityAI", "ApplyBreakHit"), ("BreakFx", "EntityAI", "ApplyBreakFx"), ("WorldBreak", "EntityAI", "ApplyWorldBreak"), ("AiState", "EntityAI", "ApplyAiState"), ("AiSound", "EntityAI", "ApplyAiSound"), ("Worn", "EntityHands", "ApplyWorn"), ("IgnitionKey", "EntitySeats", "ApplyKey"), ("ShotgunIn", "EntitySeats", "ApplyShotgun"), ("RadioSync", "EntityRadio", "ApplyRadio"), ("WorldClock", "EntityWorldClock", "ReceiveWorldClock"), ("StormSync", "EntityWeather", "ApplyStorms"), ("ShotFx", "EntityEffects", "ApplyShotFx"), ("PlayerSound", "EntityEffects", "ApplyPlayerSound"), ("ExplodeReq", "EntityExplosions", "ApplyExplodeReq"), ("ExplosionFx", "EntityExplosions", "ApplyExplosionFx") })
        {
            Check(handler.Item1 + ": routed to its feature owner", client.Contains("case " + handler.Item1 + ": " + handler.Item2 + "." + handler.Item3 + "(r);"));
            Check(handler.Item1 + ": handler reads the message at its new owner", Method(handler.Item2, handler.Item3).Contains("r."));
        }

        foreach (var handler in new[] { ("IgnitionKey", "ServerKey"), ("ShotgunIn", "ServerShotgun") })
        {
            Check(handler.Item1 + ": server routes to seats", server.Contains("case " + handler.Item1 + ": EntitySeats." + handler.Item2 + "(from, r);"));
            Check(handler.Item1 + ": server owner reads payload", Method("EntitySeats", handler.Item2).Contains("r.U32()"));
        }

        Check("RabbitState: authority stays on kernel", server.Contains("ServerRabbitState(from, r);") && Method("Entities", "ServerRabbitState").Contains("se.Epoch != s.Epoch") && Method("Entities", "ServerRabbitState").Contains("se.OwnerId != from"));
        Check("LiveUsable: routing and authority stay on kernel", server.Contains("ServerLiveUsable(from, r);") && client.Contains("ApplyLiveUsable(r);") && Method("Entities", "ServerLiveUsable").Contains("k.Epoch != se.Epoch"));
        Check("PoiUsable: handlers remain beside live sequence state", server.Contains("ServerPoiUsable(from, r);") && client.Contains("ApplyPoiUsable(r);") && Method("Entities", "ServerPoiMotion").Contains("_poiServerOrder"));
        Check("LiveUsable: bytes read by usables owner", Method("EntityUsables", "ReadLiveUsable").Contains("r.U32()") && Method("EntityUsables", "ReadLiveUsable").Contains("r.U16()"));
        Check("PoiUsable: payload codec at usables owner", src.Values.Any(s => s.Contains("class EntityUsables") && s.Contains("internal static bool ReadPoi(") && s.Contains("X = r.F64()")));
        foreach (var handler in new[] { ("Edit", "ServerEdit", "ApplyEdit"), ("Resync", "ServerResync", "ApplyResync"), ("HandoverState", "ServerHandover", "ApplyHandover") })
        {
            Check(handler.Item1 + ": authority and routing remain on kernel", server.Contains("case " + handler.Item1 + ": " + handler.Item2 + "(from, r);") && client.Contains("case " + handler.Item1 + ": " + handler.Item3 + "(r);"));
            Check(handler.Item1 + ": retained handlers read their payload", Method("Entities", handler.Item2).Contains("r.U32()") && Method("Entities", handler.Item3).Contains("r.U32()"));
        }
        Check("Resync: record normalization has its own owner", src.Values.Any(s => s.Contains("class EntityResync") && s.Contains("internal static byte[] StateRecord(") && s.Contains("hash = h;")));
        Check("Edit: POI validation and rate state have their own owner", src.Values.Any(s => s.Contains("class EntityEditCheck") && s.Contains("internal static bool CheckPoiUsable(") && s.Contains("private static readonly Dictionary<int, RateBucket>")));
        Check("TPA: refresh preprocessing owner keeps defer before copy", client.IndexOf("EntityTeleportRefresh.DeferTpa(type, r)") >= 0 && client.IndexOf("EntityTeleportRefresh.DeferTpa(type, r)") < client.IndexOf("EntityTeleportRefresh.TpaCopyMessage(type, r)"));
        Check("RabbitState: stale counter belongs to rabbits", Method("Entities", "ServerRabbitState").Contains("EntityRabbits.RabbitStatesStale++"));
        Check("RabbitState: host keeps durable snapshot before relaying", Method("Entities", "ServerRabbitState").IndexOf("se.RabbitSnapshot = FinalSnapshot(r, true);") < Method("Entities", "ServerRabbitState").IndexOf("WS.Reset();"));
        Check("RabbitFx: reliable generic relay stays on kernel", server.Contains("case RabbitFx: ServerFwdAll(from, r, true);"));
        Check("RabbitState: receiver uses existing codec", Method("EntityRabbits", "ApplyRabbitState").Contains("RabbitStateCodec.Read(r)"));

        Check("State: limb tail reads at ragdoll owner", client.Contains("EntityRagdolls.ReadRagdoll(r, e, sp, sq);"));
        Check("State: late keys reach ragdoll owner", client.Contains("EntityRagdolls.KeepLateKey(r, msgAt, e, epoch)"));
        Check("State: ragdoll owner reads its block", Method("EntityRagdolls", "ReadRagdoll").Contains("RagdollWire.Read(r, b)"));
        Check("RemoveItem: dormant death removal stays reliable at ragdoll owner", Method("EntityRagdolls", "ReleaseDeathCorpse").Contains("W.U8(Entities.RemoveItem); W.U32(net);") && Method("EntityRagdolls", "ReleaseDeathCorpse").Contains("Entities.SendToServer(W, true);"));

        Check("Worn: server routes to hands", server.Contains("case Worn: EntityHands.ServerWorn(from, r);"));
        Check("Worn: hands reads request payload", Method("EntityHands", "ServerWorn").Contains("r.U32()"));
        Check("CarTune: routes to its owner", client.Contains("case CarTune: EntityCarTuning.ApplyCarTune(r);"));
        Check("CarTune: owner reads its payload", Method("EntityCarTuning", "ApplyCarTune").Contains("r.U32()"));
        Check("CarTune: keep before reliable forward", server.Contains("case CarTune: if (EntityCarTuning.ServerKeepTune(from, r)) ServerFwdAll(from, r, true); break;"));
        Check("BreakFx: keep before shared reliable forward", server.Contains("case BreakFx: EntityAI.ServerKeepBreak(r); ServerFwdAll(from, r, true); break;"));
        Check("WorldBreak: keep before shared reliable forward", server.Contains("case WorldBreak: EntityAI.ServerKeepWorldBreak(r); ServerFwdAll(from, r, true); break;"));
        Check("BreakHit: authority and shared writer stay in kernel", server.Contains("case BreakHit: ServerBreakHit(from, r);") && Method("Entities", "ServerBreakHit").Contains("se.OwnerId == from") && Method("Entities", "ServerBreakHit").Contains("ServerSendTo(se.OwnerId, WS, true);"));
        Check("AiState: stale authority still counted at feature owner", server.Contains("se.OwnerId != from) { EntityAI.AiStatesStale++; break; }") && server.Contains("r.Remaining != 15) { EntityAI.AiStatesStale++; break; }"));
        Check("AiSound: shared reliable forward unchanged", server.Contains("case AiSound: ServerFwdAll(from, r, true);"));
        var byId = rows.ToDictionary(r => r.Id, r => r, EqualityComparer<int>.Default);
        foreach (int id in toServer.Concat(toClient).Distinct().OrderBy(i => i))
            Check("a routed message has a row: id " + id, byId.ContainsKey(id));

        var names = consts.SelectMany(k => k.Value).ToLookup(n => n.Key, n => n.Value);
        foreach (var r in rows)
        {
            Check($"{r.Id} {r.Name}: the code has this name at this id", names[r.Name].Contains(r.Id));
            Check($"{r.Id} {r.Name}: kind '{r.Kind}' is one of the five", Kinds.Contains(r.Kind));
            bool s = toServer.Contains(r.Id), c = toClient.Contains(r.Id);
            string dir = s && c ? "both" : s ? "c>s" : c ? "s>c" : "none";
            Check($"{r.Id} {r.Name}: direction '{r.Dir}', the dispatchers say {dir}", r.Dir == dir || (dir == "none" && r.Dir == "transport"));
            bool lasting = r.Kind == "state" || r.Kind == "stream";
            if (lasting)
            {
                bool gap = r.Heal == "gap";
                var ids = Regex.Matches(r.Heal, @"`(\w+)`").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
                Check($"{r.Id} {r.Name}: says what heals it", gap || ids.Count > 0);
                Check($"{r.Id} {r.Name}: a stream is healed by the next message, it can't be a gap", !(gap && r.Kind == "stream"));
                Check($"{r.Id} {r.Name}: a gap says what stays different", !gap || r.Note.Length > 0);
                foreach (string id in ids) Check($"{r.Id} {r.Name}: `{id}` exists in the source", Regex.IsMatch(allSource, @"\b" + id + @"\b"));
            }
            else Check($"{r.Id} {r.Name}: only state and stream rows have a heal path", r.Heal == "-");
            Check($"{r.Id} {r.Name}: is a gap exactly when the list below says so", (r.Heal == "gap") == KnownGaps.Contains(r.Name));
        }
        foreach (string gap in KnownGaps) Check("the known gap is in the table: " + gap, rows.Any(r => r.Name == gap));

        Console.WriteLine(failures == 0 ? $"ALL PASS ({rows.Count} messages, {KnownGaps.Length} known gaps)" : failures + " failures");
        return failures == 0 ? 0 : 1;
    }

    /// The message ids a dispatcher accepts. The labels in front of a call to Entities' receiver only count if it has a case
    /// for them: the lists name a few it ignores.
    static HashSet<int> Routed(string dispatcher, string forward, string receiver, Dictionary<string, Dictionary<string, int>> consts)
    {
        var ids = Labels(dispatcher, consts);
        var handled = Labels(receiver, consts);
        for (int at = dispatcher.IndexOf(forward); at >= 0; at = dispatcher.IndexOf(forward, at + 1))
        {
            int start = dispatcher.LastIndexOf("break;", at);
            foreach (int id in Labels(dispatcher.Substring(start, at - start), consts)) if (!handled.Contains(id)) ids.Remove(id);
        }
        return ids;
    }

    static HashSet<int> Labels(string code, Dictionary<string, Dictionary<string, int>> consts)
    {
        var ids = new HashSet<int>();
        foreach (Match m in Regex.Matches(code, @"case (?:(\w+)\.)?(\w+):"))
        {
            if (int.TryParse(m.Groups[2].Value, out int reserved)) { ids.Add(reserved); continue; }
            string owner = m.Groups[1].Success ? m.Groups[1].Value : "Entities";
            if (consts.TryGetValue(owner, out var names) && names.TryGetValue(m.Groups[2].Value, out int id)) { ids.Add(id); continue; }
            // a file's nested class can stand in front of a const of the outer one: a name only one class declares is that one
            var others = consts.Values.Where(n => n.ContainsKey(m.Groups[2].Value)).Select(n => n[m.Groups[2].Value]).Distinct().ToList();
            if (others.Count == 1) ids.Add(others[0]);
        }
        return ids;
    }
}
