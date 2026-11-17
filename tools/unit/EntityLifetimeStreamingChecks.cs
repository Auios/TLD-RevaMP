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
using LF = LifetimeStreamingFakes;
using LK = LifetimeStreamingFakes.Kernel;

static partial class EntitySplitTest
{
    static object LifetimeHook(string type, string method, params object[] args) => typeof(EntityLifetimeStreaming).GetNestedType(type, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);
    static void LifetimeStreamingChecks()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        string Pose(Entities.Ent e) => e.FarPos.x + ":" + e.FarPos.y + ":" + e.FarPos.z + ":" + e.FarDirty;
        var owner = typeof(EntityLifetimeStreaming);
        var e = new Entities.Ent { FarPos = new Vector3d(1, 2, 3), FarRot = Quaternion.identity };
        EntityLifetimeStreaming.UpdateFarRecord(e, e.FarPos, e.FarRot); Row("same-pose", Pose(e));
        EntityLifetimeStreaming.UpdateFarRecord(e, new Vector3d(1.01, 2, 3), e.FarRot); Row("changed-position", Pose(e));
        e.FarDirty = false; EntityLifetimeStreaming.UpdateFarRecord(e, e.FarPos, new Quaternion(0, .70710677f, 0, .70710677f)); Row("changed-rotation", Pose(e));
        e.FarDirty = true; EntityLifetimeStreaming.UpdateFarRecord(e, e.FarPos, e.FarRot); Row("same-keeps-dirty", e.FarDirty);
        foreach (bool proxy in new[] { false, true })
        { LK.Proxy = proxy; LK.Suppressed = 0; Row("falloff-" + proxy, LifetimeHook("NoProxyFallOff", "Prefix", new attachablescript()) + ":" + LK.Suppressed); }
        LF.Placer.s = null; LF.Menu.s = null; Row("far-no-game", EntityLifetimeStreaming.FarFromHere(default));
        LF.Placer.s = new LF.Placer { itemRemoveDist = 10 }; LF.Menu.s = new LF.Menu(); Row("far-no-map", EntityLifetimeStreaming.FarFromHere(default));
        LF.Menu.s.currentMainMap = new LF.Menu.Map(); Row("far-null-centers", EntityLifetimeStreaming.FarFromHere(default));
        LF.Menu.s.currentMainMap.genArounds = new List<LF.Menu.Center>(); Row("far-empty-centers", EntityLifetimeStreaming.FarFromHere(default));
        LF.Menu.s.currentMainMap.genArounds.Add(new LF.Menu.Center());
        foreach (double x in new[] { 0, 9.99, 10, 10.01, double.NaN, double.PositiveInfinity }) Row("far-distance-" + x, EntityLifetimeStreaming.FarFromHere(new Vector3d(x, 50000, 0)));
        LF.Menu.s.currentMainMap.genArounds.Add(new LF.Menu.Center { upos = new Vector3(20, 0, 0) }); Row("far-second-center", EntityLifetimeStreaming.FarFromHere(new Vector3d(20, 0, 0)));
        itemdatabase.s = null; Row("prefab-no-database", EntityLifetimeStreaming.PrefabName(42));
        itemdatabase.s = new itemdatabase(); Row("prefab-missing", EntityLifetimeStreaming.PrefabName(42));
        itemdatabase.s.Prefab = new GameObject { name = "test-item" }; Row("prefab-present", EntityLifetimeStreaming.PrefabName(42));

        LF.Save.s = new LF.Save(); LF.Save.s.data.itemData.items = new List<int> { 7, 8 };
        // Object lists let the real reflection loop see both id-bearing rows and nulls.
        LF.Save.s.data.itemData.partconditions = new List<object> { new LF.Row(7, "old"), null, new LF.Row(8, "other") };
        LF.Save.s.data.itemData.attachable = new List<save_attachable> { new save_attachable(null, 0) { id = 7 } };
        var changes = new itemDataClass { partconditions = new List<object> { new LF.Row(7, "new") }, attachable = new List<save_attachable>() };
        e.ItemIds = new List<uint> { 7, 0, 99 };
        EntityLifetimeStreaming.FarResyncItems = 0;
        string Store() => string.Join("|", LF.Save.s.data.itemData.partconditions.Select(x => x is LF.Row r ? r.id + ":" + r.value : "null")) + ":attach=" + LF.Save.s.data.itemData.attachable.Count + ":count=" + EntityLifetimeStreaming.FarResyncItems;
        foreach (int idx in new[] { -1, 1, 2, 3 }) { EntityLifetimeStreaming.FarResync(e, idx, changes); Row("resync-invalid-" + idx, Store()); }
        EntityLifetimeStreaming.FarResync(e, 0, changes); Row("resync-replaces-one-id", Store());
        changes.partconditions.Clear(); EntityLifetimeStreaming.FarResync(e, 0, changes); Row("resync-empty-clears", Store());
        LF.Save.s.items[7] = new tosaveitemscript(); EntityLifetimeStreaming.RemoveRecord(7); Row("record-removed", LF.Save.s.items.Count + ":" + string.Join(",", LF.Save.s.data.itemData.items));
        LF.Save.s = null; EntityLifetimeStreaming.RemoveRecord(7); Row("remove-without-save", "returned");

        var item = new tosaveitemscript(); item.Children.Add(item);
        Row("save-not-ready", EntityLifetimeStreaming.SaveReady(item)); item.useThisForDistance = new object(); Row("save-ready", EntityLifetimeStreaming.SaveReady(item));
        EntityLifetimeStreaming.ClearPlayerDeleted(); var cheat = new cheatedStuff { ChildComponent = item };
        LK.LifetimeInSession = false; LifetimeHook("PlayerDelete", "Prefix", cheat); Row("delete-outside-session", EntityLifetimeStreaming.TakePlayerDeleted(item));
        LK.LifetimeInSession = true; LifetimeHook("PlayerDelete", "Prefix", (object)null); LifetimeHook("PlayerDelete", "Prefix", new cheatedStuff());
        LifetimeHook("PlayerDelete", "Prefix", cheat); Row("delete-once", EntityLifetimeStreaming.TakePlayerDeleted(item) + ":" + EntityLifetimeStreaming.TakePlayerDeleted(item));
        LifetimeHook("PlayerDelete", "Prefix", cheat); EntityLifetimeStreaming.ClearPlayerDeleted(); Row("delete-reset", EntityLifetimeStreaming.TakePlayerDeleted(item));

        var player = new fpscontroller(); mainscript.s = new mainscript { player = player };
        Set(owner, "_playerAction", 0); ((IList)Field(owner, "_playerMade")).Clear(); EntityLifetimeStreaming.PlayerMadeShared = 0; LK.Shared.Clear(); LK.LifetimeSpawnCount = 0;
        void ActionStart(fpscontroller p) => LifetimeHook("PlayerActions", "Prefix", p);
        void ActionEnd(fpscontroller p) => LifetimeHook("PlayerActions", "Finalizer", p);
        void New(tosaveitemscript it, bool started) => LifetimeHook("NewItemInAction", "Postfix", it, started);
        string Made() => Field(owner, "_playerAction") + ":" + ((IList)Field(owner, "_playerMade")).Count + ":" + LK.Shared.Count + ":" + EntityLifetimeStreaming.PlayerMadeShared;
        ActionStart(new fpscontroller()); ActionEnd(player); Row("action-foreign-empty", Made());
        ActionStart(player); ActionStart(player); item.started = true; New(item, false); New(item, true); ActionEnd(player); Row("action-nested-defers", Made());
        LK.DuringShare = go => Row("queue-cleared-before-share", Made()); ActionEnd(player); LK.DuringShare = null; Row("action-outer-shares", Made());
        LK.LifetimeInSession = false; ActionStart(player); New(item, false); ActionEnd(player); Row("action-outside-session", Made()); LK.LifetimeInSession = true;
        ActionStart(player); var bad = new tosaveitemscript { Go = new GameObject { Destroyed = true }, started = true }; New(bad, false); ActionEnd(player); Row("action-no-share-count", Made());
        item.started = false; object[] startedArgs = { item, true }; LifetimeHook("NewItemInAction", "Prefix", startedArgs); Row("new-item-prefix-state", startedArgs[1]);

        foreach (int idx in new[] { 0, 127, 128, 16384, int.MaxValue })
        { var writer = new NetWriter(); writer.U8(99); EntityLifetimeStreaming.WriteMemberGone(writer, 0x12345678, idx); Row("member-gone-" + idx, Convert.ToHexString(writer.Buf.AsSpan(0, writer.Len))); }
        var detached = new attachablescript { attached = true }; Row("unmount-loose", EntityLifetimeStreaming.Unmount(detached, false) + ":" + detached.attached);
        detached.attached = true; detached.slot = new partslotscript { Part = detached }; Row("unmount-slotted", EntityLifetimeStreaming.Unmount(detached, true) + ":" + detached.attached);
        detached.slot.Part = new attachablescript(); Row("unmount-slot-mismatch", EntityLifetimeStreaming.Unmount(detached, false) + ":" + detached.attached);

        LF.TrailerGrip.All.Clear(); LK.Copies.Clear(); ((IList)Field(owner, "_linkedHeld")).Clear(); Set(owner, "_linkedAt", 0f);
        EntityLifetimeStreaming.LinkedGroups = EntityLifetimeStreaming.LinkedHeld = 0; EntityLifetimeStreaming.LinkedLastHoldMs = 0;
        EntityLifetimeStreaming.LinkedDelayEveryS = .2f; EntityLifetimeStreaming.LinkedHoldMaxS = .5f;
        var tow = new Transform(); var trailer = new tosaveitemscript(); trailer.attachable = new attachablescript { attached = true, point = tow };
        LF.TrailerGrip.All.Add(new LF.TrailerGrip.Grip { tosaveitem = trailer });
        var a = new Entities.Ent { Ip = new PoseInterpolator { Ready = true, Target = 5 } }; var b = new Entities.Ent { Ip = new PoseInterpolator { Ready = true, Target = 6 } };
        LK.Copies[tow] = a; LK.Copies[trailer.transform] = b;
        string Linked() => a.Ip.Hold + ":" + b.Ip.Hold + ":" + EntityLifetimeStreaming.LinkedGroups + ":" + EntityLifetimeStreaming.LinkedHeld + ":" + EntityLifetimeStreaming.LinkedLastHoldMs;
        Time.time = .199f; EntityLifetimeStreaming.LinkedDelayTick(); Row("linked-before-interval", Linked());
        Time.time = .2f; EntityLifetimeStreaming.LinkedDelayTick(); Row("linked-at-interval-cap", Linked());
        a.Ip.Target = 6.1; b.Ip.Target = 6; Time.time = .4f; EntityLifetimeStreaming.LinkedDelayTick(); Row("linked-holds-reassigned", Linked());
        LF.TrailerGrip.All.Clear(); Time.time = .6f; EntityLifetimeStreaming.LinkedDelayTick(); Row("linked-unhitched-clears", Linked());
        // A chain and a carried car all share the oldest ready copy's display moment.
        LF.TrailerGrip.All.Add(new LF.TrailerGrip.Grip { tosaveitem = trailer });
        var tail = new tosaveitemscript { attachable = new attachablescript { attached = true, point = trailer.transform } };
        LF.TrailerGrip.All.Add(new LF.TrailerGrip.Grip { tosaveitem = tail });
        var cargo = new tosaveitemscript(); var mount = new mountStuff(); mount.storedcars.Add(cargo); trailer.transform.Children.Add(mount);
        var c = new Entities.Ent { Ip = new PoseInterpolator { Ready = true, Target = 5.8 } }; var d = new Entities.Ent { Ip = new PoseInterpolator { Ready = true, Target = 6.2 } };
        LK.Copies[tail.transform] = c; LK.Copies[cargo.transform] = d; Time.time = 1; EntityLifetimeStreaming.LinkedDelayTick(); Row("linked-chain-cargo", Linked() + ":" + c.Ip.Hold + ":" + d.Ip.Hold);
        c.Ip.Ready = d.Ip.Ready = false; b.Ip.Ready = false; Time.time = 1.3f; EntityLifetimeStreaming.LinkedDelayTick(); Row("linked-one-ready", Linked());

        string path = "tools/unit/EntityLifetimeStreamingRows.txt";
        GoldenRows.UpdateText(path, string.Join("\r\n", rows) + "\r\n");
        { var expected = File.ReadAllLines(path); Check(expected.Length == rows.Count, "lifetime expected row count"); for (int i = 0; i < rows.Count; i++) Check(expected[i] == rows[i], "lifetime expected row " + i + ": " + rows[i]); }
        LifetimeBoundaryChecks();
        EntityLifetimeStreaming.FarResyncItems = 0;
        CultureInfo.CurrentCulture = oldCulture;
        mainscript.s = null; itemdatabase.s = null; LF.Save.s = null; LF.Menu.s = null; LF.Placer.s = null; LF.TrailerGrip.All.Clear(); LK.Copies.Clear();
    }

    static void LifetimeBoundaryChecks()
    {
        var owner = typeof(EntityLifetimeStreaming);
        Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, "lifetime has an internal static owner");
        Check(BridgeReflect.Set("EntityLifetimeStreaming.LinkedHoldMaxS", "0.25") == "0.25" && EntityLifetimeStreaming.LinkedHoldMaxS == .25f, "linked setting owner path writes owner");
        Check(BridgeReflect.Set("TLDRevamp.Net.EntityLifetimeStreaming.PlayerMadeShared", "123") == "123" && EntityLifetimeStreaming.PlayerMadeShared == 123, "player-made full owner path writes owner");
        string[] allowed = { "Ent", "MemberGone", "LifetimeInSession", "LifetimeSpawnCount", "ShareLifetimeSpawn", "LifetimePartIsProxy", "RecordLifetimeFallOff", "ProxyForLinkedDelay" };
        foreach (string part in new[] { "FallOff", "FarPose", "FarStore", "Lifecycle", "LinkedDelay", "MemberGone", "PlayerMade", "Removal" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            string feature = source.Split("    public static partial class Entities")[0]; string code = Regex.Replace(feature, @"//[^\r\n]*", "");
            Check(!Regex.IsMatch(code, @"\b(?:ByNet|Server|SEnt|WS)\b") && !code.Contains("using static") && !Regex.IsMatch(code, @"Entities\.W\b"), part + " exposes no kernel tables records or writer");
            foreach (Match m in Regex.Matches(code, @"Entities\.(\w+)")) Check(allowed.Contains(m.Groups[1].Value), "narrow lifetime dependency " + m.Value);
        }
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.Contains("EntityLifetimeStreaming.ClearPlayerDeleted();") && kernel.IndexOf("EntityLifetimeStreaming.ClearPlayerDeleted();") < kernel.IndexOf("ClockReset();"), "deletion markers reset in the original slot");
        Check(File.ReadAllText("src/TLDRevamp/Net/Entities.Physical.cs").Contains("EntityLifetimeStreaming.LinkedDelayTick();"), "fixed tick retains linked delay call site");
        Check(kernel.Contains("if (wasHost) SetProxy(e, false);") && kernel.Contains("UnityEngine.Object.Destroy(e.Root.gameObject);"), "host promotion and joiner cleanup remain kernel decisions");
        string memberSource = File.ReadAllText("src/TLDRevamp/Net/Entities.MemberGone.cs");
        Check(memberSource.Contains("private static void ServerMemberGone(int from, NetReader r)") && memberSource.Contains("private static void ApplyMemberGone(NetReader r)"), "member removal handlers retain kernel authority");
    }
}
