using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TLDRevamp.VR;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using static TLDRevamp.Fixes.OpenXrBridge;

namespace TLDRevamp.Fixes
{
    internal static class OpenXrBridgeTools
    {

        /// The bridge's entry point: `openxr [state|start|retry|stop|recenter]`.
        ///
        /// `state` is a pure read (the state), the rest change the session. Which is which is decided by
        /// VrBridgeCommandPolicy (the pure core), so the debug bridge's read/mutation split and this bridge's
        /// dispatch are the same rule and cannot drift apart.
        internal static string Command(string arg)
        {
            var what = (arg ?? "").Trim().ToLowerInvariant();
            if (what.Length == 0 || what == "state") return State();
            if (what == "start") return Enable();
            if (what == "retry") return Retry();
            if (what == "stop") return Disable();
            if (what == "recenter") { Recenter(false); return State(); }
            // `recenter origin`: the runtime's tracking origin as well. Explicit, because the game's own rig
            // recenter moves the camera rig and the two have to stay in step.
            //
            // The request is for THIS recenter only. It used to be a sticky flag, so one `recenter origin`
            // made every LATER plain recenter move the origin as well: the world kept moving under the rig
            // and the two drifted apart again, which is the exact failure the one-owner rule exists to stop.
            if (what == "recenter origin") { Recenter(true); return State(); }
            // The usage line is the policy's, so the help and the bridge can never disagree about it.
            return "{\"error\":\"usage: " + VrBridgeCommandPolicy.Syntax + "\"}";
        }
    }
}
