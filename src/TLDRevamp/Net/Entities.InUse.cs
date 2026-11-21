using System;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        [Flags]
        private enum BusyKind : ushort
        {
            None = 0,
            Hitched = 1 << 0, CrashLease = 1 << 1, Held = 1 << 2, Stored = 1 << 3, Worn = 1 << 4,
            Driven = 1 << 5, Mounted = 1 << 6, GroupPart = 1 << 7, NonLooseGroupPart = 1 << 8,
            LockedInContainer = 1 << 9
        }

        // What stops an owner change (* = yes, + = checked separately in the path).
        //                     Hitch Crash Held Stored Worn Driven Mounted Part NonLoose Locked
        // Claim                 *     *    *     *     *     +      -      -     -       -
        // Crash claim           +     +    -     -     -     -      -      -     -       -
        // Hand-off              *     *    *     *     *     *      *      -     *       -
        // Nearest rabbit        -     *    *     *     -     -      -      *     -       *
        // Silent takeover       +     -    *     *     *     -      -      -     -       -
        // Player left           *     -    -     -     -     -      -      -     -       -
        // Cascade               +     -    *     *     -     -      -      -     -       -
        // Worn take-back        +     -    *     *     *     -      -      -     -       -
        // Parent attach, crash return and hitch completion have no busy veto.
        private const BusyKind ClaimBusy = BusyKind.Hitched | BusyKind.CrashLease | BusyKind.Held | BusyKind.Stored | BusyKind.Worn;
        private const BusyKind HandoffBusy = BusyKind.Hitched | BusyKind.CrashLease | BusyKind.NonLooseGroupPart | BusyKind.Mounted | BusyKind.Driven | BusyKind.Held | BusyKind.Stored | BusyKind.Worn;
        private const BusyKind RabbitBusy = BusyKind.GroupPart | BusyKind.CrashLease | BusyKind.Held | BusyKind.Stored | BusyKind.LockedInContainer;
        private const BusyKind CascadeBusy = BusyKind.Held | BusyKind.Stored;
        private const BusyKind SilentBusy = BusyKind.Held | BusyKind.Stored | BusyKind.Worn;
        private const BusyKind LeaveBusy = BusyKind.Hitched;
        private const BusyKind WornTakeBackBusy = BusyKind.Held | BusyKind.Stored | BusyKind.Worn;

        // First match wins. The hand-off counts mounted and carried skips, so keep its order here.
        // Only look for a mount or a loose split if the caller asks and nothing earlier stopped it.
        private static BusyKind Busy(SEnt se, BusyKind kinds, float? now = null)
        {
            if ((kinds & BusyKind.Hitched) != 0 && se.HitchCar != 0) return BusyKind.Hitched;   // follows the towing car
            if ((kinds & BusyKind.GroupPart) != 0 && se.PartIndex >= 0) return BusyKind.GroupPart;   // belongs to another object's group
            if ((kinds & BusyKind.CrashLease) != 0 && se.CrashReturnTo >= 0) return BusyKind.CrashLease;   // on loan for a crash
            if ((kinds & BusyKind.NonLooseGroupPart) != 0 && se.PartIndex >= 0 && !LooseSplit(se))
                return BusyKind.NonLooseGroupPart;   // goes with the group unless loose on its own
            if ((kinds & BusyKind.Mounted) != 0 && Mounted(se)) return BusyKind.Mounted;   // rides another shared item
            if ((kinds & BusyKind.Driven) != 0 && se.Driven && (now ?? Time.realtimeSinceStartup) - se.DrivenAt < 1f)
                return BusyKind.Driven;   // the owner reported a driver in the last second
            if ((kinds & BusyKind.Held) != 0 && se.Held) return BusyKind.Held;   // in the owner's hands
            if ((kinds & BusyKind.Stored) != 0 && se.Stored) return BusyKind.Stored;   // in the owner's inventory
            if ((kinds & BusyKind.Worn) != 0 && ServerWornNow(se)) return BusyKind.Worn;   // on the owner's head
            if ((kinds & BusyKind.LockedInContainer) != 0 && se.LockFrame != 0) return BusyKind.LockedInContainer;   // rides in a container
            return BusyKind.None;
        }
    }
}
