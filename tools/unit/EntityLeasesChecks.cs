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
using LK = LeaseFakes.Kernel;
using LM = LeaseFakes.Main;
using Mp = LeaseFakes.Transport;

static partial class EntitySplitTest
{
    static object LeaseCall(string name, params object[] args) => typeof(EntityWorldLeases).GetMethod(name, Static).Invoke(null, args);
    static void LeaseHook(string name, string phase, params object[] args) => typeof(EntityWorldLeases).GetNestedType(name, BindingFlags.NonPublic).GetMethod(phase, Static).Invoke(null, args);
    static void LeasesChecks()
    {
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        void State(string name)
        {
            var until = (IDictionary)Field(typeof(EntityWorldLeases), "LeaseUntil");
            Row(name, LeaseCall("LeaseTable") + ":until=" + string.Join(",", until.Keys.Cast<object>().Select(k => k + "@" + until[k]))
                + ":wire=" + string.Join("|", LK.Packets));
            LK.Packets.Clear();
        }
        LeaseFakes.Generator.poiClass Setup()
        {
            foreach (string n in new[] { "Leases", "LeaseTypes", "LeaseUntil", "LeaseRefusedWhy", "_leaseRetryAt", "DisabledBuiltIn" }) ((IDictionary)Field(typeof(EntityWorldLeases), n)).Clear();
            foreach (string n in new[] { "LeaseAsked", "_leasesDone" }) ((HashSet<string>)Field(typeof(EntityWorldLeases), n)).Clear();
            ((IList)Field(typeof(EntityWorldLeases), "PendingGrants")).Clear(); ((IList)Field(typeof(EntityWorldLeases), "Captures")).Clear();
            Set(typeof(EntityWorldLeases), "_captureKnown", null);
            foreach (string n in new[] { "BuiltInShared", "BuiltInDisabled", "BuiltInReEnabled", "LeasesDone", "LeasesFreed", "LeasesAsked", "LeasesGranted", "LeaseSpawns", "LeaseItemsShared", "HostItemsShared", "LeaseTypeMismatches" }) Set(typeof(EntityWorldLeases), n, 0L);
            EntityWorldLeases.LeaseTypeCheck = true; EntityWorldLeases.LeaseRetryS = 2;
            LM.s = new LM { Scene = 12 }; LM.s.player.transform.position = new Vector3(20, 0, 40);
            LeaseFakes.Menu.s = new LeaseFakes.Menu(); var gen = new LeaseFakes.Generator { chunkSize = 1000 }; var ch = new LeaseFakes.Chunk();
            var poi = new LeaseFakes.Generator.poiClass { posID = new Vector3d(20, 0, 40), id = 7, poiName = "site", pobj = new LeaseFakes.PoiObject() };
            poi.pobj.transform.position = new Vector3(20, 0, 40); ch.pois[1] = poi; gen.chunks[1] = ch; LeaseFakes.Menu.s.currentMainMap.poiGens.Add(gen);
            LeaseFakes.Database.s = new LeaseFakes.Database(); LeaseFakes.Database.s.Buildings[7] = new GameObject { name = "site" }; LeaseFakes.Database.s.Buildings[8] = new GameObject { name = "other" };
            Mp.Server = new LeaseFakes.Server(); Mp.Server.Positions[1] = new Vector3d(20, 0, 40); Mp.Server.Positions[2] = new Vector3d(20, 0, 40);
            LeaseFakes.Saved.s = new LeaseFakes.Saved(); LK.IsHost = false; LK.InSession = true; LK.Packets.Clear(); LK.Anomalies.Clear(); LK.Shared.Clear(); LK.Request = null; LK.Peer = null;
            Time.realtimeSinceStartup = 10;
            return poi;
        }
        var p = Setup();
        // Overlapping faults establish which refusal wins.
        LeaseCall("ServerLease", 99, "bad", -1, ""); State("not-ready-before-key");
        LeaseCall("ServerLease", 1, "bad", -1, ""); State("key-before-type");
        LeaseCall("ServerLease", 1, "90000,40", -1, ""); State("reach-before-type");
        LeaseCall("ServerLease", 1, "20,40", -1, ""); State("type-before-mismatch");
        LeaseCall("ServerLease", 1, "20,40", 8, ""); State("mismatch");
        LeaseCall("ServerLease", 1, "20,40", 7, "untrusted"); State("grant");
        Time.realtimeSinceStartup = 20; LeaseCall("ServerLease", 1, "20,40", 7, ""); State("repeat-no-renewal");
        LeaseCall("ServerLease", 2, "20,40", 8, ""); State("taken-holder-type");
        LeaseCall("ServerLeaseDone", 2, "20,40"); State("wrong-holder");
        Time.realtimeSinceStartup = 70; LeaseCall("ServerLeaseDone", 1, "20,40"); State("expiry-before-done");
        Setup(); LeaseCall("ServerLease", 1, "20,40", 7, ""); LeaseCall("ServerLeaseLeave", 1); State("leave-before-capture");
        LeaseCall("ServerLease", 2, "20,40", 7, ""); LeaseCall("ServerLeaseDone", 2, "20,40"); LeaseCall("ServerLeaseLeave", 2); State("leave-after-capture");
        Setup(); LeaseCall("ServerLease", 1, "20,40", 7, ""); Mp.Server.Positions.Remove(1); LeaseCall("ServerLeaseExpiry"); State("unready-holder");
        foreach (string key in new[] { "20,40", "20.0,40", "NaN,40", "100000001,0", "-20.5,40.25", "0,0", "-0,0" })
        { object[] a = { key, default(Vector3d) }; Row("key-" + key, typeof(EntityWorldLeases).GetMethod("LeasePosition", Static).Invoke(null, a)); }
        foreach (float size in new[] { 0, -1, float.NaN, float.PositiveInfinity, 1000, 2000 })
        { LeaseFakes.Menu.s.currentMainMap.poiGens[0].chunkSize = size; Row("reach-" + size, LeaseCall("LeaseReachM")); }
        Setup(); var w = new NetWriter(); w.Str("20,40"); w.I32(7); w.Str("untrusted");
        for (int i = 0; i <= w.Len + 1; i++)
        { LeaseCall("ServerLeaseRequest", 1, new NetReader { Buf = w.Buf, End = i }); State("request-cut-" + i); }
        var d = new NetWriter(); d.Str("20,40");
        foreach (int i in new[] { 0, d.Len - 1, d.Len + 1, d.Len }) { LeaseCall("ServerLeaseDoneRequest", 1, new NetReader { Buf = d.Buf, End = i }); State("done-cut-" + i); }
        p = Setup(); Row("gate", typeof(EntityWorldLeases).GetNestedType("LeaseGate", BindingFlags.NonPublic).GetMethod("Prefix", Static).Invoke(null, new object[] { p })); State("request-type-tail");
        p.spawnedItems = p.spawnedFixed = false; EntityWorldLeases.LeaseTypeCheck = false; ((HashSet<string>)Field(typeof(EntityWorldLeases), "LeaseAsked")).Clear(); LeaseHook("LeaseGate", "Prefix", p); State("request-legacy");
        var built = new tosaveitemscript { idInSave = 1 }; p.pobj.Items.Add(built);
        LeaseCall("OnLeaseTaken", "20,40", 8, "other"); Row("disabled", built.gameObject.activeSelf + ":" + EntityWorldLeases.BuiltInDisabled + ":" + EntityWorldLeases.LeaseTypeMismatches);
        LeaseCall("OnLeaseFreed", "20,40"); Row("freed-local", built.gameObject.activeSelf + ":" + p.spawnedItems + ":" + p.spawnedFixed + ":" + EntityWorldLeases.BuiltInReEnabled);
        Time.realtimeSinceStartup = 11.999f; LeaseHook("LeaseGate", "Prefix", p); State("retry-before");
        Time.realtimeSinceStartup = 12; LeaseHook("LeaseGate", "Prefix", p); State("retry-at");
        p = Setup(); p.pobj = null; LeaseCall("OnLeaseGrant", "20,40"); Row("pending", ((IList)Field(typeof(EntityWorldLeases), "PendingGrants")).Count);
        p.pobj = new LeaseFakes.PoiObject(); p.pobj.transform.position = new Vector3(20, 0, 40);
        var fresh = new tosaveitemscript { idInSave = 2 }; fresh.transform.position = new Vector3(20, 0, 40);
        p.pobj.Spawn = _ => { LeaseFakes.Saved.s.items[2] = fresh; Row("spawn-guard", Field(typeof(EntityWorldLeases), "_leaseSpawning")); };
        LeaseCall("LeaseTick"); Row("captured", EntityWorldLeases.LeaseSpawns + ":" + EntityWorldLeases.LeaseItemsShared + ":" + LK.Shared.Count + ":" + ((IList)Field(typeof(EntityWorldLeases), "PendingGrants")).Count);
        Time.realtimeSinceStartup = 14; LeaseCall("CaptureTick"); State("capture-exact-end");
        Time.realtimeSinceStartup = 14.001f; LeaseCall("CaptureTick"); State("capture-after-end"); Row("known-released", Field(typeof(EntityWorldLeases), "_captureKnown") == null);
        // A second grant must not restart the first window or forget its newly spawned items.
        p = Setup(); LeaseCall("OnLeaseGrant", "20,40");
        var p2 = new LeaseFakes.Generator.poiClass { posID = new Vector3d(55, 0, 40), id = 7, pobj = new LeaseFakes.PoiObject() };
        LeaseFakes.Menu.s.currentMainMap.poiGens[0].chunks[1].pois[2] = p2;
        Time.realtimeSinceStartup = 11; LeaseCall("OnLeaseGrant", "55,40");
        Time.realtimeSinceStartup = 14.001f; LeaseCall("CaptureTick"); State("first-of-two-done");
        Row("second-window", ((IList)Field(typeof(EntityWorldLeases), "Captures")).Count);
        LeaseCall("OnLeaseFreed", "55,40"); Row("freed-capture-known", Field(typeof(EntityWorldLeases), "_captureKnown") == null);
        // File operations stay under the test's isolated build output.
        string dir = Path.Combine(AppContext.BaseDirectory, "lease-worlds"); Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        p = Setup(); string first = Path.Combine(dir, "first.tlds"), second = Path.Combine(dir, "second.tlds");
        File.WriteAllLines(first + ".leases", new[] { "20,40\t1\t0", "bad\t0\t1", "55,40\t2\t1" });
        LeaseHook("WorldFromReadSave", "Postfix", first); Row("read-world", LeaseCall("get_WorldSaveFile") == (object)first);
        LeaseCall("LoadLeases"); State("load-legacy-done");
        typeof(EntityWorldLeases).GetMethod("SaveLeases", Static, null, new[] { typeof(string) }, null).Invoke(null, new object[] { second });
        Row("saved-lines", string.Join("|", File.ReadAllLines(second + ".leases")));
        LeaseHook("WorldReset", "Postfix"); LM.s.Scene++; Row("new-world", LeaseCall("get_WorldSaveFile") == null);
        Row("new-world-lines", ((IList)Field(typeof(EntityWorldLeases), "_worldLines")).Count);
        var expected = File.ReadAllLines("tools/unit/EntityLeasesRows.txt"); Check(expected.Length == rows.Count, "lease HEAD row count");
        for (int i = 0; i < rows.Count; i++) Check(rows[i] == expected[i], "lease HEAD row " + i + ": " + rows[i]);
        p = Setup();
        p.pobj.Spawn = _ => throw new InvalidOperationException("spawn failed");
        bool threw = false;
        try { LeaseCall("OnLeaseGrant", "20,40"); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { threw = true; }
        Check(threw && !(bool)Field(typeof(EntityWorldLeases), "_leaseSpawning") && EntityWorldLeases.LeaseSpawns == 0, "failed game spawn unwinds lease guard without counting a spawn");
        p = Setup();
        p.pobj.Spawn = poi => Check((bool)typeof(EntityWorldLeases).GetNestedType("LeaseGate", BindingFlags.NonPublic).GetMethod("Prefix", Static).Invoke(null, new object[] { poi }), "inner spawn passes lease gate");
        LK.Request = writer =>
        {
            var r = new NetReader { Buf = writer.Buf.Take(writer.Len).ToArray(), End = writer.Len }; byte kind = r.U8();
            if (kind == 28) LeaseCall("ServerLeaseRequest", 0, r); else if (kind == 33) LeaseCall("ServerLeaseDoneRequest", 0, r);
        };
        LK.Peer = (peer, bytes) => { var r = new NetReader { Buf = bytes, End = bytes.Length }; if (r.U8() == 29) LeaseCall("OnLeaseGrant", r.Str()); };
        LeaseHook("LeaseGate", "Prefix", p);
        Check(LK.Packets.Count == 2 && LK.Packets[0].EndsWith("1C0632302C3430070000000573697465") && LK.Packets[1].EndsWith("1D0632302C3430"), "synchronous host grant retains captured request before reply writer reuse");
        Time.realtimeSinceStartup = 14.001f; LeaseCall("CaptureTick");
        Check(((HashSet<string>)Field(typeof(EntityWorldLeases), "_leasesDone")).Contains("20,40") && EntityWorldLeases.LeasesDone == 1, "synchronous capture completion reaches done validation once");
        LK.Request = null; LK.Peer = null;
        LeaseBoundaryChecks();
        Mp.Server = null;
    }

    static void LeaseBoundaryChecks()
    {
        CoreMembers.Register();
        var owner = typeof(EntityWorldLeases);
        Check(owner.IsAbstract && owner.IsSealed && !owner.IsPublic, "world leases have an internal static owner");
        foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly))
        {
            if (!(member is FieldInfo || member is PropertyInfo || member is MethodInfo) || typeof(Entities).GetMember(member.Name, Static).Length != 0) continue;
            foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                Check(Reflect.MemberPath(prefix + member.Name) == owner.FullName + "." + member.Name, "lease alias " + prefix + member.Name);
        }
        Check(BridgeReflect.Set("Entities.LeaseRetryS", "3.5") == "3.5" && EntityWorldLeases.LeaseRetryS == 3.5f, "lease setting alias writes its only backing field");
        EntityWorldLeases.LeaseRetryS = 2;
        Check(!ReferenceEquals(Field(owner, "W"), Field(typeof(Entities), "W")), "leases own a writer");
        Check(Entities.LeasesDone == EntityWorldLeases.LeasesDone && Entities.LeaseTable() == EntityWorldLeases.LeaseTable(), "compiled facade reads lease owner");
        string leases = File.ReadAllText("src/TLDRevamp/Net/Entities.Leases.cs");
        string feature = leases.Substring(0, leases.IndexOf("    public static partial class Entities"));
        string validation = File.ReadAllText("src/TLDRevamp/Net/Entities.LeaseValidation.cs");
        string code = Regex.Replace(feature + validation, @"//[^\r\n]*", "");
        Check(!Regex.IsMatch(code, @"\b(ByNet|WS|SEnt)\b") && !code.Contains("using static") && !code.Contains("Mp.Server"), "leases cannot read kernel maps writers or server records");
        string[] allowed = { "InSession", "IsHost", "LeaseReq", "LeaseDone", "LeaseGrant", "LeaseTaken", "LeaseFreed", "SendLeaseRequest", "SendToPeer", "SendToPeers", "IsFinite", "LeaseAnomaly", "TryLeasePlayerPosition", "ShareLeaseRoots", "DisableLeaseBuiltIns" };
        foreach (Match call in Regex.Matches(code, @"Entities\.(\w+)")) Check(allowed.Contains(call.Groups[1].Value), "narrow lease dependency " + call.Value);
        Check(!owner.GetNestedType("SaveLeasesWithWorld", BindingFlags.NonPublic).GetMethod("Postfix", Static).GetCustomAttributes(typeof(HarmonyLib.HarmonyPriority), false).Any(), "lease save retains normal priority between tuning and player-record postfixes");
        Check(feature.Contains("[HarmonyLib.HarmonyPrefix, HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]"), "lease rotation follows autosave blockers");
        foreach (string name in new[] { "PickupClaim", "CarCopyNoLift", "HandCopyTick", "GrabRefusedTick", "ShareRoots", "AttachedToItem" })
            Check(!feature.Contains("class " + name) && !Regex.IsMatch(feature, @"\b" + name + @"\("), name + " stays with kernel authority");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.IndexOf("EntityWorldLeases.RestoreBuiltIns();") < kernel.IndexOf("HitchReset(wasHost);") && kernel.IndexOf("HitchReset(wasHost);") < kernel.IndexOf("EntityWorldLeases.ResetLeaseSession();"), "built-in restoration and lease clear keep reset slots");
        Check(kernel.Contains("ResetPoiCensus(); EntityWorldLeases.ResetLeaseSession();") && kernel.Replace("\r\n", "\n").Contains("EntityWorldLeases.LeaseTick();\n            LiveUsableTick();"), "lease reset and tick neighbors are unchanged");
        EntityWorldLeases.LeaseTypeMismatches = 19; EntityWorldLeases.LeaseRetryS = 7;
        Set(owner, "_leaseSpawning", true);
        long done = EntityWorldLeases.LeasesDone, mismatches = EntityWorldLeases.LeaseTypeMismatches;
        object worldFile = Field(owner, "_worldFile"), worldLines = Field(owner, "_worldLines");
        EntityWorldLeases.ResetLeaseSession();
        Check(((IDictionary)Field(owner, "Leases")).Count == 0 && EntityWorldLeases.LeasesDone == done && EntityWorldLeases.LeaseTypeMismatches == mismatches && EntityWorldLeases.LeaseRetryS == 7 && (bool)Field(owner, "_leaseSpawning") && Equals(worldFile, Field(owner, "_worldFile")) && ReferenceEquals(worldLines, Field(owner, "_worldLines")), "session reset keeps counters settings spawning guard and world state");
        EntityWorldLeases.ResetLeaseCounters(); Check(EntityWorldLeases.LeasesDone == 0 && EntityWorldLeases.LeaseTypeMismatches == mismatches, "counter reset keeps historical mismatch omission");
        Set(owner, "_leaseSpawning", false); EntityWorldLeases.LeaseRetryS = 2; EntityWorldLeases.LeaseTypeMismatches = 0;
    }
}
