using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using static TLDRevamp.Net.Mp;

namespace TLDRevamp.Net
{
    internal static class MpTools
    {
        /// Test only (savetail.py): treat the player's Autosave setting as off — the real setting is never touched (the
        /// game writes settings.tldc when its menu closes: a test's "off" once reached the player's real settings).
        public static bool TestAutosaveOff = default;
    }
}
