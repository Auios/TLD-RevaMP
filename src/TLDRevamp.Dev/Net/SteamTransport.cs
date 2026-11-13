using System.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using static TLDRevamp.Net.SteamTransport;

namespace TLDRevamp.Net
{
    internal static class SteamTransportTools
    {
        internal static bool Refuse() { if (TestRefuseSends <= 0) return false; TestRefuseSends--; return true; }
        public static int TestRefuseSends;             // test: Steam "refuses" the next N sends
    }
}
