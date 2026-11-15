using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static void ResyncEditsChecks()
    {
        bool capture = Environment.GetEnvironmentVariable("CAPTURE_BATCH13_HEAD") == "1";
        var rows = new List<string>();
        void Row(string key, object value) => rows.Add(key + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        void Packet(string key) { var p = Entities.Packets.Last(); Row(key, p.Peer + ":" + p.Reliable + ":" + Convert.ToHexString(p.Bytes)); }
        Mp.Server = new Mp.SeatServer();
        EntityEditCheck.ResetRates(); EntityEditBack.RefusedEditsAsked = 0;
        ((IDictionary)Field(typeof(EntityEditBack), "_editBack")).Clear();
        Entities.Packets.Clear(); Entities.ClearServer(); Entities.TestServer(7, 2);
        Time.realtimeSinceStartup = 100;
        EntityEditBack.EditBackLater(7, 18, false); EntityEditBack.EditBackTick(); Row("back-held", Entities.Packets.Count);
        Entities.ChangeBackOwner(7, 4, 9);
        Time.realtimeSinceStartup = 101.71f; EntityEditBack.EditBackTick(); Packet("back-new-owner");
        EntityEditBack.EditBackTick(); Row("back-once", EntityEditBack.RefusedEditsAsked);
        EntityEditBack.EditBackLater(7, -1, false); EntityEditBack.EditBackLater(7, 1, true);
        EntityEditBack.RefusedEditsComeBack = false; EntityEditBack.EditBackLater(7, 1, false); EntityEditBack.RefusedEditsComeBack = true;
        Row("back-invalid", ((IDictionary)Field(typeof(EntityEditBack), "_editBack")).Count);
        for (int i = 0; i < 300; i++) EntityEditBack.EditBackAfterHold(7, i);
        Row("back-bounded", ((IDictionary)Field(typeof(EntityEditBack), "_editBack")).Count);
        ((IDictionary)Field(typeof(EntityEditBack), "_editBack")).Clear();
        EntityEditBack.EditBackAfterHold(99, 1); Time.realtimeSinceStartup += 2; EntityEditBack.EditBackTick(); Row("back-gone", EntityEditBack.RefusedEditsAsked);
        EntityEditCheck.EditRatePerS = 2.5f; EntityEditCheck.EditBurst = 2;
        Time.realtimeSinceStartup = 200;
        foreach (float offset in new[] { 0f, 0f, 0f, .2f, .4f, .4f, 1f, .5f, 10f })
        { Time.realtimeSinceStartup = 200 + offset; Row("token-" + offset.ToString("R", CultureInfo.InvariantCulture), EntityEditCheck.TakeEditToken(2)); }
        EntityEditCheck.ForgetEditRate(2); Row("token-forget", EntityEditCheck.TakeEditToken(2));
        long count = 0; Entities.AnomalyLogCap = 40; Entities.EditAnomalyTotal = 0; Entities.EditAnomalyLogs.Clear();
        for (int i = 0; i < 7; i++) EntityEditCheck.Refuse(ref count, "same", "failure " + i);
        Row("refusal", count + ":" + Entities.EditAnomalyTotal + ":" + string.Join("|", Entities.EditAnomalyLogs) + ":" + EntityEditCheck.EditRefusedLast);
        var key = new Entities.PoiKey { Gen = -1, X = 1000, U = -1 };
        var state = new Entities.PoiState { Slide = float.NaN };
        EntityEditCheck.PoiBurst = 0; Entities.EditPeerKnown = false;
        Row("poi-host", EntityEditCheck.CheckPoiUsable(0, key, state));
        Row("poi-rate-first", EntityEditCheck.CheckPoiUsable(2, key, state) + ":" + EntityEditCheck.EditRefusedLast);
        EntityEditCheck.ResetRates(); EntityEditCheck.PoiBurst = 10;
        Row("poi-value-next", EntityEditCheck.CheckPoiUsable(2, key, state) + ":" + EntityEditCheck.EditRefusedLast);
        key.Gen = 0; key.U = 1; state.Slide = 0;
        Row("poi-unknown-position", EntityEditCheck.CheckPoiUsable(2, key, state) + ":" + EntityEditCheck.EditRefusedLast);
        Entities.EditPeerKnown = true; Entities.EditPeerPosition = new Vector3d(0, 0, 0);
        Row("poi-distance", EntityEditCheck.CheckPoiUsable(2, key, state) + ":" + EntityEditCheck.EditRefusedLast);
        key.X = 150; Row("poi-edge", EntityEditCheck.CheckPoiUsable(2, key, state));
        foreach (float v in new[] { float.NaN, float.PositiveInfinity, -100001, -100000, 0, 100000, 100001 }) Row("sane-" + v.ToString("R", CultureInfo.InvariantCulture), EntityEditCheck.Sane(v));
        EntityEditCheck.EditRatePerS = 20; EntityEditCheck.EditBurst = 40; EntityEditCheck.PoiBurst = 240; EntityEditCheck.ResetRates();

        var tank = new tankscript { F = new fluid() }; tank.F.fluids.Add(new mainscript.FluidValue { type = 1, amount = 5 });
        var item = new tosaveitemscript { idInSave = 77, tanks = new List<tankscript> { tank } };
        EntityCarTuning.CopyTanksAre(77, new List<save_tankscript> { new save_tankscript(tank, 77, 0) });
        EntityEdits.RememberTankTruth(item); tank.F.fluids[0].amount = 8;
        var adds = new List<TankAdds.Add>(); Row("tank-diff", EntityEdits.TankChangesNow(item, adds));
        var w = new NetWriter(); TankAdds.Write(w, adds); Row("tank-tail", Convert.ToHexString(w.Buf.AsSpan(0, w.Len)));
        tank.F.DuringChange = () => Check(Entities.ApplyingState, "tank change sees the kernel guard");
        EntityEdits.ApplyTankAdds(item, new List<TankAdds.Add> { new() { Tank = 0, Type = 1, Litres = 100 }, new() { Tank = 4, Type = 0, Litres = 1 } });
        Row("tank-clamped", tank.F.GetAmount() + ":" + EntityEdits.TankAddsApplied + ":" + Entities.ApplyingState);
        tank.F.DuringChange = () => throw new InvalidOperationException("fixture");
        try { EntityEdits.ApplyTankAdds(item, new List<TankAdds.Add> { new() { Tank = 0, Type = 1, Litres = -1 } }); } catch (InvalidOperationException) { }
        Check(!Entities.ApplyingState, "throwing tank apply clears the same guard");
        EntityEdits.ForgetTankEditTruth(77); Row("tank-forget", EntityEdits.TankChangesNow(item, adds));
        Row("kinds", string.Join(",", new[] { "usable", "tank", "part", "color", "food", "ammo", "plate", "busdoor", "blood", "other" }.Select(EntityEdits.KindBit)));

        var data = new itemDataClass { car = new List<save_carscript> { new() } };
        data.attachable.Add(new save_attachable((attachablescript)null, 0) { attachType = 1, parentid = 77, indexType = 1, lpos = new Vector3d(.124, -.126, .025) });
        data.attachable.Add(new save_attachable((attachablescript)null, 0) { attachType = 1, parentid = 123, indexType = 2 });
        var normalized = EntityResync.StateOnly(data);
        Row("normalize", normalized.attachable[0].lpos.x + ":" + normalized.attachable[0].lpos.y + ":" + normalized.attachable[0].lpos.z + ":" + normalized.attachable[0].gpos.x + ":" + normalized.attachable[1].lpos.x);
        Check(ReferenceEquals(normalized.tanks, data.tanks) && !ReferenceEquals(normalized.attachable[0], data.attachable[0]), "record list and attachment copy lifetimes preserved");
        var map = EntityResync.CanonMap(new List<tosaveitemscript> { item, null }); Row("canon", map[77]);
        EntityResync.ResyncForeignAttachDropped = 0;
        Row("record", Convert.ToHexString(EntityResync.StateRecord(data, map, out ulong hash, false)) + ":" + hash + ":" + EntityResync.ResyncForeignAttachDropped);
        EntityResync.DropUncanonAttach(data); Row("uncanon", data.attachable.Count + ":" + EntityResync.ResyncForeignAttachDropped);
        Entities.Packets.Clear(); Entities.ResyncBudgetMs = 1000; Time.realtimeSinceStartup = 300;
        var e = new Entities.Ent { NetId = 17, Epoch = 6, Root = item, ResyncPhased = true }; e.Items.Add(item); e.Items.Add(null);
        Entities.BeginResyncFrame(); Entities.TestResync(e, 3); Packet("resync-first"); Row("resync-cursor", e.ResyncCursor);
        Entities.BeginResyncFrame(); Entities.TestResync(e, 3); Row("resync-equal", Entities.Packets.Count + ":" + e.ResyncCursor);
        e.Proxy = true; Entities.TestResync(e, 0); Row("resync-proxy", (e.ResyncHashes == null) + ":" + e.ResyncCursor);
        e.Proxy = false; e.ResyncCursor = 0; Entities.BeginResyncFrame(); Entities.TestResync(e, 0); Packet("resync-reowned");
        e.ResyncCursor = 0; e.ResyncHashes = null; Entities.ResyncBudgetMs = -1; Entities.BeginResyncFrame(); Entities.TestResync(e, 0);
        Row("resync-budget-one", e.ResyncCursor); Entities.TestResync(e, 0); Row("resync-deferred", Entities.TestDeferred + ":" + e.ResyncCursor);
        Entities.ResyncBudgetMs = 1000; Entities.ByNet.Clear(); Entities.Packets.Clear();
        string actual = string.Join("\n", rows) + "\n";
        if (capture) File.WriteAllText("tools/unit/EntityResyncEditsRows.txt", actual.Replace("\n", "\r\n"));
        else
        {
            string expected = File.ReadAllText("tools/unit/EntityResyncEditsRows.txt").Replace("\r\n", "\n");
            Check(actual == expected, "edit-back, rate, refusal, tank and resync rows match f183a37: " + string.Join(" | ", rows.Zip(expected.Split('\n')).Where(p => p.First != p.Second).Select(p => p.First + " expected " + p.Second)));
        }
        if (!capture) ResyncEditsSourceChecks();
        ServerEditRefusalChecks();
    }

    static void ServerEditRefusalChecks()
    {
        var rows = new List<string>();
        byte[] Edit(uint net = 7, double stamp = 123.5, bool payload = true, bool tail = false) => Bytes(w =>
        {
            w.Write((byte)32); w.Write(net); w.Write(stamp);
            if (payload) { w.Write((byte)0); w.Write((byte)2); w.Write((byte)3); w.Write(new byte[] { 1, 2, 3 }); if (tail) w.Write((byte)255); }
        });
        void Run(string name, byte[] message)
        {
            SessionLog.Events.Clear(); Entities.Packets.Clear();
            double rejected = Entities.EditsRejected; long rate = EntityEditCheck.EditRefusedRate, kinds = EntityEditCheck.EditRefusedKinds, values = EntityEditCheck.EditRefusedValues, reach = Entities.EditsOutOfReach;
            Entities.TestServerEdit(2, Reader(message));
            rows.Add(name + "=" + string.Join(",", SessionLog.Events) + ":" + (Entities.EditsRejected - rejected) + ":" + (EntityEditCheck.EditRefusedRate - rate) + ":" + (EntityEditCheck.EditRefusedKinds - kinds) + ":" + (EntityEditCheck.EditRefusedValues - values) + ":" + (Entities.EditsOutOfReach - reach) + ":" + string.Join(",", Entities.Packets.Select(p => p.Peer + ":" + p.Reliable + ":" + Convert.ToHexString(p.Bytes))));
        }
        Entities.ClearServer(); Entities.TestServer(7, 4); SessionLog.St = 123.5;
        Entities.SeatToken = false; Entities.SeatNear = false;
        Run("timestamp-first", Edit(stamp: double.NaN));
        Run("missing-before-rate", Edit(net: 99));
        Entities.ChangeBackOwner(7, 2, 3); Entities.EditsComeBack = false; Run("already-owner-drop", Edit(payload: false));
        Entities.EditsComeBack = true; Run("already-owner-echo-before-payload", Edit(payload: false));
        Entities.ChangeBackOwner(7, 4, 3); Run("rate-before-reach-and-malformed", Edit(payload: false));
        Entities.SeatToken = true; Run("reach-before-malformed", Edit(payload: false));
        Entities.SeatNear = true; Run("malformed-before-values", Edit(payload: false));
        Entities.TestEditValidation = "kinds 65535"; Run("kinds-before-bad-tail", Edit(tail: true));
        Entities.TestEditValidation = "tank amount NaN"; Run("values-before-bad-tail", Edit(tail: true));
        Entities.TestEditValidation = null; Run("bad-tail", Edit(tail: true));
        Run("accepted", Edit());
        string actual = string.Join("\n", rows) + "\n";
        if (Environment.GetEnvironmentVariable("CAPTURE_BATCH13_SERVER_HEAD") == "1")
            File.WriteAllText("tools/unit/EntityEditRefusalRows.txt", actual.Replace("\n", "\r\n"));
        else Check(actual == File.ReadAllText("tools/unit/EntityEditRefusalRows.txt").Replace("\r\n", "\n"), "actual ServerEdit refusal order and relay bytes match f183a37");
        Entities.TestEditValidation = Entities.TestAddsValidation = null;
    }

    static void ResyncEditsSourceChecks()
    {
        foreach (string row in File.ReadAllLines("tools/unit/EntityBatch13SourceRows.txt"))
        {
            string[] c = row.Split('|');
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + c[0] + ".cs").Replace("\r\n", "\n");
            var match = Regex.Match(source, @"^        (?:public|private|internal) static [^\n=]*?\b" + c[1] + @"\(", RegexOptions.Multiline);
            Check(match.Success, "baseline method still exists " + c[0] + "." + c[1]);
            int brace = source.IndexOf('{', match.Index), arrow = source.IndexOf("=>", match.Index, StringComparison.Ordinal);
            string body;
            if (arrow >= 0 && (brace < 0 || arrow < brace)) body = source.Substring(arrow, source.IndexOf(';', arrow) + 1 - arrow);
            else
            {
                int end = brace + 1, depth = 1;
                while (depth > 0) { depth += source[end] == '{' ? 1 : source[end] == '}' ? -1 : 0; end++; }
                body = source.Substring(brace, end - brace);
            }
            body = body.Replace("Entities.RecordEditRefusal(what, n < 5);", "if (n < 5) Anomaly(what + \" (refused)\"); else Anomalies++;")
                .Replace("!Entities.TryEditPeerPosition(from, out var p)", "Mp.Server == null || !Mp.Server.PeerPos(from, out var p)")
                .Replace("Entities.EditBackHold", "EditHold").Replace("Entities.EditReachRefusals", "EditsOutOfReach").Replace("Entities.EditAnomalyCount", "Anomalies")
                .Replace("Entities.SetFluidApplyingState(true);", "_applyingState = true;").Replace("Entities.SetFluidApplyingState(false);", "_applyingState = false;")
                .Replace("Entities.IsFinite(", "Finite(")
                .Replace("EntityEdits.RememberTankTruth(it);", "if (TankAddEdits && it != null && !_tankTold.ContainsKey(it.idInSave) && EntityCarTuning.TryCopyTankTruth(it.idInSave, out var truth)) _tankTold[it.idInSave] = truth;")
                .Replace("TakeEditToken(from)", "TakeToken(_editBuckets, from, EditRatePerS, EditBurst)")
                .Replace("EntityEditBack.", "").Replace("EntityEditCheck.", "").Replace("EntityEdits.", "").Replace("EntityResync.", "");
            body = Regex.Replace(body, @"Entities\.(?=Kind\w+\b|CanonBase\b)", "");
            if (c[0] == "EditBack") body = body.Replace("EditBackAfterHold(net, idx)", "EditBackAfterHold(se, idx)").Replace("(ulong)net <<", "(ulong)se.NetId <<");
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Regex.Replace(body, @"\s+", ""))));
            Check(hash == c[2], c[0] + "." + c[1] + " matches f183a37 after narrow boundary substitutions");
        }
        CoreMembers.Register();
        foreach (var owner in new[] { typeof(EntityEditBack), typeof(EntityEditCheck), typeof(EntityEdits), typeof(EntityResync) })
        {
            Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, owner.Name + " internal static");
            string part = owner.Name.Substring(6);
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Split("    public static partial class Entities")[0];
            Check(!Regex.IsMatch(source, @"using\s+static\s+.*Entities\s*;|Entities\.(?:W|WS|ByNet|Server|Placed|ObservedEntities)\b|\bSEnt\b"), owner.Name + " has no map, record walk or shared writer boundary");
            string code = Regex.Replace(source, @"//[^\r\n]*", "");
            string[] allowed = part switch
            {
                "EditBack" => new[] { "EditBackHold", "RequestEditBack" },
                "EditCheck" => new[] { "RecordEditRefusal", "IsFinite", "PoiKey", "PoiState", "TryEditPeerPosition", "EditReachRefusals", "EditAnomalyCount" },
                "Edits" => new[] { "SetFluidApplyingState", "KindUsable", "KindTank", "KindPart", "KindColor", "KindFood", "KindAmmo", "KindPlate", "KindBusDoor", "KindBlood", "KindAll" },
                _ => new[] { "CanonBase" }
            };
            Check(Regex.Matches(code, @"Entities\.(\w+)").Cast<Match>().All(m => allowed.Contains(m.Groups[1].Value)), owner.Name + " uses only its named narrow kernel facts");
            Check(!code.Contains("HarmonyPatch"), owner.Name + " moves no attributed type");
            foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
            {
                bool retained = typeof(Entities).GetMember(member.Name, Static).Length > 0;
                foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                    Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "resync/edit alias " + prefix + member.Name);
            }
        }
        foreach (string name in new[] { "_editBack", "_editBackDue", "_editBuckets", "_poiBuckets", "_refusalsLogged", "_tankTold", "TankAddsApplied", "ResyncForeignAttachDropped" })
            Check(typeof(Entities).GetField(name, Static) == null, name + " has no duplicate kernel storage");
        string edits = File.ReadAllText("src/TLDRevamp/Net/Entities.Edits.cs").Split("    public static partial class Entities")[1];
        Check(edits.Contains("private static bool _applyingState;") && edits.Contains("private static readonly Dictionary<tosaveitemscript, float> EditDirty") && edits.Contains("private static float _editFlushAt;"), "kernel keeps the shared guard, dirty marks and flush clock");
        foreach (string patch in new[] { "EditUsable", "EditTank", "TankFilled", "PouredFromHand", "PouredThroughTube", "EditPartCondition", "EditColor", "EditFood", "FoodStart", "EditAmmo", "EditPlate", "EditBusDoors" })
            Check(Regex.Matches(edits, @"private static class " + patch + @"\b").Count == 1, patch + " stays nested in the kernel with its old registration order");
        string check = File.ReadAllText("src/TLDRevamp/Net/Entities.EditCheck.cs").Split("    public static partial class Entities")[1];
        Check(check.Contains("EntityEditCheck.ForgetEditRate(player); _liveServerIn.Remove(player); EntityEditCheck.ForgetPoiRate(player);") && check.Contains("EntityEditCheck.ResetRates(); _groupRecords.Clear();"), "forget and reset keep rate/live/group-cache order");
        string features = File.ReadAllText("src/TLDRevamp/Net/Entities.Features.cs");
        Check(features.Contains("WS.Reset(); WS.U8(CensusRepair); WS.U32(se.NetId); WS.U32(se.Epoch); WS.U8(8);") && features.Contains("WS.VarU32(1); WS.VarU32(item);") && features.Contains("ServerSendTo(se.OwnerId, WS, true);"), "edit-back keeps the kernel census writer and current owner transaction");
        Check(BridgeReflect.Set("TLDRevamp.Net.Entities.TankAddsApplied", "19") == "19" && EntityEdits.TankAddsApplied == 19, "full tank apply counter alias writes its owner");
        Check(BridgeReflect.Set("Entities.EditRefusedRate", "23") == "23" && EntityEditCheck.EditRefusedRate == 23, "short refusal counter alias writes its owner");
        Check(BridgeReflect.Get("TLDRevamp.Net.Entities._editBack.Count") == "0", "historical private queue path resolves to its owner");
    }
}
