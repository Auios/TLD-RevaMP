using System.Linq;
using System;
using static TLDRevamp.Net.SessionSettingsPolicy;

namespace TLDRevamp.Net
{
    internal static class SessionSettingsPolicyTools
    {

        /// The bridge's gate as one rule: a read-only command always runs; the host's own session control runs on
        /// the host; every other command needs the host's policy to allow it here.
        public static bool BridgeAllowed(bool hosting, bool policyAllows, bool readOnly, string cmd, string arg)
        {
            if (readOnly) return true;
            if (IsHostControlCommand(hosting, cmd, arg)) return true;
            return policyAllows;
        }
    }
}
