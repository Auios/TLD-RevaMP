using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityCorpsesTools
    {

        /// Test hook (bridge `mp outfit [random]`): the local player's outfit, or a new random one first (the game's own
        /// SetRandom, so the test's dead player looks unlike the default one on the other machine).
        public static string OutfitTest(bool random)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || pl.outfit == null) return "{\"error\":\"no player\"}";
            if (random) { pl.outfit.SetRandom(_FullRandom: true); pl.outfit.Refresh(fromMultiplayer: false); }
            pl.outfit.GetData(out int sel, out bool[] en, out Color[] col);
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"character\":").Append(sel).Append(",\"on\":\"");
            foreach (bool b in en) sb.Append(b ? '1' : '0');
            sb.Append("\",\"colors\":\"");
            foreach (var c in col) sb.Append(ColorUtility.ToHtmlStringRGB(c)).Append(' ');
            sb.Append("\",\"hash\":").Append(PlayerLook.OutfitHash(sel, en, col)).Append('}');
            return sb.ToString();
        }
    }
}
