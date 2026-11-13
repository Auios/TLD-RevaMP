using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// A spray can in use. The game sprays only on the machine of the player holding it (sprayscript.Fire from their
    /// input: the valve opens, its Update emits the mist and plays the hiss); the paint itself reaches the car's owner as
    /// an edit (Entities.Edits). Everyone else saw the colour appear with no spray at all. The owner's state carries
    /// "spraying" (flag 8, sent on change, three times); on the copies the can is kept firing — the game's own Update
    /// then shows the mist from the can, pointing where the holder points it, and plays the hiss. Only the effect: a
    /// copy paints nothing (its raycast stroke is the owner's alone: Fire is not called here).
    public static partial class Entities
    {
        public static bool SprayFx = true;   // A/B: false = the copies stay silent (before v0.65.23)
        public static long SprayShown;
        private static readonly HashSet<Ent> _spraying = new HashSet<Ent>();

        /// Owner: the can in this player's hands is spraying (its valve open past the first frames).
        internal static bool IsSpraying(tosaveitemscript it)
        {
            if (it == null || !IsHeld(it)) return false;
            var sp = it.GetComponent<sprayscript>();
            return sp != null && sp.valve > 0.1f && sp.hasPaint;
        }

        private static void ShowSpraying(Ent e, bool spraying)
        {
            if (e.Spraying == spraying) return;
            e.Spraying = spraying;
            if (spraying) { _spraying.Add(e); SprayShown++; } else _spraying.Remove(e);
        }

        /// Per frame: copies of cans being sprayed keep their valve open, as Fire does on the holder's machine. Their
        /// Update closes it again when nothing keeps it open (firing false) — the flag off, or the copy gone.
        private static void SprayTick(float dt)
        {
            SprayHoldTick();
            if (_spraying.Count == 0) return;
            List<Ent> gone = null;
            foreach (var e in _spraying)
            {
                var sp = e.Root != null && e.Proxy && e.Spraying ? e.Root.GetComponent<sprayscript>() : null;
                if (sp == null) { (gone ?? (gone = new List<Ent>())).Add(e); continue; }
                if (!SprayFx) continue;
                sp.firing = true;
                sp.valve = Mathf.Lerp(sp.valve, 1f, dt * sp.valveSpeed);
            }
            if (gone != null) foreach (var e in gone) { e.Spraying = false; _spraying.Remove(e); }
        }

        internal static sprayscript HeldSpray(fpscontroller pl)
        {
            if (pl == null) return null;
            var p = pl.pickedUp;
            if (p != null && p.spray != null) return p.spray;
            foreach (var s in Object.FindObjectsOfType<sprayscript>()) if (s.P != null && s.P.inRightHand) return s;
            return null;
        }

        private static void SprayHoldTick() => DevHooks.SprayTick?.Invoke();


        public static string LampState(uint net)
        {
            if (!ByNet.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            int on = 0, all = 0; var states = new List<string>(); int sw = -1;
            foreach (var h in e.Root.GetComponentsInChildren<headlightscript>(true))
            {
                states.Add(h.currentState.ToString());
                if (sw < 0 && h.usableSet != null) sw = h.usableSet.currentTurnState;
            }
            foreach (var L in e.Root.GetComponentsInChildren<Light>(true)) { all++; if (L.enabled && L.gameObject.activeInHierarchy && L.intensity > 0.01f) on++; }
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"switch\":" + sw + ",\"states\":\"" + string.Join(" ", states) + "\",\"lit\":[" + on + "," + all + "]}";
        }

        /// `mp sprayfx <net>`: a can's spray as shown here — valve, mist emitting, hiss playing; owner or copy.
        public static string SprayFxState(uint net)
        {
            if (!ByNet.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            var sp = e.Root.GetComponent<sprayscript>();
            if (sp == null) return "{\"error\":\"not a spray can\"}";
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"flag\":" + (e.Spraying ? "true" : "false") + ",\"valve\":" + sp.valve.ToString("F2", ic) +
                   ",\"mist\":" + (sp.particle != null && sp.particle.emission.enabled ? "true" : "false") + ",\"hiss\":" + (sp.Saudio != null && sp.Saudio.isPlaying ? "true" : "false") +
                   ",\"playing\":" + (sp.particle != null && sp.particle.isPlaying ? "true" : "false") + ",\"particles\":" + (sp.particle != null ? sp.particle.particleCount : -1) +
                   ",\"paint\":" + (sp.hasPaint ? "true" : "false") + ",\"shown\":" + SprayShown + "}";
        }
    }
}
