using System;

namespace TLDRevamp.Replay
{
    /// Which replay bridge commands only read. The rest change what this machine shows.
    public static class ReplayCommandPolicy
    {
        public static bool IsReadOnly(string arg)
        {
            if (arg == null) return true;
            arg = arg.Trim();
            return arg.Length == 0 || arg == "status" || arg == "list" || arg == "near" || arg.StartsWith("near ", StringComparison.Ordinal);
        }
    }
}
