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
using AK = AuthorityInterestFakes.Kernel;
using AM = AuthorityInterestFakes.Transport;
using AS = AuthorityInterestFakes.Server;

static partial class EntitySplitTest
{
    static object AuthorityCall(string name, params object[] args) => typeof(EntityAuthorityInterest).GetMethod(name, Static).Invoke(null, args);
    static void AuthorityInterestChecks()
    {
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        void State(string name)
        { Row(name, EntityAuthorityInterest.InterestJson() + ":" + string.Join("|", AK.Events)); AK.Events.Clear(); }
        NetWriter Message(byte tag) { var w = new NetWriter(); w.U8(tag); w.U32(0x10203040); return w; }
        void Setup()
        {
            EntityAuthorityInterest.ResetRelay(); AK._staleEnts.Clear();
            foreach (string name in new[] { "ResyncRelayed", "ResyncHeldBack", "ResyncCaughtUp", "PoiRelayed", "PoiHeldBack", "PoiCaughtUp", "SharesDropped", "SharesKicked" }) Set(typeof(EntityAuthorityInterest), name, 0L);
            ((IDictionary)Field(typeof(EntityAuthorityInterest), "_shareBuckets")).Clear(); ((IList)Field(typeof(EntityAuthorityInterest), "_kickQ")).Clear();
            EntityAuthorityInterest.InterestRelay = true; EntityAuthorityInterest.StateInterestM = 1000;
            EntityAuthorityInterest.ShareBurst = 2; EntityAuthorityInterest.ShareRefillPerS = 1; EntityAuthorityInterest.ShareMaxRecordBytes = 10; EntityAuthorityInterest.ShareKickDrops = 4;
            AK.Records.Clear(); AK.Pois.Clear(); AK.Events.Clear(); AK.DuringSend = null; AK.InterestCatchUpLimit = 2;
            AK.Records[7] = (default, new byte[] { 31, 7, 9, 11 });
            AM.Server = new AS();
            AM.Server.Spots.Add(new AS.PeerSpot { Id = 1, HasPos = true, Pos = new Vector3d(1000, 0, 0) });
            AM.Server.Spots.Add(new AS.PeerSpot { Id = 2, HasPos = true, Pos = new Vector3d(1000.01, 0, 0) });
            AM.Server.Spots.Add(new AS.PeerSpot { Id = 3, HasPos = false, Pos = new Vector3d(9e6, 0, 0) });
            Time.realtimeSinceStartup = 10;
        }
        void Relay(int from = 9) => EntityAuthorityInterest.RelayResync(7, default, from, Message(31));
        Setup(); Relay(); State("boundary-unknown-position");
        Relay(2); State("source-is-excluded");
        AM.Server.Spots[1] = new AS.PeerSpot { Id = 2, HasPos = true };
        Relay(); State("stale-next-relay-sends-full");
        Relay(0); State("host-source-not-delivered");
        EntityAuthorityInterest.InterestRelay = false; Relay(); State("disabled-broadcast");
        EntityAuthorityInterest.InterestRelay = true; AM.Server = null; Relay(); State("no-server-broadcast");

        Setup(); Relay(); AK.Events.Clear();
        AM.Server.Spots[1] = new AS.PeerSpot { Id = 2, HasPos = true };
        EntityAuthorityInterest.ServerCatchUpTick(); State("arrival-catches-up");
        EntityAuthorityInterest.ServerCatchUpTick(); State("no-repeat-catch-up");
        Setup(); Relay(); AK.Events.Clear(); AK.Records.Remove(7);
        EntityAuthorityInterest.ServerCatchUpTick(); State("deleted-debt-cleared");
        Setup(); Relay(); AK.Events.Clear(); AK.Records[7] = (default, Array.Empty<byte>());
        AM.Server.Spots[1] = new AS.PeerSpot { Id = 2, HasPos = true };
        EntityAuthorityInterest.ServerCatchUpTick(); State("empty-record-debt-cleared");
        Setup(); Relay(); AK.Events.Clear(); AK.InterestGotAll(2, 7);
        AM.Server.Spots[1] = new AS.PeerSpot { Id = 2, HasPos = true };
        EntityAuthorityInterest.ServerCatchUpTick(); State("send-object-clears-debt");
        Setup();
        for (uint net = 7; net < 12; net++) { AK.Records[net] = (default, new byte[] { 31, (byte)net }); EntityAuthorityInterest.RelayResync(net, default, 9, Message(31)); }
        AK.Events.Clear(); AM.Server.Spots[1] = new AS.PeerSpot { Id = 2, HasPos = true };
        for (int i = 0; i < 4; i++) { EntityAuthorityInterest.ServerCatchUpTick(); State("bounded-catch-up-" + i); }

        var poi = new Entities.PoiKey { Gen = 2, X = 0, Y = 7, Z = 0, U = 4 };
        Setup(); AK.Pois[poi] = new byte[] { 90, 8, 7, 6 };
        EntityAuthorityInterest.RelayPoi(poi, Message(90), 9, false); State("poi-unreliable-boundary");
        AM.Server.Spots[1] = new AS.PeerSpot { Id = 2, HasPos = true };
        EntityAuthorityInterest.ServerCatchUpTick(); State("poi-arrival-reliable-cache");
        Setup(); EntityAuthorityInterest.PreparePoiStale(2); State("empty-poi-all-stale");
        EntityAuthorityInterest.MarkPoiStale(2, poi); AK.Pois[poi] = new byte[] { 90, 4, 5 };
        EntityAuthorityInterest.ServerCatchUpTick(); State("joiner-far-owes-poi");
        AM.Server.Spots[1] = new AS.PeerSpot { Id = 2, HasPos = true };
        EntityAuthorityInterest.ServerCatchUpTick(); State("joiner-near-gets-poi");
        EntityAuthorityInterest.MarkPoiStale(2, poi); AK.Pois.Remove(poi);
        EntityAuthorityInterest.ServerCatchUpTick(); State("deleted-poi-clears-debt");
        EntityAuthorityInterest.MarkPoiStale(2, poi); EntityAuthorityInterest.ForgetRelay(2); State("leave-clears-both-debts");
        EntityAuthorityInterest.MarkPoiStale(1, poi); EntityAuthorityInterest.ResetRelay(); State("reset-keeps-counters");

        Setup(); AK.DuringSend = player => { if (player == 0) { AK.Events.Add("local-reentry"); AK.InterestGotAll(2, 7); } };
        Relay(); State("peers-before-local-reentry"); AK.DuringSend = null;
        Setup();
        void Allowed(string label, int player = 1, int bytes = 1) { Row(label, EntityAuthorityInterest.ShareAllowed(player, bytes) + ":" + EntityAuthorityInterest.ShareQuotaStats() + ":" + string.Join("|", AK.Events)); AK.Events.Clear(); }
        Allowed("host-exempt", 0, int.MaxValue);
        Allowed("oversize-first", 1, 11); Allowed("first-token"); Allowed("last-token"); Allowed("no-tokens");
        Time.realtimeSinceStartup = 10.999f; Allowed("before-refill"); Time.realtimeSinceStartup = 11; Allowed("at-refill");
        EntityAuthorityInterest.ShareQuotaTick(); Row("queued-kick", string.Join("|", AK.Events)); AK.Events.Clear();
        Setup(); EntityAuthorityInterest.ShareBurst = 0;
        for (int i = 0; i < 5; i++) Allowed("drop-" + i);
        AM.Server = null; EntityAuthorityInterest.ShareQuotaTick(); AM.Server = new AS(); EntityAuthorityInterest.ShareQuotaTick(); Row("kick-drained-without-server", AK.Events.Count);
        Time.realtimeSinceStartup = 70; Allowed("exact-drop-window"); Time.realtimeSinceStartup = 70.001f; Allowed("after-drop-window");
        EntityAuthorityInterest.ShareQuotaForget(1); Allowed("forgot-player");
        var record = new AK.FullRecord { NetId = 0x12345678, Epoch = 0x87654321 };
        var full = new NetWriter(); AK.EncodeRecord(full, record); Row("full-empty-record", Convert.ToHexString(full.Buf.AsSpan(0, full.Len)));
        record.ItemState[0] = new byte[] { 1, 2, 3 }; record.ItemState[16384] = new byte[128];
        AK.EncodeRecord(full, record); Row("full-multiple-records", Convert.ToHexString(full.Buf.AsSpan(0, full.Len)));
        string path = "tools/unit/EntityAuthorityInterestRows.txt";
        GoldenRows.Update(path, rows);
        var expected = File.ReadAllLines(path);
        Check(expected.Length == rows.Count, "authority/interest expected row count");
        for (int i = 0; i < rows.Count; i++) Check(expected[i] == rows[i], "authority/interest expected row " + i + ": " + rows[i]);
        AuthorityInterestBoundaryChecks();
        Setup(); AM.Server = null;
    }

    static void AuthorityInterestBoundaryChecks()
    {
        CoreMembers.Register();
        var owner = typeof(EntityAuthorityInterest);
        Check(owner.IsAbstract && owner.IsSealed && !owner.IsPublic, "authority/interest has an internal static owner");
        foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly))
        {
            if (!(member is FieldInfo || member is PropertyInfo || member is MethodInfo) || typeof(Entities).GetMember(member.Name, Static).Length != 0) continue;
            foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." }) Check(Reflect.MemberPath(prefix + member.Name) == owner.FullName + "." + member.Name, "authority alias " + prefix + member.Name);
        }
        Check(BridgeReflect.Set("Entities.ShareRefillPerS", "2.5") == "2.5" && EntityAuthorityInterest.ShareRefillPerS == 2.5f, "quota alias reaches only backing field");
        Check(BridgeReflect.Set("Entities.StateInterestM", "1500") == "1500" && EntityAuthorityInterest.StateInterestM == 1500, "interest facade reaches only backing field");
        Check((string)typeof(Entities).GetMethod("InterestJson", Static).Invoke(null, null) == EntityAuthorityInterest.InterestJson() && (string)typeof(Entities).GetMethod("ShareQuotaStats", Static).Invoke(null, null) == EntityAuthorityInterest.ShareQuotaStats(), "compiled status facades reach owner");
        string[] allowed = { "SendToPeers", "DeliverInterestMessage", "RecordInterestSkip", "InterestCatchUpLimit", "MarkInterestStale", "TakeInterestStale", "CountInterestStale", "WriteInterestResync", "CatchUpInterestRecords", "HasInterestPoi", "TryInterestPoi" };
        foreach (string part in new[] { "RelayInterest", "ShareQuota" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Split("    public static partial class Entities")[0];
            string code = Regex.Replace(source, @"//[^\r\n]*", "");
            Check(!Regex.IsMatch(code, @"\b(?:ByNet|WS|SEnt|ServerPoiUsables)\b") && !code.Contains("using static"), part + " exposes no kernel maps records or writer");
            foreach (Match m in Regex.Matches(code, @"Entities\.(\w+)")) Check(m.Groups[1].Value == "PoiKey" || allowed.Contains(m.Groups[1].Value), "narrow authority dependency " + m.Value);
        }
        Check(!ReferenceEquals(Field(owner, "WFull"), Field(typeof(Entities), "W")), "catch-up owns its writer");
        Entities.MarkInterestStale(77, 7); Entities.MarkInterestStale(77, 7);
        Check(Entities.CountInterestStale() == 1 && Entities.TakeInterestStale(77, 7) && !Entities.TakeInterestStale(77, 7) && Entities.CountInterestStale() == 0, "kernel debt operations preserve set semantics");
        Entities.ClearServer(); ((IDictionary)Field(typeof(Entities), "_staleEnts")).Clear();
        var distance = typeof(Entities).GetMethod("NearInterestRecord", Static);
        foreach (double x in new[] { 0, 1500, 1500.01, double.NaN, double.PositiveInfinity })
        {
            var viewer = new Vector3d(x, 0, 0);
            bool near = (bool)distance.Invoke(null, new object[] { viewer, default(Vector3d) });
            bool before = (bool)AuthorityCall("Near", new AS.PeerSpot { Id = 1, HasPos = true, Pos = viewer }, 0d, 0d);
            Check(near == before, "record catch-up uses the original inclusive distance rule at " + x);
        }
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.Contains("if (!ShareAllowed(from, n)) return;") && kernel.Contains("if (IsHost) ShareQuotaTick();"), "share validation and tick guards retain call sites");
        string relay = File.ReadAllText("src/TLDRevamp/Net/Entities.RelayInterest.cs");
        Check(relay.Contains("foreach (var k in ServerPoiUsables.Keys) EntityAuthorityInterest.MarkPoiStale(player, k);"), "POI all-stale map iteration stays in kernel");
        Check(relay.Contains("EntityAuthorityInterest.ForgetRelay(player); _poseNear.Remove(player); _rabbitNear.Remove(player); _interestSkips.Remove(player);") && relay.Contains("EntityAuthorityInterest.ResetRelay(); _poseNear.Clear(); _rabbitNear.Clear(); _interestSkips.Clear();"), "leave and reset preserve exact cleanup order");
    }
}
