using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using static TLDRevamp.KeyBindings;

namespace TLDRevamp
{
    internal static class KeyBindingsTools
    {

        /// `mp keys`: every key and whether the F1 check was found. `mp keys set <name> <KeyCode|none>`: rebind one (as the
        /// settings row does; saved at once) — the settings restart check uses it.
        internal static string Command(string[] a)
        {
            if (a.Length >= 4 && a[1] == "set")
            {
                string name = string.Join(" ", a, 2, a.Length - 3);
                var bind = TLDRevamp.KeyBindings.ObservedAll.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (bind == null) return "{\"error\":\"no key named " + name.Replace("\"", "") + "\"}";
                if (!Enum.TryParse(a[a.Length - 1], true, out KeyCode k)) return "{\"error\":\"not a key code\"}";
                bind.Entry.Value = new KeyboardShortcut(k);
                RevampMenu.RefreshOpen();
            }
            var sb = new System.Text.StringBuilder("{\"capturing\":").Append(TLDRevamp.KeyBindings.ObservedCapturing != null ? Json.Str(TLDRevamp.KeyBindings.ObservedCapturing.Name) : "null").Append(",\"devMenuPatched\":").Append(DevMenuKey.Patched ? "true" : "false").Append(",\"devModeKeys\":").Append(TLDRevamp.DevModeKeys.Status()).Append(",\"devMenuList\":").Append(Json.Str(DevModeKeys.ListShown ?? "")).Append(",\"keys\":[");
            for (int i = 0; i < TLDRevamp.KeyBindings.ObservedAll.Count; i++) { if (i > 0) sb.Append(','); sb.Append("{\"name\":").Append(Json.Str(TLDRevamp.KeyBindings.ObservedAll[i].Name)).Append(",\"key\":").Append(Json.Str(Show(TLDRevamp.KeyBindings.ObservedAll[i]))).Append('}'); }
            return sb.Append("]}").ToString();
        }
    }
}
