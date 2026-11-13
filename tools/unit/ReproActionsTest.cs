using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static class ReproActionsTest
{
    private static int _checks;
    private static void Check(bool ok, string why) { _checks++; if (!ok) throw new Exception(why); }
    private static object Hook(string name, string method, params object[] args) => typeof(Entities).GetNestedType(name, BindingFlags.NonPublic)
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    private static JsonElement Snapshot()
    {
        var data = (ReproData)typeof(ReproRecorder).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        using var writer = new StringWriter(); data.Write(writer, Protocol.Version);
        using var doc = JsonDocument.Parse(writer.ToString()); return doc.RootElement.Clone();
    }
    private static int Count(ReproKind kind)
    {
        int n = 0; foreach (var row in Snapshot().GetProperty("rows").EnumerateArray()) if (row[2].GetInt32() == (int)kind) n++; return n;
    }
    private static void Main()
    {
        DevHooks.Playback = () => ReproPlayback.Active;
        ReproRecorder.SimulationSnapshot = () => NetworkConditions.ReproRow(NetworkConditions.Current);
        var p = mainscript.s.player;
        foreach (string guid in new[] { "tldrevamp.copyisolation", "someone.tld.copyisolation" })
        {
            BepInEx.Bootstrap.Chainloader.PluginInfos.Clear(); BepInEx.Bootstrap.Chainloader.PluginInfos.Add(guid, null);
            Check(!ReproPlayback.Command("arm").Contains("error") && ReproPlayback.Active, "windowed copy arms with " + guid);
            Check(TestSafetyTools.BlockAutoSave && Application.runInBackground, "armed safety flags");
            ReproPlayback.Command("end"); Check(!TestSafetyTools.BlockAutoSave && !Application.runInBackground, "flags restored");
        }
        DedicatedServer.Enabled = true; Check(ReproPlayback.Command("arm").Contains("error"), "dedicated server refused"); DedicatedServer.Enabled = false;
        Plugin.BridgeEnabled = false; Check(ReproPlayback.Command("arm").Contains("error"), "bridge required"); Plugin.BridgeEnabled = true;
        mainscript.s.player = null; Check(ReproPlayback.Command("arm").Contains("error"), "local player required"); mainscript.s.player = p;
        BepInEx.Bootstrap.Chainloader.PluginInfos.Clear(); Check(ReproPlayback.Command("arm").Contains("error"), "isolation required");
        BepInEx.Bootstrap.Chainloader.PluginInfos.Add("someone.tld.copyisolation", null);
        DevHooks.ReproDriving = EntityReproPlayback.ApplyDriveInput;
        DevHooks.PreventDrop = (player, destroyed) => DevHooks.Playing && !destroyed && player.pickedUp != null && EntityCargoTools.ForceHeld == player.pickedUp.tosave;
        ReproPlayback.Command("arm"); ReproRecorder.Toggle(); Check(Plugin.Instance.Overlay.Last.Contains("end playback"), "hotkey refusal toast");
        string limited = ReproPlayback.Command("act 22 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0");
        Check(limited.Contains("skipped") && !limited.Contains("error"), "rate-limited replay mark counted as skip");
        Check(ReproPlayback.Command("act 21 0 0 0 0 0 0 0 4 0 0 0 0 0 0 0").Contains("skipped"), "command mark uses same route");
        ReproPlayback.Command("end");
        Entities.InSession = false; ReproRecorder.Toggle(); Check(Plugin.Instance.Overlay.Last.Contains("join a multiplayer world"), "world refusal toast"); Entities.InSession = true;
        string folder = Path.Combine(Path.GetTempPath(), "repro-actions-" + Guid.NewGuid().ToString("N")); SessionLog.MarksFolder = folder;
        p.transform.eulerAngles = new Vector3(0, 10, 0); p.BodyRot.eulerAngles = new Vector3(0, 120, 0);
        ReproRecorder.Start();
        Check(Snapshot().GetProperty("rows")[1][10].GetDouble() == 120, "recorder samples body yaw");
        p.BodyRot = null; Time.realtimeSinceStartupAsDouble += .2; ReproRecorder.Tick();
        Check(Snapshot().GetProperty("rows")[2][10].GetDouble() == 10, "yaw falls back to root"); p.BodyRot = new Transform();
        var part = Entities.Make(7); var parent = Entities.Make(8);
        part.Root.attachable = new attachablescript { tosave = part.Root }; part.Root.P = new pickupable { tosave = part.Root };
        parent.Root.Mounts = new[] { new mountStuff { parentTosave = parent.Root } };
        part.Proxy = true; part.OwnerId = 2; Entities.ReproAttach(7, 8, 0, 0);
        Check(Count(ReproKind.EntityPose) == 0 && Count(ReproKind.Attach) == 1, "proxy attach records request without owner pose");
        part.Proxy = false; part.OwnerId = Entities.MyId; Entities.ReproAttach(7, 8, 0, 0);
        Check(Count(ReproKind.EntityPose) == 1 && Count(ReproKind.Attach) == 2, "owner attach retains pose");
        p.pickedUp = part.Root.P; p.throwForce = 1.02f; p.Th.forward = new Vector3(0, 0, 1);
        Hook("ReproDrop", "Prefix", p, false);
        Check(Count(ReproKind.Throw) == 1, "full-force throw recorded");
        foreach (var row in Snapshot().GetProperty("rows").EnumerateArray()) if (row[2].GetInt32() == (int)ReproKind.Throw) Check(row[10].GetDouble() == 1, "crossing-frame throw clamped");
        var bare = new mountStuff(); bare.Store(part.Root.P, false); Hook("ReproLock", "Postfix", bare, part.Root.P, false, true);
        Check(Snapshot().GetProperty("skipped").GetInt32() == 1, "null mount parent skipped without throwing");
        part.Root.P.UnPhysicsLock(); parent.Root.Mounts[0].Store(part.Root.P, false);
        parent.Root.ThrowMounts = true; Hook("ReproLock", "Postfix", parent.Root.Mounts[0], part.Root.P, false, true); parent.Root.ThrowMounts = false;
        Check(Snapshot().GetProperty("skipped").GetInt32() == 2, "mount hook exceptions contained");
        // The game stores through mountStuff.Store (PhysLock itself is inlined): a lock is recorded only when Store locked it.
        part.Root.P.UnPhysicsLock();
        object[] store = { parent.Root.Mounts[0], part.Root.P, false };
        Hook("ReproLock", "Prefix", store); Hook("ReproLock", "Postfix", parent.Root.Mounts[0], part.Root.P, store[2], true);
        Check(Count(ReproKind.PhysLock) == 0, "store that locked nothing not recorded");
        Hook("ReproLock", "Prefix", store); parent.Root.Mounts[0].Store(part.Root.P, false); Hook("ReproLock", "Postfix", parent.Root.Mounts[0], part.Root.P, store[2], false);
        Check(Count(ReproKind.PhysLock) == 0, "store skipped by another prefix not recorded");
        part.Root.P.UnPhysicsLock();
        Hook("ReproLock", "Prefix", store); parent.Root.Mounts[0].Store(part.Root.P, false); Hook("ReproLock", "Postfix", parent.Root.Mounts[0], part.Root.P, store[2], true);
        Check(Count(ReproKind.PhysLock) == 1, "game store records the lock");
        foreach (var row in Snapshot().GetProperty("rows").EnumerateArray())
            if (row[2].GetInt32() == (int)ReproKind.PhysLock) Check(row[3].GetUInt32() == 7 && row[4].GetUInt32() == 8 && row[5].GetInt32() == 0 && row[6].GetInt32() == 0 && row[10].GetDouble() == 0, "lock row names item, container, mount and member");
        Hook("ReproLock", "Prefix", store); parent.Root.Mounts[0].Store(part.Root.P, false); Hook("ReproLock", "Postfix", parent.Root.Mounts[0], part.Root.P, store[2], true);
        Check(Count(ReproKind.PhysLock) == 1, "store of an item already locked there not recorded again");
        part.Root.P.UnPhysicsLock();
        object[] uncraft = { new partslotscript { Part = part.Root.attachable }, null };
        Hook("ReproUncraft", "Prefix", uncraft);
        Hook("ReproUncraft", "Postfix", uncraft[1], false); Check(Count(ReproKind.Detach) == 0, "refused uncraft not recorded");
        object[] detach = { part.Root.attachable, null };
        Hook("ReproDetach", "Prefix", detach);
        Hook("ReproDetach", "Postfix", part.Root.attachable, detach[1], false); Check(Count(ReproKind.Detach) == 0, "suppressed detach not recorded");
        Time.frameCount++; Hook("ReproUncraft", "Postfix", uncraft[1], true); Check(Count(ReproKind.Detach) == 1, "successful uncraft recorded");
        Time.frameCount++; ReproRecorder.Event("detach-request", 7, 0, 0, 0, Entities.MyId, ""); Check(Count(ReproKind.Detach) == 2, "successful proxy request recorded");
        // The host creates an own entity inside Detach, before either recording postfix returns.
        foreach (bool detachFirst in new[] { false, true })
        {
            Time.frameCount++;
            var car = Entities.Make(detachFirst ? 30u : 20u);
            var member = new tosaveitemscript(); car.Items.Add(member);
            var at = new attachablescript { tosave = member, attached = true };
            uncraft = new object[] { new partslotscript { Part = at }, null };
            detach = new object[] { at, null };
            Hook("ReproUncraft", "Prefix", uncraft); Hook("ReproDetach", "Prefix", detach);
            int before = Count(ReproKind.Detach);
            at.attached = false;
            if (detachFirst) Hook("ReproDetach", "Postfix", at, detach[1], true);
            var split = Entities.Make(car.NetId + 1); split.Root = member; split.Items.Clear(); split.Items.Add(member);
            if (!detachFirst) Hook("ReproDetach", "Postfix", at, detach[1], true);
            Hook("ReproUncraft", "Postfix", uncraft[1], true);
            Check(Count(ReproKind.Detach) == before + 1, "nested detach recorded once across host split, postfix first=" + detachFirst);
            foreach (var row in Snapshot().GetProperty("rows").EnumerateArray())
            {
                Check(row[3].GetUInt32() != split.NetId, "split entity is never bound or referenced before replay detaches it");
                if (row[2].GetInt32() == (int)ReproKind.Detach && row[3].GetUInt32() == car.NetId)
                    Check(row[5].GetInt32() == 1, "detach keeps original car member address");
            }
        }
        // A missing binding must not leave an action referring to an unbound object in a complete file.
        int skipped = Snapshot().GetProperty("skipped").GetInt32();
        ReproRecorder.Add(new ReproRow { Kind = ReproKind.Pickup, Net = 99 });
        Check(Snapshot().GetProperty("dropped").GetInt32() == 0 && Count(ReproKind.Pickup) == 0, "unloaded target and action skipped");
        Check(Snapshot().GetProperty("skipped").GetInt32() == skipped + 1, "one failed binding counts one skipped action");
        Entities.Make(99); ReproRecorder.Add(new ReproRow { Kind = ReproKind.Pickup, Net = 99 });
        Check(Count(ReproKind.Pickup) == 1, "binding retried after target loads");
        ReproRecorder.Stop();
        Check(Plugin.Instance.Overlay.Last.Contains("skipped") && Plugin.Instance.Overlay.Last.Contains("dropped"), "stop toast includes both counts");
        var save = (System.Threading.Tasks.Task)typeof(ReproRecorder).GetField("_save", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); save.GetAwaiter().GetResult();
        Directory.Delete(folder, true);
        ReproPlayback.Command("arm");
        part.Root.car = new carscript { isPlayerDriving = true }; part.Proxy = true; part.OwnerId = 2;
        foreach (var kind in new[] { ReproKind.Drive, ReproKind.PhysLock, ReproKind.PhysUnlock, ReproKind.EntityPose })
        {
            var r = new ReproRow { Kind = kind, Net = 7, Other = 8, X = .7, Y = .1, Z = -.2 };
            Check(EntityReproPlayback.ReproAct(r).Contains("skipped"), "ownership latency skipped for " + kind);
            part.Proxy = false; part.OwnerId = Entities.MyId;
            Check(EntityReproPlayback.ReproAct(r).Contains("ok"), "ownership arrival applies " + kind);
            if (kind == ReproKind.Drive)
            {
                Hook("ReproDriveInput", "Postfix", part.Root.car);
                Check(part.Root.car.ithrottle == .7f && part.Root.car.isteer == -.2f, "driver input reaches actual input hook after lease arrives");
            }
            if (kind == ReproKind.PhysLock) Check(part.Root.P.physlock == parent.Root.Mounts[0], "owned lock stores in mount");
            if (kind == ReproKind.PhysUnlock) Check(part.Root.P.physlock == null, "owned unlock releases mount");
            if (kind == ReproKind.EntityPose) Check(part.Root.transform.position.x == .7f, "owned entity pose applied");
            part.Proxy = true; part.OwnerId = 2;
            r.Net = 999; Check(EntityReproPlayback.ReproAct(r).Contains("error"), "missing target still fatal for " + kind);
        }
        Check(EntityReproPlayback.ReproAct(new ReproRow { Kind = ReproKind.Attach, Net = 7, Other = 8 }).Contains("ok") && part.Root.attachable.attached, "proxy attach action replays without owner pose");
        Check(EntityReproPlayback.ReproAct(new ReproRow { Kind = ReproKind.Detach, Net = 7 }).Contains("requested") && Entities.DetachRequests == 1, "proxy detach routes request");
        p.pickedUp = part.Root.P;
        Check(!EntityReproPlayback.ReproAct(new ReproRow { Kind = ReproKind.Throw, Net = 7, A = 1, Z = 1 }).Contains("error"), "recorded full throw replays");
        ReproPlayback.Command("end");
        Console.WriteLine("ALL PASS (" + _checks + " real recorder/playback/hook checks)");
    }
}
