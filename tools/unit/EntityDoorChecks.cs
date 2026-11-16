using System;
using System.Collections;
using System.IO;
using System.Linq;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static (Entities.Ent Entity, attachablescript At, Transform Frame) DoorSetup()
    {
        var car = new tosaveitemscript();
        var frame = new Transform { ParentComponent = car }; frame.SetParent(car.transform, true);
        var usable = new usablescript { TDoor = frame, slideAble = true }; car.Children.Add(usable); car.usables.Add(usable);
        var item = new tosaveitemscript(); var at = new attachablescript { tosave = item, Target = car, attached = true, point = new Transform() };
        item.attachable = at; at.point.SetParent(frame, true); at.point.position = new Vector3(.25f, -.5f, .75f);
        at.transform.position = new Vector3(1.25f, -2.5f, 3.75f);
        var e = new Entities.Ent { NetId = 401, Root = item }; e.Items.Add(item);
        Entities.ByNet[401] = e;
        return (e, at, frame);
    }
    static byte[] DoorWire(attachablescript at)
    {
        var w = new NetWriter(); Invoke(typeof(Entities), "WriteAttachPose", w, at); return w.Buf.Take(w.Len).ToArray();
    }
    static byte[] LiveDoorWire()
    {
        var w = new NetWriter();
        Invoke(typeof(EntityUsables), "WriteLiveUsable", w, new Entities.UsableKey { Net = 401, Epoch = 3, Item = 2, Usable = 7 }, (ushort)513, 123.5d,
            new Entities.UsableValue { X = 1, Y = 65535, Slide = 32768, Turn = -2, Tuned = 1234 }, default(Entities.PoiState));
        return w.Buf.Take(w.Len).ToArray();
    }
    // Captured from the HEAD 08b120c shared encoders, compiled by ExtractFeatureWire.py --baseline.
    const string DoorAttachVector = "02C4138727CC3A030000000000000100F403E707DC0B";
    const string LiveDoorVector = "5D9101000003000000020701020000000000E05E400100FFFF008003D204";
    static void DoorChecks()
    {
        Entities.ByNet.Clear(); EntityDoors.ResetDoorAttachments();
        var d = DoorSetup();
        Check(Convert.ToHexString(DoorWire(d.At)) == DoorAttachVector, "door frame tail matches HEAD encoder bytes through moved hooks");
        var stored = (IDictionary)Field(typeof(EntityDoors), "_storedDoorAttaches");
        var restored = (IList)Field(typeof(EntityDoors), "_restoredDoorAttaches");
        Check(stored.Count == 1 && EntityDoors.DoorAttachFrame(d.Frame), "sending door frame remembers attachment");
        d.At.slot = new partslotscript(); Check(Convert.ToHexString(DoorWire(d.At)) == "00", "slot attachment tail matches HEAD bytes"); d.At.slot = null;
        Check(Convert.ToHexString(LiveDoorWire()) == LiveDoorVector, "car door live usable message matches HEAD bytes");
        Check(EntityDoors.AttachFramePath(d.At, d.Frame).SequenceEqual(new[] { 0 }), "door path uses frame hierarchy indices");
        Check(EntityDoors.RestoreAttachFrame(d.At, null, default), "no stored frame preserves existing attach");
        long bad = Entities.AttachPosesBad;
        Check(!EntityDoors.RestoreAttachFrame(d.At, new[] { 99 }, default) && Entities.AttachPosesBad == bad + 1, "bad frame path increments original counter");
        var oldPoint = d.At.point; d.At.point = new Transform();
        EntityDoors.KeepDoorAttach(401, new[] { 0 }, new Vector3(.25f, -.5f, .75f), new Vector3(2, 3, 4), Quaternion.identity, oldPoint);
        d.Entity.Proxy = true; d.Entity.HandDraw.Source = new object(); EntityDoors.RestoreStoredDoorAttaches();
        Check(d.At.point.parent == null && stored.Count == 1, "hand managed attachment waits for release");
        d.Entity.HandDraw.Reset(); d.Entity.Proxy = false; Entities._applyingAttach = true; EntityDoors.RestoreStoredDoorAttaches();
        Check(d.At.point.parent == d.Frame && d.At.parentAtAttach && d.At.transform.position == d.Frame.position + d.Frame.rotation * new Vector3(2, 3, 4) && Entities._applyingAttach, "restore rebuilds moving frame pose and preserves incoming attach guard"); Entities._applyingAttach = false;
        var bound = d.At.point; EntityDoors.BindStoredDoorAttach(401, bound);
        EntityDoors.RestoreStoredDoorAttaches(); Check(stored.Count == 1 && restored.Count == 0, "bound point keeps stored entry for a later stream placement");
        d.Entity.Root = null; EntityDoors.RestoreStoredDoorAttaches(); Check(stored.Count == 1, "stream out retains stored door attach");
        d.Entity.Root = d.At.tosave; d.At.attached = false; EntityDoors.RestoreStoredDoorAttaches();
        Check(stored.Count == 0 && restored.Count == 1, "detached stored entry removed after traversal");
        EntityDoors.KeepDoorAttach(999, new[] { 0 }, default, default, Quaternion.identity); EntityDoors.RestoreStoredDoorAttaches();
        Check(stored.Count == 0 && restored.Count == 1, "missing entity removes stored attach after traversal");
        EntityDoors.KeepDoorAttach(401, new[] { 0 }, default, default, Quaternion.identity);
        Entities._applyingDetach = true; Patch(typeof(EntityDoors), "ForgetDoorAttachment", "Postfix", d.At);
        Check(stored.Count == 1, "replayed detach keeps stored moving frame"); Entities._applyingDetach = false;
        Patch(typeof(EntityDoors), "ForgetDoorAttachment", "Postfix", d.At); Check(stored.Count == 0, "ordinary detach forgets stored moving frame");
        EntityDoors.KeepDoorAttach(401, new[] { 0 }, default, default, Quaternion.identity); EntityDoors.ResetDoorAttachments();
        Check(stored.Count == 0 && restored.Count == 1, "session reset clears only stored table and keeps scratch lifetime");
        var control = new usablescript(); var bus = new busdoorscripts { tosave = d.At.tosave, doors = new[] { new busdoorscripts.Door { UBoth = new[] { control } } } };
        d.At.tosave.busdoorscript = bus;
        Check(EntityDoors.BusControl(d.At.tosave, control) && !EntityDoors.BusControl(d.At.tosave, new usablescript()), "paired bus controls identified by original arrays");
        object[] args = { bus, false }; Patch(typeof(EntityDoors), "BusDoorChanged", "Prefix", args);
        bus.somethingChanged = true; Entities.LastEdit = ""; Patch(typeof(EntityDoors), "BusDoorChanged", "Postfix", bus, (bool)args[1]);
        Check((bool)args[1] && Entities.LastEdit == "busdoor", "powered bus change marks original busdoor edit");
        bus.Powered = false; Patch(typeof(EntityDoors), "BusDoorChanged", "Prefix", args); Check(!(bool)args[1], "unpowered bus retains prefix guard");
        var car = d.At.Target; var ce = new Entities.Ent { NetId = 402, Root = car }; Entities.ByNet[402] = ce;
        Check(Entities.DoorsOf(ce).Count == 1 && Entities.CarDoors(402) == EntityDoors.CarDoors(402), "door discovery and diagnostics retain compiled facade");
        Check(Entities.Food(0) == "{\"gone\":true}" && Entities.Rabbit(0) == "{\"error\":\"no entity\"}", "existing colocated diagnostics keep their facade results");
        string attach = File.ReadAllText("src/TLDRevamp/Net/Entities.Attach.cs");
        Check(attach.Contains("if (hasPose && EntityDoors.RestoreAttachFrame(at, framePath, pointLp)) ApplyAttachPose(pe, at, poseLp, poseLq);"), "attach restores frame before applying pose at original call sites");
        string late = File.ReadAllText("src/TLDRevamp/Net/Entities.CarDoors.cs");
        Check(late.Replace("\r\n", "\n").Contains("RestoreStoredDoorAttaches();\n            Entities.TelemetryRender();\n            DevHooks.EntityLateSample?.Invoke();"), "late phase restores attachments before telemetry and Dev sampling");
        Entities.ByNet.Clear(); EntityDoors.ResetDoorAttachments();
    }
}
