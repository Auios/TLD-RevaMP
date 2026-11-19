using static TLDRevamp.Net.EntityHealthRules;
using static TLDRevamp.Net.EntityHealthWatch;
using static TLDRevamp.Net.EntityPartWatch;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityHealth
    {
        private static string N(double n) => double.IsNaN(n) || double.IsInfinity(n) ? "?" : n.ToString(n < 10 ? "F2" : "F0", CultureInfo.InvariantCulture);
        /// The checks of ItemHealth for object `net`: this machine's own state (`local`, from ObjectPanels.Capture, with
        /// its last 35 s of events) and every other player's view of it (`partners`, the live comparison through the
        /// host). Each problem says why, and lists the recent events that led there.
        internal static void HealthChecks(uint net, ObjectPanelState local, List<(int Player, ObjectPanelState State, ObjectPanelState Mine, double Age)> partners,
                                          bool comparing, List<HealthCheck> into)
        {
            into.Clear();
            var ic = CultureInfo.InvariantCulture;
            var e = EntityOf(net);
            void Add(int level, string title, string why, string how = null, string key = null) => into.Add(new HealthCheck { Level = level, Title = title, Why = why, How = how, Key = key });

            // 1. loaded here at all
            if (e == null || e.Root == null || !e.Root.gameObject.activeInHierarchy)
            {
                Add(2, "Not loaded here", e == null ? "This machine has no object " + net + " (never shared to it, or removed)." : "Known, but not in the world here (streamed out: far from you, or its group isn't loaded).",
                    HowOf(local, "add-", "remove-", "owner-"));
                return;
            }
            var pl = mainscript.s != null ? mainscript.s.player : null;

            // 2. owner: this machine against every other player's view
            string me = WorldOverlayLogic.Owner(e.OwnerId, MyId) + (e.Proxy ? ", shown here as a copy" : ", simulated here") + " · epoch " + e.Epoch;
            var ownerDiff = new List<string>();
            foreach (var p in partners)
                if (p.State.Has(ObjectPanelState.Bits.Found) && (p.State.Owner != e.OwnerId || p.State.Epoch != e.Epoch))
                    ownerDiff.Add("player " + p.Player + " has owner " + WorldOverlayLogic.Owner(p.State.Owner, MyId) + " epoch " + p.State.Epoch);
            if (ownerDiff.Count > 0)
                Add(2, "Owner differs", "Here: " + me + ". " + string.Join("; ", ownerDiff) + ". The machines disagree on who simulates it, so each moves it differently.",
                    HowOf(local, "owner-", "handover-", "claim-", "lease-", "crash-"));
            else Add(0, "Owner", me + (partners.Count > 0 ? " (same for every player)" : ""));

            // 3. position: a copy against its owner's last pose here, and every other player's view
            CheckPosition(e, prefixes => HowOf(local, prefixes), Add);
            foreach (var p in partners)
            {
                if (!p.State.Has(ObjectPanelState.Bits.Found)) { Add(2, "Missing for player " + p.Player, "Player " + p.Player + " has no object " + net + " loaded: it was never sent to them, or removed there.", HowOf(p.State, "add-", "remove-")); continue; }
                if (p.Mine == null || !p.Mine.Has(ObjectPanelState.Bits.Found)) continue;
                double d = p.State.Distance(p.Mine), a = p.State.Angle(p.Mine);
                int sev = ObjectPanelState.Severity(d, a);
                if (sev > 0) Add(sev == 2 ? 2 : 1, "Player " + p.Player + " sees it elsewhere", "Player " + p.Player + " has it " + N(d) + " m and " + N(a) + "° from here (" + N(p.Age) + " s ago)." +
                                 (e.Proxy ? " Yours is the copy: compare with the owner's view." : " Yours is the owner's: their copy is behind or stuck."),
                                 HowOf(p.State, "proxy-", "owner-", "speed-", "crash-") ?? HowOf(local, "proxy-", "owner-", "crash-"));
            }

            // 4. held, stored, attached, locked: against every other player's view
            var mask = ~(ObjectPanelState.Bits.Proxy | ObjectPanelState.Bits.EventsNotShared | ObjectPanelState.Bits.Live);
            foreach (var p in partners)
            {
                if (p.Mine == null || !p.State.Has(ObjectPanelState.Bits.Found) || !p.Mine.Has(ObjectPanelState.Bits.Found)) continue;
                var diffs = new List<string>();
                var a = p.Mine.Flags & mask; var b = p.State.Flags & mask;
                foreach (ObjectPanelState.Bits bit in new[] { ObjectPanelState.Bits.Held, ObjectPanelState.Bits.Stored, ObjectPanelState.Bits.Attached, ObjectPanelState.Bits.Locked })
                    if ((a & bit) != (b & bit)) diffs.Add(bit.ToString().ToLowerInvariant() + ": " + ((a & bit) != 0 ? "yes" : "no") + " here, " + ((b & bit) != 0 ? "yes" : "no") + " for player " + p.Player);
                if (p.Mine.Parent != p.State.Parent || p.Mine.Part != p.State.Part) diffs.Add("attached to " + p.Mine.Parent + "/" + p.Mine.Part + " here, " + p.State.Parent + "/" + p.State.Part + " for player " + p.Player);
                if (p.Mine.LockedTo != p.State.LockedTo) diffs.Add("locked to " + p.Mine.LockedTo + " here, " + p.State.LockedTo + " for player " + p.Player);
                int slotDiff = 0;
                for (int i = 0; i < Math.Min(p.State.SlotCount, p.Mine.SlotCount); i++)
                {
                    var s = p.State.Slots[i]; var m = p.Mine.Slots[i];
                    if (s.Content != m.Content || s.Attached != m.Attached) { slotDiff++; if (slotDiff <= 3) diffs.Add("slot " + s.Item + "/" + s.Index + ": " + (m.Attached ? "net " + m.Content : "empty") + " here, " + (s.Attached ? "net " + s.Content : "empty") + " for player " + p.Player); }
                }
                if (p.State.SlotCount != p.Mine.SlotCount) diffs.Add(p.Mine.SlotCount + " slots here, " + p.State.SlotCount + " for player " + p.Player);
                if (slotDiff > 3) diffs.Add((slotDiff - 3) + " more slots differ");
                if (diffs.Count > 0)
                    Add(2, "State differs from player " + p.Player, string.Join("; ", diffs) + ". A message about it was lost, refused, or applied on one machine only.",
                        HowOf(local, "attach-", "detach-", "part-", "physlock-", "joint-", "remove-") ?? HowOf(p.State, "attach-", "detach-", "part-", "physlock-"));
            }

            // 5. its body: what the mod keeps on it here; and what can never be right (sanity)
            CheckBody(e, prefixes => HowOf(local, prefixes), Add);
            CheckPickup(e, prefixes => HowOf(local, prefixes), Add);
            CheckSanity(e, Add);

            // 6. its fitted parts: shown but unreachable (PartWatch)
            var broken = new List<string>(); int fitted = 0;
            if (pl != null && e.Items != null)
                foreach (var it in e.Items)
                    if (it != null && it.partslotscripts != null)
                        foreach (var ps in it.partslotscripts)
                        {
                            if (ps == null || !ps.hasPart()) continue;
                            var part = ps.part(); if (part == null || part.tosave == null || part.tosave.P == null) continue;
                            fitted++;
                            var r = EntityDiag.Reach(part, pl);
                            if (PartWatchRules.Broken(r.Visible, r.Targetable, r.Expected, r.Held)) broken.Add(EntityLifetimeStreaming.PrefabName(part.tosave.id) + " (" + PartWatchRules.Why(r.Live, r.Held) + ", colliders " + r.Live + "/" + r.Cols + ")");
                        }
            if (e.Root.attachable != null && e.Root.attachable.attached && e.Root.attachable.slot != null && pl != null)
            {
                var r = EntityDiag.Reach(e.Root.attachable, pl);
                if (PartWatchRules.Broken(r.Visible, r.Targetable, r.Expected, r.Held)) broken.Add("this part itself (" + PartWatchRules.Why(r.Live, r.Held) + ")");
            }
            if (broken.Count > 0) Add(2, "Parts you can't reach", string.Join("; ", broken) + ". You see them but get no prompt: the crosshair's use ray can't hit them.", HowOf(local, "part-", "attach-", "detach-", "owner-", "handover-"));
            else if (fitted > 0) Add(0, "Fitted parts", fitted + " fitted, all reachable");

            // 7. what was refused or corrected lately
            var trouble = new List<string>();
            for (int i = 0; i < local.EventCount; i++)
            {
                var ev = local.Events[i]; string k = ObjectPanelEvents.Kinds[ev.Kind];
                if (k.EndsWith("-refused", StringComparison.Ordinal) || k == "proxy-correction" || k == "proxy-snap" || k == "speed-jump" || k == "speed-anomaly" || k == "census-repair-failed"
                    || k == "health-problem" || k == "item-untargetable" || k == "part-untargetable")
                    trouble.Add(EventText(ev, 0));
            }
            if (trouble.Count > 0) Add(1, "Refused or corrected lately", string.Join("; ", trouble.GetRange(0, Math.Min(4, trouble.Count))) + (trouble.Count > 4 ? "; " + (trouble.Count - 4) + " more" : ""));

            // 8. whether the other players' views are in
            if (!comparing) Add(1, "Other players' views", "Not compared (not in a session, or the comparison is off).");
            else if (partners.Count == 0) Add(1, "Other players' views", "Waiting for their replies through the host.");
        }

        /// The recent events (35 s) of the kinds given, newest first, as a short timeline — how the state came about.
        private static string HowOf(ObjectPanelState s, params string[] prefixes)
        {
            if (s == null || s.EventCount == 0) return null;
            var lines = new List<string>();
            for (int i = 0; i < s.EventCount && lines.Count < 4; i++)
            {
                string k = ObjectPanelEvents.Kinds[s.Events[i].Kind];
                foreach (var p in prefixes) if (k.StartsWith(p, StringComparison.Ordinal)) { lines.Add(EventText(s.Events[i], 0)); break; }
            }
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }
        internal static string EventText(ObjectPanelState.Recent ev, double age)
        {
            string k = ObjectPanelEvents.Kinds[ev.Kind], reason = ObjectPanelEvents.Reasons[ev.Reason];
            string who = ev.From >= 0 || ev.To >= 0 ? " (" + (ev.From >= 0 ? WorldOverlayLogic.Owner(ev.From, MyId) : "?") + (ev.To >= 0 ? " → " + WorldOverlayLogic.Owner(ev.To, MyId) : "") + ")" : "";
            return "-" + ObjectPanelState.N(ev.Age + age) + " s " + k + who + (ev.Epoch != 0 ? " epoch " + ev.Epoch : "") + (reason != "ok" && reason != "reason unavailable" ? ": " + reason : "");
        }
    }
}
