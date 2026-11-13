using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.PlayerSave;

namespace TLDRevamp.Net
{
    internal static class PlayerSaveTools
    {
        public static string TestRestoreDrop = "";   // test: "all" = this joiner ignores a Restore, a number = it can't spawn that many of the items
    }
}
