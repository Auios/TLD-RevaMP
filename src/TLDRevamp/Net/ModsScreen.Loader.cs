using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLDRevamp.Net
{
    /// The TLD Loader mods in the mods list (Installed): the mods RevaMP runs from BepInEx/TLDLoaderMods, never from the
    /// catalog (RevaMP doesn't offer them). Each row says who made it and which version, and its last error if it had
    /// one; Settings opens its own settings window, as TLD Loader's mod list does (ModHost.Settings.cs). The window is
    /// IMGUI, over the list: while it is open a backdrop takes the clicks the list would otherwise get under it.
    public static partial class ModsScreen
    {
        private static GameObject _loaderBlock;

        /// The Loader mods the Installed tab shows for the search.
        private static List<ModHost.LoaderModInfo> LoaderShown(string search)
        {
            var all = ModHost.LoaderMods();
            if (string.IsNullOrWhiteSpace(search)) return all;
            string q = search.Trim();
            return all.FindAll(m => Has(m.Name, q) || Has(m.Id, q) || Has(m.Author, q));
        }
        private static bool Has(string s, string q) => s != null && s.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static string LoaderKey(ModHost.LoaderModInfo m) => m.Id + m.Version + m.Error + m.HasSettings;

        private static void LoaderRow(ModHost.LoaderModInfo m)
        {
            var row = new GameObject("Mod loader:" + m.Id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(_rows, false);
            var img = row.GetComponent<Image>(); img.sprite = _paper; img.type = Image.Type.Sliced; img.color = Paper; img.raycastTarget = true;
            row.GetComponent<LayoutElement>().preferredHeight = 178f;
            var rt = (RectTransform)row.transform;

            var tile = (RectTransform)new GameObject("Tile", typeof(RectTransform), typeof(Image)).transform;
            tile.SetParent(rt, false); TopLeft(tile, 22f, 24f, 130f, 130f);
            var timg = tile.GetComponent<Image>(); timg.sprite = _buttonSprite; timg.type = Image.Type.Sliced;
            timg.color = Tiles8[ModList.Hue(m.Id) % Tiles8.Length]; timg.raycastTarget = false;
            var initials = NewText(tile, _paperStyle, 56f, Color.white, TextAlignmentOptions.Center, "Initials");
            Stretch(initials.transform); initials.fontStyle = FontStyles.Bold; initials.text = Esc(ModList.Initials(m.Name ?? m.Id));

            const float textLeft = 178f, textRight = 360f;
            var head = NewText(rt, _paperStyle, 42f, Ink, TextAlignmentOptions.MidlineLeft, "Head");
            Band(head.rectTransform, textLeft, textRight, 6f, 58f);
            head.text = "<b>" + Esc(m.Name ?? m.Id) + "</b>"
                + (string.IsNullOrEmpty(m.Author) ? "" : "  <size=28><color=#" + ColorUtility.ToHtmlStringRGB(Faded) + ">by " + Esc(m.Author) + "</color></size>")
                + "    <size=27><b><color=#" + ColorUtility.ToHtmlStringRGB(OnPaper(On)) + ">On · TLD Loader mod</color></b></size>";
            var desc = NewText(rt, _paperStyle, 27f, Body, TextAlignmentOptions.TopLeft, "Description");
            Band(desc.rectTransform, textLeft, textRight, 74f, 66f); desc.enableWordWrapping = true; desc.maxVisibleLines = 2;
            desc.text = string.IsNullOrEmpty(m.Error)
                ? "Installed by hand in BepInEx/TLDLoaderMods; RevaMP runs it as TLD Loader would."
                : "<color=#" + ColorUtility.ToHtmlStringRGB(OnPaper(Removing)) + ">Its last error: " + Esc(m.Error) + "</color>";
            var meta = NewText(rt, _paperStyle, 24f, Faded, TextAlignmentOptions.MidlineLeft, "Numbers");
            Band(meta.rectTransform, textLeft, textRight, 141f, 30f);
            meta.text = (string.IsNullOrEmpty(m.Version) ? "" : "v" + Esc(m.Version) + "     ·     ") + Esc(m.Id);

            var primary = NewButton(rt, m.HasSettings ? "Settings" : "No settings", ButtonGrey, 44f, () => OpenLoaderSettings(m.Id));
            TopRight((RectTransform)primary.transform, 24f, 18f, 310f, 84f);
            SetButton(primary, m.HasSettings ? "Settings" : "No settings", m.HasSettings ? ButtonGreen : ButtonGrey, m.HasSettings);
        }

        internal static void OpenLoaderSettings(string id)
        {
            if (!ModHost.OpenConfig(id)) return;
            if (_loaderBlock == null && _list != null)
            {
                _loaderBlock = new GameObject("LoaderSettingsBlock", typeof(RectTransform));
                _loaderBlock.transform.SetParent(_list.transform, false); Stretch(_loaderBlock.transform);
                Backdrop(_loaderBlock.transform, new Color(0f, 0f, 0f, .55f));
                _list.AddComponent<LoaderBlockFollower>();
            }
            if (_loaderBlock != null) { _loaderBlock.SetActive(true); _loaderBlock.transform.SetAsLastSibling(); }
        }

        /// The backdrop goes when the window does (its x, or the list closing).
        private sealed class LoaderBlockFollower : MonoBehaviour
        {
            private void Update()
            {
                if (_loaderBlock != null && _loaderBlock.activeSelf != ModHost.ConfigScreenOpen) _loaderBlock.SetActive(ModHost.ConfigScreenOpen);
            }
        }
    }
}
