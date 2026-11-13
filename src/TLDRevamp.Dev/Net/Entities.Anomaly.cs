using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityAnomalyTools
    {

        // ---- test hooks (bridge): a glitch made on purpose, to see the watch catch it
        /// `mp displace <net> <m>`: our copy put m metres off, and the game's removal pass at once (a displaced copy)
        public static string TestDisplace(uint net, float m)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !e.Proxy || !Resolve(e) || e.Root == null || itemPlaceRemoveScript.s == null) return "{\"error\":\"no copy of it placed here\"}";
            e.Root.transform.root.position += new Vector3(m, 0f, 0f);
            double d = NearestPlayerM(mainscript.GlobalFromUnityPos(e.Root.transform.position));
            itemPlaceRemoveScript.s.RemoveStuff();
            return "{\"displaced\":" + net + ",\"fromPlayer\":" + d.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + ",\"ipReady\":" + (e.Ip != null && e.Ip.Ready ? "true" : "false") + "}";
        }
        /// `mp copypartgone <net> <name>`: a part of our copy gone here only (no message), then its census asked
        public static string TestCopyPartGone(uint net, string name)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !e.Proxy || !Resolve(e) || e.Root == null) return "{\"error\":\"no copy of it placed here\"}";
            foreach (var it in e.Root.transform.root.GetComponentsInChildren<tosaveitemscript>(true))
            {
                if (it == null || it == e.Root || !it.name.Contains(name)) continue;
                string n = it.name;
                SuppressDestroyEcho = true;
                try { Object.DestroyImmediate(it.gameObject); } finally { SuppressDestroyEcho = false; }
                ScheduleCensus(net);
                return "{\"gone\":" + Json.Str(n) + "}";
            }
            return "{\"error\":\"no such part on it\"}";
        }
        public static string LastDevCmd = ""; public static float LastDevAt = -1000f;   // Last developer command
        internal static string CommandNote()
        {
            float ago = Time.realtimeSinceStartup - LastDevAt;
            return ago < 30f ? $" (dev command {ago:F0} s before: {LastDevCmd})" : "";
        }

    }
}
