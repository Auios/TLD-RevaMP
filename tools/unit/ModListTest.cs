using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TLDRevamp.Net;

static class ModListTest
{
    static int fails;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }
    static ModCatalog.Mod Mod(string id, string name, string author, params string[] urls) => new ModCatalog.Mod
    {
        Id = id, Name = name, Author = author, Description = name + " does things.", Tags = new List<string>(),
        Versions = urls.Select(u => new ModCatalog.Release { Version = "1.0.0", Url = u }).ToList()
    };

    static int Main()
    {
        const string json = @"[
          {""draft"": false, ""published_at"": ""2026-11-18T10:00:00Z"", ""assets"": [
            {""browser_download_url"": ""https://github.com/TLD-RevaMP/TLD-RevaMP/releases/download/v0.69.1/HeadUnit-0.2.1.zip"", ""download_count"": 120},
            {""browser_download_url"": ""https://github.com/TLD-RevaMP/TLD-RevaMP/releases/download/v0.69.1/CarTrailer-0.1.2.zip"", ""download_count"": 7}]},
          {""draft"": false, ""published_at"": ""2026-10-01T10:00:00Z"", ""assets"": [
            {""browser_download_url"": ""https://github.com/TLD-RevaMP/TLD-RevaMP/releases/download/v0.69.0/HeadUnit-0.2.0.zip"", ""download_count"": 1300}]},
          {""draft"": true, ""published_at"": null, ""assets"": [
            {""browser_download_url"": ""https://github.com/TLD-RevaMP/TLD-RevaMP/releases/download/v0.70.0/HeadUnit-0.3.0.zip"", ""download_count"": 999}]}]";
        var stats = ModList.Parse(Encoding.UTF8.GetBytes(json));
        Check("release JSON: three published assets, the draft left out", stats.Count == 3 && !stats.ContainsKey("v0.70.0/HeadUnit-0.3.0.zip"));
        Check("asset key is tag/file", ModList.AssetKey("https://github.com/x/y/releases/download/v1/a.zip") == "v1/a.zip" && ModList.AssetKey("https://example.com/a.zip") == null);

        // a mod whose catalog still uses the project's old address: matched by tag/file, not by owner
        var head = Mod("tldrevamp.headunit", "Head Unit", "Normal99",
            "https://github.com/Normal99/TLD-RevaMP/releases/download/v0.69.1/HeadUnit-0.2.1.zip",
            "https://github.com/Normal99/TLD-RevaMP/releases/download/v0.69.0/HeadUnit-0.2.0.zip");
        var trailer = Mod("tldrevamp.cartrailer", "Car Trailer", "Normal99", "https://github.com/TLD-RevaMP/TLD-RevaMP/releases/download/v0.69.1/CarTrailer-0.1.2.zip");
        var bus = Mod("anonymoustld.busgut", "BusGut", "Someone", "https://github.com/TLD-RevaMP/TLD-Mods/releases/download/busgut-v0.2.0/BusGut-0.2.0.zip");
        bus.Tags.Add("buses");
        var s = ModList.ForMod(head, stats);
        Check("a mod's downloads add up over its versions (1300 + 120)", s.Downloads == 1420);
        Check("its date is the newest release's", s.Published == new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc));
        Check("no numbers for a mod GitHub didn't list", ModList.ForMod(bus, stats).Downloads == 0 && ModList.ForMod(bus, stats).Published == default);

        var all = new List<ModCatalog.Mod> { bus, trailer, head };
        Check("repos: each once, in catalog order", string.Join(",", ModList.Repos(all)) == "TLD-RevaMP/TLD-Mods,TLD-RevaMP/TLD-RevaMP,Normal99/TLD-RevaMP");
        Check("not a GitHub release: no repo", ModList.Repo("https://github.com/x/y/archive/a.zip") == null && ModList.Repo(null) == null);

        string Names(IEnumerable<ModCatalog.Mod> l) => string.Join(",", l.Select(m => m.Name));
        Check("most downloaded first, ties by name", Names(ModList.View(all, "", false, m => true, ModList.Sort.Downloads, stats)) == "Head Unit,Car Trailer,BusGut");
        Check("recently updated first", Names(ModList.View(all, "", false, m => true, ModList.Sort.Updated, stats)) == "Car Trailer,Head Unit,BusGut");
        Check("name order ignores case", Names(ModList.View(all, "", false, m => true, ModList.Sort.Name, stats)) == "BusGut,Car Trailer,Head Unit");
        Check("search: name, any case", Names(ModList.View(all, "HEAD", false, m => true, ModList.Sort.Name, stats)) == "Head Unit");
        Check("search: author", Names(ModList.View(all, "someone", false, m => true, ModList.Sort.Name, stats)) == "BusGut");
        Check("search: tag", Names(ModList.View(all, "buses", false, m => true, ModList.Sort.Name, stats)) == "BusGut");
        Check("search: nothing matches", ModList.View(all, "zzz", false, m => true, ModList.Sort.Name, stats).Count == 0);
        Check("Installed tab: only installed mods", Names(ModList.View(all, "", true, m => m.Id != "tldrevamp.cartrailer", ModList.Sort.Name, stats)) == "BusGut,Head Unit");
        Check("no stats at all: every order still works", Names(ModList.View(all, "", false, m => true, ModList.Sort.Downloads, new Dictionary<string, ModList.Stat>())) == "BusGut,Car Trailer,Head Unit");

        Check("short numbers", ModList.Short(999) == "999" && ModList.Short(1420) == "1.4K" && ModList.Short(2500000) == "2.5M");
        var now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        Check("ages", ModList.Ago(now.AddMinutes(-5), now) == "just now" && ModList.Ago(now.AddHours(-5), now) == "5 hours ago" &&
              ModList.Ago(now.AddDays(-3), now) == "3 days ago" && ModList.Ago(now.AddDays(-90), now) == "3 months ago" &&
              ModList.Ago(new DateTime(2024, 1, 2), now) == "2 Jan 2024");
        Check("initials", ModList.Initials("Head Unit") == "HU" && ModList.Initials("BusGut") == "BU" && ModList.Initials("") == "?");
        Check("tile colour steady from the id", ModList.Hue("tldrevamp.headunit") == ModList.Hue("tldrevamp.headunit") && ModList.Hue("a") != ModList.Hue("b"));

        // the shipped catalog parses with tags absent (optional field)
        var catalog = ModCatalog.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "mods.json")));
        Check("real catalog: tags optional, every mod has a (maybe empty) list", catalog.Mods.All(m => m.Tags != null));
        var tagged = ModCatalog.Parse(Encoding.UTF8.GetBytes(@"{""format"":1,""mods"":[{""id"":""a.b"",""name"":""A"",""tags"":["" cars "",""cars"","""",""aaaaaaaaaaaaaaaaaaaaaaaaaaaaa"",""1"",""2"",""3"",""4"",""5"",""6""],
            ""versions"":[{""version"":""1.0.0"",""url"":""https://github.com/o/r/releases/download/v1/a.zip""}]}]}"));
        Check("catalog tags: trimmed, each once, none empty or long, at most six", string.Join(",", tagged.Mods[0].Tags) == "cars,1,2,3,4,5");

        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
        return fails == 0 ? 0 : 1;
    }
}
