using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Bootstrap;
using TLDRevamp.Multiplayer;
using static TLDRevamp.Net.ModBrowser;

namespace TLDRevamp.Net
{
    internal static class ModBrowserTools
    {
        private static string Needed(string arg)
        {
            var list = ModsScreen.RequiredList;
            if (list == null) return "{\"open\":false}";
            if (arg == "needed all") ModsScreen.InstallNeededAll();
            else if (arg == "needed copy") ModsScreen.CopyNeeded();
            else if (arg == "needed close") { ModsScreen.Close(); return "{\"open\":false}"; }
            else if (arg.StartsWith("needed enable ", StringComparison.Ordinal))
                ModsScreen.NeededEnable(list.Mods.FindIndex(m => m.Id == arg.Substring(14)));
            else if (arg.StartsWith("needed install ", StringComparison.Ordinal))
                ModsScreen.NeededAction(list.Mods.FindIndex(m => m.Id == arg.Substring(15)));
            var rows = new List<string>();
            foreach (var need in list.Mods)
            {
                var row = ModsScreen.NeededRow(need);
                rows.Add("{\"id\":" + Json.Str(need.Id) + ",\"text\":" + Json.Str(row.Text) + ",\"action\":" + Json.Str(row.Do.ToString()) + "}");
            }
            foreach (var problem in list.Problems) rows.Add("{\"text\":" + Json.Str(ModsNeeded.Problem(problem).Text) + ",\"action\":\"None\"}");
            return "{\"open\":true,\"host\":" + Json.Str(list.Host) + ",\"busy\":" + (ModCatalog.Busy ? "true" : "false") +
                ",\"message\":" + Json.Str(ModCatalog.Progress) + ",\"rows\":[" + string.Join(",", rows) + "],\"copy\":" + Json.Str(ModsNeeded.Copy(list, ModCatalog.Current)) + "}";
        }

        internal static string Bridge(string arg)
        {
            if (arg.Trim() == "needed" || arg.StartsWith("needed ", StringComparison.Ordinal)) return Needed(arg.Trim());
            var parts = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && (parts[0] == "enable" || parts[0] == "disable" || parts[0] == "remove"))
            {
                Action(parts[1], parts[0] == "enable" ? "enabled" : parts[0] == "disable" ? "disabled" : "removed");
                return "{\"ok\":true}";
            }
            if (arg.Trim() == "check") { ModsScreen.CheckNow(); return "{\"ok\":true}"; }
            if (arg.Trim() == "updates")
            {
                var hand = new List<string>(); var ups = ModsScreen.Updates(hand); var names = new List<string>();
                foreach (var u in ups) names.Add(Json.Str(u.Key.Id + " " + u.Value.Version));
                foreach (var h in hand) names.Add(Json.Str("by hand: " + h));
                return "{\"busy\":" + (ModCatalog.Busy ? "true" : "false") + ",\"catalog\":" + (ModCatalog.Current != null ? "true" : "false") +
                    ",\"open\":" + (ModsScreen.IsOpen ? "true" : "false") + ",\"question\":" + (ModsScreen.Question == null ? "null" : Json.Str(ModsScreen.Question)) +
                    ",\"message\":" + Json.Str(ModCatalog.Message) + ",\"updates\":[" + string.Join(",", names) + "]}";
            }
            if (arg.Trim() == "yes" || arg.Trim() == "no") { ModsScreen.Answer(arg.Trim() == "yes"); return "{\"ok\":true}"; }
            if (arg.Trim() != "list") return "{\"error\":\"mods list | check | updates | yes | no | enable|disable|remove <id>\"}";
            var sb = new StringBuilder("{\"mods\":["); bool comma = false;
            foreach (var m in Items())
            {
                if (comma) sb.Append(','); comma = true;
                bool core = ModActivation.Core(m.Id), loaded = false, registered = false;
                string version = null;
                foreach (var p in Chainloader.PluginInfos.Values)
                    if (p.Metadata.GUID == m.Id)
                    {
                        loaded = true; version = p.Metadata.Version.ToString();
                        foreach (var api in ModObjects.ObservedMods)
                            if (!string.IsNullOrEmpty(api.File) && ModFiles.PathEq(p.Location, api.File)) registered = true;
                    }
                var files = core ? new List<string>() : ModActivation.Files(ModInstall.InstallDir, m.Id);
                foreach (var p in ModObjects.ObservedMods)
                    if (p.Id == m.Id || !string.IsNullOrEmpty(p.File) && files.Exists(f => ModFiles.PathEq(f, p.File))) registered = true;
                foreach (string file in files) version = Version(file, m.Id);
                string desired = core ? "enabled" : ModActivation.Desired(ModInstall.InstallDir, m.Id);
                bool installed = files.Count > 0 || loaded;
                bool pending = !core && Pending(m.Id);
                sb.Append("{\"id\":").Append(Json.Str(m.Id)).Append(",\"version\":").Append(Json.Str(version))
                    .Append(",\"installed\":").Append(installed ? "true" : "false")
                    .Append(",\"enabled\":").Append(installed && desired == "enabled" ? "true" : "false")
                    .Append(",\"loaded\":").Append(loaded ? "true" : "false")
                    .Append(",\"registered\":").Append(registered ? "true" : "false")
                    .Append(",\"pending\":").Append(pending ? Json.Str(desired) : "null")   // null when nothing is queued (Json.Str would write "")
                    .Append(",\"state\":").Append(Json.Str(core ? "Protected core" : files.Count == 0 && loaded ? "Enabled (installed by hand)" : State(m.Id))).Append('}');
            }
            return sb.Append("]}").ToString();
        }
    }
}
