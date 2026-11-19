using System;
using System.Collections.Generic;

namespace TLDRevamp.Diagnostics
{
    /// Contained diagnostics for failures which callers have always swallowed.
    internal static class QuietErrors
    {
        // Initialize inside the guarded methods: even allocating the table must not escape a catch.
        private static Dictionary<string, long> _counts;

        /// One warning attempt per fixed catch-site name for the process lifetime. Count includes the first failure.
        internal static void Once(string site, Exception error)
        {
            try
            {
                site = site ?? "unknown";
                lock (typeof(QuietErrors))
                {
                    if (_counts == null) _counts = new Dictionary<string, long>(StringComparer.Ordinal);
                    if (_counts.TryGetValue(site, out long count))
                    {
                        if (count < long.MaxValue) _counts[site] = count + 1;
                        return;
                    }
                    _counts.Add(site, 1); // mark before logging: a broken or reentrant logger cannot flood
                }
                string message = "exception unavailable";
                try { if (error != null) message = error.Message ?? "exception message unavailable"; }
                catch { /* best effort: custom exception Message getters can themselves throw */ }
                Plugin.Log.LogWarning(site + ": " + message);
            }
            catch { /* best effort: diagnostics must never change the caller's error containment */ }
        }

        internal static long Count(string site)
        {
            try
            {
                lock (typeof(QuietErrors))
                    return _counts != null && _counts.TryGetValue(site ?? "unknown", out long count) ? count : 0;
            }
            catch { return 0; } // best effort: reading diagnostics must not affect the game
        }
    }
}
