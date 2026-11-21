using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private static readonly List<string> Rows = new();
        private static SEnt Fresh(int crash = -1)
        {
            Server.Clear(); Known.Clear(); Trace.Clear(); SessionLog.History.Clear(); _reconfirmedAt.Clear(); _refuseLogged.Clear(); _hitchDeparted.Clear(); _hitchTransfers.Clear(); _hitchOps.Clear();
            Handoffs = HandoffLogged = OwnerCascaded = SilentTakeovers = ClaimsRefused = RefusedHeld = RefusedCrash = RefusedStored = RefusedDriven = 0;
            EntityRabbits.RabbitOwnerMoves = EntityHands.WornTakenBack = EntityPhysics.CrashGranted = EntityPhysics.CrashLeasesKept = OwnerReconfirmed = 0;
            Time.realtimeSinceStartup = 100; _handAcc = 0;
            var se = new SEnt { NetId = 7, OwnerId = 1, Epoch = 12, PrevOwnerId = 2, PrevOwnerAt = 99, CrashReturnTo = crash, CrashUntil = 80, HasState = true, Speed = 3, RagdollKeyState = new byte[] { 1 }, RagdollKeyEpoch = 12 };
            Server[7] = se;
            return se;
        }
        private static NetReader Packet(NetWriter w, int pos = 0) => new() { Buf = w.Buf[..w.Len], Pos = pos, End = w.Len };
        private static NetReader ClaimPacket(uint net = 7) { var w = new NetWriter(); w.U32(net); w.U8((byte)ClaimReason.Driver); return Packet(w); }
        private static string Fields(SEnt e) => $"{e.NetId}:{e.OwnerId}@{e.Epoch}:prev={e.PrevOwnerId},{e.PrevOwnerAt},{e.HandoverOpen}:crash={e.CrashReturnTo},{e.CrashUntil}:claim={e.ClaimedBy},{e.ClaimedAt},{e.ClaimedFromEpoch}:rabbit={e.RabbitHoldUntil}:rag={e.RagdollKeyState == null},{e.RagdollKeyEpoch}";
        private static void Row(string name)
        {
            Rows.Add(name + "|" + string.Join("/", Server.Values.Select(Fields)) + "|" + string.Join("/", Trace) + $"|counts:{Handoffs},{HandoffLogged},{OwnerCascaded},{SilentTakeovers},{EntityRabbits.RabbitOwnerMoves},{EntityHands.WornTakenBack},{EntityPhysics.CrashGranted},{EntityPhysics.CrashLeasesKept},{OwnerReconfirmed},{ClaimsRefused}");
#if !OWNER_BASELINE
            if (char.IsDigit(name[0]))
            {
                int path = int.Parse(name.Split('-')[0]);
                var changed = Server[path == 6 ? 8u : 7u];
                Require(changed.OwnerHistoryCount == 1 && changed.OwnerHistory[0].Why == (OwnerChange)path && changed.OwnerHistory[0].Epoch == 13,
                    "real path records its reason and new epoch: " + name);
                if (path == 1)
                    Require(OwnerHistoryJson(7) == "{\"net\":7,\"owner\":2,\"epoch\":13,\"count\":1,\"history\":\"0.000-1>2@13:Claim\"}", "bridge JSON example uses the real core formatter");
            }
#endif
        }
        internal static void CheckRows()
        {
            var se = Fresh(); se.Rabbit = true; ServerClaim(2, ClaimPacket()); Row("1-claim-rabbit");
            se = Fresh(3); WornTakesBack(se, 2, 1); Row("2-worn-keeps-crash");
            se = Fresh(); se.Rabbit = true; float elapsed = 0; ChooseRabbitOwner(1, ref elapsed, new()); Row("3-rabbit-nearest");
            se = Fresh(); ServerHandoffTick(1); Row("4-handoff");
            se = Fresh(3); Server[8] = new SEnt { NetId = 8, OwnerId = 2, Epoch = 3 };
            var w = new NetWriter(); w.U8(AttachSync); w.U32(7); w.U8(1); w.U8(0); w.VarU32(1); w.U32(8); w.VarU32(0); w.VarU32(0);
            for (int i = 0; i < 9; i++) w.F64(0); w.U8(0);
            ServerAttach(1, Packet(w, 1)); Row("5-attach-keeps-crash");
            se = Fresh(); se.OwnerId = 2; se.AttachKids = new() { 8 }; Server[8] = new SEnt { NetId = 8, OwnerId = 1, Epoch = 12, AttachParent = 7, CrashReturnTo = 3 }; CascadeOwner(se); Row("6-cascade-clears-crash");
            se = Fresh(); ServerCrashClaim(2, 7, 0); Row("7-crash-claim");
            se = Fresh(2); ReturnCrash(se); Row("8-crash-return");
            se = Fresh(3); ServerTakeSilent(1); Row("9-silent-clears-crash");
            se = Fresh(3); ServerOnLeave(1); Row("10-left-keeps-crash");
            se = Fresh(3); se.HitchCar = 8; Server[8] = new SEnt { NetId = 8, OwnerId = 2, Epoch = 3 }; CompleteHitchTransfer(se, true, new(1, 2, 3), Quaternion.identity, default, true, default, Quaternion.identity); Row("11-hitch-transfer");
            se = Fresh(); se.Held = true; ServerClaim(2, ClaimPacket()); Row("refused-held");
            se = Fresh(); ServerClaim(1, ClaimPacket()); ServerClaim(1, ClaimPacket()); Row("reconfirmed-once");
            string path = "tools/unit/EntityOwnerChangeRows.txt";
            if (Environment.GetEnvironmentVariable("OWNER_CAPTURE") == "1")
            {
#if OWNER_BASELINE
                File.WriteAllLines(path, Rows);
#else
                throw new Exception("capture requires immutable baseline mode");
#endif
            }
            var expected = File.ReadAllLines(path);
            if (!Rows.SequenceEqual(expected))
            {
                File.WriteAllLines("tools/unit/obj/EntityOwnerChange/actual-rows.txt", Rows);
                throw new Exception("owner change rows differ: " + string.Join(",", Rows.Where((r, i) => i >= expected.Length || r != expected[i]).Select(r => r.Split('|')[0])));
            }
            Console.WriteLine(Rows.Count + " owner path rows match HEAD");
        }

        private static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        internal static void CheckHistory()
        {
            _ownerHistoryEvents = 0;
            var se = Fresh();
            Require(se.OwnerHistory == null && OwnerHistoryJson(7).Contains("\"count\":0"), "history is lazy and empty JSON is readable");
            Require(OwnerHistoryJson(99) == "{\"net\":99,\"error\":\"no such host object\"}", "unknown host net JSON");
            for (int i = 0; i < 10; i++)
            {
                Time.realtimeSinceStartup = 100 + i;
                ServerSetOwner(se, i % 2 + 1, OwnerChange.Claim, CrashReturnRule.Keep, W, "fixture");
            }
            Require(se.OwnerHistoryCount == 8 && se.OwnerHistoryNext == 2, "ring wraps at eight");
            var result = OwnerHistoryResult(se, 110);
            Require(result == string.Join(";", Enumerable.Range(2, 8).Select(i => $"{i - 10:F3}-{(i - 1) % 2 + 1}>{i % 2 + 1}@{13 + i}:Claim")), "history is oldest first with relative times, owners, new epochs and reasons");
            var savedCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Require(OwnerHistoryResult(se, 110) == result, "history uses invariant decimal points");
            CultureInfo.CurrentCulture = savedCulture;
            Require(OwnerHistoryJson(7).Contains("\"count\":8") && OwnerHistoryJson(7).Contains("@22:Claim"), "on-request JSON reads the ring");
            SessionLog.History.Clear(); SessionLog.HistoryRows.Clear(); _ownerHistoryEvents = 0; se.OwnerHistoryLogged = false;
            Time.realtimeSinceStartup = 110; LogOwnerHistory(se);
            Require(SessionLog.History.Count == 1 && SessionLog.History[0] == $"event:owner-history:7:0:-1:{se.OwnerId}:{se.Epoch}:8:now", "a dump starts with the current owner, epoch and how many changes follow");
            Require(SessionLog.HistoryRows.SequenceEqual(Enumerable.Range(2, 8).Select(i => $"event:owner-history:7:0:{(i - 1) % 2 + 1}:{i % 2 + 1}:{13 + i}:{(int)OwnerChange.Claim}:Claim:{i - 10:F3}")),
                "then one row per change, oldest first, in numeric fields (from, to, epoch, reason, seconds ago)");
            SessionLog.History.Clear(); SessionLog.HistoryRows.Clear();
            Require(Enum.GetValues(typeof(OwnerChange)).Cast<OwnerChange>().All(c => OwnerChangeNames[(int)c] == c.ToString()), "reason names match the enum");
            string privacy = File.ReadAllText("src/TLDRevamp/TelemetryPrivacy.cs");
            Require(privacy.Contains(" owner-history ") || privacy.Contains(" owner-history\"") , "owner-history is an allowed event kind (else marks show [removed])");
            Require(OwnerChangeNames.Skip(1).Append("now").All(n => privacy.Contains("\"" + n + "\"")), "every owner-history result is an allowed result word");
            var buffer = se.OwnerHistory;
            RecordTrace = false;
            for (int i = 0; i < 30; i++) ServerSetOwner(se, i % 2 + 1, OwnerChange.Cascade, CrashReturnRule.Clear, W, "fixture");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) ServerSetOwner(se, i % 2 + 1, OwnerChange.Cascade, CrashReturnRule.Clear, W, "fixture");
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            RecordTrace = true;
            Require(ReferenceEquals(buffer, se.OwnerHistory) && allocated == 0, "changes reuse the ring and allocate no memory after its first allocation");

            se = Fresh();
            DuringDeliver = () =>
            {
                DuringDeliver = null;
                ServerSetOwner(se, 0, OwnerChange.PlayerSilent, CrashReturnRule.Clear, W, "fixture reentry");
            };
            ServerSetOwner(se, 2, OwnerChange.Claim, CrashReturnRule.Keep, W, "fixture");
            Require(se.OwnerHistoryCount == 2 && se.OwnerHistory[0].Epoch == 13 && se.OwnerHistory[1].Epoch == 14 &&
                se.OwnerHistory[0].To == 2 && se.OwnerHistory[1].From == 2, "synchronous local delivery preserves chronological history");
            Require(Trace[1].StartsWith("peer:1:True:1907000000020D000000") && Trace[2].StartsWith("peer:2:True:1907000000020D000000") && Trace[3].StartsWith("local:0:1907000000020D000000"),
                "both peers get the old grant before the local handler reuses its writer for the next grant");

            _ownerHistoryEvents = 0; se = Fresh(); se.Held = true;
            ServerClaim(2, ClaimPacket());
            Require(SessionLog.History.Count == 1 && _ownerHistoryEvents == 1, "refused claim logs history even before the first change");
            Time.realtimeSinceStartup = 109.999f; ServerClaim(2, ClaimPacket());
            Require(SessionLog.History.Count == 1, "refusals share the ten second window");
            Time.realtimeSinceStartup = 110; ServerClaim(1, ClaimPacket());
            Require(SessionLog.History.Count == 2 && OwnerReconfirmed == 1, "same-owner claim logs at the ten second boundary");
            Time.realtimeSinceStartup = 111; ServerClaim(1, ClaimPacket());
            Require(SessionLog.History.Count == 2 && OwnerReconfirmed == 1, "history leaves the independent two second reconfirmation limit alone");
            Time.realtimeSinceStartup = 120; ServerClaim(1, ClaimPacket());
            Require(SessionLog.History.Count == 3 && OwnerReconfirmed == 2, "reconfirmed claim logs history again when due");
            se.OwnerId = 0; Time.realtimeSinceStartup = 130; ServerClaim(0, ClaimPacket());
            Require(SessionLog.History.Count == 4 && OwnerReconfirmed == 2, "host's same-owner branch also dumps history without sending a reconfirmation");

            _ownerHistoryEvents = 0; Fresh();
            for (uint n = 1; n <= 201; n++)
            {
                var e = new SEnt { NetId = n, OwnerId = 1, Epoch = 1, Held = true };
                Server[n] = e; ServerClaim(2, ClaimPacket(n));
            }
            Require(SessionLog.History.Count == 200 && _ownerHistoryEvents == 200, "two hundred history events per session across nets");
            Time.realtimeSinceStartup += 20; ServerClaim(2, ClaimPacket(1));
            Require(SessionLog.History.Count == 200, "session cap also applies to later dumps of an existing net");
            // Reset clears the server map and budget; a reused net gets a fresh SEnt with no ring or throttle state.
            Server.Clear(); _ownerHistoryEvents = 0;
            se = Fresh(); se.Held = true; ServerClaim(2, ClaimPacket());
            Require(SessionLog.History.Count == 1 && se.OwnerHistory == null, "new session starts with an empty ring and fresh budget");
            Console.WriteLine("owner history wrap, order, allocation, triggers and caps PASS");
        }

        internal static void CheckOwnerSources()
        {
            var net = "src/TLDRevamp/Net";
            var writes = new List<string>();
            foreach (var file in Directory.GetFiles(net, "Entities*.cs").Concat(new[] { net + "/TelemetryMarks.cs" }).OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                if (Path.GetFileName(file) == "Entities.OwnerChange.cs") continue;
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                var lines = new SortedSet<int>();
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    ExpressionSyntax target = node switch
                    {
                        AssignmentExpressionSyntax a => a.Left,
                        PrefixUnaryExpressionSyntax p when p.IsKind(SyntaxKind.PreIncrementExpression) || p.IsKind(SyntaxKind.PreDecrementExpression) => p.Operand,
                        PostfixUnaryExpressionSyntax p when p.IsKind(SyntaxKind.PostIncrementExpression) || p.IsKind(SyntaxKind.PostDecrementExpression) => p.Operand,
                        _ => null
                    };
                    string name = target switch { MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText, IdentifierNameSyntax i => i.Identifier.ValueText, _ => "" };
                    if (name == "OwnerId" || name == "Epoch") lines.Add(target.GetLocation().GetLineSpan().StartLinePosition.Line);
                }
                var sourceLines = File.ReadAllLines(file);
                foreach (int line in lines) writes.Add(Path.GetFileName(file) + "|" + sourceLines[line].Split(new[] { "//" }, StringSplitOptions.None)[0].Trim());
            }
            var expected = File.ReadAllLines("tools/unit/EntityOwnerChangeWrites.txt");
            Require(writes.SequenceEqual(expected), "owner writes outside the kernel changed:\n" + string.Join("\n", writes.Except(expected).Concat(expected.Except(writes))));
            Require(writes.Count(r => r.Contains("new SEnt")) == 3, "only three SEnt creation initializers remain outside the owner function");
            string owner = File.ReadAllText(net + "/Entities.OwnerChange.cs");
            Require(Regex.Matches(owner, @"se\.OwnerId\s*=(?!=)").Count == 1 && Regex.Matches(owner, @"se\.Epoch\+\+").Count == 1,
                "one owner assignment and one epoch increment in the kernel");
            string reset = File.ReadAllText(net + "/Entities.cs");
            Require(reset.Contains("Server.Clear(); _ownerHistoryEvents = 0;"), "real session reset clears records and history budget together");
            string commands = File.ReadAllText("src/TLDRevamp.Dev/EntityCommands.cs");
            string register = File.ReadAllText("src/TLDRevamp.Dev/DevCommands.cs");
            Require(commands.Contains("case \"mp ownerhistory\": return Net.Entities.OwnerHistoryJson(uint.Parse(ma[1]));") &&
                register.Contains("DebugBridge.Register(\"mp ownerhistory\", arg => EntityCommands.Run(\"mp ownerhistory\", arg));"), "Dev command has both dispatch and registration");
            Require(!owner.Contains("HarmonyPatch") && !owner.Contains("using static"), "owner kernel has no patches or static imports");
            Console.WriteLine(writes.Count + " remaining client/DTO/creation writes pinned; bridge and reset wiring PASS");
        }
    }
}
internal static class EntityOwnerChangeChecks
{
    private static void Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        TLDRevamp.Net.Entities.CheckRows();
#if !OWNER_BASELINE
        TLDRevamp.Net.Entities.CheckHistory();
        TLDRevamp.Net.Entities.CheckOwnerSources();
#endif
        Console.WriteLine("EntityOwnerChangeTest PASS");
    }
}
