using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    // Capture mode is used on 1aa82bc before changing production ownership.
    static void TelemetryChecks()
    {
        TelemetrySourceChecks();
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var rows = new List<string>();
        void Row(string name, string value) => rows.Add(name + "=" + (value ?? "null"));
        void Packet(string name) { var p = Entities.Packets.Last(); Row(name, p.Peer + ":" + p.Reliable + ":" + Convert.ToHexString(p.Bytes)); }
        Entities.ByNet.Clear(); Entities.ClearServer(); Entities.Packets.Clear(); Entities.MyId = 1;
        mainscript.s = null;
        Row("no-car", Entities.CarDiagnostics()); Row("no-lock", Entities.LockDiagnostics());
        Row("empty-status", Entities.Status());
        var player = new fpscontroller(); mainscript.s = new mainscript { player = player };
        settingsscript.s = new settingsscript();
        var root = new tosaveitemscript { name = "car\"test", id = 12, P = new pickupable() };
        var car = root.car = player.Car = new carscript { ParentComponent = root, gear = 2, throttle = .75f, ithrottle = 1, speed = .1f, selectedgr = 1.23456f };
        car.transform.name = "car root";
        car.Engine = new enginescript { running = true, rpm = 1234.5678f, actualOutput = float.NaN, gas = float.PositiveInfinity };
        car.RB = new Rigidbody { mass = 123.4567f, velocity = new Vector3(1.2345f, -2, 3), useGravity = true };
        car.whColliders = new List<BoxCollider> { new BoxCollider(), new BoxCollider { enabled = false }, null };
        var wheel = new WheelCollider { name = "wheel", rpm = 12.34567f, radius = .25f, attachedRigidbody = car.RB, forwardFriction = new WheelFrictionCurve { stiffness = 1.23456f } };
        car.transform.Children.Add(wheel);
        var joint = new Joint { name = "joint", connectedBody = car.RB, breakForce = 12.3456f };
        UnityEngine.Object.Found = new object[] { car, joint };
        var e = new Entities.Ent { NetId = 7, Epoch = 3, OwnerId = 1, Root = root, RootId = 17, LastSentPos = new Vector3d(1, 2, 3) };
        e.Items.Add(root); Entities.ByNet[7] = e;
        Row("car", Entities.CarDiagnostics());
        Time.realtimeSinceStartup = 10;
        EntityDiag.StuckWatch(car);
        Check(Entities.StuckCarJson == null, "stuck car waits 1.5 seconds");
        Time.realtimeSinceStartup = 11.5f; EntityDiag.StuckWatch(car);
        Row("stuck", Entities.StuckCarJson);
        string stuck = Entities.StuckCarJson; car.gear = 3;
        Time.realtimeSinceStartup = 20; EntityDiag.StuckWatch(car);
        Check(Entities.StuckCarJson == stuck, "stuck snapshot replacement waits ten seconds");
        Time.realtimeSinceStartup = 21.5f; EntityDiag.StuckWatch(car);
        Check(Entities.StuckCarJson != stuck, "stuck snapshot replaces at ten seconds");
        car.ithrottle = 0; EntityDiag.StuckWatch(car);
        Check((float)Field(typeof(EntityDiag), "_stuckSince") == -1, "stuck timer clears when throttle releases");
        var pick = new pickupable { dontMount = true, DroppedTime = 8, cols = new List<pickupable.ColliderLink> { null, new pickupable.ColliderLink(), new pickupable.ColliderLink { p = root.P } } };
        var item = new tosaveitemscript { name = "oil can", id = 14, P = pick, started = true, mppickedup = true };
        var ie = new Entities.Ent { NetId = 8, Epoch = 4, Root = item, OwnerId = 2, Proxy = true, Held = true }; ie.Items.Add(item); Entities.ByNet[8] = ie;
        pick.tosave = item; player.inHandP = pick;
        Time.time = 10;
        var mount = new mountStuff { name = "seat", col = new BoxCollider { size = Vector3.one * 4 } };
        UnityEngine.Object.Found = new object[] { mount };
        Row("lock", Entities.LockDiagnostics()); Row("lock-facts", Entities.WhyNotLock(item));
        root.transform.position = new Vector3(3.25f, -4.5f, 5.75f);
        ie.Ip = new PoseInterpolator { Ready = true, LastPos = new Vector3d(6, 7, 8), RenderTime = 12.34567, DelayMs = 123.4, JitterMs = 5.6 };
        Entities.ByNet[9] = new Entities.Ent { NetId = 9, RootId = 19, OwnerId = 2, Proxy = true };
        Row("status", Entities.Status()); Row("nearest-status", Entities.Status(1));
        player.Cam = new Component(); Physics.World = new Collider { name = "aim", ParentComponent = root };
        SessionLog.History.Trace.Nets = new uint[] { 8, 7, 42, 0, 42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        player.pickedUp = new pickupable { tosave = root, name = "dragged" };
        Row("mark-nets", string.Join(",", Entities.MarkNets(99)));
        var state = new TelemetryState { PlayerCount = 3 };
        state.Players[0] = new StatePlayer { Id = 1, HasPos = true };
        state.Players[1] = new StatePlayer { Id = 2, HasPos = true, X = 3, Z = 4 };
        state.Players[2] = new StatePlayer { Id = 3, HasPos = true, X = 6, Z = 8 };
        Row("mark-focus", FocusJson(Entities.CaptureMarkFocus(state)));
        player.Bsitting = true; player.seat = new seatscript { Car = car, name = "driver" };
        root.Children.Add(player.seat);
        Row("seated-focus", FocusJson(Entities.CaptureMarkFocus(state)));
        Physics.World = null; player.Cam = null; player.inHandP = null; player.pickedUp = null; player.Bsitting = false;
        Entities.AnomalyLogCap = 2;
        Entities.AnomalyLine("first\"line"); Entities.AnomalyLine("second"); Entities.AnomalyLine("capped");
        Row("anomaly", Entities.AnomalyJson());
        Entities.TestServer(7, 2);
        var se = Entities.TestSeatServer(7); se.GetType().GetField("Epoch").SetValue(se, 3u);
        byte[] Ask(uint hash, bool tail) => Bytes(w => { w.Write((byte)65); w.Write(7u); w.Write(3u); w.Write(hash); w.Write((byte)2); if (tail) { w.Write((ushort)1); w.Write((ushort)513); w.Write((ushort)65535); } });
        Entities.ServerCensusAsk(300, Reader(Ask(1234, true))); Packet("census-ask-tail");
        Entities.ServerCensusAsk(300, Reader(Ask(1234, false))); Packet("census-ask");
        int sent = Entities.Packets.Count;
        Entities.ServerCensusAsk(2, Reader(Ask(1234, false)));
        Check(Entities.Packets.Count == sent, "census server ignores owner asking itself");
        e.Proxy = false;
        var w = new NetWriter(); w.U8(65); w.U32(7); w.U32(3); w.U32(1234); w.U8(2); w.VarU32(300);
        Entities.ApplyCensusAsk(Reader(w.Buf.Take(w.Len).ToArray())); Packet("census-owner-reply");
        sent = Entities.Packets.Count;
        w.Reset(); w.U8(65); w.U32(7); w.U32(3); w.U32((uint)Invoke(typeof(Entities), "CensusHash", Entities.CensusTestRows)); w.U8(2); w.VarU32(300);
        Entities.ApplyCensusAsk(Reader(w.Buf.Take(w.Len).ToArray()));
        Check(Entities.Packets.Count == sent, "matching census hash stays silent");
        w.Reset(); w.U8(66); w.U32(7); w.VarU32(300); w.U8(2); w.Str("wheel\ncar");
        Entities.ServerCensusReply(2, Reader(w.Buf.Take(w.Len).ToArray())); Packet("census-forward-reply");
        CultureInfo.CurrentCulture = oldCulture;
        const string path = "tools/unit/EntityTelemetryRows.txt";
        if (Environment.GetEnvironmentVariable("CAPTURE_TELEMETRY_HEAD") == "1") File.WriteAllText(path, string.Join("\r\n", rows) + "\r\n");
        else Check(File.ReadAllLines(path).SequenceEqual(rows), "complete diagnostic status focus and census rows match 1aa82bc");
        CoreMembers.Register();
        foreach (var owner in new[] { typeof(EntityDiag), typeof(EntityTelemetry) })
        {
            Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, owner.Name + " is internal static");
            foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
            {
                bool retained = typeof(Entities).GetMember(member.Name, Static).Length > 0;
                foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                    Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "telemetry historical alias " + prefix + member.Name);
            }
        }
        Check(typeof(Entities).GetField("StuckCarJson", Static) == null && typeof(Entities).GetField("_stuckSince", Static) == null && typeof(Entities).GetField("ic", Static) == null, "diagnostic state has one owner");
        Check(BridgeReflect.Set("Entities._stuckSince", "42") == "42" && (float)Field(typeof(EntityDiag), "_stuckSince") == 42, "short stuck timer alias writes owner");
        Entities.StuckCarJson = "saved";
        Check(BridgeReflect.Get("TLDRevamp.Net.Entities.StuckCarJson") == Json.Str("saved") && EntityDiag.StuckCarJson == "saved", "compiled snapshot facade and full bridge path share state");
        Entities.StuckCarJson = null;
        Check(EntityDiag.StuckCarJson == null, "bug report clears feature snapshot through facade");
        BridgeReflect.Call("TLDRevamp.Net.Entities.MarkNets", new[] { "99" });
        Check(Entities.FocusRefreshes > 0 && Entities.LastFocusForced, "marks still force focus refresh before capture");
        car.ChildComponent = root; player.seat = new seatscript { Car = car, name = "driver" }; player.Bsitting = true;
        root.transform.ChildComponent = root; player.seat.transform.parent = root.transform;
        root.transform.Children.Clear(); root.transform.Children.Add(player.seat);
        var seated = Entities.CaptureMarkFocus(state);
        Check(seated.Car?.Net == 7 && seated.Seat == 0, "seated focus uses kernel seat and vehicle index");
        mainscript.s = null;
        Check(Entities.CaptureMarkFocus(state).ClosestPlayer == -1 && Entities.CarDiagnostics() == null && Entities.LockDiagnostics() == null, "focus and diagnostic missing-player guards");
        Check(Entities.DiagnosticMine(e) && !Entities.DiagnosticMine(ie), "diagnostic ownership comparison remains in kernel");
        Check(Entities.CountActive(car.whColliders) == 1 && Entities.CountActive(null) == 0, "Dev collider-count facade preserves enabled and null rules");
        Set(typeof(EntityDiag), "_stuckSince", -1f); Set(typeof(EntityDiag), "_stuckTaken", -100f);
        Entities.ByNet.Clear(); Entities.ClearServer(); Entities.Packets.Clear(); mainscript.s = null; UnityEngine.Object.Found = Array.Empty<object>();
    }

    static string FocusJson(MarkFocus focus)
    {
        string Target(MarkFocus.Target? t) => !t.HasValue ? "null" : t.Value.Net + ":" + t.Value.Prefab + ":" + t.Value.Name + ":" + t.Value.Distance.ToString("R", CultureInfo.InvariantCulture);
        return Target(focus.Crosshair) + "|" + Target(focus.Car) + "|" + focus.Seat + "|" + Target(focus.Held) + "|" + Target(focus.Dragging) + "|" + focus.ClosestPlayer + ":" + focus.PlayerDistance.ToString("R", CultureInfo.InvariantCulture);
    }

    static void TelemetrySourceChecks()
    {
        var hashes = new[] {
            ("Entities.CarDiag.cs", "581595452810A3EDE3535A3BE1B7B177BCCC76B9D0C65DB438E34DCA1D7B796C"),
            ("Entities.LockDiag.cs", "D8E0B5E3958E4E47E02334EF238FA8CFB8C71FEEDB4166DA40819F64B1D5FEB3"),
            ("TelemetryMarks.cs", "4802417848ECF5A0171C8B1AB39C94009A8A4BAE76074EA1DC3EF28C01525D6B"),
            ("Entities.Anomaly.cs", "CA0EC5A4557F46067B47DA8950CE889FFFD802FCCA0E60A193C564CC7E575AE8"),
            ("Entities.Telemetry.cs", "E05F83F680ABD11C390A7C57D82BAA379735782B3E55F26FFE73BFA079C1D135"),
            ("Entities.PartWatch.cs", "5D2DBEDB84FF43137C3F9843DC19C6A73480EBAC031D56E44DE456C7E4970B24"),
            ("Entities.Status.cs", "BFBE9AC0C6D2EF1F6C19D3D45BE157800131E13BFEEC3388499552D82ADEB8C2") };
        foreach (var pair in hashes)
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/" + pair.Item1).Replace("\r\n", "\n").Replace("EntityResync.ResyncForeignAttachDropped", "ResyncForeignAttachDropped");
            source = source.Replace("EntityWorldLeases.AskedKeys", "string.Join(\" | \", LeaseAsked)");
            if (pair.Item1 == "TelemetryMarks.cs")
            {
                source = source.Replace("internal static class EntityTelemetry", "public static partial class Entities");
                source = source.Replace("    }\n    public static partial class Entities\n    {\n        private const int TelemetryRecordLimit", "        private const int TelemetryRecordLimit");
                source = source.Replace("Entities.LocalSeat(", "LocalSeat(").Replace("Entities.VehicleSeats(", "VehicleSeats(");
                source = source.Replace("Entities.MarkIsLocal(p.Id)", "p.Id == MyId");
                source = source.Replace("uint net = Entities.MarkItemNet(item);", "var entity = item != null ? SharedGroupOf(item) : null;");
                source = source.Replace("Net = net, Prefab = item", "Net = entity != null ? entity.NetId : 0, Prefab = item");
                source = source.Replace("Entities.RefreshMarkFocus();", "TelemetryFocusTick(Time.realtimeSinceStartupAsDouble, true);");
            }
            if (pair.Item1 == "Entities.CarDiag.cs" || pair.Item1 == "Entities.LockDiag.cs")
            {
                string[] allowed = { "DiagnosticNet", "DiagnosticMine", "DiagnosticProxy", "DiagnosticStoreRefusals", "TryGetEntity" };
                Check(!source.Contains("using static") && Regex.Matches(source, @"Entities\.(\w+)").Cast<Match>().All(m => allowed.Contains(m.Groups[1].Value)), pair.Item1 + " uses only narrow kernel calls");
                source = source.Replace("internal static partial class EntityDiag", "public static partial class Entities");
                source = source.Replace("Entities.DiagnosticNet(", "TelemetryNet(").Replace("Entities.TryGetEntity(", "ByNet.TryGetValue(");
                source = source.Replace("Entities.DiagnosticMine(e)", "e.OwnerId == MyId").Replace("Entities.DiagnosticProxy(m)", "IsProxy(m)");
                source = source.Replace("Entities.DiagnosticStoreRefusals(false)", "ContainerStoresRefused").Replace("Entities.DiagnosticStoreRefusals(true)", "AttachedStoresRefused");
                source = source.Replace("internal static void StuckWatch(", "private static void StuckWatch(");
                source = source.Replace("internal static readonly System.Globalization.CultureInfo ic", "private static readonly System.Globalization.CultureInfo ic");
            }
            Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source))) == pair.Item2, pair.Item1 + " complete source matches 1aa82bc after boundary substitutions");
        }
        string marks = File.ReadAllText("src/TLDRevamp/Net/TelemetryMarks.cs");
        string feature = marks.Substring(marks.IndexOf("    internal static class EntityTelemetry"));
        feature = feature.Substring(0, feature.IndexOf("    public static partial class Entities"));
        string[] markAllowed = { "LocalSeat", "VehicleSeats", "MarkIsLocal", "MarkItemNet", "RefreshMarkFocus" };
        Check(!feature.Contains("using static") && Regex.Matches(feature, @"Entities\.(\w+)").Cast<Match>().All(m => markAllowed.Contains(m.Groups[1].Value)), "mark focus has no map writer or broad observation boundary");
        string doors = File.ReadAllText("src/TLDRevamp/Net/Entities.CarDoors.cs");
        Check(doors.IndexOf("RestoreStoredDoorAttaches();") < doors.IndexOf("Entities.TelemetryRender();") && doors.IndexOf("Entities.TelemetryRender();") < doors.IndexOf("DevHooks.EntityLateSample?.Invoke();"), "late sample keeps restore render Dev order");
        string reset = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(!reset.Contains("StuckCarJson") && !reset.Contains("_stuckSince") && !reset.Contains("_stuckTaken"), "session reset preserves existing stuck snapshot and timer lifetime");
    }
}
