using System;

namespace TLDRevamp.Cinematic
{
    /// How the bridge treats the cinematic camera's commands.
    ///
    /// The bridge has one rule that decides everything: a command that changes nothing is a *query* and may be
    /// run by anyone; a command that changes something is a *mutation* and may only be run when the host's
    /// developer policy allows it. Getting this wrong in either direction is bad: a mutation classified as a
    /// query would let any joiner move the camera on a machine it does not own, and a query classified as a
    /// mutation would make the camera's status unaskable for a joiner (the test tooling needs it).
    ///
    /// The camera's status is the only query. Everything else — switching modes, changing the speed, the field of
    /// view, the orbit distance, loading a shot list — changes what this machine shows.
    public static class CinematicCommandPolicy
    {
        /// Whether the command only reads. `arg` is everything after the command name.
        ///
        /// Only the status is a query. A screenshot is a mutation even though it changes nothing on screen: it
        /// writes a file, and a file is something a machine should only write when its owner says so.
        public static bool IsReadOnly(string arg)
        {
            if (arg == null) return true;
            arg = arg.Trim();
            if (arg.Length == 0) return true;                       // `cinema` on its own: the status
            if (arg == "status") return true;
            if (arg.StartsWith("status ", StringComparison.Ordinal)) return true;
            return false;
        }

        /// Whether the command would be allowed, given the host's policy. The host's own policy is not re-implemented
        /// here: the caller passes in what the bridge would allow.
        /// <param name="readOnly">whether the command is a query.</param>
        /// <param name="developerAccessAllowed">whether the host's policy lets this machine run mutations.</param>
        public static bool Allowed(bool readOnly, bool developerAccessAllowed) => readOnly || developerAccessAllowed;
    }
}
