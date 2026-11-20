using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TLDRevamp.Net
{
    /// The Mods list's rules without Unity (tools/unit/ModListTest): download numbers from GitHub's release lists, the
    /// search, the Installed tab and the sort orders. Same numbers and orders as the RevaMP launcher's Mods page.
    public static class ModList
    {
        public enum Sort { Downloads, Updated, Name }
        public static readonly string[] SortNames = { "Most downloaded", "Recently updated", "Name" };

        public struct Stat { public long Downloads; public DateTime Published; }

        /// The part of a release asset URL that names it ("tag/file"): the catalog's URLs may still use a project's old
        /// address, which GitHub redirects.
        public static string AssetKey(string url)
        {
            if (url == null) return null;
            const string marker = "/releases/download/";
            int i = url.IndexOf(marker, StringComparison.Ordinal);
            return i < 0 ? null : url.Substring(i + marker.Length);
        }

        /// The GitHub repositories ("owner/name") the catalog's downloads come from, each once.
        public static List<string> Repos(IEnumerable<ModCatalog.Mod> mods)
        {
            var list = new List<string>();
            foreach (var m in mods)
                foreach (var v in m.Versions ?? new List<ModCatalog.Release>())
                {
                    string repo = Repo(v.Url);
                    if (repo != null && !list.Exists(r => string.Equals(r, repo, StringComparison.OrdinalIgnoreCase))) list.Add(repo);
                }
            return list;
        }

        public static string Repo(string url)
        {
            const string host = "https://github.com/";
            if (url == null || !url.StartsWith(host, StringComparison.Ordinal) || AssetKey(url) == null) return null;
            var parts = url.Substring(host.Length).Split('/');
            return parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0 ? parts[0] + "/" + parts[1] : null;
        }

        public static string ReleasesUrl(string repo) => "https://api.github.com/repos/" + repo + "/releases?per_page=100";

#pragma warning disable CS0649   // filled by the JSON reader
        [DataContract] private sealed class GhRelease
        {
            [DataMember(Name = "draft")] public bool Draft;
            [DataMember(Name = "published_at")] public string Published;
            [DataMember(Name = "assets")] public List<GhAsset> Assets;
        }
        [DataContract] private sealed class GhAsset
        {
            [DataMember(Name = "browser_download_url")] public string Url;
            [DataMember(Name = "download_count")] public long Downloads;
        }
#pragma warning restore CS0649

        /// Downloads and publish date of every release asset in GitHub's releases JSON, by AssetKey; drafts left out.
        public static Dictionary<string, Stat> Parse(byte[] json)
        {
            var d = new Dictionary<string, Stat>(StringComparer.Ordinal);
            if (json == null || json.Length == 0) return d;
            List<GhRelease> releases;
            using (var s = new MemoryStream(json, false))
                releases = (List<GhRelease>)new DataContractJsonSerializer(typeof(List<GhRelease>)).ReadObject(s);
            foreach (var r in releases ?? new List<GhRelease>())
            {
                if (r == null || r.Draft) continue;
                DateTime.TryParse(r.Published, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at);
                foreach (var a in r.Assets ?? new List<GhAsset>())
                {
                    string key = AssetKey(a?.Url);
                    if (key != null) d[key] = new Stat { Downloads = Math.Max(0, a.Downloads), Published = at };
                }
            }
            return d;
        }

        /// A mod's downloads (every version in the catalog) and the date of its newest release.
        public static Stat ForMod(ModCatalog.Mod m, IDictionary<string, Stat> stats)
        {
            var s = new Stat();
            if (m?.Versions == null || stats == null) return s;
            foreach (var v in m.Versions)
            {
                string key = AssetKey(v?.Url);
                if (key != null && stats.TryGetValue(key, out var x))
                {
                    s.Downloads += x.Downloads;
                    if (x.Published > s.Published) s.Published = x.Published;
                }
            }
            return s;
        }

        /// Search: name, author, description, id and tags, any case.
        public static bool Matches(ModCatalog.Mod m, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            string q = query.Trim();
            bool In(string s) => s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            if (In(m.Name) || In(m.Author) || In(m.Description) || In(m.Id)) return true;
            foreach (var t in m.Tags ?? new List<string>()) if (In(t)) return true;
            return false;
        }

        /// What the list shows: the search, then the Installed tab (installed: is this mod on this game), then the order.
        public static List<ModCatalog.Mod> View(IEnumerable<ModCatalog.Mod> mods, string query, bool installedOnly, Func<ModCatalog.Mod, bool> installed,
            Sort sort, IDictionary<string, Stat> stats)
        {
            var list = new List<ModCatalog.Mod>();
            foreach (var m in mods)
                if (m != null && Matches(m, query) && (!installedOnly || installed(m))) list.Add(m);
            string Name(ModCatalog.Mod m) => m.Name ?? m.Id ?? "";
            Comparison<ModCatalog.Mod> byName = (a, b) => string.Compare(Name(a), Name(b), StringComparison.OrdinalIgnoreCase);
            Comparison<ModCatalog.Mod> order;
            if (sort == Sort.Downloads) order = (a, b) => { int c = ForMod(b, stats).Downloads.CompareTo(ForMod(a, stats).Downloads); return c != 0 ? c : byName(a, b); };
            else if (sort == Sort.Updated) order = (a, b) => { int c = ForMod(b, stats).Published.CompareTo(ForMod(a, stats).Published); return c != 0 ? c : byName(a, b); };
            else order = byName;
            // a stable sort: List.Sort is not, and equal mods must not swap places between refreshes
            var indexed = new List<KeyValuePair<int, ModCatalog.Mod>>();
            for (int i = 0; i < list.Count; i++) indexed.Add(new KeyValuePair<int, ModCatalog.Mod>(i, list[i]));
            indexed.Sort((a, b) => { int c = order(a.Value, b.Value); return c != 0 ? c : a.Key.CompareTo(b.Key); });
            return indexed.ConvertAll(kv => kv.Value);
        }

        public static string Short(long n) => n >= 1000000 ? (n / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M"
            : n >= 1000 ? (n / 1e3).ToString("0.#", CultureInfo.InvariantCulture) + "K" : n.ToString(CultureInfo.InvariantCulture);

        public static string Ago(DateTime t, DateTime now)
        {
            var d = now - t;
            if (d.TotalDays >= 365) return t.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            if (d.TotalDays >= 60) return (int)(d.TotalDays / 30) + " months ago";
            if (d.TotalDays >= 30) return "a month ago";
            if (d.TotalDays >= 2) return (int)d.TotalDays + " days ago";
            if (d.TotalHours >= 24) return "yesterday";
            if (d.TotalHours >= 2) return (int)d.TotalHours + " hours ago";
            return "just now";
        }

        /// The initials on a mod's tile: the first letters of its first two words, else its first two letters.
        public static string Initials(string name)
        {
            var words = (name ?? "").Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            string s = words.Length >= 2 ? words[0].Substring(0, 1) + words[1].Substring(0, 1) : words.Length == 1 ? words[0].Substring(0, Math.Min(2, words[0].Length)) : "?";
            return s.ToUpperInvariant();
        }

        /// A steady number from a mod's id (its tile colour), the same on every machine.
        public static int Hue(string id)
        {
            int h = 0;
            foreach (char c in id ?? "") h = unchecked(h * 31 + c);
            return h & 0x7fffffff;
        }
    }
}
