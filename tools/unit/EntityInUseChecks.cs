using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        private sealed record UseCase(string Name, Action<SEnt> Set);

        private static void GroupPart(SEnt se, bool loose)
        {
            se.ParentNet = 9; se.PartIndex = 3;
            var car = Server[9] = new SEnt { NetId = 9, OwnerId = 3, HitchCar = 99 };
            if (loose) car.SplitOff.Add(se.PartIndex);
        }

        private static SEnt UseFixture(string path)
        {
            var se = Fresh();
            ByNet.Clear(); SessionLog.HistoryRows.Clear(); _ownerHistoryEvents = 0;
            HandoffKeptMounted = HandoffKeptCarried = EntityHands.WornRefused = EntityHands.WornTooLate = 0;
            EntitySeats.DriverSeatRefused = EntityPhysics.CrashRefused = EntityPhysics.CrashAutoReturned = 0;
            EntitySeats.SeatedPlayer = -1; _crashTickAt = -1; HeldClearedOnAttach = StaleAttachesForwarded = 0;
            NativeMountChecks = 0;
            se.Rabbit = path == "rabbit"; se.Car = path == "claim";
            if (path is "cascade" or "attach")
            {
                var parent = Server[8] = new SEnt { NetId = 8, OwnerId = 2, Epoch = 3 };
                if (path == "cascade") { se.AttachParent = 8; parent.AttachKids = new() { 7 }; }
            }
            return se;
        }

        private static string RunUsePath(string path, SEnt se)
        {
            switch (path)
            {
                case "claim": ServerClaim(2, ClaimPacket()); break;
                case "crash": ServerCrashClaim(2, 7, 9); break;
                case "handoff": ServerHandoffTick(1); break;
                case "rabbit": float elapsed = 0; ChooseRabbitOwner(1, ref elapsed, new()); break;
                case "silent": ServerTakeSilent(1); break;
                case "leave": ServerOnLeave(1); break;
                case "cascade": CascadeOwner(Server[8]); break;
                case "worn": WornTakesBack(se, 2, (byte)(se.WornSlot == 255 ? 255 : 1)); break;
                case "attach":
                    var w = new NetWriter(); w.U8(AttachSync); w.U32(7); w.U8(1); w.U8(0); w.VarU32(1); w.U32(8); w.VarU32(0); w.VarU32(0);
                    for (int i = 0; i < 9; i++) w.F64(0);
                    w.U8(0);
                    ServerAttach(1, Packet(w, 1));
                    break;
                case "release": ServerCrashRelease(1, 7); break;
                case "crash-tick": ServerCrashTick(); break;
                case "claim-same-owner": ServerClaim(1, ClaimPacket()); break;
                case "crash-same-owner": ServerCrashClaim(1, 7, 9); break;
                default: throw new ArgumentException(path);
            }
            if (se.Epoch != 12) return "allowed";
            return ClaimsRefused + EntityPhysics.CrashRefused + EntityHands.WornTooLate > 0 ? "refused" : "skipped";
        }

        private static string CaptureUseRow(string path, UseCase scenario)
        {
            var se = UseFixture(path);
            scenario.Set(se);
            string outcome = RunUsePath(path, se);
            string counts = $"claim={ClaimsRefused},{RefusedCrash},{RefusedHeld},{RefusedStored},{EntityHands.WornRefused},{RefusedDriven},{EntitySeats.DriverSeatRefused},{OwnerReconfirmed}" +
                $":crash={EntityPhysics.CrashGranted},{EntityPhysics.CrashRefused},{EntityPhysics.CrashLeasesKept},{EntityPhysics.CrashAutoReturned}" +
                $":handoff={Handoffs},{HandoffLogged},{HandoffKeptMounted},{HandoffKeptCarried}:rabbit={EntityRabbits.RabbitOwnerMoves}" +
                $":silent={SilentTakeovers}:cascade={OwnerCascaded}:worn={EntityHands.WornTooLate},{EntityHands.WornTakenBack}:attach={HeldClearedOnAttach},{StaleAttachesForwarded}";
            return $"{path}/{scenario.Name}|{outcome}|{Fields(se)}:hands={se.Held},{se.Stored}|{counts}|" +
                string.Join("/", Trace) + "|" + string.Join("/", SessionLog.History.Concat(SessionLog.HistoryRows));
        }

        internal static void CheckUseRows()
        {
            var free = new UseCase("free", _ => { });
            var hitched = new UseCase("hitched", se => se.HitchCar = 97);
            var crash = new UseCase("crash-lease", se => se.CrashReturnTo = 3);
            var held = new UseCase("held", se => se.Held = true);
            var stored = new UseCase("stored", se => se.Stored = true);
            var worn = new UseCase("worn", se => se.WornBy = se.OwnerId);
            var driven = new UseCase("driven", se => { se.Driven = true; se.DrivenAt = 99.5f; });
            var mounted = new UseCase("mounted", se => ByNet[7] = new Ent { Root = new Root { attachable = new Mount { attached = true, toItem = true } } });
            var group = new UseCase("group", se => GroupPart(se, false));
            var loose = new UseCase("loose-group", se => GroupPart(se, true));
            var locked = new UseCase("lock-frame", se => se.LockFrame = 6);
            var seated = new UseCase("seat-taken", _ => EntitySeats.SeatedPlayer = 3);
            var claimHold = new UseCase("seat-claim-hold", se => { se.ClaimedBy = se.OwnerId; se.ClaimedAt = 99; });
            var staleDriver = new UseCase("driver-at-1s", se => { se.Driven = true; se.DrivenAt = 99; });
            var partner = new UseCase("partner-lease-to-claimer", _ => Server[9] = new SEnt { NetId = 9, OwnerId = 3, CrashReturnTo = 2 });
            UseCase pair(UseCase a, UseCase b) => new(a.Name + "+" + b.Name, se => { a.Set(se); b.Set(se); });
            var matrix = new[] { free, hitched, crash, held, stored, worn, driven, mounted, group, loose, locked, seated,
                claimHold, staleDriver, pair(hitched, crash), pair(crash, held), pair(held, stored), pair(held, worn),
                pair(worn, driven), pair(driven, seated), pair(seated, claimHold), pair(mounted, driven),
                pair(held, mounted), pair(held, driven), pair(group, mounted), pair(loose, mounted), pair(hitched, mounted) };
            var rows = new List<string>();
            foreach (var path in new[] { "claim", "crash", "handoff", "rabbit", "silent", "leave", "cascade", "worn", "attach" })
                foreach (var scenario in matrix)
                    rows.Add(CaptureUseRow(path, scenario));

            void Add(string path, params UseCase[] cases)
            {
                foreach (var scenario in cases) rows.Add(CaptureUseRow(path, scenario));
            }
            Add("claim-same-owner", pair(hitched, crash), pair(held, seated));
            Add("crash-same-owner", hitched, pair(crash, partner));
            Add("crash", partner, pair(hitched, partner), pair(crash, partner),
                new("partner-lease-to-other", _ => Server[9] = new SEnt { NetId = 9, OwnerId = 3, CrashReturnTo = 4 }));
            Add("release", free, pair(crash, held), pair(crash, hitched));
            Add("crash-tick", free, pair(crash, worn), new("lease-at-deadline", se => { crash.Set(se); se.CrashUntil = 100; }));
            Add("handoff", new("mounted-by-record", se => { se.AttachParent = 9; Server[9] = new SEnt { NetId = 9, HitchCar = 97 }; }),
                new("split-still-on-carrier", se => { GroupPart(se, true); se.CarrierNet = 5; }));
            Add("rabbit", new("not-rabbit", se => se.Rabbit = false), new("hold-in-future", se => se.RabbitHoldUntil = 101),
                new("hold-at-deadline", se => se.RabbitHoldUntil = 100),
                new("locked-bit-without-frame", se => se.Locked = true));
            Add("silent", new("missing-state", se => se.HasState = false), new("speed-at-half", se => se.Speed = 0.5f),
                new("speed-not-a-number", se => se.Speed = float.NaN));
            Add("leave", new("lease-returns-to-leaver", se => se.CrashReturnTo = 1),
                new("tow-known", se => { se.HitchCar = 8; Server[8] = new SEnt { NetId = 8, OwnerId = 2 }; }));
            Add("cascade", new("wrong-parent", se => se.AttachParent = 9), new("same-owner", se => se.OwnerId = 2),
                new("self-and-missing-kids", _ => Server[8].AttachKids.UnionWith(new uint[] { 8, 9 })));
            Add("worn", new("wrong-previous-owner", se => se.PrevOwnerId = 3), new("window-at-deadline", se => se.PrevOwnerAt = 95),
                new("window-expired", se => se.PrevOwnerAt = 94), new("taken-off", se => se.WornSlot = 255),
                new("old-owner-still-marked-worn", se => se.WornBy = se.PrevOwnerId));
            Add("attach", new UseCase("tow-known", se => { se.HitchCar = 8; se.Held = true; }));

            const string golden = "tools/unit/EntityInUseRows.txt";
#if ENTITY_IN_USE_BASELINE
            if (Environment.GetEnvironmentVariable("ENTITY_IN_USE_CAPTURE") == "1")
                File.WriteAllText(golden, string.Join("\n", rows) + "\n");
#endif
            string[] expected = File.ReadAllLines(golden);
            if (!rows.SequenceEqual(expected))
            {
                const string actual = "tools/unit/obj/EntityInUseTest/actual-rows.txt";
                File.WriteAllLines(actual, rows);
                int first = Enumerable.Range(0, Math.Min(rows.Count, expected.Length)).FirstOrDefault(i => rows[i] != expected[i], -1);
                throw new Exception($"in-use rows differ (first mismatch at line {first + 1}, {rows.Count} vs {expected.Length} rows). See {actual}");
            }
            Console.WriteLine($"{rows.Count} in-use decision rows match 96e49c8");

            foreach (string path in new[] { "claim", "crash", "handoff", "rabbit", "silent", "leave", "cascade", "worn", "attach" })
            {
                CaptureUseRow(path, mounted);
                Require(NativeMountChecks == (path == "handoff" ? 1 : 0), path + " uses only the requested native mount lookup");
            }
            CaptureUseRow("handoff", pair(hitched, mounted));
            Require(NativeMountChecks == 0, "hitched skips the native mount lookup");
            CaptureUseRow("handoff", pair(group, mounted));
            Require(NativeMountChecks == 0, "non-loose group part skips the native mount lookup");

            foreach (var (age, speed, owner) in new[] { (0.5f, 0f, 3), (1f, 0f, 2), (1f, 2f, 3) })
            {
                var se = UseFixture("rabbit");
                Server[9] = new SEnt { NetId = 9, OwnerId = 3, Car = true, HasState = true, Driven = true, DrivenAt = 100 - age, Speed = speed };
                RunUsePath("rabbit", se);
                Require(se.OwnerId == owner, $"car anchor with driver report age {age} and speed {speed}");
            }
            Console.WriteLine("Native mount lookup short circuits and rabbit car anchor boundaries PASS");
        }
    }
}

internal static class EntityInUseChecks
{
    private static void Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        TLDRevamp.Net.Entities.CheckUseRows();
#if !ENTITY_IN_USE_BASELINE
        EntityInUseSourceChecks.Run();
#endif
        Console.WriteLine("EntityInUseTest PASS");
    }
}
