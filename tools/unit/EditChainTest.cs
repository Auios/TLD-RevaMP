using System;
using TLDRevamp.Net;

// Replayed edits (Net/EditChain.cs, SharedType.ReplayEdits): which edit a copy applies, when the owner sends the whole
// record again, what the host keeps for later copies, and the record hash both sides compare.
static class EditChainTest
{
    static int checks, failed;
    static void Check(string what, bool ok) { checks++; if (!ok) { failed++; Console.WriteLine("FAIL " + what); } }

    static int Main()
    {
        // a copy at version 5
        Check("the next version applies", EditChain.Next(5, 6) == EditChain.Step.Apply);
        Check("one it has is skipped", EditChain.Next(5, 5) == EditChain.Step.Old && EditChain.Next(5, 2) == EditChain.Step.Old);
        Check("a gap waits for the whole record", EditChain.Next(5, 7) == EditChain.Step.Gap && EditChain.Next(0, 9) == EditChain.Step.Gap);
        Check("a fresh copy (version 0) takes edit 1", EditChain.Next(0, 1) == EditChain.Step.Apply);
        Check("the version wraps", EditChain.Next(uint.MaxValue, 0) == EditChain.Step.Apply);

        // the owner: a typed word into a small record, then the whole record
        int edits = 0, bytes = 0;
        while (EditChain.Forward(edits, bytes, 15, 158)) { edits++; bytes += 15; }
        Check("a small record takes a typed word before it goes again (" + edits + " edits)", edits >= 20 && edits <= EditChain.MaxChainEdits);
        Check("never more edits than MaxChainEdits", !EditChain.Forward(EditChain.MaxChainEdits, 0, 1, 1 << 20));
        Check("a large record takes up to MaxChainEdits small edits", EditChain.Forward(EditChain.MaxChainEdits - 1, 63 * 15, 15, 28000));
        Check("an edit as large as the record goes as the record", !EditChain.Forward(0, 0, 4000, 3000));

        // whatever the owner forwards, the host keeps all of it (a new owner and later copies rebuild from it)
        int ce = 0, cb = 0, kept = 0, kb = 0; bool all = true;
        while (EditChain.Forward(ce, cb, 1000, 1 << 20)) { ce++; cb += 1000; all &= EditChain.Keep(kept, kb, 1000 + EditChain.KeptHeaderBytes); kept++; kb += 1000 + EditChain.KeptHeaderBytes; }
        Check("the host keeps every edit the owner forwards (" + ce + " edits of 1000 B on a 1 MB record)", all && ce > 0 && cb <= EditChain.MaxChainBytes);

        // the host's kept edits for later copies
        Check("the host keeps edits under its caps", EditChain.Keep(0, 0, 100) && EditChain.Keep(EditChain.MaxKeptEdits - 1, 0, 100));
        Check("past the count or the bytes it keeps none", !EditChain.Keep(EditChain.MaxKeptEdits, 0, 1) && !EditChain.Keep(1, EditChain.MaxKeptBytes - 10, 11));

        // the hash
        var a = new byte[] { 1, 2, 3, 4 }; var b = new byte[] { 1, 2, 3, 5 };
        Check("the same record gives the same hash", EditChain.Hash(a, 4) == EditChain.Hash((byte[])a.Clone(), 4));
        Check("one byte apart gives another", EditChain.Hash(a, 4) != EditChain.Hash(b, 4));
        Check("only the first n bytes count", EditChain.Hash(a, 3) == EditChain.Hash(b, 3));
        Check("FNV-1a 64 of nothing is its offset basis", EditChain.Hash(Array.Empty<byte>(), 0) == 14695981039346656037UL);

        Console.WriteLine($"EditChainTest: {checks - failed}/{checks} passed");
        return failed == 0 ? 0 : 1;
    }
}
