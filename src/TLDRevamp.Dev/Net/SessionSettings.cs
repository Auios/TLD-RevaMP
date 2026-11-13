using System.Linq;
using System;
using UnityEngine;
using static TLDRevamp.Net.DeveloperAccess;

namespace TLDRevamp.Net
{
    internal static class DeveloperAccessTools
    {

        internal static bool DenyBridge()
        {
            DeniedBridgeCommands++;
            return false;
        }
    }
}
