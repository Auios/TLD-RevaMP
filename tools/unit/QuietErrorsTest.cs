using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TLDRevamp.Diagnostics;

namespace TLDRevamp
{
    internal static class Plugin
    {
        internal static TestLog Log = new TestLog();
    }

    internal sealed class TestLog
    {
        internal readonly List<string> Lines = new List<string>();
        internal Action<string> Write;
        internal void LogWarning(string message)
        {
            if (Write != null) Write(message);
            else lock (Lines) Lines.Add(message);
        }
    }
}

internal static class QuietErrorsTest
{
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    private sealed class BrokenMessage : Exception
    {
        public override string Message => throw new InvalidOperationException("broken getter");
    }

    private sealed class NullMessage : Exception
    {
        public override string Message => null;
    }

    static int Main()
    {
        var log = TLDRevamp.Plugin.Log;
        var error = new InvalidOperationException("failed step");
        QuietErrors.Once("Mp.Sync", error);
        for (int i = 0; i < 100; i++) QuietErrors.Once("Mp.Sync", error);
        QuietErrors.Once("Mp.Send", error);
        Check(log.Lines.Count == 2 && log.Lines[0] == "Mp.Sync: failed step" && log.Lines[1] == "Mp.Send: failed step", "one warning per site with area and message");
        Check(QuietErrors.Count("Mp.Sync") == 101 && QuietErrors.Count("Mp.Send") == 1 && QuietErrors.Count("unseen") == 0, "counts include first and all suppressed failures independently");

        QuietErrors.Once(null, null);
        QuietErrors.Once(null, null);
        QuietErrors.Once("bad-message", new BrokenMessage());
        QuietErrors.Once("null-message", new NullMessage());
        Check(QuietErrors.Count(null) == 2 && log.Lines.Contains("unknown: exception unavailable") && log.Lines.Contains("bad-message: exception unavailable") && log.Lines.Contains("null-message: exception message unavailable"), "null arguments and throwing or null message getters stay contained");

        int attempts = 0;
        log.Write = _ => { attempts++; throw new Exception("logger failed"); };
        QuietErrors.Once("broken-logger", error);
        QuietErrors.Once("broken-logger", error);
        Check(attempts == 1 && QuietErrors.Count("broken-logger") == 2, "throwing logger is attempted once and later failures counted");
        TLDRevamp.Plugin.Log = null;
        QuietErrors.Once("missing-logger", error);
        TLDRevamp.Plugin.Log = log;
        QuietErrors.Once("missing-logger", error);
        Check(QuietErrors.Count("missing-logger") == 2 && attempts == 1, "missing logger never throws or retries");

        attempts = 0;
        log.Write = _ => { attempts++; QuietErrors.Once("reentrant", error); };
        QuietErrors.Once("reentrant", error);
        Check(attempts == 1 && QuietErrors.Count("reentrant") == 2, "reentrant logger cannot recurse or flood");

        attempts = 0;
        log.Write = _ => Interlocked.Increment(ref attempts);
        Parallel.For(0, 10000, _ => QuietErrors.Once("parallel", error));
        Check(attempts == 1 && QuietErrors.Count("parallel") == 10000, "concurrent failures log once with exact counts");
        return 0;
    }
}
