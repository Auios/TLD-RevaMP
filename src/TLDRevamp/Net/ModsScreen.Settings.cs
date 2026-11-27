using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLDRevamp.Net
{
    /// A mod's own settings in the mods list (playtest 2026-11-27: "mods need their own settings using a settings button
    /// on each mod"). They are the ones the mod keeps in its BepInEx config (Config.Bind, BepInEx/config/<id>.cfg), so
    /// every BepInEx mod has them without doing anything for it: the list's Settings button opens them on a paper card,
    /// one row per setting, with the mod's own description under it. On/off, choices (an enum, a list of values), a
    /// number in its range (- and +), a key (pressed to set), text. A change is the config's own (saved at once);
    /// whether the mod takes it at once or at its next start is the mod's, so the card says so. A setting of a kind
    /// the card can't change shows its value and points at the file.
    public static partial class ModsScreen
    {
        private static GameObject _modSettings;
        private static RectTransform _modSettingsRows;
        private static ScrollRect _modSettingsScroll;
        private static TextMeshProUGUI _modSettingsTitle;
        private static readonly List<Action> ModSettingsRefresh = new List<Action>();
        /// The key row waiting for a key (KeyCapture), and its button.
        private static ConfigEntryBase _capturing;
        private static Button _captureButton;

        /// A loaded mod's settings: its plugin's config, when it has any (the catalog's id is the plugin's BepInEx GUID).
        internal static ConfigFile ModConfig(string id)
        {
            // a plain reference test: the game destroys BepInEx's manager object, plugins included, so Unity's == null says
            // every plugin is gone; its Config (a plain C# object) is still there and still saves
            if (string.IsNullOrEmpty(id) || !Chainloader.PluginInfos.TryGetValue(id, out var info) || info == null || ReferenceEquals(info.Instance, null)) return null;
            var cfg = info.Instance.Config;
            return cfg != null && cfg.Count > 0 ? cfg : null;
        }

        internal static bool ModSettingsOpen => _modSettings != null && _modSettings.activeSelf;

        private static void BuildModSettings()
        {
            _modSettings = new GameObject("ModSettings", typeof(RectTransform));
            _modSettings.transform.SetParent(_list.transform, false); Stretch(_modSettings.transform);
            Backdrop(_modSettings.transform, new Color(0f, 0f, 0f, .55f));
            _modSettings.AddComponent<KeyCapture>();
            var card = (RectTransform)new GameObject("Card", typeof(RectTransform), typeof(Image)).transform;
            card.SetParent(_modSettings.transform, false); card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(1400f, 940f);
            var cImg = card.GetComponent<Image>(); cImg.sprite = _paper; cImg.type = Image.Type.Sliced; cImg.color = Paper;

            _modSettingsTitle = NewText(card, _paperStyle, 50f, Ink, TextAlignmentOptions.MidlineLeft, "Title");
            Band(_modSettingsTitle.rectTransform, 50f, 50f, 24f, 70f); _modSettingsTitle.fontStyle = FontStyles.Bold;

            var listGo = new GameObject("List", typeof(RectTransform));
            var listRt = (RectTransform)listGo.transform; listRt.SetParent(card, false);
            listRt.anchorMin = Vector2.zero; listRt.anchorMax = Vector2.one; listRt.offsetMin = new Vector2(40f, 150f); listRt.offsetMax = new Vector2(-40f, -110f);
            var viewport = (RectTransform)new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).transform;
            viewport.SetParent(listRt, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero; viewport.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);   // catches the wheel between rows
            _modSettingsRows = (RectTransform)new GameObject("Rows", typeof(RectTransform)).transform;
            _modSettingsRows.SetParent(viewport, false);
            _modSettingsRows.anchorMin = new Vector2(0f, 1f); _modSettingsRows.anchorMax = new Vector2(1f, 1f); _modSettingsRows.pivot = new Vector2(.5f, 1f);
            _modSettingsRows.offsetMin = Vector2.zero; _modSettingsRows.offsetMax = Vector2.zero;
            var column = _modSettingsRows.gameObject.AddComponent<VerticalLayoutGroup>(); column.spacing = 6f;
            column.childControlWidth = true; column.childControlHeight = true; column.childForceExpandWidth = true; column.childForceExpandHeight = false;
            _modSettingsRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _modSettingsScroll = listGo.AddComponent<ScrollRect>();
            _modSettingsScroll.viewport = viewport; _modSettingsScroll.content = _modSettingsRows; _modSettingsScroll.horizontal = false;
            _modSettingsScroll.movementType = ScrollRect.MovementType.Clamped; _modSettingsScroll.scrollSensitivity = 60f; _modSettingsScroll.inertia = false;

            var foot = NewText(card, _paperStyle, 26f, Faded, TextAlignmentOptions.MidlineLeft, "Foot");
            var fr = foot.rectTransform; fr.anchorMin = fr.anchorMax = fr.pivot = Vector2.zero;
            fr.anchoredPosition = new Vector2(50f, 40f); fr.sizeDelta = new Vector2(940f, 80f); foot.enableWordWrapping = true;
            foot.text = "Saved at once. Some settings take effect at the mod's next start: restart the game if one doesn't.";
            var close = NewButton(card, "Close", ButtonGrey, 50f, CloseModSettings);
            var cr = (RectTransform)close.transform; cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 0f);
            cr.anchoredPosition = new Vector2(-50f, 36f); cr.sizeDelta = new Vector2(300f, 88f);
            _modSettings.SetActive(false);
        }

        internal static void OpenModSettings(ModCatalog.Mod m)
        {
            var cfg = ModConfig(m.Id);
            if (cfg == null || _list == null) return;
            if (_modSettings == null) BuildModSettings();
            _capturing = null; _captureButton = null;
            foreach (Transform c in _modSettingsRows) UnityEngine.Object.Destroy(c.gameObject);
            ModSettingsRefresh.Clear();
            _modSettingsTitle.text = Esc(m.Name ?? m.Id) + "  <size=30><color=#" + ColorUtility.ToHtmlStringRGB(Faded) + ">settings</color></size>";
            var entries = new List<ConfigEntryBase>();
            foreach (var kv in cfg) if (kv.Value != null) entries.Add(kv.Value);
            entries.Sort((a, b) =>
            {
                int s = string.CompareOrdinal(a.Definition.Section, b.Definition.Section);
                return s != 0 ? s : string.CompareOrdinal(a.Definition.Key, b.Definition.Key);
            });
            string section = null;
            foreach (var e in entries)
            {
                if (e.Definition.Section != section) { section = e.Definition.Section; SectionRow(section); }
                SettingRow(e);
            }
            _modSettings.SetActive(true);
            _modSettings.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases(); _modSettingsScroll.verticalNormalizedPosition = 1f;
        }

        internal static void CloseModSettings()
        {
            _capturing = null; _captureButton = null;
            if (_modSettings != null) _modSettings.SetActive(false);
        }

        private static void SectionRow(string section)
        {
            var row = new GameObject("Section " + section, typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(_modSettingsRows, false);
            row.GetComponent<LayoutElement>().preferredHeight = 64f;
            var t = NewText(row.transform, _paperStyle, 34f, Faded, TextAlignmentOptions.BottomLeft, "Text");
            Stretch(t.transform); t.margin = new Vector4(10f, 0f, 0f, 6f); t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            t.text = Esc(Words(section));
        }

        private static void SettingRow(ConfigEntryBase e)
        {
            string help = e.Description?.Description ?? "";
            var row = new GameObject("Setting " + e.Definition.Key, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(_modSettingsRows, false);
            var img = row.GetComponent<Image>(); img.color = new Color(0f, 0f, 0f, .05f); img.raycastTarget = false;
            row.GetComponent<LayoutElement>().preferredHeight = string.IsNullOrEmpty(help) ? 86f : help.Length > 95 ? 150f : 118f;
            var rt = (RectTransform)row.transform;
            const float controlW = 420f;

            var label = NewText(rt, _paperStyle, 34f, Ink, TextAlignmentOptions.MidlineLeft, "Label");
            Band(label.rectTransform, 16f, controlW + 40f, 8f, 50f); label.text = Esc(Words(e.Definition.Key)); label.fontStyle = FontStyles.Bold;
            if (!string.IsNullOrEmpty(help))
            {
                var d = NewText(rt, _paperStyle, 23f, Body, TextAlignmentOptions.TopLeft, "Help");
                Band(d.rectTransform, 16f, controlW + 40f, 58f, 88f); d.enableWordWrapping = true; d.maxVisibleLines = 3;
                d.text = Esc(help.Replace('\n', ' '));
            }
            Control(rt, e, controlW);
        }

        /// The row's control at its right edge, by the setting's type.
        private static void Control(RectTransform rt, ConfigEntryBase e, float w)
        {
            var type = e.SettingType;
            var range = e.Description?.AcceptableValues;
            Button Wide(Action click)
            {
                var b = NewButton(rt, "", ButtonGrey, 34f, click);
                TopRight((RectTransform)b.transform, 16f, 14f, w, 64f); return b;
            }
            if (type == typeof(bool))
            {
                Button b = null; b = Wide(() => { e.BoxedValue = !(bool)e.BoxedValue; Show(); });
                void Show() { bool on = (bool)e.BoxedValue; SetButton(b, on ? "On" : "Off", on ? ButtonGreen : ButtonGrey, true); }
                Show(); ModSettingsRefresh.Add(Show); return;
            }
            if (type == typeof(KeyCode) || type == typeof(KeyboardShortcut))
            {
                Button b = null; b = Wide(() => { _capturing = e; _captureButton = b; SetButton(b, "Press a key…", ButtonOrange, true); });
                void Show() { SetButton(b, KeyName(e), ButtonGrey, true); }
                Show(); ModSettingsRefresh.Add(Show); return;
            }
            var choices = Choices(e, range);
            if (choices != null && choices.Count > 0)
            {
                Button b = null; b = Wide(() =>
                {
                    int i = choices.FindIndex(c => Equals(c, e.BoxedValue));
                    e.BoxedValue = choices[(i + 1) % choices.Count]; Show();
                });
                void Show() { SetButton(b, Esc(Words(Convert.ToString(e.BoxedValue, CultureInfo.InvariantCulture))), ButtonGrey, true); }
                Show(); ModSettingsRefresh.Add(Show); return;
            }
            if ((type == typeof(int) || type == typeof(float) || type == typeof(double)) && range != null && Range(range, out double min, out double max))
            {
                Button minus = null, plus = null;
                double step = type == typeof(int) ? Math.Max(1.0, Math.Round((max - min) / 20.0)) : (max - min) / 20.0;
                var value = NewText(rt, _paperStyle, 36f, Ink, TextAlignmentOptions.Center, "Value");
                TopRight(value.rectTransform, 16f + 96f, 14f, w - 192f, 64f); value.fontStyle = FontStyles.Bold;
                void Set(double v)
                {
                    v = Math.Max(min, Math.Min(max, v));
                    e.BoxedValue = type == typeof(int) ? (object)(int)Math.Round(v) : type == typeof(float) ? (object)(float)v : v;
                    Show();
                }
                double Now() => Convert.ToDouble(e.BoxedValue, CultureInfo.InvariantCulture);
                minus = NewButton(rt, "−", ButtonGrey, 44f, () => Set(Now() - step));
                TopRight((RectTransform)minus.transform, 16f + w - 90f, 14f, 90f, 64f);
                plus = NewButton(rt, "+", ButtonGrey, 44f, () => Set(Now() + step));
                TopRight((RectTransform)plus.transform, 16f, 14f, 90f, 64f);
                void Show()
                {
                    double v = Now();
                    value.text = type == typeof(int) ? ((int)v).ToString(CultureInfo.InvariantCulture)
                        : max - min <= 2.0 ? v.ToString("0.00", CultureInfo.InvariantCulture) : v.ToString("0.#", CultureInfo.InvariantCulture);
                    SetButton(minus, "−", ButtonGrey, v > min + 1e-9); SetButton(plus, "+", ButtonGrey, v < max - 1e-9);
                }
                Show(); ModSettingsRefresh.Add(Show); return;
            }
            if (type == typeof(string) || type == typeof(int) || type == typeof(float) || type == typeof(double))
            {
                var field = UnityEngine.Object.Instantiate(_search.gameObject, rt, false).GetComponent<TMP_InputField>();
                field.name = "Value"; Quiet(field.gameObject);
                field.onValueChanged = new TMP_InputField.OnChangeEvent(); field.onEndEdit = new TMP_InputField.SubmitEvent();
                field.onSubmit = new TMP_InputField.SubmitEvent(); field.onSelect = new TMP_InputField.SelectionEvent();
                field.onDeselect = new TMP_InputField.SelectionEvent();
                field.characterLimit = 300;
                if (field.placeholder is TMP_Text ph) ph.text = "";
                TopRight((RectTransform)field.transform, 16f, 14f, w, 64f);
                void Show() => field.SetTextWithoutNotify(e.GetSerializedValue());
                field.onEndEdit.AddListener(text =>
                {
                    try { e.SetSerializedValue(text); }
                    catch (Exception ex) { Plugin.Log.LogWarning($"mod settings: {e.Definition}: '{text}' not taken ({ex.Message})"); }
                    Show();
                });
                Show(); ModSettingsRefresh.Add(Show); return;
            }
            // a kind the card can't change (a colour, a vector, a mod's own type): its value, and where to change it
            var shown = NewText(rt, _paperStyle, 26f, Faded, TextAlignmentOptions.MidlineRight, "Value");
            TopRight(shown.rectTransform, 16f, 14f, w, 64f);
            shown.text = Esc(e.GetSerializedValue()) + "\n<size=20>change it in BepInEx/config</size>";
        }

        /// Test (bridge `mods settings [press=<key>:<label>]`): the open card's rows (label = control's text), after pressing
        /// a row's button as a player would. Null when the card is closed.
        internal static string SettingsView(string press)
        {
            if (!ModSettingsOpen) return null;
            if (press != null)
            {
                int at = press.LastIndexOf(':');
                var row = at > 0 ? _modSettingsRows.Find("Setting " + press.Substring(0, at)) : null;
                Button hit = null;
                if (row != null)
                    foreach (var b in row.GetComponentsInChildren<Button>(true))
                        if (b.GetComponentInChildren<TextMeshProUGUI>(true).text == press.Substring(at + 1)) hit = b;
                if (hit == null) return "no button " + press;
                if (!hit.interactable) return "button " + press + " can't be pressed now";
                hit.onClick.Invoke();
            }
            var rows = new List<string> { _modSettingsTitle.text };
            for (int i = 0; i < _modSettingsRows.childCount; i++)
            {
                var r = _modSettingsRows.GetChild(i);
                var parts = new List<string>();
                foreach (var t in r.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text) && t.name != "Help") parts.Add(t.text);
                foreach (var f in r.GetComponentsInChildren<TMP_InputField>(true)) parts.Add("[" + f.text + "]");
                rows.Add(r.name + " :: " + string.Join(" | ", parts));
            }
            return string.Join("\n", rows);
        }

        /// The values a choice setting cycles through: an enum's, or the setting's list of acceptable values.
        private static List<object> Choices(ConfigEntryBase e, AcceptableValueBase range)
        {
            if (range != null)
            {
                var p = range.GetType().GetProperty("AcceptableValues");
                if (p?.GetValue(range) is Array values) { var l = new List<object>(); foreach (var v in values) l.Add(v); return l; }
            }
            if (e.SettingType.IsEnum && !e.SettingType.IsDefined(typeof(FlagsAttribute), false))
            {
                var l = new List<object>(); foreach (var v in Enum.GetValues(e.SettingType)) if (!l.Contains(v)) l.Add(v); return l;
            }
            return null;
        }

        /// A setting's range (AcceptableValueRange<T>: MinValue and MaxValue).
        private static bool Range(AcceptableValueBase range, out double min, out double max)
        {
            min = max = 0;
            var t = range.GetType();
            var lo = t.GetProperty("MinValue")?.GetValue(range); var hi = t.GetProperty("MaxValue")?.GetValue(range);
            if (lo == null || hi == null) return false;
            min = Convert.ToDouble(lo, CultureInfo.InvariantCulture); max = Convert.ToDouble(hi, CultureInfo.InvariantCulture);
            return max > min;
        }

        private static string KeyName(ConfigEntryBase e)
        {
            if (e.BoxedValue is KeyboardShortcut s) return s.MainKey == KeyCode.None ? "None" : s.ToString();
            var k = (KeyCode)e.BoxedValue;
            return k == KeyCode.None ? "None" : k == KeyCode.Equals ? "=" : k == KeyCode.Minus ? "-" : Words(k.ToString());
        }

        /// "ZoomIn" -> "Zoom in", "MaxHeight" -> "Max height", "PhosphorPicture" -> "Phosphor picture" (a key is words run together).
        internal static string Words(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            var w = Regex.Replace(s.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
            var parts = w.Split(' ');
            for (int i = 1; i < parts.Length; i++)
                if (parts[i].Length > 1 && char.IsUpper(parts[i][0]) && !char.IsUpper(parts[i][1])) parts[i] = char.ToLowerInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        /// Waits for the key a key row asks for: the next key pressed (Escape: leave it as it was; Backspace: none).
        private sealed class KeyCapture : MonoBehaviour
        {
            private void Update()
            {
                if (_capturing == null || !Input.anyKeyDown) return;
                var e = _capturing;
                KeyCode got = KeyCode.None; bool any = false;
                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                {
                    if (k == KeyCode.None || k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue;   // the click that asked
                    if (Input.GetKeyDown(k)) { got = k; any = true; break; }
                }
                if (!any) return;
                _capturing = null;
                if (got != KeyCode.Escape)
                {
                    if (got == KeyCode.Backspace) got = KeyCode.None;
                    if (e.SettingType == typeof(KeyboardShortcut)) e.BoxedValue = new KeyboardShortcut(got);
                    else e.BoxedValue = got;
                }
                _captureButton = null;
                foreach (var r in ModSettingsRefresh) r();
            }
        }
    }
}
