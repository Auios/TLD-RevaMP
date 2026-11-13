using System;
using System.Collections.Generic;

namespace TLDRevamp.VR
{
    /// The XR bridge's commands, and which of them read and which of them change the session.
    ///
    /// The rule lives in the pure core so it can be exercised offline and so the bridge and the debug bridge
    /// cannot drift apart: both ask this policy, and neither re-decides for itself.
    ///
    /// The split is the same one the whole bridge uses: a QUERY only reads the session's state, so it is safe
    /// for any peer and needs no developer access; a MUTATION changes the session (it starts, stops, retries or
    /// recenters it), so it goes through the session's developer-access policy like every other mutation.
    ///
    /// The documented query verbs are `state` and the bare command (no argument). Everything else that the
    /// bridge understands is a mutation. An argument the bridge does not know is NOT a read: it is refused, and
    /// refusing it must not need developer access, so the unknown form is reported as neither.
    public enum VrBridgeVerb
    {
        Unknown,     // the bridge does not know this form
        Query,       // a pure read
        Mutation     // it changes the session
    }

    /// The rules of the XR bridge's command surface, as pure functions.
    public static class VrBridgeCommandPolicy
    {
        /// The verbs that only read the session's state.
        public static readonly string[] QueryVerbs = { "state" };

        /// The verbs that change the session. `start`, `retry` and `stop` act on the session's life; `recenter`
        /// moves the rig (and, when it is asked for, the runtime's tracking origin), which changes what the
        /// player sees, so it is a mutation too.
        public static readonly string[] MutationVerbs = { "start", "retry", "stop", "recenter" };

        /// The help line's syntax for the command, exactly as the bridge's help prints it. The help and the
        /// policy are pinned together: a second syntax for the same command in the help is a wrong syntax for
        /// the reader, so the help prints this one form and only this one.
        public const string Syntax = "openxr [state|start|retry|stop|recenter [origin]]";

        /// What a command form does: a query, a mutation, or nothing the bridge knows.
        ///
        /// The empty argument is the bare `openxr`, which reads the state. `state` reads it too. The mutation
        /// verbs change the session. `recenter origin` is a mutation: it moves the runtime's tracking origin.
        public static VrBridgeVerb Classify(string arg)
        {
            var what = (arg ?? "").Trim().ToLowerInvariant();
            if (what.Length == 0) return VrBridgeVerb.Query;
            // "recenter origin": the runtime's tracking origin as well. Still a mutation.
            if (what == "recenter origin") return VrBridgeVerb.Mutation;
            for (int i = 0; i < MutationVerbs.Length; i++)
                if (what == MutationVerbs[i]) return VrBridgeVerb.Mutation;
            for (int i = 0; i < QueryVerbs.Length; i++)
                if (what == QueryVerbs[i]) return VrBridgeVerb.Query;
            return VrBridgeVerb.Unknown;
        }

        /// Whether a command form only reads. A form the bridge does not know is not a read: it is refused, and
        /// the refusal is not something that needs developer access.
        public static bool IsQuery(string arg) { return Classify(arg) == VrBridgeVerb.Query; }

        /// Whether a command form changes the session.
        public static bool IsMutation(string arg) { return Classify(arg) == VrBridgeVerb.Mutation; }
    }
}
