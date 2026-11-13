using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using static TLDRevamp.Net.WorldCheck;

namespace TLDRevamp.Net
{
    internal static class WorldCheckTools
    {
        /// Tests (bridge `set`): this machine claims these game data instead of its own, to see a join refused.
        public static string TestGameData = default;
    }
}
