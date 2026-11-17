using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Expected outputs are updated only when an intentional behaviour change calls for new rows.
internal static class GoldenRows
{
    internal static bool Updating => Environment.GetEnvironmentVariable("UPDATE_GOLDEN_ROWS") == "1";

    internal static void Update(string path, IEnumerable<string> rows)
    {
        if (Updating) UpdateText(path, string.Join("\r\n", rows) + "\r\n");
    }

    internal static void UpdateText(string path, string text)
    {
        if (Updating) File.WriteAllText(path, text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
    }

    // A smaller codec fixture shares the rows whose complete producer runs in LiveUsablesTest.
    internal static void UpdatePrefix(string path, IEnumerable<string> rows)
    {
        if (!Updating) return;
        var prefix = rows.ToArray();
        Update(path, prefix.Concat(File.ReadAllLines(path).Skip(prefix.Length)));
    }
}
