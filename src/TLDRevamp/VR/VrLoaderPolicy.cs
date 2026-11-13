using System;
using System.Collections.Generic;

namespace TLDRevamp.VR
{
    /// Which loader a session may use, and what has to happen before it is initialized.
    ///
    /// This is the pure part of the OpenXR boundary: the decisions are made from the names and the kinds of the
    /// loaders the XR manager holds, never from the manager itself, so the whole selection rule can be exercised
    /// offline. The runtime part (src/TLDRevamp/Fixes/OpenXrBridge.cs) only does what this policy says.
    ///
    /// Why a policy at all: the shipped game lists two OpenXR loaders in its XR General Settings asset
    /// (`OpenXRLoader` and `OpenXRLoaderNoPreInit`) and the manager keeps them in a list whose order is not a
    /// contract. Picking "the first OpenXR loader in the list" would make the choice depend on that order, so the
    /// two are ranked explicitly here: the plain loader first, the no-pre-init one only when it is the only one.
    public enum VrLoaderKind
    {
        None,           // no loader at all
        OpenXr,         // UnityEngine.XR.OpenXR.OpenXRLoader
        OpenXrNoPreInit // UnityEngine.XR.OpenXR.OpenXRLoaderNoPreInit
    }

    /// The loader a session may use, and why.
    public struct VrLoaderChoice
    {
        public VrLoaderKind Kind;
        public string Name;        // the loader's type name, for the diagnostics
        public int Index;          // its place in the manager's loader list
        public bool Usable;
        public string Reason;      // why it is not usable, or "" when it is

        public override string ToString() =>
            Kind + (Usable ? " ok" : " refused: " + Reason) + " (" + Name + ")";
    }

    /// The rules of the loader's life, as pure functions.
    public static class VrLoaderPolicy
    {
        /// The type name of the loader the game should use. The plain OpenXR loader: it performs the runtime's
        /// pre-init handshake, which is what a headset that is streamed to the machine (a Quest over WiVRn) needs.
        public const string OpenXrName = "UnityEngine.XR.OpenXR.OpenXRLoader";
        /// The loader that skips the pre-init handshake. It is a deliberate fallback, never a first choice.
        public const string OpenXrNoPreInitName = "UnityEngine.XR.OpenXR.OpenXRLoaderNoPreInit";

        /// The kind of a loader, from its type name. Unknown names are `None`: the session refuses them rather than
        /// guessing, because a wrong loader would start a runtime the game does not expect.
        public static VrLoaderKind KindOf(string typeName)
        {
            if (typeName == OpenXrName) return VrLoaderKind.OpenXr;
            if (typeName == OpenXrNoPreInitName) return VrLoaderKind.OpenXrNoPreInit;
            return VrLoaderKind.None;
        }

        /// The loader to use, out of the manager's list, in a fixed order.
        ///
        /// The order is the policy, not the list's: the plain OpenXR loader wins over the no-pre-init one, whatever
        /// order the manager keeps them in. The no-pre-init loader is used only when it is the only OpenXR loader
        /// there is. `allowNoPreInit` says whether that fallback is allowed at all.
        public static VrLoaderChoice Choose(IList<string> loaderTypeNames, bool allowNoPreInit)
        {
            var none = new VrLoaderChoice { Kind = VrLoaderKind.None, Index = -1, Usable = false, Reason = "no OpenXR loader in the XR manager" };
            if (loaderTypeNames == null || loaderTypeNames.Count == 0) return none;

            int plain = -1, noPreInit = -1;
            for (int i = 0; i < loaderTypeNames.Count; i++)
            {
                var kind = KindOf(loaderTypeNames[i]);
                if (kind == VrLoaderKind.OpenXr && plain < 0) plain = i;
                else if (kind == VrLoaderKind.OpenXrNoPreInit && noPreInit < 0) noPreInit = i;
            }
            if (plain >= 0)
                return new VrLoaderChoice { Kind = VrLoaderKind.OpenXr, Name = OpenXrName, Index = plain, Usable = true, Reason = "" };
            if (noPreInit >= 0)
            {
                if (!allowNoPreInit)
                    return new VrLoaderChoice { Kind = VrLoaderKind.OpenXrNoPreInit, Name = OpenXrNoPreInitName, Index = noPreInit, Usable = false,
                        Reason = "only the no-pre-init loader is there, and this install does not allow it" };
                return new VrLoaderChoice { Kind = VrLoaderKind.OpenXrNoPreInit, Name = OpenXrNoPreInitName, Index = noPreInit, Usable = true, Reason = "" };
            }
            return none;
        }

        /// The controller interaction profiles to switch on, in the order they are configured.
        ///
        /// The order is fixed so that the configuration is reproducible: the same set is enabled in the same order
        /// on every run, whatever order the settings asset lists them in. The names are the type names of the
        /// profiles the game ships (its `globalgamemanagers.assets` holds all seven).
        public static readonly string[] ControllerProfiles =
        {
            "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchProControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.ValveIndexControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.HTCViveControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.HPReverbG2ControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.MicrosoftMotionControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.KHRSimpleControllerProfile",
        };

        /// Whether a profile is one of the controller profiles this policy knows how to switch on.
        public static bool IsControllerProfile(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            for (int i = 0; i < ControllerProfiles.Length; i++)
                if (ControllerProfiles[i] == typeName) return true;
            return false;
        }

        /// The order of the steps of a start. The profiles are configured BEFORE the loader is initialized: the
        /// OpenXR loader reads the enabled features when it initializes, so a profile switched on afterwards is
        /// never used for that session.
        public enum VrStartStep
        {
            ChooseLoader,      // pick the loader, by the policy and not by the list's order
            ConfigureFeatures, // switch the controller profiles on
            Initialize,        // loader.Initialize()
            StartSubsystems,   // manager.StartSubsystems()
            Verify             // the display and the input really run
        }

        /// The steps of a start, in order. Pure: the runtime does them in this order and stops at the first failure.
        public static VrStartStep[] StartSteps()
        {
            return new[]
            {
                VrStartStep.ChooseLoader,
                VrStartStep.ConfigureFeatures,
                VrStartStep.Initialize,
                VrStartStep.StartSubsystems,
                VrStartStep.Verify,
            };
        }

        /// The order of the steps of a stop. The subsystems are stopped before the loader is deinitialized, which is
        /// the order the XR manager itself uses (its DeinitializeLoader calls StopSubsystems first).
        public enum VrStopStep
        {
            StopSubsystems,     // manager.StopSubsystems()
            DeinitializeLoader, // manager.DeinitializeLoader()
            Verify
        }

        public static VrStopStep[] StopSteps()
        {
            return new[] { VrStopStep.StopSubsystems, VrStopStep.DeinitializeLoader, VrStopStep.Verify };
        }
    }

    /// Who owns the recenter, and who owns the tracking origin.
    ///
    /// The game already recenters: `mainScript2.LateUpdate` reads `inputscript.vrRecenterDown` and calls
    /// `mainScript2.VRRecenter()`, which moves the camera rig (`CamParent`) onto the head-lean transform. That is the
    /// rig's recenter, and it is the one the player sees.
    ///
    /// The runtime's recenter (`XRInputSubsystem.TryRecenter`) moves the runtime's tracking origin: a different thing,
    /// in a different place, and it is not needed for the game to feel right. Doing both on one key press moves the
    /// rig once and the world origin once, so the two disagree and the disagreement compounds on the next press.
    ///
    /// So the policy is: the game owns the rig recenter, the runtime owns nothing unless it is asked explicitly.
    public enum VrRecenterOwner
    {
        None,       // nobody: no recenter happened
        GameRig,    // the game's own rig recenter (mainScript2.VRRecenter)
        Runtime,    // the runtime's tracking-origin recenter (XRInputSubsystem.TryRecenter)
    }

    /// The tracking origin the session uses. The game's rig recenter moves the camera rig, so the runtime's origin
    /// has to stay where it is: changing it under the rig is what makes the two disagree.
    public enum VrTrackingOrigin
    {
        Unchanged,  // the runtime's origin is left alone (the default, and the game's own behaviour)
        Device,     // the runtime's origin is set to the device's
        Floor,      // the runtime's origin is set to the floor
    }

    /// The rules of the recenter and the tracking origin, as pure functions.
    public static class VrTrackingPolicy
    {
        /// Who may recenter. The game's own rig recenter always wins: it is the one the player sees and the one the
        /// game already performs on its own.
        public static VrRecenterOwner RecenterOwner(bool gameOwnsRigRecenter, bool runtimeRecenterRequested)
        {
            if (gameOwnsRigRecenter) return VrRecenterOwner.GameRig;
            if (runtimeRecenterRequested) return VrRecenterOwner.Runtime;
            return VrRecenterOwner.None;
        }

        /// The tracking origin a session may set. The game's rig recenter moves the camera rig, so the runtime's
        /// origin stays as it is: only an explicit request may change it, and only to a mode that is supported.
        public static VrTrackingOrigin OriginFor(bool explicitRequest, bool gameOwnsRigRecenter, bool deviceSupported, bool floorSupported)
        {
            if (!explicitRequest) return VrTrackingOrigin.Unchanged;
            if (gameOwnsRigRecenter) return VrTrackingOrigin.Unchanged;   // the rig already recenters: do not move the world too
            if (deviceSupported) return VrTrackingOrigin.Device;
            if (floorSupported) return VrTrackingOrigin.Floor;
            return VrTrackingOrigin.Unchanged;
        }
    }

}
