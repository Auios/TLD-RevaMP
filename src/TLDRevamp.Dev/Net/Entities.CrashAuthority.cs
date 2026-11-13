using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityCrashAuthorityTools
    {

        /// Test (`mp crashlease <net>`, host): this machine claims `net` as if its car had just hit it — the server's crash
        /// lease without staging a crash.
        public static string TestCrashLease(uint net)
        {
            if (Mp.Server == null) return "{\"error\":\"host only\"}";
            if (!ObservedEntities.TryGetValue(net, out var e) || !e.Proxy) return "{\"error\":\"not another player's object here\"}";
            ServerCrashClaim(MyId, net, 0);
            return "{\"leased\":" + net + ",\"returnTo\":" + (ObserveServer(net, out var se) ? se.CrashReturnTo : -9) + "}";
        }

        /// Test (`mp holdcopy <net> <s>`): this machine's physical copy of `net` stays where it is for `s` seconds while its
        /// owner's poses keep coming — a copy left behind, as the host's copy of the joiner's car was at 10:49 (CrashGuard).
        public static string TestHoldCopy(uint net, float seconds)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !e.Proxy) return "{\"error\":\"not another player's object here\"}";
            if (!e.Physical) return "{\"error\":\"not a physical copy (no car simulated here within reach)\"}";
            HoldCrashUntil(net, Time.time + seconds);
            return "{\"held\":" + net + ",\"seconds\":" + seconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "}";
        }
    }
}
