using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
#if TLD_UNITY
using UnityEngine;
#endif

namespace TLDLoader
{
    /// A mod's saved settings: one value per setting id (the title, lower case, spaces as '_', as TLD Loader names them),
    /// in Settings.json in the mod's config folder, in TLD Loader 3.0.3's own format: the same serializer
    /// (DataContractJsonSerializer, KeyCode as a known type) over the same shape, {"settings":[{"ID":..,"Value":..}]}, so
    /// a file either one wrote reads in the other. A player's settings from TLD Loader (its own folder, `import`) come
    /// in the first time; so do RevaMP's older Settings.txt ("id<TAB>kind<TAB>value" lines, `legacy`). Free of Unity,
    /// so the unit test runs it (with its own known types).
    public sealed class SettingStore
    {
        [DataContract(Name = "Settings")] private sealed class Doc { [DataMember] public List<Item> settings { get; set; } }
        [DataContract(Name = "Setting")] private sealed class Item { [DataMember] public string ID { get; set; } [DataMember] public object Value { get; set; } }

        private readonly string _path;
        private readonly Type[] _known;
        private List<Item> _items = new List<Item>();
        public string LastError = "";
        /// Where the values came from: "file", "import" (TLD Loader's), "legacy" (Settings.txt) or "" (nothing saved yet).
        public string Source = "";

        public SettingStore(string path) : this(path, null, null, null) { }

        public SettingStore(string path, Type[] knownTypes, string import, string legacy)
        {
            _path = path; _known = knownTypes ?? new Type[0];
            if (Read(_path)) { Source = "file"; return; }
            if (!string.IsNullOrEmpty(import) && Read(import)) { Source = "import"; Save(); return; }
            if (!string.IsNullOrEmpty(legacy) && ReadLegacy(legacy)) { Source = "legacy"; Save(); }
        }

        public static string IdOf(string title) => (title ?? "").Replace(' ', '_').ToLowerInvariant();

        public bool TryGet(string title, out object value)
        {
            var it = Find(IdOf(title));
            value = it?.Value;
            return it != null && it.Value != null;
        }

        /// Stores the value and writes the file, only when it changed.
        public void Set(string title, object value)
        {
            string id = IdOf(title);
            var it = Find(id);
            if (it != null && Equals(it.Value, value)) return;
            if (it == null) _items.Add(new Item { ID = id, Value = value }); else it.Value = value;
            Save();
        }

        private Item Find(string id) { foreach (var it in _items) if (it.ID == id) return it; return null; }

        private DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(Doc), _known);

        private bool Read(string path)
        {
            // a file written but not yet moved into place (the game closed in between) is the newest there is
            if (path == _path && !File.Exists(path) && File.Exists(path + ".tmp")) { try { File.Move(path + ".tmp", path); } catch (Exception e) { LastError = "recovering " + path + ": " + e.Message; } }
            if (!File.Exists(path)) return false;
            try
            {
                using (var f = File.OpenRead(path))
                    if (Serializer().ReadObject(f) is Doc d && d.settings != null) { _items = d.settings.FindAll(x => x != null && x.ID != null); return true; }
            }
            catch (Exception e) { LastError = "reading " + path + ": " + e.Message; }
            return false;
        }

        /// RevaMP's settings file before 2026-11-27.
        private bool ReadLegacy(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var p = line.Split('\t');
                    if (p.Length != 3) continue;
                    object v = ParseLegacy(p[1], Unescape(p[2]));
                    if (v != null && Find(p[0]) == null) _items.Add(new Item { ID = p[0], Value = v });
                }
                return _items.Count > 0;
            }
            catch (Exception e) { LastError = "reading " + path + ": " + e.Message; return false; }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                using (var f = File.Create(_path + ".tmp")) Serializer().WriteObject(f, new Doc { settings = _items });
                // replaced in one step: deleting first and moving after lost the mod's settings to a crash in between
                if (File.Exists(_path)) File.Replace(_path + ".tmp", _path, null);
                else File.Move(_path + ".tmp", _path);
            }
            catch (Exception e) { LastError = "writing " + _path + ": " + e.Message; }
        }

        private static object ParseLegacy(string kind, string s)
        {
            var ci = CultureInfo.InvariantCulture;
            switch (kind)
            {
                case "bool": return bool.TryParse(s, out var b) ? (object)b : null;
                case "int": case "key": return int.TryParse(s, NumberStyles.Integer, ci, out var i) ? (object)i : null;
                case "float": return float.TryParse(s, NumberStyles.Float, ci, out var f) ? (object)f : null;
                default: return s;
            }
        }
        private static string Unescape(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
                char c = s[++i];
                sb.Append(c == 't' ? '\t' : c == 'n' ? '\n' : c == 'r' ? '\r' : c);
            }
            return sb.ToString();
        }
    }

#if TLD_UNITY
    /// TLD Loader's settings helpers. A mod calls them inside Config(): the host calls Config() once at load, where each
    /// one only returns the saved value (or the default), and then while the settings window is open, where it draws its
    /// control at (posx, posy) and saves a change. Same signatures and behaviour as TLD Loader's; RevaMP's own code.
    public class SettingAPI
    {
        private readonly Mod _mod;
        private readonly SettingStore _store;

        public SettingAPI(Mod mod)
        {
            _mod = mod;
            string dir = ModLoader.GetModSettingsFolder(mod);
            _store = new SettingStore(Path.Combine(dir, "Settings.json"), new[] { typeof(KeyCode) }, TldLoaderSettings(mod), Path.Combine(dir, "Settings.txt"));
            if (_store.Source == "import") Debug.Log("[SettingAPI] " + mod.ID + ": settings taken over from TLD Loader's folder");
        }

        /// Where TLD Loader itself keeps the mod's settings (Documents/TheLongDrive/Mods/Config/Mod Settings/<ID>): a player
        /// who had them there keeps them.
        private static string TldLoaderSettings(Mod mod)
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                return string.IsNullOrEmpty(docs) ? null : Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(docs, "TheLongDrive"), "Mods"), "Config"), "Mod Settings"), mod.ID), "Settings.json");
            }
            catch { return null; }
        }

        private bool Drawing => _mod.isConfigLoaded;

        // a value as saved: JSON gives a number back as whichever type holds it (an int, a decimal...)
        private static bool Num(object v, out double d)
        {
            d = 0;
            if (v == null || v is bool || v is string) return false;
            try { d = Convert.ToDouble(v, CultureInfo.InvariantCulture); return true; } catch { return false; }
        }

        public bool GUICheckbox(bool toggle, string text, int posx, int posy)
        {
            if (_store.TryGet(text, out var v) && v is bool b) toggle = b;
            if (!Drawing) return toggle;
            toggle = GUI.Toggle(new Rect(posx, posy, 430f, 20f), toggle, text);
            _store.Set(text, toggle);
            return toggle;
        }

        public bool GUIButtonToggle(bool toggle, string text, int posx, int posy)
        {
            if (_store.TryGet(text, out var v) && v is bool b) toggle = b;
            if (!Drawing) return toggle;
            if (GUI.Button(new Rect(posx, posy, 430f, 20f), toggle ? "<color=#0F0>" + text + "</color>" : text)) toggle = !toggle;
            _store.Set(text, toggle);
            return toggle;
        }

        public float GUISlider(string title, float val, float valmin, float valmax, int posx, int posy) => GUISlider(title, val, valmin, valmax, posx, posy, 2);

        public float GUISlider(string title, float val, float valmin, float valmax, int posx, int posy, int precision = 2)
        {
            if (_store.TryGet(title, out var v) && Num(v, out var saved)) val = (float)saved;
            if (!Drawing) return val;
            GUI.Box(new Rect(posx - 3, posy - 10, 430f, 80f), title);
            GUI.Label(new Rect(posx + 15, posy + 40, 250f, 30f), valmin.ToString(CultureInfo.InvariantCulture));
            GUI.Label(new Rect(posx + 380, posy + 40, 250f, 30f), valmax.ToString(CultureInfo.InvariantCulture));
            if (float.TryParse(GUI.TextField(new Rect(posx + 160, posy + 43, 100f, 20f), val.ToString(CultureInfo.InvariantCulture)), NumberStyles.Float, CultureInfo.InvariantCulture, out var typed)) val = typed;
            val = Mathf.Clamp(val, valmin, valmax);
            float s = (float)Math.Round(GUI.HorizontalSlider(new Rect(posx + 15, posy + 30, 400f, 30f), val, valmin, valmax), Math.Max(0, precision));
            _store.Set(title, s);
            return s;
        }

        public int GUISliderInt(string title, int val, int valmin, int valmax, int posx, int posy) =>
            Mathf.RoundToInt(GUISlider(title, val, valmin, valmax, posx, posy, 0));

        public KeyCode GUIKeyBinding(KeyCode key, string name, int posx, int posy)
        {
            if (_store.TryGet(name, out var v)) { if (v is KeyCode kc) key = kc; else if (Num(v, out var n)) key = (KeyCode)(int)n; }
            if (!Drawing) return key;
            GUI.Box(new Rect(posx - 3, posy - 10, 430f, 80f), "");
            GUI.Label(new Rect(posx + 120, posy + 25, 300f, 40f), "Hold a key, then click this button");
            if (GUI.Button(new Rect(posx, posy + 30, 100f, 30f), "Change key"))
                foreach (KeyCode c in Enum.GetValues(typeof(KeyCode)))
                    if (c != KeyCode.None && c != KeyCode.Mouse0 && Input.GetKey(c)) { key = c; break; }
            GUI.Label(new Rect(posx, posy, 430f, 20f), "Key: " + name + ": " + key);
            _store.Set(name, key);   // a KeyCode, as TLD Loader saves it (a known type in the file)
            return key;
        }

        public void GUIDescriptionText(string text, int height, int posx, int posy)
        {
            if (Drawing) GUI.TextArea(new Rect(posx - 3, posy - 10, 430f, height), text);
        }

        public void GUILabel(string text, int height, int posx, int posy)
        {
            if (Drawing) GUI.Label(new Rect(posx - 3, posy - 10, 430f, height), text);
        }

        public string GUITextArea(string title, string text, int height, int posx, int posy)
        {
            if (_store.TryGet(title, out var v) && v is string s) text = s;
            if (!Drawing) return text;
            GUI.Box(new Rect(posx - 3, posy - 10, 430f, 20f), title);
            text = GUI.TextArea(new Rect(posx - 3, posy + 10, 430f, height), text ?? "");
            _store.Set(title, text);
            return text;
        }

        public int GUIDropdown(string title, List<string> options, int selected, int posx, int posy)
        {
            if (_store.TryGet(title, out var v) && Num(v, out var n)) selected = (int)n;
            if (!Drawing || options == null) return selected;
            GUI.Box(new Rect(posx - 3, posy - 10, 430f, 20f + options.Count * 25), title);
            for (int k = 0; k < options.Count; k++)
                if (GUI.Button(new Rect(posx - 3, posy + 15 + k * 25, 430f, 25f), selected == k ? "<color=#0F0>" + options[k] + "</color>" : options[k])) selected = k;
            _store.Set(title, selected);
            return selected;
        }
    }
#endif
}
