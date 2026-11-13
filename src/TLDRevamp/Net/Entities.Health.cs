using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TLDRevamp
{
    /// One line of an item's health (ItemHealth): ok, a warning or a problem, what is wrong, why, and what happened.
    internal sealed class HealthCheck
    {
        internal int Level;      // 0 ok, 1 warning, 2 problem
        internal string Title, Why, How;
        internal string Key;     // the background watch's fixed name for a problem (HealthWatchRules), or null
    }
}

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static bool Loose(Ent e, fpscontroller pl) =>
            e.Root != null && e.Root.gameObject.activeInHierarchy && e.Root.P != null && e.Root.car == null
            && (e.Root.attachable == null || !e.Root.attachable.attached) && !e.Held && !e.Stored && !e.ReleaseGhost && !e.CargoGhost
            && !InLocalHands(pl, e);

        /// In the local player's hands: picked up, or held in hand (a lamp while seated: inHandP).
        internal static bool InLocalHands(fpscontroller pl, Ent e) =>
            pl != null && e.Root != null && e.Root.P != null && (pl.pickedUp == e.Root.P || pl.inHandP == e.Root.P);

        internal static Ent EntityOf(uint net) => ByNet.TryGetValue(net, out var e) ? e : null;

        internal delegate void CheckAdd(int level, string title, string why, string how = null, string key = null);

        /// A copy against its owner's last pose here. The optional explanation comes from the developer panel's events.
        internal static void CheckPosition(Ent e, Func<string[], string> local, CheckAdd Add)
        {
            // a part fitted to a car (or bolted to its parent) sends no poses of its own: its last one is from before it was
            // fitted, minutes old and anywhere (a bus tank "3956 m off", a glovebox "884 m off" while drawn right in the car)
            if (e.Proxy && (OverlayBolted(e) || AttachedCarPart(e)))
            {
                Add(0, "Position", "Rides its car or parent: no poses of its own to compare.");
                return;
            }
            if (e.Proxy && OverlaySample(e, out var pose, out var rot))
            {
                var frame = OverlayFrame(e);
                double err = frame != null ? (mainscript.GlobalFromUnityPos(frame.position) - pose).magnitude : double.NaN;
                double age = e.TelemetryReceived > 0 ? Time.realtimeSinceStartupAsDouble - e.TelemetryReceived : double.NaN;
                bool moving = OverlayMoving(e), stale = (moving && age > BandwidthRules.KeepaliveS + .5);
                // a copy is drawn its interpolation delay behind the last pose: at speed that is metres (the host's car at
                // 49 m/s, 3.39 m off a pose 0.01 s old, was marked "left behind")
                double speed = e.LastVel.magnitude, tol = 2 + speed * ((e.Ip != null && e.Ip.DelayMs > 0 ? e.Ip.DelayMs / 1000.0 : 0.1) + (double.IsNaN(age) ? 0 : Math.Min(age, 1)));
                if (stale && !(err >= tol)) stale = false;   // quiet but where it should be: an event's worth, not a problem
                int level = stale || err >= tol ? 2 : err >= .2 ? 1 : 0;
                string why = "Shown " + N(err) + " m from its owner's last pose here, which is " + N(age) + " s old" + (moving ? " (moving)" : "") + ".";
                if (stale) why += " No update for over 1.5 s while it moves: its owner stopped sending, or this machine stopped hearing it.";
                else if (err >= tol) why += " Left behind its owner's poses: held back by something here (a collision, a hold, a lost interpolation).";
                Add(level, level == 0 ? "Position" : "Position off", why, level > 0 ? Explain(local, "proxy-", "speed-", "owner-", "crash-", "tpa-") : null, level == 2 ? HealthWatchRules.Position : null);
            }
        }

        /// What the mod keeps on the object here: colliders, layers, the body.
        internal static void CheckBody(Ent e, Func<string[], string> local, CheckAdd Add)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var rb = e.Root.GetComponent<Rigidbody>();
            _pwCols.Clear(); e.Root.GetComponentsInChildren(true, _pwCols);
            int all = _pwCols.Count, on = 0, heldLayer = 0;
            foreach (var c in _pwCols)
            {
                if (c == null) continue;
                if (c.enabled && c.gameObject.activeInHierarchy) on++;
                // on the held layer because what it is on is being carried (a tyre on a wheel in someone's hands): the game's
                // pickupable.Pickup puts the parts in its slots there too
                if (c.gameObject.layer == mainscript.pickedUpLayer && !HeldAbove(c.transform, pl)) heldLayer++;
            }
            bool inHand = InLocalHands(pl, e);
            // a ragdoll (a corpse, a dead rabbit): the game unparents its bones' colliders, the root keeps none switched on
            bool ragdoll = Ragdoll(e.Root);
            // a fitted part whose slot switches its colliders off on purpose (partslotscript.disableCollidersOnCraft)
            bool offByDesign = e.Root.attachable != null && e.Root.attachable.attached && !CollidersExpected(e.Root.attachable);
            bool worn = e.WornBy >= 0 || e.SentWornSlot >= 0 || LocalWornSlot(e.Root) >= 0;   // the game takes a worn item's body away
            bool keptFrozen = e.Root.buried || Fixes.UnfreezeOnGround.Holding(e.Root);       // kinematic on purpose until it has ground under it
            string body = rb == null ? "no rigidbody" : (rb.isKinematic ? "kinematic" : "dynamic") + (rb.detectCollisions ? "" : ", collisions off");
            string flags = (e.Held ? " held" : "") + (e.Stored ? " stored" : "") + (worn ? " worn" : "") + (keptFrozen ? " kept-frozen" : "") + (e.ReleaseGhost ? " release-ghost" : "") + (e.CargoGhost ? " cargo-ghost" : "") + (e.Physical ? " physical-copy" : "") + (e.PartsColOff ? " part-colliders-off" : "");
            string bodyText = "Colliders " + on + "/" + all + " on · " + body + (flags.Length > 0 ? " · mod flags:" + flags : "");
            var bodyProblems = new List<string>();
            if (all > 0 && on == 0 && !e.Stored && !worn && !ragdoll && !offByDesign) bodyProblems.Add("every collider is off, so nothing can touch or target it");
            if (heldLayer > 0 && !inHand && !e.Held) bodyProblems.Add(heldLayer + " colliders on the held-item layer while nobody holds it: the crosshair passes through them");
            if (rb != null && !rb.detectCollisions && !e.Held && !e.Stored && !e.ReleaseGhost && !e.CargoGhost && !inHand && !worn) bodyProblems.Add("collisions are off without a reason the mod knows (not held, stored or a ghost)");
            bool heldFrozen = Fixes.LoadFreeze.Holding || Tpa.Holding(rb) || (e.Root.P != null && e.Root.P.inRightHand);   // frozen on purpose for a moment
            if (rb != null && !e.Proxy && rb.isKinematic && !e.Stored && e.Root.attachable != null && !e.Root.attachable.attached && !inHand && !worn && !keptFrozen && !heldFrozen && e.Root.car == null) bodyProblems.Add("ours but kinematic and loose: it won't fall or be pushed");
            if (rb != null && e.Proxy && !rb.isKinematic && !e.Physical && !e.CrashTaking) bodyProblems.Add("a copy with a dynamic body outside a crash: it will drift from its owner's poses");
            if (bodyProblems.Count > 0) Add(2, "Body", bodyText + ". " + string.Join("; ", bodyProblems) + ".", Explain(local, "owner-", "physlock-", "attach-", "detach-", "part-", "handover-"), HealthWatchRules.Body);
            else Add(0, "Body", bodyText);

        }

        /// The nearest pickupable above this collider (itself included) is being carried: here, by another player, or by us.
        internal static bool HeldAbove(Transform t, fpscontroller pl)
        {
            var p = t.GetComponentInParent<pickupable>();
            for (int i = 0; p != null && i < 8; i++)
            {
                if (p.pickedUp || (pl != null && (p == pl.pickedUp || p == pl.inHandP))) return true;
                var pe = p.tosave != null ? EntOfRoot(p.tosave.transform.root) : null;
                if (pe != null && pe.Held) return true;
                // what it is fitted to, up the chain (a tyre on a rim on a wheel)
                var at = p.tosave != null ? p.tosave.attachable : null;
                var holder = at != null && at.attached && at.slot != null && at.slot.tosaveitem != null ? at.slot.tosaveitem.P : null;
                if (holder == null || holder == p) return false;
                p = holder;
            }
            return false;
        }

        /// A ragdoll's root (a player's corpse, a rabbit gone limp): its colliders are on bones the game unparents.
        internal static bool Ragdoll(tosaveitemscript t)
        {
            if (t == null) return false;
            if (t.ragdollPos != null) return true;
            var r = t.GetComponentInChildren<ragdollactivatescript>(true);
            return r != null && r.ragdoll;
        }

        /// A loose item: can the game's pickup ray (fpscontroller.pickupLayer) hit it? Colliders all off or on the held layer
        /// are the body check's; here, colliders on but none on a layer the ray hits ("I could see it just fine").
        internal static void CheckPickup(Ent e, Func<string[], string> local, CheckAdd Add)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || !Loose(e, pl)) return;
            _pwCols.Clear(); e.Root.GetComponentsInChildren(true, _pwCols);
            int live = 0, held = 0, pickup = 0, mask = pl.pickupLayer.value;
            var layers = new List<string>();
            foreach (var c in _pwCols)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                live++;
                if (c.gameObject.layer == mainscript.pickedUpLayer) held++;
                if ((mask & (1 << c.gameObject.layer)) != 0) pickup++;
                string ln = LayerMask.LayerToName(c.gameObject.layer); if (!layers.Contains(ln)) layers.Add(ln);
            }
            if (live == 0 || held > 0) return;
            bool shows = false;
            foreach (var rd in e.Root.GetComponentsInChildren<Renderer>()) if (rd.enabled && rd.gameObject.activeInHierarchy) { shows = true; break; }
            if (pickup == 0 && shows)
                Add(2, "Can't pick it up", live + " colliders on, none on a layer the game's pickup ray hits (they are on " + string.Join(", ", layers) +
                    "): it shows, but the crosshair passes through it and there is no prompt.", Explain(local, "owner-", "remove-", "add-", "detach-", "handover-"), HealthWatchRules.ItemNotPickup);
            else Add(0, "Pick up", pickup + " of " + live + " colliders on the pickup layer");
        }

        private static string Explain(Func<string[], string> explain, params string[] prefixes) => explain?.Invoke(prefixes);
        private static string N(double n) => double.IsNaN(n) || double.IsInfinity(n) ? "?" : n.ToString(n < 10 ? "F2" : "F0", CultureInfo.InvariantCulture);
    }
}
