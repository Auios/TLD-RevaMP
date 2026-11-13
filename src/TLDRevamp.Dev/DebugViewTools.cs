using System;
using static TLDRevamp.DebugView;

namespace TLDRevamp
{
    internal static class DebugViewTools
    {
        internal static string Command(string action)
        {
            if (action != "state" && action != "")
            {
                if (!Enum.TryParse(action, true, out DebugViewMode mode) || !Enum.IsDefined(typeof(DebugViewMode), mode))
                    return "{\"error\":\"use off|focus|problems|everything|state\"}";
                Mode = mode;
            }
            return "{\"mode\":\"" + Mode.ToString().ToLowerInvariant() + "\"}";
        }
    }
}
