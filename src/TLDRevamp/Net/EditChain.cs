using System;

namespace TLDRevamp.Net
{
    /// Replayed edits (SharedType.ReplayEdits, ModObjects): the rules the owner, the host and every copy follow, kept
    /// apart from Unity so tools/unit/EditChainTest can check them.
    ///  - The owner sends an accepted edit itself (ModEdited) with the record's next version and a hash of the record it
    ///    gave, while the edits since its last whole record stay small beside that record (Forward). Past that, or with
    ///    a change of its own not sent yet, it sends the whole record again (ModRecord, also a version).
    ///  - A copy applies the version after its own (Next: Apply), skips one it has (Old), and on a gap, a refusal or
    ///    another hash waits for the next whole record and asks for it (ModResync).
    ///  - The host keeps the edits since the last whole record (Keep) for players who get the object later.
    internal static class EditChain
    {
        internal enum Step { Apply, Old, Gap }

        /// A copy at version `have` gets an edit that makes version `seq`.
        internal static Step Next(uint have, uint seq) => seq == unchecked(have + 1) ? Step.Apply : seq <= have ? Step.Old : Step.Gap;

        /// The edits since the last whole record the owner forwards before it sends the whole record again: at most
        /// MaxChainEdits, and no more bytes than the record itself (at least MinChainBytes, so a small record still
        /// takes a typed word; at most MaxChainBytes, so the host can always keep them all: a new owner and a later copy
        /// rebuild from what it keeps).
        internal const int MaxChainEdits = 64, MinChainBytes = 1024, MaxChainBytes = 32 * 1024;
        /// What the host adds to each kept edit at most (its ModEdited header).
        internal const int KeptHeaderBytes = 40;
        internal static bool Forward(int chainEdits, int chainBytes, int editBytes, int recordBytes) =>
            chainEdits < MaxChainEdits && chainBytes + editBytes <= Math.Min(MaxChainBytes, Math.Max(MinChainBytes, recordBytes));

        /// What the host keeps for later copies: past this it keeps none and asks the owner for a whole record.
        internal const int MaxKeptEdits = 128, MaxKeptBytes = 64 * 1024;
        internal static bool Keep(int keptEdits, int keptBytes, int msgBytes) => keptEdits < MaxKeptEdits && keptBytes + msgBytes <= MaxKeptBytes;

        /// FNV-1a, 64 bits, of a record's bytes: the owner's and a copy's result compared.
        internal static ulong Hash(byte[] b, int n)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < n; i++) { h ^= b[i]; h *= 1099511628211UL; }
            return h;
        }
    }
}
