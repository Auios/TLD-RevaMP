using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityRadioTools
    {

        /// Diagnostics (bridge `mp radiocar <net>`): the radios of a shared object here — on, band, frequency, volume knob,
        /// the clip playing and how far into it.
        public static string RadioCar(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e) || e.Root == null) return "{\"error\":\"no such object here\"}";
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>();
            foreach (var r in e.Root.transform.root.GetComponentsInChildren<radioscript>(true))
                rows.Add("{\"on\":" + (r.ON ? "true" : "false") + ",\"am\":" + (r.amfm ? "true" : "false") + ",\"fr\":" + r.FR.ToString("F1", ic) +
                         ",\"volume\":" + (r.Bvolume != null ? r.Bvolume.tunedLerp : -1f).ToString("F2", ic) +
                         ",\"clip\":" + Json.Str(r.SC != null && r.SC.clip != null ? r.SC.clip.name : "") + ",\"time\":" + (r.SC != null ? r.SC.time : -1f).ToString("F1", ic) +
                         ",\"playing\":" + (r.SC != null && r.SC.isPlaying ? "true" : "false") + ",\"electronics\":" + (r.electronics != null ? r.electronics.output : -1f).ToString("F2", ic) +
                         ",\"tune\":" + DialAddress(r.Btune) + ",\"vol\":" + DialAddress(r.Bvolume) + "}");
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"radios\":[" + string.Join(",", rows) + "]}";
        }

        /// Test hook (bridge `mp radio advance i`): station i (FM first, then AM) moves to its next clip, as when a clip ends.
        public static string RadioAdvance(int i)
        {
            if (mainscript.s == null) return "{\"error\":\"no game\"}";
            var fm = mainscript.s.radioChs; var am = mainscript.s.radioChsAM;
            var ch = i < fm.Count ? fm[i] : i - fm.Count < am.Count ? am[i - fm.Count] : null;
            if (ch == null) return "{\"error\":\"no station " + i + "\"}";
            ch.AdvanceRadio();
            return "{\"station\":" + Json.Str(ch.name) + ",\"clip\":" + Json.Str(ch.currentClip != null ? ch.currentClip.name : "") + "}";
        }

        internal static string DialAddress(usablescript u) =>
            UsableAddress(u, out uint net, out int item, out int usable) ? "[" + net + "," + item + "," + usable + "]" : "null";
    }
}
