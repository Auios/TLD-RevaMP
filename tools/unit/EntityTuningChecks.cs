using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;
using TuningOwner = TLDRevamp.Net.EntityCarTuning;

static partial class EntitySplitTest
{
    static carscript TunedCar(float diff = 4.25f)
    {
        var c = new carscript { differentialRatio = diff, gears = new[] { new Gear { ratio = -2.5f }, new Gear { ratio = 1.25f } } };
        var wheel = new WheelCollider { name = "front", ParentComponent = c, suspensionDistance = .45f, center = new Vector3(.1f, -.25f, .3f) };
        wheel.transform.localPosition = new Vector3(1.25f, -.5f, 2.75f);
        c.Children.Add(wheel); c.WHDriven = new List<WheelCollider> { wheel }; c.WHSteer = new List<WheelCollider> { wheel };
        return c;
    }
    // Captured by running HEAD 08b120c's CarTuneTick and SendTuneTo before moving their implementations.
    const string TuneVector = "712D010000010000884002000020C00000A03F010666726F6E740000A03F000000BF000030406666E63ECDCCCC3D000080BE9A99993E03";
    // HEAD EditTick with an opaque three-byte record fixture; TankAdds.Write is the production encoder.
    static void EditWireChecks()
    {
        Entities.ByNet.Clear(); Entities.Packets.Clear();
        Set(typeof(Entities), "_editFlushAt", 0f); Entities.TestTankChanges = false; SessionLog.St = 123.5;
        var it = new tosaveitemscript { idInSave = 49, tanks = new List<tankscript> { new tankscript() } };
        var e = new Entities.Ent { NetId = 501, Proxy = true, Root = it }; e.Items.Add(it); Entities.ByNet[501] = e;
        string[] vectors = { "20F50100000000000000E05E4000800203010203", "20F50100000000000000E05E40000203010203" }; int index = 0;
        foreach (int kind in new[] { Entities.KindBlood, Entities.KindTank })
        {
            Entities.EditDirty[it] = 0; Entities.EditKinds[it] = kind; Time.realtimeSinceStartup += 1;
            Invoke(typeof(Entities), "EditTick");
            Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == vectors[index++] && Entities.Packets.Last().Reliable, "blood and fluid edit envelopes match HEAD bytes and reliability");
            Check(Entities.TestWithCar == (kind == Entities.KindBlood), "only blood edit includes car record");
            Check(Entities.EditDirty.Count == 0 && e.EditHoldUntil[0] == Time.realtimeSinceStartup + 1.5f, "edit flush cleanup and hold order retained");
        }
        Entities.TestTankChanges = true; Entities.EditDirty[it] = 0; Entities.EditKinds[it] = Entities.KindTank; Time.realtimeSinceStartup += 1;
        Invoke(typeof(Entities), "EditTick");
        Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == "20F50100000000000000E05E400002030102030100020000A0BF" && Entities.TankAddsSent == 1, "fluid delta edit envelope and production TankAdds tail match HEAD bytes");
        Entities.TestTankChanges = false;
    }
    static void TuningChecks()
    {
        var owner = typeof(TuningOwner);
        Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.IsHost = false;
        TuningOwner.CarTuneReset(); Set(owner, "_tuneAt", 0f);
        var c = TunedCar(); var root = new tosaveitemscript { car = c, id = 4, idInSave = 41 };
        var e = new Entities.Ent { NetId = 301, RootId = 41, Root = root }; Entities.ByNet[301] = e;
        Time.realtimeSinceStartup = 100;
        TuningOwner.CarTuneTick();
        Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == TuneVector && Entities.Packets.Last().Reliable, "tuning owner send matches HEAD bytes and reliability");
        int sent = Entities.Packets.Count; TuningOwner.CarTuneTick();
        Check(Entities.Packets.Count == sent, "tuning scan waits half a second");
        Time.realtimeSinceStartup = 100.5f; TuningOwner.CarTuneTick();
        Check(Entities.Packets.Count == sent, "unchanged tuning waits thirty seconds");
        Time.realtimeSinceStartup = 130; TuningOwner.CarTuneTick();
        Check(Entities.Packets.Count == sent + 1 && TuningOwner.TunesResent == 1, "tuned owner resends at thirty seconds");
        Entities.TestServer(301, 9);
        var message = Entities.Packets.Last().Bytes;
        Check(!TuningOwner.ServerKeepTune(8, Reader(message)) && TuningOwner.ServerKeepTune(9, Reader(message)), "only current server owner can keep tuning");
        TuningOwner.SendTuneTo(7, 301, message.Skip(5).ToArray());
        Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == TuneVector && Entities.Packets.Last().Peer == 7 && Entities.Packets.Last().Reliable, "tuning replay matches HEAD bytes target and reliability");
        long refused = TuningOwner.TunesRefused;
        TuningOwner.ServerKeepTune(9, Reader(message.Take(9).ToArray()));
        TuningOwner.ApplyCarTune(Reader(message.Take(9).ToArray()));
        TuningOwner.ApplyCarTune(new NetReader { Buf = new byte[2054], End = 2054 });
        Check(TuningOwner.TunesRefused == refused + 3, "unreadable and oversized tuning rejected");
        e.Proxy = true; c.differentialRatio = 1;
        TuningOwner.ApplyCarTune(Reader(message));
        Check(c.differentialRatio == 4.25f && TuningOwner.TunesApplied == 1, "copy applies tuning");
        var replacement = TunedCar(1); e.Root = new tosaveitemscript { car = replacement, id = 4, idInSave = 41 };
        Time.realtimeSinceStartup = 131; TuningOwner.CarTuneTick();
        Check(replacement.differentialRatio == 4.25f && TuningOwner.TunesReapplied == 1, "known tuning reappears before owner scan on a new placement");
        e.Proxy = false; sent = Entities.Packets.Count; Time.realtimeSinceStartup = 131.5f; TuningOwner.CarTuneTick();
        Check(Entities.Packets.Count == sent + 1 && TuningOwner.TunesResent == 2, "received tuning's old sent timestamp resends right after takeover");
        var into = new Dictionary<uint, byte[]>(); Entities.KnownTunings(into); var ids = new HashSet<uint>(); Entities.TunedRootIds(ids);
        Check(Convert.ToHexString(into[41]) == TuneVector.Substring(10) && ids.SetEquals(new uint[] { 41 }), "world save forwarders keep local root ids and tuning bytes");
        var wheels = TuningOwner.TuneWheels(replacement); replacement.WHDriven[0].motorTorque = 7;
        var t = TuningOwner.CaptureTuning(replacement); t.Wheels[0].Roles = CarTuning.Steer; t.Gears[0] = -3;
        Check(TuningOwner.ApplyTuning(replacement, t) == 1 && replacement.WHDriven.Count == 0 && wheels[0].motorTorque == 0 && replacement.gears[0].ratio == -3, "tuning keeps roles torque cleanup and gear application");
        long kept = TuningOwner.TunesKept; float at = (float)Field(owner, "_tuneAt"); TuningOwner.CarTuneReset();
        Check(TuningOwner.ObservedTuneHere.Count == 0 && TuningOwner.TunesKept == kept && (float)Field(owner, "_tuneAt") == at, "tuning reset clears caches and keeps counters and scan timer");
        FluidBloodChecks();
        EditWireChecks();
        Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.ClearServer();
    }

    static void FluidBloodChecks()
    {
        var owner = typeof(TuningOwner);
        var tank = new tankscript(); tank.F.fluids.Add(new mainscript.FluidValue { type = 2, amount = 1 });
        var it = new tosaveitemscript { idInSave = 45, tanks = new List<tankscript> { tank } };
        var e = new Entities.Ent { NetId = 302, Root = it, Items = new List<tosaveitemscript> { it }, ItemIds = new List<uint> { 45 }, Proxy = true };
        Entities.ByNet.Clear(); Entities.ByNet[302] = e;
        TuningOwner.CopyTanksAre(45, new List<save_tankscript> { new save_tankscript { id = 45, ind = 0, type = new[] { 2 }, amount = new[] { 3f } } });
        Time.realtimeSinceStartup = 200; TuningOwner.CopyFluidTick();
        Check(tank.F.GetAmount() == 3 && TuningOwner.CopyFluidsRestored == 1 && TuningOwner.CopyFluidChecks == 1 && !Entities.ApplyingState, "copy fluid truth restores tank and balances apply guard");
        TuningOwner.CopyFluidTick(); Check(TuningOwner.CopyFluidChecks == 1, "copy tank check waits one second");
        Check(!(bool)Patch(owner, "CopyFluidLoss", "Prefix", tank.F, -1f) && (bool)Patch(owner, "CopyFluidLoss", "Prefix", tank.F, 1f), "copy blocks simulation loss and allows fills");
        Entities.EditDirty[it] = 0; tank.F.fluids[0].amount = 2; Time.realtimeSinceStartup = 201; TuningOwner.CopyFluidTick();
        Check(tank.F.GetAmount() == 2 && TuningOwner.CopyFluidChecks == 1, "pending player edit holds truth application"); Entities.EditDirty.Clear();
        TuningOwner.CopyTanksAreNow(it); TuningOwner.CopyTanksOnOwnerChange(e, false);
        Check(!TuningOwner.TryCopyTankTruth(45, out _), "takeover drops old tank truth");
        TuningOwner.CopyTanksOnOwnerChange(e, true); Check(TuningOwner.TryCopyTankTruth(45, out var truth) && truth[0].amount[0] == 2, "hand off starts with current tank contents");
        TuningOwner.CopyTanksAre(999, null); Time.realtimeSinceStartup = 259; TuningOwner.CopyFluidTick();
        Check(TuningOwner.TryCopyTankTruth(999, out _), "fluid truth sweep waits sixty seconds");
        Time.realtimeSinceStartup = 260; TuningOwner.CopyFluidTick();
        Check(!TuningOwner.TryCopyTankTruth(999, out _) && TuningOwner.TryCopyTankTruth(45, out _), "fluid truth sweep keeps only copy item ids");
        var wash = new washtriggerscript { ON = true }; var car = new carscript { blood = wash }; it.car = car;
        var data = new itemDataClass { car = new List<save_carscript> { new save_carscript() } };
        TuningOwner.BloodFollow(it, data); Check(!wash.ON && TuningOwner.BloodCleanedOnCopy == 1, "clean owner record cleans blood on copy");
        wash.ON = true; TuningOwner.BloodEditApply(it, data); Check(!wash.ON && TuningOwner.BloodCleanApplied == 1, "clean edit cleans blood on owner");
        wash.ON = true; wash.destroyOnRepair = true; TuningOwner.BloodFollow(it, data); TuningOwner.BloodEditApply(it, data);
        Check(wash.ON && TuningOwner.BloodCleanedOnCopy == 1 && TuningOwner.BloodCleanApplied == 1, "blood destroy guard keeps counters unchanged");
        wash.ParentComponent = car; car.ChildComponent = it; wash.destroyOnRepair = false;
        Patch(owner, "BloodWashed", "Postfix", wash);
        Check(TuningOwner.BloodCleanSent == 1 && Entities.LastEdit == "blood", "copy washing marks blood edit and counts send");
        Entities._applyingState = true; Patch(owner, "BloodWashed", "Postfix", wash); Entities._applyingState = false;
        Check(TuningOwner.BloodCleanSent == 1, "state application cannot echo blood clean edit");
    }
}
