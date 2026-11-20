using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TLDRevamp.Net
{
    /// Format 1 is the live list of installable releases and their DLL hashes.
    /// No Unity or filesystem writes here: these checks also run in the offline tests.
    public static partial class ModCatalog
    {
        // The browser and its catalog are on dev; main does not have the catalog yet.
        public const string DefaultUrl = "https://raw.githubusercontent.com/TLD-RevaMP/TLD-RevaMP/refs/heads/dev/packaging/mods.json";
        /// The default before the repository moved to the TLD-RevaMP organisation. BepInEx saved it into every cfg, and
        /// it answers only through GitHub's redirect, so a cfg that still has it reads the catalog at its own address.
        internal const string OldDefaultUrl = "https://raw.githubusercontent.com/Normal99/TLD-RevaMP/refs/heads/dev/packaging/mods.json";
        public const int MaxCatalogBytes = 1 << 20;
        [DataContract] public sealed class Catalog
        {
            [DataMember(Name = "format")] public int Format;
            [DataMember(Name = "mods")] public List<Mod> Mods;
        }
        [DataContract] public sealed class Mod
        {
            [DataMember(Name = "id")] public string Id;
            [DataMember(Name = "name")] public string Name;
            [DataMember(Name = "author")] public string Author;
            [DataMember(Name = "description")] public string Description;
            [DataMember(Name = "source")] public string Source;
            [DataMember(Name = "versions")] public List<Release> Versions;
            /// optional: short words the Mods list shows and searches (the launcher shows them too)
            [DataMember(Name = "tags")] public List<string> Tags;
        }
        [DataContract] public sealed class Release
        {
            [DataMember(Name = "version")] public string Version;
            [DataMember(Name = "url")] public string Url;
            [DataMember(Name = "file")] public string File;
            [DataMember(Name = "sha256")] public string Hash;
            [DataMember(Name = "size")] public long Size;
            [DataMember(Name = "revamp")] public string Revamp;
        }

        public static bool ReleaseUrl(string url)
        {
            if (url == null || !Regex.IsMatch(url,
                @"\Ahttps://github\.com/[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+/releases/download/[^/\s?#\\]+/[^/\s?#\\]+\z") ||
                !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Host != "github.com" || u.Port != 443 || u.UserInfo.Length != 0) return false;
            // Check the original path too: Uri normalizes dot segments before exposing AbsolutePath.
            return SafeEntry(Uri.UnescapeDataString(url.Substring("https://github.com/".Length)));
        }

        public static bool SourceUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) &&
            u.Scheme == "https" && u.UserInfo.Length == 0;

        public static Catalog Parse(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > MaxCatalogBytes) throw new InvalidDataException("Catalog size limit exceeded or empty catalog.");
            Catalog c;
            using (var s = new MemoryStream(data, false))
                c = (Catalog)new DataContractJsonSerializer(typeof(Catalog)).ReadObject(s);
            if (c == null || c.Format != 1 || c.Mods == null) throw new InvalidDataException("Unsupported mod catalog format (expected 1).");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            c.Mods.RemoveAll(m => m == null || string.IsNullOrEmpty(m.Id) || !ids.Add(m.Id) || m.Versions == null);
            foreach (var m in c.Mods)
            {
                m.Versions.RemoveAll(v => v == null || !ReleaseUrl(v.Url));
                if (!SourceUrl(m.Source)) m.Source = null;
                // tags: at most six short words, none empty, each once
                var tags = new List<string>();
                foreach (var t in m.Tags ?? new List<string>())
                {
                    string tag = (t ?? "").Trim();
                    if (tag.Length > 0 && tag.Length <= 24 && !tags.Contains(tag) && tags.Count < 6) tags.Add(tag);
                }
                m.Tags = tags;
            }
            c.Mods.RemoveAll(m => m.Versions.Count == 0);
            return c;
        }

        /// <paramref name="revamp"/>: this RevaMP's version; a release whose "revamp" is newer needs a newer RevaMP (its API).
        public static string ApprovalReason(Mod m, Release v, int maxBytes, Func<string, bool> validId, string revamp = null)
        {
            if (m == null || v == null || !validId(m.Id)) return "Invalid mod id.";
            if (!ModFiles.SafeVersion(v.Version)) return "Invalid mod version.";
            if (!ReleaseUrl(v.Url)) return "Only GitHub release assets are allowed.";
            if (v.Size <= 0 || v.Size > maxBytes) return "Download exceeds the size limit.";
            if (!string.IsNullOrEmpty(v.File) && (!SafeEntry(v.File) || !v.File.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))) return "Invalid DLL path in archive.";
            string hash = (v.Hash ?? "").ToLowerInvariant();
            if (!Regex.IsMatch(hash, @"\A[0-9a-f]{64}\z")) return "Invalid catalog hash.";
            if (revamp != null && !string.IsNullOrEmpty(v.Revamp) && Version.TryParse(v.Revamp, out var need) &&
                Version.TryParse(revamp, out var have) && need > have) return "Needs RevaMP " + v.Revamp + ".";
            return null;
        }

        public static Release Offered(Mod m, int maxBytes, Func<string, bool> validId, string revamp = null) =>
            m.Versions.Find(v => ApprovalReason(m, v, maxBytes, validId, revamp) == null) ?? m.Versions[0];

        /// The version an installed mod can be updated to: the newest valid one, if it is newer than the installed one.
        public static Release UpdateTo(Mod m, string installed, int maxBytes, Func<string, bool> validId, string revamp = null)
        {
            if (installed == null || m.Versions.Count == 0) return null;
            var v = Offered(m, maxBytes, validId, revamp);
            return ApprovalReason(m, v, maxBytes, validId, revamp) == null && Newer(v.Version, installed) ? v : null;
        }

        public static bool Newer(string offered, string installed) =>
            System.Version.TryParse(offered, out var a) && System.Version.TryParse(installed, out var b) ? a > b : offered != installed;

        // Backslashes, rooted paths and dot segments are refused on every OS, including harmless-looking directories.
        public static bool SafeEntry(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] == '/' || path.Contains("\\") || path.Contains(":")) return false;
            foreach (var p in path.TrimEnd('/').Split('/'))
            {
                if (p.Length == 0 || p.EndsWith(".", StringComparison.Ordinal) || p.EndsWith(" ", StringComparison.Ordinal)) return false;
                foreach (char c in p) if (char.IsControl(c)) return false;
            }
            return true;
        }

        public static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal sealed class LimitedBuffer : IDisposable
        {
            private readonly MemoryStream _data = new MemoryStream();
            private readonly int _limit;
            internal bool Exceeded { get; private set; }
            internal long Length => _data.Length;
            internal byte[] Bytes => _data.ToArray();
            internal LimitedBuffer(int limit) { _limit = limit; }
            internal void ContentLength(ulong length) { if (length > (ulong)_limit) Exceeded = true; }
            internal bool Add(byte[] data, int length)
            {
                if (Exceeded || data == null || length < 0 || length > data.Length || length > _limit - _data.Length) { Exceeded = true; return false; }
                _data.Write(data, 0, length); return true;
            }
            public void Dispose() => _data.Dispose();
        }

        public static byte[] Verify(Mod m, Release v, byte[] download, int maxBytes, Func<string, bool> validId)
        {
            string reason = ApprovalReason(m, v, maxBytes, validId);
            if (reason != null) throw new InvalidDataException(reason);
            if (download == null || download.Length == 0 || download.Length > maxBytes || download.LongLength != v.Size)
                throw new InvalidDataException("Download size differs from the catalog or exceeds the limit.");
            byte[] dll = download;
            if (!string.IsNullOrEmpty(v.File))
            {
                using (var stream = new MemoryStream(download, false))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry selected = null;
                    long total = 0;
                    foreach (var entry in zip.Entries)
                    {
                        if (!SafeEntry(entry.FullName)) throw new InvalidDataException("Unsafe archive path.");
                        if (entry.Length > maxBytes - total) throw new InvalidDataException("Archive uncompressed size exceeds the limit.");
                        total += entry.Length;
                        if (entry.FullName == v.File)
                        {
                            if (selected != null) throw new InvalidDataException("Duplicate DLL entry.");
                            selected = entry;
                        }
                        else if (entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive contains another DLL.");
                    }
                    if (selected == null || selected.Length == 0) throw new InvalidDataException("Catalog DLL is missing from the archive.");
                    using (var input = selected.Open())
                    using (var output = new MemoryStream())
                    {
                        var buffer = new byte[8192]; int n;
                        while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (output.Length + n > maxBytes || output.Length + n > selected.Length) throw new InvalidDataException("DLL uncompressed size exceeds the limit.");
                            output.Write(buffer, 0, n);
                        }
                        if (output.Length != selected.Length) throw new InvalidDataException("Truncated archive DLL.");
                        dll = output.ToArray();
                    }
                }
            }
            if (dll.Length > maxBytes || Sha256(dll) != v.Hash.ToLowerInvariant()) throw new InvalidDataException("DLL hash differs from the catalog file; refused.");
            return dll;
        }
    }
}
