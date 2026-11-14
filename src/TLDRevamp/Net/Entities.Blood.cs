using UnityEngine;

namespace TLDRevamp.Net
{
    /// A car's blood (carscript.blood, a washtriggerscript: the windscreen splatter after running something over). It is
    /// part of the car's own record (save_carscript.blood), which only the owner's resync carries, and its loader only
    /// ever dirties (Load: if (blood) GotDirty()). So, playtest 2026-10-18 ("sponge not synced blood clean"):
    ///  - cleaned by the owner: every copy kept the blood for good;
    ///  - cleaned on a copy (a sponge, drotkefescript → washtriggerscript.Wash → GotClean): nothing went to the owner, and
    ///    its next resync put the blood back on the cleaner's screen.
    /// The game kills a driver who crashes with blood already on the car (carscript.DamageStuff: blood.ON &&
    /// isPlayerDriving → Death), so blood that one player had washed off but the car's owner still had was deadly: "he
    /// killed himself by getting in the car" came 6 minutes after the uncleaned wash, on that car.
    /// Now a copy follows the owner's record both ways, and a wash on a copy goes to the owner as an edit (KindBlood:
    /// the car record, of which the owner takes only "clean").
    internal static partial class EntityCarTuning
    {
        public static long BloodCleanedOnCopy, BloodCleanSent, BloodCleanApplied;

        /// The record says clean, this car is bloody: wash it (the game's own way, as a sponge would).
        internal static void BloodFollow(tosaveitemscript it, itemDataClass d)
        {
            if (it == null || it.car == null || d.car == null || d.car.Count == 0 || d.car[0] == null) return;
            var w = it.car.blood;
            if (w == null || !w.ON || d.car[0].blood || w.destroyOnRepair) return;
            w.GotClean();
            BloodCleanedOnCopy++;
        }

        /// Owner: an edit that washed our car's blood off.
        internal static void BloodEditApply(tosaveitemscript it, itemDataClass d)
        {
            if (it == null || it.car == null || it.car.blood == null || d.car == null || d.car.Count == 0 || d.car[0] == null) return;
            var w = it.car.blood;
            if (!w.ON || d.car[0].blood || w.destroyOnRepair) return;
            w.GotClean();
            BloodCleanApplied++;
        }

        [HarmonyLib.HarmonyPatch(typeof(washtriggerscript), nameof(washtriggerscript.GotClean))]
        private static class BloodWashed
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(washtriggerscript __instance)
            {
                if (!Entities.InSession || Entities.ApplyingState || __instance == null) return;
                var car = __instance.GetComponentInParent<carscript>();
                if (car == null || car.blood != __instance) return;
                var it = car.GetComponent<tosaveitemscript>();
                if (it == null) it = car.GetComponentInParent<tosaveitemscript>();
                if (it == null) return;
                if (Entities.IsProxy(it)) BloodCleanSent++;
                Entities.MarkFeatureEdit(it, kind: "blood");   // a copy: to the owner; our own car: its resync goes out now
            }
        }
    }
}
