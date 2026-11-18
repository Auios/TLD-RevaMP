using System.Text.RegularExpressions;

/// Follow extracted message handlers for the source-level boundary checks.
static class HotPathSource
{
    internal static string Expand(string source, string dispatcher)
    {
        return Regex.Replace(dispatcher, @"\b((?:Client|ServerMessage|Message)\w+)\([^;\n]*\); return;", call =>
        {
            var method = Regex.Match(source, @"^        private (?:static )?void " + call.Groups[1].Value + @"\([^\n]*\)\n        \{\n(.*?)\n        \}", RegexOptions.Multiline | RegexOptions.Singleline);
            if (!method.Success) throw new System.InvalidOperationException("Missing handler " + call.Groups[1].Value);
            return "{\n" + method.Groups[1].Value + "\n                    break;\n                }";
        });
    }
}
