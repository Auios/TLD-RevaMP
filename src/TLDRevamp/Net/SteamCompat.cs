using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Steamworks;

namespace TLDRevamp.Net
{
    /// The game ships Steamworks.NET 20.2 (written for Steam SDK 1.57) together with an older steam_api64.dll whose flat
    /// networking functions were compiled for ISteamNetworkingSockets008 / ISteamNetworkingUtils003. Steamworks.NET asks
    /// the Steam client for SteamNetworkingSockets012, so the old flat functions call into a newer function table: calls
    /// whose slot didn't move work, others land in the wrong function (CreatePollGroup crashed the game;
    /// GetConnectionRealTimeStatus doesn't exist in the DLL at all). Found 2026-09-27 with `net probe`.
    ///
    /// Fix: request the interface versions this DLL was built for, the same way Steamworks.NET does
    /// (SteamInternal_FindOrCreateUserInterface), and put them into its context. The game itself never uses these
    /// interfaces (checked: no SteamNetworkingSockets/Utils/Messages in Assembly-CSharp), so nothing else is affected.
    public static class SteamCompat
    {
        public const string SocketsVersion = "SteamNetworkingSockets008";
        public const string UtilsVersion = "SteamNetworkingUtils003";
        public static bool Applied;
        public static string Info = "";

        [DllImport("steam_api64", EntryPoint = "SteamInternal_FindOrCreateUserInterface", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FindOrCreateUserInterface(int hSteamUser, [MarshalAs(UnmanagedType.LPStr)] string version);

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworkingSockets_GetQuickConnectionStatus", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool GetQuickConnectionStatusNative(IntPtr self, uint hConn, ref QuickStatus status);

        /// SteamNetworkingQuickConnectionStatus as in SDK 1.48 (the DLL's era).
        [StructLayout(LayoutKind.Sequential)]
        public struct QuickStatus
        {
            public int State, Ping;
            public float QualityLocal, QualityRemote, OutPacketsPerSec, OutBytesPerSec, InPacketsPerSec, InBytesPerSec;
            public int SendRateBytesPerSecond, PendingUnreliable, PendingReliable, SentUnackedReliable;
            public long QueueTimeUsec;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public uint[] Reserved;
        }

        public static void Ensure()
        {
            if (Applied) return;
            var ctx = GameLookups.AssemblyType(typeof(SteamClient), "Steamworks.CSteamAPIContext");
            var fs = ctx == null ? null : GameLookups.NamedField(typeof(SteamClient), "Steamworks.CSteamAPIContext:m_pSteamNetworkingSockets", ctx, BindingFlags.NonPublic | BindingFlags.Static);
            var fu = ctx == null ? null : GameLookups.NamedField(typeof(SteamClient), "Steamworks.CSteamAPIContext:m_pSteamNetworkingUtils", ctx, BindingFlags.NonPublic | BindingFlags.Static);
            if (fs == null || fu == null) throw new InvalidOperationException("Steamworks.NET context fields not found");
            int user = SteamAPI.GetHSteamUser().m_HSteamUser;
            IntPtr s = FindOrCreateUserInterface(user, SocketsVersion), u = FindOrCreateUserInterface(user, UtilsVersion);
            if (s == IntPtr.Zero || u == IntPtr.Zero) throw new InvalidOperationException("Steam client has no " + SocketsVersion + "/" + UtilsVersion);
            Info = "sockets " + fs.GetValue(null) + " → " + s + ", utils " + fu.GetValue(null) + " → " + u;
            fs.SetValue(null, s);
            fu.SetValue(null, u);
            Applied = true;
            Plugin.Log.LogInfo("SteamCompat: " + Info);
        }

        public static QuickStatus Status(HSteamNetConnection c)
        {
            var st = new QuickStatus { Reserved = new uint[16] };
            GetQuickConnectionStatusNative(CSteamSockets(), c.m_HSteamNetConnection, ref st);
            return st;
        }
        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworkingSockets_GetConnectionInfo", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool GetConnectionInfoNative(IntPtr self, uint hConn, IntPtr info);

        /// "sgp", "fra", … from a SteamNetworkingPOPID (the characters packed big-endian).
        public static string PopName(uint id)
        {
            var sb = new System.Text.StringBuilder();
            for (int sh = 24; sh >= 0; sh -= 8) { char c = (char)((id >> sh) & 0xFF); if (c != 0) sb.Append(c); }
            return sb.ToString();
        }

        /// A connection's route (SteamNetConnectionInfo_t, SDK 1.48 layout, Windows packing): the remote end's datacenter
        /// and the relay it runs through (0 = direct), state and Steam's own description of the connection.
        public static string Route(HSteamNetConnection c)
        {
            Ensure();
            var buf = Marshal.AllocHGlobal(2048);
            try
            {
                for (int i = 0; i < 2048; i += 8) Marshal.WriteInt64(buf, i, 0);
                bool ok = GetConnectionInfoNative(CSteamSockets(), c.m_HSteamNetConnection, buf);
                uint popRemote = (uint)Marshal.ReadInt32(buf, 168), popRelay = (uint)Marshal.ReadInt32(buf, 172);
                int state = Marshal.ReadInt32(buf, 176);
                string desc = Marshal.PtrToStringAnsi(IntPtr.Add(buf, 312)) ?? "";
                var q = Status(c);
                return "{\"ok\":" + (ok ? "true" : "false") + ",\"state\":" + state + ",\"ping\":" + q.Ping + ",\"popRemote\":" + Json.Str(PopName(popRemote))
                       + ",\"popRelay\":" + Json.Str(PopName(popRelay)) + ",\"relayed\":" + (popRelay != 0 ? "true" : "false") + ",\"desc\":" + Json.Str(desc) + "}";
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public const int IdentitySteamId = 16;   // ESteamNetworkingIdentityType.k_ESteamNetworkingIdentityType_SteamID

        /// Who the remote end of a connection is as Steam authenticated it (SteamNetConnectionInfo_t.m_identityRemote,
        /// SDK 1.48 layout: the identity first — type i32 at 0, its SteamID u64 at 8). 0 when it is not a SteamID (an IP
        /// connection that came in without Steam's authentication) or the call failed; `type` = the identity's kind.
        public static ulong RemoteSteamId(HSteamNetConnection c, out int type)
        {
            type = -1;
            Ensure();
            var buf = Marshal.AllocHGlobal(2048);
            try
            {
                for (int i = 0; i < 2048; i += 8) Marshal.WriteInt64(buf, i, 0);
                if (!GetConnectionInfoNative(CSteamSockets(), c.m_HSteamNetConnection, buf)) return 0;
                type = Marshal.ReadInt32(buf, 0);
                return type == IdentitySteamId ? (ulong)Marshal.ReadInt64(buf, 8) : 0;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        private static IntPtr CSteamSockets()
        {
            var ctx = GameLookups.AssemblyType(typeof(SteamClient), "Steamworks.CSteamAPIContext");
            return (IntPtr)GameLookups.NamedMethod(typeof(SteamClient), "Steamworks.CSteamAPIContext:GetSteamNetworkingSockets", ctx, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }
    }
}
