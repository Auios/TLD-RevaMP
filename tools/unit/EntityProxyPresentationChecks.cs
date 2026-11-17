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
using UnityEngine.Rendering;

static partial class EntitySplitTest
{
    static object PresentationCall(string name, params object[] args) => typeof(EntityProxyPresentation).GetMethod(name, Static).Invoke(null, args);
    static bool PresentationPrefix(string name, params object[] args) => (bool)typeof(EntityProxyPresentation).GetNestedType(name, BindingFlags.NonPublic).GetMethod("Prefix", Static).Invoke(null, args);
    static void ProxyPresentationChecks()
    {
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "=" + Convert.ToString(value, CultureInfo.InvariantCulture));
        void Packet(string name, NetWriter w) => Row(name, Convert.ToHexString(w.Buf.AsSpan(0, w.Len)));
        var oldCulture = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var w = new NetWriter();
        var e = new Entities.Ent { Root = new tosaveitemscript { car = new carscript() } };
        e.Root.car.ParentComponent = e.Root; e.Root.car.ChildComponent = e.Root;
        Entities.ByNet.Clear(); Entities.ProxyEngineOwner.Clear(); Entities.ProxyWheelOwner.Clear(); Entities.ProxyWheelIndex.Clear();
        EntityProxyPresentation.SuppressedDamage = EntityProxyPresentation.HandoverDamageSuppressed = EntityProxyPresentation.DistFreezeSkipped = 0;
        EntityProxyPresentation.ProxyStarterSounds = EntityProxyPresentation.ProxyStopSounds = 0;
        EntityProxyPresentation.CopyLodRestoreAll(); EntityProxyPresentation.CopyLodOff = EntityProxyPresentation.CopyLodOn = 0;
        Set(typeof(EntityProxyPresentation), "_copyLodAt", 0f);
        mainscript.s = new mainscript { player = new fpscontroller { Cam = new Component() } };
        Time.time = 10; Time.deltaTime = .1f; Time.realtimeSinceStartup = 10;
        foreach (int count in new[] { 0, 1, 4, 16, 20 })
        {
            e.Wheels = Enumerable.Range(0, count).Select(i => new wheelgraphicsscript
            {
                W = new WheelCollider { suspensionDistance = .4f, center = new Vector3(0, -.25f, 0), steerAngle = i % 2 == 0 ? 63.75f : -64,
                    rpm = i == 0 ? 2 : i == 1 ? -2.01f : 40000, brakeTorque = i * 1000, motorTorque = i % 2 == 0 ? 5000 : -5000 },
                T = new Transform { position = new Vector3(0, i == 0 ? -.45f : -1.5f, 0) }
            }).ToArray();
            foreach (bool torques in new[] { false, true })
            {
                Entities.ByNet.Clear();
                if (torques) Entities.ByNet[2] = new Entities.Ent { Proxy = true, Root = new tosaveitemscript { car = new carscript() } };
                w.Reset(); EntityProxyPresentation.WriteWheels(w, e); Packet("wheels-" + count + "-" + torques, w);
                var copy = new Entities.Ent(); var r = new NetReader { Buf = w.Buf, End = w.Len };
                EntityProxyPresentation.ReadWheels(r, copy);
                Row("wheels-read", copy.HasTorques + ":" + r.Pos + ":" + r.Bad + ":" + string.Join(",", copy.WTravel) + ":" + string.Join(",", copy.WSteer) + ":" + string.Join(",", copy.WRpm) + ":" + string.Join(",", copy.WBrake) + ":" + string.Join(",", copy.WMotor));
            }
        }
        e.PartIndex = 0; w.Reset(); EntityProxyPresentation.WriteWheels(w, e); Packet("part-no-wheels", w); e.PartIndex = -1;
        var fallback = new wheelgraphicsscript { W = new WheelCollider { suspensionDistance = 0, Pose = new Vector3(0, -.1f, 0) }, W2 = new WheelCollider { Pose = new Vector3(0, -.3f, 0) } };
        e.Wheels = new[] { fallback, new wheelgraphicsscript() }; w.Reset(); EntityProxyPresentation.WriteWheels(w, e); Packet("paired-null-wheel", w);
        var engine = e.Root.car.Engine = new enginescript { rpm = 1234.6f, temp = -12.34f, running = true, start = true };
        foreach (int part in new[] { -1, 0 })
        {
            e.PartIndex = part; w.Reset(); EntityProxyPresentation.WriteEngine(w, e); Packet("engine-part-" + part, w);
        }
        e.PartIndex = -1;
        foreach (float rpm in new[] { -3f, 0, 65535.6f }) foreach (float temp in new[] { -4000f, 20.05f, 4000f })
        { engine.rpm = rpm; engine.temp = temp; w.Reset(); EntityProxyPresentation.WriteEngine(w, e); Packet("engine-clamp", w); }
        foreach (byte[] bytes in new[] { new byte[] { 0 }, new byte[] { 7, 0x34, 0x12 }, new byte[] { 15, 0x34, 0x12, 0x85, 0xff }, new byte[] { 15, 1 } })
        {
            e.Proxy = true; e.EngHasTemp = true; e.EngTemp = 99; Entities.ProxyEngineOwner.Clear();
            var r = new NetReader { Buf = bytes, End = bytes.Length }; EntityProxyPresentation.ReadEngine(r, e);
            Row("engine-read", r.Bad + ":" + r.Pos + ":" + e.EngRunning + ":" + e.EngStart + ":" + e.EngRpm + ":" + e.EngHasTemp + ":" + e.EngTemp + ":" + Entities.ProxyEngineOwner.Count);
        }
        e.Proxy = false; Entities.BindPresentationEngine(e); Row("unbind-own-engine", Entities.ProxyEngineOwner.Count);
        Entities.ByNet.Clear(); Entities.ByNet[1] = e; e.Proxy = true;
        foreach (bool seen in new[] { false, true }) foreach (bool waiting in new[] { false, true }) foreach (bool rest in new[] { false, true })
        {
            PresentationFloorFixture.Seen = seen; Tpa.WaitingWorld = waiting; e.ReceivedAtRest = rest;
            Row("floor", EntityProxyPresentation.TpaFloorReady(e.Root));
        }
        e.Proxy = false; Row("floor-owned", EntityProxyPresentation.TpaFloorReady(e.Root)); Tpa.WaitingWorld = false;
        foreach (bool proxy in new[] { false, true }) foreach (bool hitch in new[] { false, true })
        {
            e.Proxy = proxy; e.HitchWaiting = hitch;
            Row("dist-freeze", PresentationPrefix("NoDistFreezeOnCopies", e.Root));
            Row("dist-unfreeze", PresentationPrefix("NoDistUnfreezeOnCopies", e.Root));
        }
        e.Proxy = true; Row("damage-copy", PresentationPrefix("NoProxyDamage", e.Root.car, 100f));
        e.Proxy = false; var grace = (IDictionary)Field(typeof(Entities), "DamageGraceUntil"); grace.Clear(); grace[e.Root.car] = 10.5f;
        Row("damage-grace", PresentationPrefix("NoProxyDamage", e.Root.car, 0f)); Time.time = 10.5f;
        Row("damage-grace-edge", PresentationPrefix("NoProxyDamage", e.Root.car, 0f)); Row("grace-count", grace.Count);
        Row("guard-counters", EntityProxyPresentation.SuppressedDamage + ":" + EntityProxyPresentation.HandoverDamageSuppressed + ":" + EntityProxyPresentation.DistFreezeSkipped);
        e.Proxy = true; e.EngRpm = 2500; e.EngRunning = true; e.EngStart = true; e.EngHasTemp = true; e.EngTemp = 90;
        engine.rpm = 0; engine.SAlap = new AudioSource(); engine.SMotor = new AudioSource(); engine.SStart = new AudioClip { length = 1 }; engine.SStop = new AudioClip(); engine.gearstick = new gearstickscript();
        Entities.ProxyEngineOwner[engine] = e;
        Row("engine-patch", PresentationPrefix("ProxyEngine", engine)); Row("engine-shown", engine.rpm + ":" + engine.running + ":" + engine.start + ":" + engine.temp + ":" + engine.TempWeak + ":" + engine.SAlap.isPlaying + ":" + engine.SMotor.isPlaying + ":" + engine.gearstick.Kicks);
        e.EngRunning = e.EngStart = false; PresentationPrefix("ProxyEngine", engine); Row("engine-stop", engine.SAlap.isPlaying + ":" + engine.SMotor.isPlaying + ":" + EntityProxyPresentation.ProxyStarterSounds + ":" + EntityProxyPresentation.ProxyStopSounds);
        e.Proxy = false; Row("engine-owned", PresentationPrefix("ProxyEngine", engine));
        e.Proxy = true; var wrong = new enginescript(); Entities.ProxyEngineOwner[wrong] = e; Row("engine-rebound", PresentationPrefix("ProxyEngine", wrong));
        var wheel = new wheelgraphicsscript { W = new WheelCollider { center = new Vector3(0, -.25f, 0), suspensionDistance = .4f }, T = new Transform() };
        Entities.ProxyWheelOwner[wheel] = e; Entities.ProxyWheelIndex[wheel] = 0;
        e.WTravel = new[] { .75f }; e.WSteer = new[] { 10f }; e.WRpm = new[] { -120f }; e.WSpin = new[] { 20f };
        Row("wheel-patch", PresentationPrefix("ProxyWheelGraphics", wheel)); Row("wheel-shown", e.WSpin[0] + ":" + wheel.T.position.y);
        wheel.T = null; Row("wheel-no-mesh", PresentationPrefix("ProxyWheelGraphics", wheel));
        Row("wheel-unknown", PresentationPrefix("ProxyWheelGraphics", new wheelgraphicsscript()));
        for (int mask = 0; mask < 64; mask++)
        {
            bool Bit(int bit) => (mask & 1 << bit) != 0;
            if (Bit(2))
            foreach (BandwidthRules.Quiet quiet in Enum.GetValues<BandwidthRules.Quiet>())
                Row("final-" + mask + "-" + quiet, EntityProxyPresentation.FinalState(Bit(0), Bit(1) ? 1u : 0u, quiet, Bit(3), Bit(4)));
            Row("release-car", EntityProxyPresentation.LooseRelease(Bit(0) ? 1u : 0, Bit(1) ? 2u : 0, Bit(2), Bit(3)));
            Row("let-go", EntityProxyPresentation.LetGoInCar(Bit(0), Bit(1), Bit(2) ? 1u : 0, Bit(3), Bit(4)));
            bool active = Bit(0), moving = Bit(1);
            Row("release-tail", EntityProxyPresentation.ReleaseMotion(ref active, Bit(2), Bit(3), Bit(4), ref moving, Bit(5)) + ":" + active + ":" + moving);
        }
        foreach (double stamp in new[] { 0, 10, 10.0001, 10.001, double.NaN })
        {
            Row("stamp", EntityProxyPresentation.PoseStamp(stamp, 10, false));
            double previous = 10; uint epoch = 1; Row("newer", EntityProxyPresentation.NewerState(stamp, 1, ref previous, ref epoch) + ":" + previous + ":" + epoch);
            var velocity = EntityProxyPresentation.CompactVelocity(true, new Vector3d(2, 3, 4), stamp, new Vector3d(1, 1, 1), 9.8);
            Row("compact-velocity", velocity.x + ":" + velocity.y + ":" + velocity.z);
        }
        var finalReader = new NetReader { Buf = new byte[] { 23, 3, 4, 5 }, End = 4, Pos = 2 };
        Row("snapshot-no", EntityProxyPresentation.FinalSnapshot(finalReader, false) == null);
        var snap = EntityProxyPresentation.FinalSnapshot(finalReader, true); finalReader.Buf[1] = 99; Row("snapshot-copy", Convert.ToHexString(snap));
        Entities.ByNet.Clear(); e.Proxy = true; Entities.ByNet[1] = e;
        var renderer = new Renderer { shadowCastingMode = ShadowCastingMode.TwoSided }; var light = new Light { shadows = LightShadows.Soft };
        e.Root.car.Children.Add(renderer); e.Root.car.Children.Add(light);
        EntityProxyPresentation.CopyFarShadowsM = 100; e.Root.car.transform.position = new Vector3(101, 0, 0);
        EntityProxyPresentation.CopyLodTick(); Row("lod-far", renderer.shadowCastingMode + ":" + EntityProxyPresentation.CopyLodOff);
        e.Root.car.transform.position = new Vector3(80, 0, 0); Time.realtimeSinceStartup = 11; EntityProxyPresentation.CopyLodTick(); Row("lod-edge", renderer.shadowCastingMode);
        e.Root.car.transform.position = new Vector3(79, 0, 0); Time.realtimeSinceStartup = 11.999f; EntityProxyPresentation.CopyLodTick(); Row("lod-near-before-timer", renderer.shadowCastingMode);
        Time.realtimeSinceStartup = 12; EntityProxyPresentation.CopyLodTick(); Row("lod-near", renderer.shadowCastingMode + ":" + EntityProxyPresentation.CopyLodOn);
        e.Root.car.transform.position = new Vector3(101, 0, 0); Time.realtimeSinceStartup = 13; EntityProxyPresentation.CopyLodTick();
        Entities.ByNet.Clear(); Time.realtimeSinceStartup = 14; EntityProxyPresentation.CopyLodTick(); Row("lod-no-longer-copy", renderer.shadowCastingMode + ":" + EntityProxyPresentation.CopyLodOn);
        EntityProxyPresentation.CopyLodRestoreAll(); EntityProxyPresentation.CopyFarShadowsM = 0;
        var path = "tools/unit/EntityProxyPresentationRows.txt";
        GoldenRows.UpdateText(path, string.Join("\r\n", rows) + "\r\n");
        Check(File.ReadAllLines(path).SequenceEqual(rows), "presentation decisions, counters, timers and wire bytes match expected rows");
        Check(typeof(EntityProxyPresentation).IsNotPublic && typeof(EntityProxyPresentation).IsAbstract && typeof(EntityProxyPresentation).IsSealed, "presentation owner is internal static");
        foreach (string part in new[] { "CopyLod", "Proxy", "VisibleState" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Split("    public static partial class Entities")[0];
            source = Regex.Replace(source, @"//[^\r\n]*", "");
            Check(!Regex.IsMatch(source, @"using\s+static\s+(?:TLDRevamp\.Net\.)?Entities"), part + " has qualified dependencies");
            Check(Regex.Matches(source, @"Entities\.(\w+)").Cast<Match>().All(m => new[] { "Ent", "IsPresentationCopy", "PresentationEntity", "PresentationHasDamageGrace", "TryPresentationDamageGrace", "RemovePresentationDamageGrace", "PresentationNeedsTorques", "BindPresentationEngine", "TryPresentationEngine", "TryPresentationWheel", "PresentationWheelIndex", "PresentationCars", "PresentationLimbs", "PresentationLimp" }.Contains(m.Groups[1].Value)), part + " uses only narrow presentation operations");
            Check(!Regex.IsMatch(source, @"\b(?:ByNet|Server|Placed|LookupSet|ProxyItems|ProxyEngineOwner|ProxyWheelOwner|ProxyWheelIndex|DamageGraceUntil)\b"), part + " receives no kernel map or record enumeration");
        }
        foreach (string patch in new[] { "NoProxyDamage", "NoDistUnfreezeOnCopies", "NoDistFreezeOnCopies", "ProxyEngine", "ProxyWheelGraphics" })
        {
            Check(typeof(EntityProxyPresentation).GetNestedType(patch, BindingFlags.NonPublic).GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length == 1, patch + " has one attributed owner");
            Check(typeof(Entities).GetNestedType(patch, BindingFlags.NonPublic) == null, patch + " leaves no attributed adapter");
        }
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs").Replace("\r\n", "\n");
        Check(kernel.Contains("e.MadeKinematic.Clear();\n                ApplyProxyBodies(e);\n                e.Ip = new PoseInterpolator();"), "copy body setup stays in the original transition slot");
        Check(kernel.IndexOf("ProxyWheelOwner.Remove(w); ProxyWheelIndex.Remove(w);") < kernel.IndexOf("BindProxyEngine(e);"), "promotion removes wheel bindings before engine rebinding");
        Check(kernel.Contains("DamageGraceUntil[carS] = Time.time + 0.5f;") && kernel.Contains("if (!keepMotion) rb.velocity = e.LastVel;"), "promotion retains grace timer and keep-motion guard");
        Check(kernel.Contains("EntityProxyPresentation.WriteWheels(WS, e); EntityProxyPresentation.WriteEngine(WS, e); EntityCarSignals.WriteSignals(WS, e);") && kernel.Contains("EntityProxyPresentation.ReadWheels(r, e); EntityProxyPresentation.ReadEngine(r, e); EntityCarSignals.ReadSignals(r, e);"), "presentation tail call order remains wheels then engine then signals");
        BridgeReflect.Set("TLDRevamp.Net.EntityProxyPresentation.CopyFarShadowsM", "17");
        Check(EntityProxyPresentation.CopyFarShadowsM == 17, "bridge owner call has one LOD setting");
        EntityProxyPresentation.CopyFarShadowsM = 0;
        BridgeReflect.Set("TLDRevamp.Net.EntityProxyPresentation.ReadCompactMoving", "True");
        Check(EntityProxyPresentation.ReadCompactMoving, "historical compact scratch path remains writable");
        EntityProxyPresentation.ReadCompactMoving = false;
        // Leave shared fixture inputs as the older fixtures expect them.
        grace.Clear(); Entities.ByNet.Clear(); Entities.ProxyEngineOwner.Clear(); Entities.ProxyWheelOwner.Clear(); Entities.ProxyWheelIndex.Clear();
        EntityProxyPresentation.SuppressedDamage = EntityProxyPresentation.HandoverDamageSuppressed = EntityProxyPresentation.ProxyStarterSounds = EntityProxyPresentation.ProxyStopSounds = EntityProxyPresentation.DistFreezeSkipped = 0;
        EntityProxyPresentation.CopyLodOff = EntityProxyPresentation.CopyLodOn = 0; mainscript.s = null; RemotePlayers.Reaches = 0; CultureInfo.CurrentCulture = oldCulture;
    }
}
