using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TLDRevamp.Net;

static class ModsNeededTest
{
    static int fails;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }
    static NetReader Reader(NetWriter w)
    {
        var r = new NetReader(); r.Load(w.Len); Buffer.BlockCopy(w.Buf, 0, r.Buf, 0, w.Len); return r;
    }
    static readonly string Hash = new string('a', 64), Other = new string('b', 64);
    static ModsNeeded.Entry Need() => new ModsNeeded.Entry { Id = "api.id", Name = "Test Mod", Version = "1.0", Hash = Hash };
    static ModCatalog.Release Release(string hash, string version) => new ModCatalog.Release {
        Hash = hash, Version = version, Size = 10, Url = "https://github.com/o/r/releases/download/v/mod.dll"
    };
    static ModCatalog.Catalog Catalog() => new ModCatalog.Catalog { Mods = new List<ModCatalog.Mod> {
        new ModCatalog.Mod { Id = "plugin.guid", Source = "https://github.com/o/r", Versions = new List<ModCatalog.Release> {
            Release(Other, "2.0"), Release(Hash, "1.0") } }
    } };
    static ModsNeeded.Row Row(ModCatalog.Catalog catalog, string installed = null, bool disabled = false, bool manual = false, bool identical = false) =>
        ModsNeeded.Decide(Need(), catalog, installed, disabled, manual, identical, 1024, id => id == "plugin.guid");
    static void Bad(string name, NetWriter w) { var r = Reader(w); Check(name, ModsNeeded.Read(r) == null && r.Bad); }
    static int Main()
    {
        var w = new NetWriter(); var need = Need();
        var mods = new List<ModsNeeded.Entry> { need }; var problems = new List<string> { "remove Other 0.4 (the host doesn't have it)", "type: different object type version" };
        ModsNeeded.Write(w, "Host", mods, problems);
        var list = ModsNeeded.Read(Reader(w));
        Check("write/read identities, host and all problem lines", list != null && list.Host == "Host" && list.Mods[0].Id == need.Id &&
            list.Mods[0].Name == need.Name && list.Mods[0].Version == need.Version && list.Mods[0].Hash == Hash && list.Problems.SequenceEqual(problems));
        w.Len--; Bad("truncated refusal list", w);
        w.Reset(); w.Str("Host"); w.VarU32(ModsNeeded.MaxEntries + 1); Bad("required count bounded", w);
        w.Reset(); w.Str("Host"); w.VarU32(0); w.VarU32(ModsNeeded.MaxProblems + 1); Bad("problem count bounded", w);
        foreach (int field in Enumerable.Range(0, 4))
        {
            var bad = Need();
            if (field == 0) bad.Id = new string('x', 65);
            if (field == 1) bad.Name = new string('x', 257);
            if (field == 2) bad.Version = new string('x', 65);
            if (field == 3) bad.Hash = new string('a', 65);
            w.Reset(); ModsNeeded.Write(w, "Host", new[] { bad }, new string[0]); Bad("identity field length " + field, w);
        }
        w.Reset(); ModsNeeded.Write(w, new string('h', 257), mods, problems); Bad("host length bounded", w);
        w.Reset(); ModsNeeded.Write(w, "Host", mods, new[] { new string('p', 257) }); Bad("problem length bounded", w);
        w.Reset(); ModsNeeded.Write(w, "Host", mods, new[] { "bad\nline" }); Bad("control characters refused", w);
        w.Reset(); ModsNeeded.Write(w, "Host", mods, problems); w.U8(0); Bad("trailing bytes refused", w);
        w.Reset(); w.Str("Host"); w.VarU32(1); w.VarU32(uint.MaxValue); Bad("encoded length overflow refused before allocation", w);
        w.Reset(); ModsNeeded.Write(w, "Host", Enumerable.Repeat(need, ModsNeeded.MaxEntries).ToList(), Enumerable.Repeat("problem", ModsNeeded.MaxProblems).ToList());
        Check("exact count limits readable", ModsNeeded.Read(Reader(w))?.Mods.Count == ModsNeeded.MaxEntries);
        var catalog = Catalog(); var row = Row(catalog);
        Check("install exact older hash despite different API and plugin IDs", row.Do == ModsNeeded.Action.Install && row.Release.Version == "1.0");
        row = Row(catalog, "0.1"); Check("different installed version shown and replaceable", row.Text.Contains("you have 0.1") && row.Do == ModsNeeded.Action.Install);
        row = Row(new ModCatalog.Catalog { Mods = new List<ModCatalog.Mod>() });
        Check("not in catalog has author advice", row.Do == ModsNeeded.Action.None && row.Text.Contains("get it from its author"));
        catalog.Mods[0].Versions.RemoveAt(1); row = Row(catalog);
        Check("other catalog hash never installable", row.Do == ModsNeeded.Action.None);
        row = Row(null); Check("catalog unavailable", row.Do == ModsNeeded.Action.None && row.Text.Contains("mod list unavailable"));
        row = Row(Catalog(), "1.0", disabled: true, identical: true);
        Check("disabled identical mod offers Enable", row.Do == ModsNeeded.Action.Enable && row.Text.Contains("disabled"));
        row = Row(Catalog(), "0.1", disabled: true);
        Check("disabled older copy can be enabled as well as updated", row.Do == ModsNeeded.Action.Install && row.CanEnable && row.Text.Contains("disabled"));
        row = Row(Catalog(), "0.1", manual: true);
        Check("manual copy never changed", row.Do == ModsNeeded.Action.None && row.Text.Contains("replace your copy of Test Mod by hand"));
        row = Row(Catalog(), "1.0", identical: true);
        Check("installed exact file requires restart rather than reinstall", row.Do == ModsNeeded.Action.None && row.Text.Contains("restart"));
        row = ModsNeeded.Problem(problems[0]); Check("remove row has no install action", row.Do == ModsNeeded.Action.None && row.Text.StartsWith("Remove: Other"));
        Check("copy list is plain names, versions, catalog source links and problems", ModsNeeded.Copy(list, Catalog()) ==
            "Test Mod 1.0 https://github.com/o/r\nRemove: Other 0.4 (the host doesn't have it)\ntype: different object type version");
        return fails == 0 ? 0 : 1;
    }
}
