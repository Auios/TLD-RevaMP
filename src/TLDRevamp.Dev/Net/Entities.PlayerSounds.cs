using System.Linq;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPlayerSoundsTools
    {

        public static string PlayerSoundsTest(string[] a)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            string did = "null";
            if (a.Length > 1 && pl != null)
            {
                switch (a[1])
                {
                    case "burp": pl.Burp(); break;
                    case "fart": pl.Fart(); break;
                    case "shit": pl.Shit(); break;
                    case "step": pl.stepScript.Step(1f); break;
                    case "walk": ObservedWalkUntil = Time.time + float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture); break;
                    default: return "{\"error\":\"burp|fart|shit|step|walk secs\"}";
                }
                did = Json.Str(a[1]);
            }
            int models = 0, modelSteps = 0, modelStepEvents = 0;
            foreach (var mp in Object.FindObjectsOfType<mpplayerscript>())
            {
                models++;
                modelSteps += mp.GetComponentsInChildren<stepSoundScript>(true).Length;
                modelStepEvents += mp.GetComponentsInChildren<animStepEventScript>(true).Length;
            }
            return "{\"did\":" + did + ",\"sent\":" + EntityEffects.PlayerSoundsSent + ",\"played\":" + EntityEffects.PlayerSoundsPlayed + ",\"sentBy\":[" + string.Join(",", EntityEffects.SoundsSentBy) +
                   "],\"playedBy\":[" + string.Join(",", EntityEffects.SoundsPlayedBy) + "],\"modelStepsMuted\":" + EntityEffects.ModelStepsMuted + ",\"models\":" + models +
                   ",\"modelStepScripts\":" + modelSteps + ",\"modelStepEvents\":" + modelStepEvents + ",\"playerMadeShared\":" + EntityLifetimeStreaming.PlayerMadeShared + ",\"last\":" + TLDRevamp.Net.EntityEffects.ObservedLastSound + "}";
        }
        [HarmonyPatch(typeof(inputscript), "Update")]
        internal static class WalkTest
        {
            [HarmonyPostfix]
            private static void Postfix(inputscript __instance) { if (Time.time < _walkUntil) __instance.forward = 1f; }
        }

        // ---- diagnostics / test hooks (bridge `mp sounds`, `mp sounds burp|fart|shit|step`, `mp sounds walk secs`: the
        // player walks straight ahead, the forward key held)
        internal static float _walkUntil;
        internal static float ObservedWalkUntil { get => _walkUntil; set => _walkUntil = value; }
    }
}
