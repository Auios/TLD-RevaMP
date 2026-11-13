using System.Linq;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Steamworks;
using static TLDRevamp.Net.SteamCompat;

namespace TLDRevamp.Net
{
    internal static class SteamCompatTools
    {
        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworkingUtils_GetDirectPingToPOP", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetDirectPingToPOPNative(IntPtr self, uint pop);
        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworkingUtils_GetPingToDataCenter", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetPingToDataCenterNative(IntPtr self, uint pop, out uint viaRelay);
        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworkingUtils_GetPOPList", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetPOPListNative(IntPtr self, [Out] uint[] list, int n);

        // ---- relay network (Valve's SDR): datacenter pings and which relay a connection runs through. Flat functions of
        // the same DLL era, called directly (as GetQuickConnectionStatus above).
        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworkingUtils_GetPOPCount", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetPOPCountNative(IntPtr self);

        internal static IntPtr CSteamUtils()
        {
            var ctx = typeof(SteamClient).Assembly.GetType("Steamworks.CSteamAPIContext");
            return (IntPtr)ctx.GetMethod("GetSteamNetworkingUtils", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        /// Every relay datacenter with this machine's ping to it (through the best relay route, and direct).
        public static string Pops()
        {
            Ensure();
            SteamNetworkingUtils.InitRelayNetworkAccess();
            var u = CSteamUtils();
            int n = GetPOPCountNative(u);
            var ids = new uint[Math.Max(0, n)];
            if (n > 0) n = GetPOPListNative(u, ids, n);
            var rows = new System.Collections.Generic.List<string>();
            for (int i = 0; i < n; i++)
            {
                int ping = GetPingToDataCenterNative(u, ids[i], out uint via), direct = GetDirectPingToPOPNative(u, ids[i]);
                rows.Add("{\"pop\":" + Json.Str(PopName(ids[i])) + ",\"ping\":" + ping + ",\"via\":" + Json.Str(PopName(via)) + ",\"direct\":" + direct + "}");
            }
            return "{\"count\":" + n + ",\"pops\":[" + string.Join(",", rows) + "]}";
        }
    }
}
