using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static object SignalHook(string type, string method, params object[] args) => typeof(EntityCarSignals).GetNestedType(type, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);

    static void CarSignalChecks()
    {
        var c = new carscript { ignition = true, brake = .02f, handbrake = .5f, gear = -1, currenthorn = .75f, steer = -15, TsteerWheel = new Transform(), Engine = new enginescript { running = true, start = true } };
        var e = new Entities.Ent { Root = new tosaveitemscript { car = c }, Proxy = true };
        Entities.ByNet[7] = e;
        var w = new NetWriter(); EntityCarSignals.WriteSignals(w, e);
        Check(w.Buf.Take(w.Len).SequenceEqual(new byte[] { 15, 255, 191, 192 }), "car signal legacy flags gear horn steer bytes");
        int key = EntityCarSignals.CarSignalKey(e);
        Check(key == (31 | 255 << 8 | 11 << 16 | 240 << 20) && EntityCarSignals.CarSignalKey(e) == key, "car signal key retains engine flags and quantization");
        var r = new NetReader { Buf = w.Buf, End = w.Len };
        EntityCarSignals.ReadSignals(r, e);
        Check(!r.Bad && r.Pos == 4 && e.SigHas && e.SigIgnition && e.SigBrake && e.SigHandbrake && e.SigGear == -1 && e.SigHorn == 191 / 255f && e.SigSteer == -64 / 127f, "car signals read and register copy");
        Check(EntityCarSignals.Signals(c, out var got) && ReferenceEquals(got, e), "car signal owner lookup");
        e.Proxy = false; Check(!EntityCarSignals.Signals(c, out _), "local car rejects copy signals"); e.Proxy = true;
        e.Root.car = new carscript(); Check(!EntityCarSignals.Signals(c, out _), "rebound car rejects stale cache entry"); e.Root.car = c;
        EntityCarSignals.ReadSignals(new NetReader { Buf = new byte[] { 1, 2 }, End = 2 }, e);
        Check(e.SigGear == -1 && e.SigHorn == 191 / 255f, "truncated signal leaves prior received state");
        EntityCarSignals.ReadSignals(new NetReader { Buf = Array.Empty<byte>(), End = 0 }, e);
        EntityCarSignals.ReadSignals(new NetReader { Buf = new byte[] { 0 }, End = 1 }, e);
        Check(e.SigHas && e.SigIgnition, "omitted or absent car signal tail retains previous values");
        c.ignition = false; SignalHook("IgnitionOnCopy", "Prefix", c);
        Check(c.ignition && RemotePlayers.Reaches == 1, "copy ignition projects before update and reaches once");
        c.gear = 1; c.handbrake = 0; SignalHook("SignalsOnCopy", "Postfix", c);
        Check(c.ihorn && c.gear == -1 && c.Shifts == 1 && c.handbrake == 1 && c.Handbrakes == 1 && EntityCarSignals.SignalsApplied == 1, "copy signals use game receivers and increment count");
        e.SigGear = 99; SignalHook("SignalsOnCopy", "Postfix", c);
        Check(c.Shifts == 1 && EntityCarSignals.SignalsApplied == 2, "invalid gear stays guarded while apply counter counts frame"); e.SigGear = -1;
        Entities.InSession = false; c.ignition = false; SignalHook("IgnitionOnCopy", "Prefix", c); SignalHook("SignalsOnCopy", "Postfix", c);
        Check(!c.ignition && EntityCarSignals.SignalsApplied == 2, "car signal patch session guards"); Entities.InSession = true;
        c.brake = .37f; object[] brake = { c, 0f }; SignalHook("BrakeLightsOnCopy", "Prefix", brake);
        Check(c.brake == 1 && (float)brake[1] == .37f, "brake light read temporarily receives owner's pedal");
        SignalHook("BrakeLightsOnCopy", "Postfix", c, brake[1]); Check(c.brake == .37f, "brake restored after light read");
        Time.deltaTime = .02f; SignalHook("SteerWheelOnCopy", "Postfix", c);
        float shown = e.SteerWheelShown;
        Check(shown < 0 && c.steerWheel == shown && c.TsteerWheel.localEulerAngles.z == shown * c.FsteerM, "owner steering drives display wheel");
        c.isPlayerDriving2 = true; c.isteer = 1; SignalHook("SteerWheelOnCopy", "Postfix", c);
        Check(e.SteerWheelShown > shown, "stronger local passenger steering keeps game's merge rule");
        Check(EntityCarSignals.CarSignals(8) == "{\"error\":\"no such car\"}", "car diagnostics reject missing net");
        w.Reset(); e.PartIndex = 0; EntityCarSignals.WriteSignals(w, e);
        Check(w.Len == 1 && w.Buf[0] == 0 && EntityCarSignals.CarSignalKey(e) == -1, "part entity has no car signals"); e.PartIndex = -1;
        long applied = EntityCarSignals.SignalsApplied; EntityCarSignals.ResetSignals();
        Check(!EntityCarSignals.Signals(c, out _) && e.SigHas && EntityCarSignals.SignalsApplied == applied, "reset clears cache only, preserving record and cumulative counter");

        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.Contains("EntityProxyPresentation.WriteWheels(WS, e); EntityProxyPresentation.WriteEngine(WS, e); EntityCarSignals.WriteSignals(WS, e);"), "state tail write order");
        Check(kernel.Contains("EntityProxyPresentation.ReadWheels(r, e); EntityProxyPresentation.ReadEngine(r, e); EntityCarSignals.ReadSignals(r, e); EntityRagdolls.ReadRagdoll(r, e, pos, rot);"), "settled replay tail read order");
        string hand = File.ReadAllText("src/TLDRevamp/Net/Entities.HandItems.cs");
        Check(hand.Contains("EntityCodec.ReadHandPose") && hand.Contains("EntityCodec.ReadHandPos") && hand.Contains("EntityCodec.ReadHandRot"), "hand items use codec owner scratch API");
        string source = File.ReadAllText("src/TLDRevamp/Net/Entities.CarSignals.cs");
        Check(System.Text.RegularExpressions.Regex.Matches(source, @"Entities\.(\w+)").Cast<System.Text.RegularExpressions.Match>().All(m => new[] { "Ent", "InSession", "TryGetEntity" }.Contains(m.Groups[1].Value)), "car signals uses only record, session and lookup kernel API");
        foreach (string patch in new[] { "IgnitionOnCopy", "SignalsOnCopy", "SteerWheelOnCopy", "BrakeLightsOnCopy" })
            Check(typeof(EntityCarSignals).GetNestedType(patch, BindingFlags.NonPublic).GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length == 1 && typeof(Entities).GetNestedType(patch, BindingFlags.NonPublic) == null, patch + " has one attributed owner");
    }
}
