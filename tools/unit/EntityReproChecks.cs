using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static Type ReproOwner = typeof(EntityReproHooks);
    static object ReproCall(string name, params object[] args) => ReproOwner.GetMethod(name, Static).Invoke(null, args);
    static object ReproPatch(string name, string method, params object[] args) => ReproOwner.GetNestedType(name, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);
    static void ReproChecks()
    {
        ReproSourceChecks();
        Entities.ByNet.Clear(); Entities.MyId = 1; Entities.PlayerActing = true;
        mainscript.s = new mainscript(); var p = mainscript.s.player = new fpscontroller();
        var it = new tosaveitemscript { id = 12 }; var parent = new tosaveitemscript { id = 13 };
        var e = new Entities.Ent { NetId = 7, Root = it, OwnerId = 1 }; e.Items.Add(it); Entities.ByNet[7] = e;
        var c = new Entities.Ent { NetId = 8, Root = parent, OwnerId = 1 }; c.Items.Add(parent); Entities.ByNet[8] = c;
        var pick = p.pickedUp = new pickupable { tosave = it }; p.inHandP = pick; p.inventory.Add(pick);
        var at = it.attachable = new attachablescript { tosave = it };
        var mount = new mountStuff { parentTosave = parent }; parent.Children.Add(mount);
        it.transform.position = new Vector3(1, 2, 3); at.transform.position = new Vector3(4, 5, 6); at.transform.eulerAngles = new Vector3(7, 8, 9);
        ReproRecorder.Rows.Clear(); ReproRecorder.Recording = true; DevHooks.Playing = false; ReproCall("ReproReset");
        ReproRecorder.Add((ReproRow)ReproCall("ReproBinding", 7u));
        ReproPatch("ReproPickup", "Postfix", p, pick);
        Check((bool)ReproPatch("ReproDrop", "Prefix", p, false), "ordinary drop permits original");
        p.throwForce = 1.02f; ReproPatch("ReproDrop", "Prefix", p, false);
        DevHooks.PreventDrop = (player, destroyed) => true;
        Check(!(bool)ReproPatch("ReproDrop", "Prefix", p, false), "Dev drop veto still suppresses original"); DevHooks.PreventDrop = null;
        int n = ReproRecorder.Rows.Count;
        ReproPatch("ReproDrop", "Prefix", p, true);
        Check(ReproRecorder.Rows.Count == n, "destroyed drop does not record");
        foreach (string method in new[] { "InvSwitchTo", "InvSwapTo", "InvStore", "InvUnStore" })
        {
            object[] outer = { p, 0, typeof(fpscontroller).GetMethod(method), null };
            ReproPatch("ReproInventory", "Prefix", outer); n = ReproRecorder.Rows.Count;
            object[] inner = { p, 0, typeof(fpscontroller).GetMethod(method), null };
            ReproPatch("ReproInventory", "Prefix", inner);
            ReproPatch("ReproPickup", "Postfix", p, pick); ReproPatch("ReproDrop", "Prefix", p, false);
            ReproPatch("ReproGrip", "Prefix", p); ReproPatch("ReproRelease", "Prefix", p);
            var error = new Exception("inventory");
            Check(ReferenceEquals(ReproPatch("ReproInventory", "Finalizer", error, inner[3]), error), "inventory finalizer returns original exception");
            ReproPatch("ReproInventory", "Finalizer", null, outer[3]);
            Check(ReproRecorder.Rows.Count == n && (int)Field(ReproOwner, "_reproInventoryDepth") == 0, "nested inventory suppresses hand rows and unwinds");
        }
        ReproPatch("ReproGrip", "Prefix", p); ReproPatch("ReproRelease", "Prefix", p);
        var u = new usablescript { ParentComponent = it, slideValue = .5f, xRot = 1, yRot = 2, currentTurnState = 3, tunedFloat = .25f };
        it.Children.Add(u); it.usables.Add(u); n = ReproRecorder.Rows.Count;
        foreach (string guard in new[] { "_applyingLive", "_applyingState", "_applyingPoi" })
        {
            var owner = guard == "_applyingPoi" ? typeof(EntityUsables) : typeof(Entities);
            Set(owner, guard, true); ReproPatch("ReproUsable", "Postfix", u, true); Set(owner, guard, false);
        }
        ReproPatch("ReproUsable", "Postfix", u, false); ReproCall("ReproFlush");
        Check(ReproRecorder.Rows.Count == n, "usable replay and sync false guards");
        ReproPatch("ReproUsable", "Postfix", u, true); u.slideValue = .75f; ReproPatch("ReproUsable", "Postfix", u, true);
        ReproCall("ReproFlush"); ReproCall("ReproFlush");
        Check(ReproRecorder.Rows.Count == n + 1 && ReproRecorder.Rows.Last().X == .75, "usable coalesces latest state and flushes once");
        ReproCall("ReproClear");
        for (int i = 0; i < 33; i++) ReproPatch("ReproUsable", "Postfix", new usablescript { setidPoi = true, poigenid = 2, usableid = i, poiid = new Vector3d(3, 4, 5), slideValue = i }, true);
        Check((int)Field(ReproOwner, "_reproUsed") == 32 && ((Array)Field(ReproOwner, "ReproUsables")).Length == 32, "usable table retains its 32 slot reuse behavior");
        ReproCall("ReproFlush");
        it.car = new carscript { ignitionUsable = u }; p.lookedUsable = u; Time.time = 10; u.lastHoldTime = 10;
        ReproPatch("ReproKey", "Postfix", p); ReproPatch("ReproKey", "Postfix", p);
        ReproCall("ReproFinish");
        Check((uint)Field(ReproOwner, "_reproKey") == 0, "finish releases held ignition after usable flush");
        ReproCall("ReproAttach", 7u, 8u, 0, 1); e.Proxy = true; ReproCall("ReproAttach", 7u, 8u, 0, 1); e.Proxy = false;
        object[] detach = { at, null }; object[] uncraft = { new partslotscript { Part = at }, null };
        Time.frameCount = 20;
        ReproPatch("ReproDetach", "Prefix", detach); ReproPatch("ReproUncraft", "Prefix", uncraft);
        n = ReproRecorder.Rows.Count; ReproPatch("ReproDetach", "Postfix", at, detach[1], false); ReproPatch("ReproUncraft", "Postfix", uncraft[1], false);
        Check(ReproRecorder.Rows.Count == n, "suppressed detach and failed uncraft do not record");
        ReproPatch("ReproDetach", "Postfix", at, detach[1], true); ReproPatch("ReproUncraft", "Postfix", uncraft[1], true);
        Check(ReproRecorder.Rows.Count == n + 1, "same frame item and net detach recorded once");
        Time.frameCount++; ReproPatch("ReproUncraft", "Postfix", uncraft[1], true);
        foreach (string guard in new[] { "_applyingDetach", "_applyingState", "_hitchApplying" })
        {
            Set(guard == "_hitchApplying" ? typeof(EntityHitches) : typeof(Entities), guard, true); object[] guarded = { at, null }; ReproPatch("ReproDetach", "Prefix", guarded);
            Check(guarded[1] == null, "detach guard " + guard); Set(guard == "_hitchApplying" ? typeof(EntityHitches) : typeof(Entities), guard, false);
        }
        pick.physlock = mount; ReproPatch("ReproLock", "Postfix", mount, pick, false, true); ReproPatch("ReproUnlock", "Prefix", pick);
        n = ReproRecorder.Rows.Count; e.Proxy = true; ReproPatch("ReproLock", "Postfix", mount, pick, false, true); ReproPatch("ReproUnlock", "Prefix", pick);
        e.Proxy = false; e.OwnerId = 2; ReproPatch("ReproLock", "Postfix", mount, pick, false, true); ReproPatch("ReproUnlock", "Prefix", pick); e.OwnerId = 1;
        Check(ReproRecorder.Rows.Count == n, "lock and unlock require local owner");
        var car = it.car; car.isPlayerDriving = true; car.ithrottle = .75f; car.ibrake = .25f; car.isteer = -.5f;
        car.iclutch = .125f; car.ihandbrake = .5f; car.iignition = car.ihorn = true;
        Time.realtimeSinceStartup = 10; ReproPatch("ReproDriveInput", "Postfix", car); n = ReproRecorder.Rows.Count;
        ReproPatch("ReproDriveInput", "Postfix", car); Check(ReproRecorder.Rows.Count == n, "drive unchanged sample waits a tenth second");
        car.iLightsBDown = true; ReproPatch("ReproDriveInput", "Postfix", car); car.iLightsBDown = false;
        ReproCall("ReproReset"); Check((float)Field(ReproOwner, "_reproDriveNext") == 0, "recorder reset resets drive sample");
        ReproPatch("ReproDriveInput", "Postfix", car);
        DevHooks.Playing = true; int callbacks = 0; DevHooks.ReproDriving = value => callbacks++;
        n = ReproRecorder.Rows.Count; ReproPatch("ReproDriveInput", "Postfix", car); ReproPatch("ReproPickup", "Postfix", p, pick);
        Check(callbacks == 1 && ReproRecorder.Rows.Count == n, "playback invokes optional drive callback and suppresses recording");
        DevHooks.Playing = false; DevHooks.ReproDriving = null;
        using var rows = new StringWriter();
        foreach (var row in ReproRecorder.Rows) { row.Write(rows); rows.WriteLine(); }
        string path = "tools/unit/EntityReproRows.txt";
        GoldenRows.UpdateText(path, rows.ToString().Replace("\r\n", "\n"));
        Check(rows.ToString().Replace("\r\n", "\n") == File.ReadAllText(path).Replace("\r\n", "\n"), "all hooked rows match expected rows");
        n = ReproRecorder.Rows.Count;
        Entities.PlayerActing = false;
        ReproCall("ReproAttach", 7u, 8u, 0, 1); ReproPatch("ReproUsable", "Postfix", u, true);
        object[] inactive = { at, null }; ReproPatch("ReproDetach", "Prefix", inactive);
        Check(inactive[1] == null && ReproRecorder.Rows.Count == n, "attach usable and detach require player action");
        Entities.PlayerActing = true; Set(typeof(EntityHitches), "_hitchApplying", true);
        ReproCall("ReproAttach", 7u, 8u, 0, 1); Set(typeof(EntityHitches), "_hitchApplying", false);
        at.utanfuto = new HitchTrailerBody(); ReproCall("ReproAttach", 7u, 8u, 0, 1);
        inactive = new object[] { at, null }; ReproPatch("ReproDetach", "Prefix", inactive); at.utanfuto = null;
        Check(inactive[1] == null && ReproRecorder.Rows.Count == n, "hitch apply and trailer attach guards remain");
        ReproPatch("ReproPickup", "Postfix", new fpscontroller { pickedUp = pick }, pick);
        Check(ReproRecorder.Rows.Count == n, "remote player pickup ignored");
        int frame = (int)Field(ReproOwner, "_reproDetachFrame");
        uint detachedNet = (uint)Field(ReproOwner, "_reproDetachNet");
        ReproCall("ReproReset");
        Check((int)Field(ReproOwner, "_reproDetachFrame") == frame && (uint)Field(ReproOwner, "_reproDetachNet") == detachedNet, "reset preserves detach deduplication guard lifetime");
        ReproRecorder.Recording = false; n = ReproRecorder.Rows.Count;
        ReproPatch("ReproPickup", "Postfix", p, pick); ReproPatch("ReproDriveInput", "Postfix", car);
        Check(ReproRecorder.Rows.Count == n, "idle recorder leaves hooks inactive without Dev");
        ReproRecorder.Recording = true;
        Check(typeof(Entities).GetField("_reproDriveNext", Static) == null && typeof(Entities).GetField("ReproUsables", Static) == null, "repro state has one owner");
        Check(BridgeReflect.Set("TLDRevamp.Net.EntityReproHooks._reproDriveNext", "42") == "42" && (float)Field(ReproOwner, "_reproDriveNext") == 42, "full drive timer owner path writes owner");
        BridgeReflect.Call("EntityReproHooks.ResetDriveSample", Array.Empty<string>());
        Check(BridgeReflect.Get("EntityReproHooks._reproDriveNext") == "0", "Dev compiled reset owner call resets moved timer");
        Check(BridgeReflect.Set("EntityReproHooks._reproInventoryDepth", "3") == "3", "short inventory depth owner path writes owner");
        BridgeReflect.Call("TLDRevamp.Net.EntityReproHooks.ReproReset", Array.Empty<string>());
        Check((int)Field(ReproOwner, "_reproInventoryDepth") == 0 && BridgeReflect.Get("EntityReproHooks._reproUsed") == "0", "recorder reset owner call clears moved state");
        ReproRecorder.Recording = false; Entities.ByNet.Clear(); mainscript.s = null;
    }

    // Drive recording precedes the passenger input merge.
    static void ReproSourceChecks()
    {
        foreach (string part in new[] { "Repro", "ReproDrive" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Replace("\r\n", "\n");
            // Keep the drive hook ahead of ShotgunCapture.
            const string order = "            // Ahead of ShotgunCapture, which merges the remote passenger's input into the car: the row keeps the driver's own\n            // input, as when this hook was declared inside Entities and so patched first.\n            [HarmonyPriority(Priority.Normal + 1)]\n";
            if (part == "ReproDrive") Check(source.Contains(order), "drive hook keeps its place ahead of ShotgunCapture");
        }
    }
}
