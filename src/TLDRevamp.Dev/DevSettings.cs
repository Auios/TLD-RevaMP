using System;
using static TLDRevamp.RevampSettings;

namespace TLDRevamp
{
    internal static class DevSettings
    {
        internal static void Install()
        {
            int at = Entries.FindIndex(e => e.Kind == Kind.Header && e.Label == "Debugging");
            Entries.RemoveAll(e => e.Key == "overlay" || e.Kind == Kind.Header && e.Label == "Debugging");
            int first = Entries.Count;
            Header("Branding",
                "Controls the small development-state mark drawn over the game view.");
            Toggle("showWipMark", "Show WIP mark", 1,
                "Draw a small \"TLD RevaMP <version> · WIP\" label in the bottom-right corner. It appears in screenshots and recordings, so shared footage shows that this build is still work in progress and which version it is.",
                () => Plugin.WipMarkEnabled ? 1 : 0,
                v => Plugin.SetWipMark(v == 1));
            // one stored copy: the BepInEx config (Branding.ShowWipMark). Saved here too, loading it put an old 1 over
            // a ShowWipMark=false set in the config file at every start (scan).
            Entries[Entries.Count - 1].RuntimeOnly = true;
            Header("Debugging",
                "Page Down saves a debug report with recent game state, performance/network measurements, numeric settings " +
                "and hardware models. Scroll Lock opens session marks: Enter saves a note, Escape cancels. Browse and rename " +
                "notes from the list. The keys can be changed in BepInEx/config/tldrevamp.core.cfg (DebugReport and Mark).");
            Choice("debugStyle", "Version", new[] { "Legacy", "V2" }, 0,
                "Legacy: the switches below, one per tool, as before.\nV2: one switch for the whole debug overlay, a tab above each " +
                "nearby shared object with everything about it (owner, sync, network use, colliders), and Right Shift to change the layout. " +
                "Legacy's switches keep their values while V2 is used.",
                () => DebugV2.Style ? 1 : 0, v => { DebugV2.Style = v == 1; DebugV2.Apply(); });
            Toggle("debugV2", "Debug overlay", 0,
                "Everything on: the performance graph, the network packets panel, the focus card, owner ghosts and links, and a tab " +
                "directly above each nearby shared object: who owns it, simulated here or a copy, its sync error, its network use, " +
                "and its colliders (switched on, and whether its fitted parts can be reached). " +
                "Right Shift (Keys → DebugLayout in BepInEx/config/tldrevamp.core.cfg) edits the layout: switch parts off, drag the panels.",
                () => DebugV2.On ? 1 : 0, v => { DebugV2.On = v == 1; DebugV2.Apply(); });
            Entries[Entries.Count - 1].Visible = () => DebugV2.Style;
            Toggle("debugShown", "Show debug overlays", 1,
                "Off hides every RevaMP debug overlay — the performance graph, the packets panel, the focus card, the tabs over objects, " +
                "ghosts, links and custom panels — without changing any of their settings. (V2 can also hide them while the game's F1 menu is open: Right Shift → Options.)",
                () => DebugV2.HiddenByHand ? 0 : 1, v => DebugV2.Hidden = v == 0);
            int legacyFrom = Entries.Count;
            Choice("debugView", "Debug view", new[] { "Off", "Focus", "Problems", "Everything" }, 0,
                "Off: no object debug work.\nFocus: one target card, ghost and links.\nProblems: Focus plus tags on faulty objects.\nEverything: all nearby labels and ghosts. Key: DebugView (unbound).",
                () => (int)DebugView.Mode, v => DebugView.Mode = (DebugViewMode)v);
            Toggle("overlay", "Performance overlay", 1,
                "The small FPS graph in the top left corner (also Page Up).",
                // ReferenceEquals: the game destroys the BepInEx plugin object at startup (Unity's == null is then true),
                // but the plugin's C# fields — the overlay included — live on and are used by the mod's runner.
                () => !ReferenceEquals(Plugin.Instance, null) && Plugin.Instance.Overlay != null && Plugin.Instance.Overlay.Visible ? 1 : 0,
                v => { if (!ReferenceEquals(Plugin.Instance, null) && Plugin.Instance.Overlay != null) Plugin.Instance.Overlay.Visible = v == 1; });
            Toggle("netPackets", "Network packets", 0,
                "In a multiplayer game: a panel in the top right corner with what your game sends and receives each second, " +
                "by kind of message (how many, how many kilobytes, and how many milliseconds your game spends on them).",
                () => TLDRevamp.Net.NetMeterTools.Show ? 1 : 0, v => TLDRevamp.Net.NetMeterTools.Show = v == 1);
            Toggle("itemTraffic", "Item traffic", 0,
                "In a multiplayer game: download and upload arrows over shared cars, items and mod objects, with how many " +
                "bytes this machine receives and sends each second (also Insert).",
                () => Net.ItemTraffic.Show ? 1 : 0, v => Net.ItemTraffic.SetVisible(v == 1));
            Toggle("ownershipOverlay", "Ownership and sync", 0,
                "World labels: problems only in Problems, full labels in Everything (nearest 40 within 300 m). Enabling from Off opens Everything.",
                () => WorldOverlays.Ownership ? 1 : 0, v => WorldOverlays.Ownership = v == 1);
            Toggle("ownerGhost", "Owner ghost", 0,
                "Owner pose box for the focused object; Problems also shows faulty copies. Locally owned objects have no received owner pose.",
                () => WorldOverlays.Ghost ? 1 : 0, v => WorldOverlays.Ghost = v == 1);
            Toggle("ownerGhostAll", "Owner ghosts for nearby objects", 0,
                "Everything only: with Owner ghost enabled, draw boxes for the nearest 40 shared objects within 300 m.",
                () => WorldOverlays.GhostAll ? 1 : 0, v => WorldOverlays.GhostAll = v == 1);
            Toggle("linkView", "Link view", 0,
                "Focus/Problems: target links only. Everything: nearby lock, hitch, part and rider links; red marks owner or reverse-link faults.",
                () => WorldOverlays.Links ? 1 : 0, v => WorldOverlays.Links = v == 1);
            Toggle("objectComparison", "Live partner comparison", 0,
                "Compare the focused shared object with every session player's view through the host, twice a second. " +
                "Inside the focus card, differences only; net compare all shows all fields. Turn off to stop requests. " +
                "Assign ObjectComparison in the BepInEx key settings; unbound by default.",
                () => ObjectPanels.Compare ? 1 : 0, v => ObjectPanels.Compare = v == 1);
            Toggle("objectEvents", "Focused object event feed", 0,
                "Expand recent events in the focus card from three to six per view (up to 35 seconds); empty histories collapse. " +
                "Local events need no report sharing; partner events require it. " +
                "Assign ObjectEvents in the BepInEx key settings; unbound by default.",
                () => ObjectPanels.Events ? 1 : 0, v => ObjectPanels.Events = v == 1);
            for (int i = legacyFrom; i < Entries.Count; i++) Entries[i].Visible = () => !DebugV2.Style;
            NetworkConditions.Settings();

            var added = Entries.GetRange(first, Entries.Count - first);
            Entries.RemoveRange(first, Entries.Count - first); Entries.InsertRange(at, added);
            Loaded += saved => {
                if (saved.TryGetValue("debugView", out string modeText) && int.TryParse(modeText, out int mode) && mode == 0) DebugView.Mode = DebugViewMode.Off;
                if (saved.TryGetValue(DebugV2.LayoutKey, out string layout)) { SetText(DebugV2.LayoutKey, layout); DebugV2.LoadLayout(layout); }
                DebugV2.Apply();
            };
            BeforeSave += DebugV2.BeforeSave; AfterSave += DebugV2.AfterSave;
            Load();
        }
    }
}
