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
                ",\"message\":" + Json.Str(ModsScreen.NeededStatus()) + ",\"rows\":["+ string.Join(",", rows) + "],\"copy\":" + Json.Str(ModsNeeded.Copy(list, ModCatalog.Current)) + "}";
        }

        /// The game's Save/Load screen and the open Mods screen as a tree: each object's name, rect, image (sprite, colour,
        /// type) and text (font, size, colour), to build the Mods screen in the game's own style.
        private static string UiDump()
        {
            var sb = new StringBuilder();
            void Walk(UnityEngine.Transform t, int depth)
            {
                if (depth > 6) return;
                var rt = t as UnityEngine.RectTransform;
                sb.Append(new string(' ', depth * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)");
                if (rt != null) sb.Append(" pos ").Append(rt.anchoredPosition.ToString("F0")).Append(" size ").Append(rt.sizeDelta.ToString("F0"))
                    .Append(" anchors ").Append(rt.anchorMin.ToString("F2")).Append(rt.anchorMax.ToString("F2"));
                var img = t.GetComponent<UnityEngine.UI.Image>();
                if (img != null) sb.Append(" | image ").Append(img.sprite ? img.sprite.name + " " + img.sprite.rect.size.ToString("F0") + " border " + img.sprite.border.ToString("F0") : "none")
                    .Append(" colour #").Append(UnityEngine.ColorUtility.ToHtmlStringRGBA(img.color)).Append(" ").Append(img.type)
                    .Append(" on ").Append(img.enabled).Append(" depth ").Append(img.canvasRenderer.absoluteDepth).Append(" mat ").Append(img.materialForRendering ? img.materialForRendering.name + "/" + img.materialForRendering.shader.name : "none").Append(" crcolour ").Append(img.canvasRenderer.GetColor()).Append(" cull ").Append(img.canvasRenderer.cull);
                var raw = t.GetComponent<UnityEngine.UI.RawImage>();
                if (raw != null) sb.Append(" | raw ").Append(raw.texture ? raw.texture.name : "none");
                var tx = t.GetComponent<TMPro.TextMeshProUGUI>();
                if (tx != null) sb.Append(" | text ").Append(tx.font ? tx.font.name : "none").Append(" ").Append(tx.fontSize.ToString("F0"))
                    .Append(" #").Append(UnityEngine.ColorUtility.ToHtmlStringRGBA(tx.color)).Append(" ").Append(tx.fontStyle).Append(" \"")
                    .Append((tx.text ?? "").Replace('\n', ' ').Substring(0, Math.Min(40, (tx.text ?? "").Length))).Append('"');
                if (t.GetComponent<UnityEngine.UI.Button>() != null) sb.Append(" | button");
                sb.AppendLine();
                for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), depth + 1);
            }
            var mh = menuhandler.s;
            if (mh == null) return "{\"error\":\"no menu\"}";
            if (mh.SaveLoadObject != null) { sb.Append("== canvas ").Append(mh.SaveLoadObject.transform.parent.name).AppendLine(); Walk(mh.SaveLoadObject.transform, 0); }
            if (mh.Menu != null) { sb.AppendLine("== menu"); Walk(mh.Menu.transform, 0); }
            var list = mh.SaveLoadObject != null ? mh.SaveLoadObject.transform.parent.Find("TLDRevampModList") : null;
            if (list != null)
            {
                sb.AppendLine("== mods list (layer " + list.gameObject.layer + ", siblings " + list.parent.childCount + ", at " + list.GetSiblingIndex() + ")");
                for (int i = 0; i < list.parent.childCount; i++) sb.AppendLine("  sibling " + i + " " + list.parent.GetChild(i).name + " layer " + list.parent.GetChild(i).gameObject.layer + " canvas " + (list.parent.GetChild(i).GetComponent<UnityEngine.Canvas>() != null));
                var c = list.GetComponentInParent<UnityEngine.Canvas>();
                sb.AppendLine("  canvas " + (c ? c.name + " mode " + c.renderMode + " camera " + (c.worldCamera ? c.worldCamera.name + " mask " + c.worldCamera.cullingMask : "none") : "none"));
                var cr = list.GetComponent<UnityEngine.CanvasRenderer>();
                sb.AppendLine("  renderer alpha " + (cr ? cr.GetAlpha().ToString("F2") + " cull " + cr.cull : "none"));
                Walk(list, 0);
            }
            return "{\"ui\":" + Json.Str(sb.ToString()) + "}";
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
            if (arg.Trim() == "open") { ModsScreen.Open(null); return "{\"open\":" + (ModsScreen.IsOpen ? "true" : "false") + "}"; }
            if (arg.Trim() == "close") { ModsScreen.Close(); return "{\"open\":false}"; }
            if (arg.Trim() == "ui") return UiDump();
            if (arg.Trim() == "menu")
            {
                // build the main menu's column again (as on every return to the menu) and report where each button is
                TLDRevamp.MainMenuButtons.TryBuild(menuhandler.s);
                var rows = new List<string>();
                foreach (var name in TLDRevamp.MainMenuButtons.Column)
                    foreach (var t in menuhandler.s.GetComponentsInChildren<UnityEngine.RectTransform>(true))
                        if (t.name == name) { var c = new UnityEngine.Vector3[4]; t.GetWorldCorners(c); rows.Add(Json.Str(name + " " + c[0].y.ToString("F1") + ".." + c[1].y.ToString("F1"))); break; }
                return "{\"last\":" + Json.Str(TLDRevamp.MainMenuButtons.Last) + ",\"column\":[" + string.Join(",", rows) + "]}";
            }
            if (arg.StartsWith("sprite ", StringComparison.Ordinal))
            {
                // mods sprite <name>: a UI sprite's whole texture as a PNG in the game folder (to pick a part of it)
                string name = arg.Substring(7).Trim();
                foreach (var sp in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Sprite>())
                {
                    if (sp.name != name || sp.texture == null) continue;
                    var tex = sp.texture;
                    var rt = UnityEngine.RenderTexture.GetTemporary(tex.width, tex.height, 0);
                    UnityEngine.Graphics.Blit(tex, rt);
                    var prev = UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active = rt;
                    var copy = new UnityEngine.Texture2D(tex.width, tex.height, UnityEngine.TextureFormat.RGBA32, false);
                    copy.ReadPixels(new UnityEngine.Rect(0, 0, tex.width, tex.height), 0, 0); copy.Apply();
                    UnityEngine.RenderTexture.active = prev; UnityEngine.RenderTexture.ReleaseTemporary(rt);
                    string path = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "sprite-" + name + ".png");
                    File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(copy));
                    return "{\"path\":" + Json.Str(path) + ",\"texture\":" + Json.Str(tex.width + "x" + tex.height) + ",\"rect\":" + Json.Str(sp.rect.ToString()) + ",\"border\":" + Json.Str(sp.border.ToString()) + "}";
                }
                return "{\"error\":\"no such sprite\"}";
            }
            if (arg.StartsWith("view", StringComparison.Ordinal))
            {
                // mods view [search=<text>|-] [tab=all|installed] [sort=0|1|2]
                string search = null, tab = null, ask = null, press = null; int sort = -1; float scroll = -1f;
                foreach (var p in arg.Substring(4).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (p.StartsWith("search=", StringComparison.Ordinal)) search = p.Substring(7).Replace('+', ' ');
                    else if (p.StartsWith("tab=", StringComparison.Ordinal)) tab = p.Substring(4);
                    else if (p.StartsWith("sort=", StringComparison.Ordinal)) int.TryParse(p.Substring(5), out sort);
                    else if (p.StartsWith("scroll=", StringComparison.Ordinal)) float.TryParse(p.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scroll);
                    else if (p.StartsWith("ask=", StringComparison.Ordinal)) ask = p.Substring(4).Replace('+', ' ');
                    else if (p.StartsWith("press=", StringComparison.Ordinal)) press = p.Substring(6).Replace('+', ' ');
                }
                var v = ModsScreen.ListView(search, tab, sort, scroll, ask, press);
                return v == null ? "{\"open\":false}" : "{\"view\":" + Json.Str(v) + "}";
            }
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
            if (arg.Trim() != "list") return "{\"error\":\"mods list | open | close | view ... | ui | sprite <name> | check | updates | yes | no | enable|disable|remove <id>\"}";
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
