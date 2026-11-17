using System;
using static TLDRevamp.GroundPlacement;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using static TLDRevamp.DebugBridge;

namespace TLDRevamp
{
    internal static class DevCommands
    {
        internal static void Install()
        {
            DebugBridge.Register("mp recsize", arg => EntityCommands.Run("mp recsize", arg));
            DebugBridge.Register("mp copies", arg => EntityCommands.Run("mp copies", arg));
            DebugBridge.Register("mp sitobj", arg => EntityCommands.Run("mp sitobj", arg));
            DebugBridge.Register("mp entstats", arg => EntityCommands.Run("mp entstats", arg));
            DebugBridge.Register("mp jumps", arg => EntityCommands.Run("mp jumps", arg));
            DebugBridge.Register("mp entdebug", arg => EntityCommands.Run("mp entdebug", arg));
            DebugBridge.Register("mp worldcar", arg => EntityCommands.Run("mp worldcar", arg));
            DebugBridge.Register("mp pickupnear", arg => EntityCommands.Run("mp pickupnear", arg));
            DebugBridge.Register("mp drop", arg => EntityCommands.Run("mp drop", arg));
            DebugBridge.Register("mp throwpoint", arg => EntityCommands.Run("mp throwpoint", arg));
            DebugBridge.Register("mp throwat", arg => EntityCommands.Run("mp throwat", arg));
            DebugBridge.Register("mp sitpassenger", arg => EntityCommands.Run("mp sitpassenger", arg));
            DebugBridge.Register("mp carbreak", arg => EntityCommands.Run("mp carbreak", arg));
            DebugBridge.Register("mp interp", arg => EntityCommands.Run("mp interp", arg));
            DebugBridge.Register("mp copystate", arg => EntityCommands.Run("mp copystate", arg));
            DebugBridge.Register("mp paint", arg => EntityCommands.Run("mp paint", arg));
            DebugBridge.Register("mp fluids", arg => EntityCommands.Run("mp fluids", arg));
            DebugBridge.Register("mp tankexplode", arg => EntityCommands.Run("mp tankexplode", arg));
            DebugBridge.Register("mp fill", arg => EntityCommands.Run("mp fill", arg));
            DebugBridge.Register("mp pour", arg => EntityCommands.Run("mp pour", arg));
            DebugBridge.Register("mp sitdriver", arg => EntityCommands.Run("mp sitdriver", arg));
            DebugBridge.Register("mp seats", arg => EntityCommands.Run("mp seats", arg));
            DebugBridge.Register("mp takeout", arg => EntityCommands.Run("mp takeout", arg));
            DebugBridge.Register("mp lights", arg => EntityCommands.Run("mp lights", arg));
            DebugBridge.Register("mp switch", arg => EntityCommands.Run("mp switch", arg));
            DebugBridge.Register("mp wheels", arg => EntityCommands.Run("mp wheels", arg));
            DebugBridge.Register("mp wheelover", arg => EntityCommands.Run("mp wheelover", arg));
            DebugBridge.Register("mp moveitem", arg => EntityCommands.Run("mp moveitem", arg));
            DebugBridge.Register("mp saverecords", arg => EntityCommands.Run("mp saverecords", arg));
            DebugBridge.Register("mp farstray", arg => EntityCommands.Run("mp farstray", arg));
            DebugBridge.Register("mp farstore", arg => EntityCommands.Run("mp farstore", arg));
            DebugBridge.Register("mp sitestate", arg => EntityCommands.Run("mp sitestate", arg));
            DebugBridge.Register("mp itemslist", arg => EntityCommands.Run("mp itemslist", arg));
            DebugBridge.Register("mp itemsnear", arg => EntityCommands.Run("mp itemsnear", arg));
            DebugBridge.Register("mp carsnear", arg => EntityCommands.Run("mp carsnear", arg));
            DebugBridge.Register("mp detachpart", arg => EntityCommands.Run("mp detachpart", arg));
            DebugBridge.Register("mp telemetrydesync", arg => EntityCommands.Run("mp telemetrydesync", arg));
            DebugBridge.Register("mp pickupnet", arg => EntityCommands.Run("mp pickupnet", arg));
            DebugBridge.Register("mp fakeinput", arg => EntityCommands.Run("mp fakeinput", arg));
            DebugBridge.Register("mp track", arg => EntityCommands.Run("mp track", arg));
            DebugBridge.Register("mp myhands", arg => EntityCommands.Run("mp myhands", arg));
            DebugBridge.Register("mp itemcheck", arg => EntityCommands.Run("mp itemcheck", arg));
            DebugBridge.Register("mp physlocks", arg => EntityCommands.Run("mp physlocks", arg));
            DebugBridge.Register("mp sleepall", arg => EntityCommands.Run("mp sleepall", arg));
            DebugBridge.Register("mp awake", arg => EntityCommands.Run("mp awake", arg));
            DebugBridge.Register("mp shared", arg => EntityCommands.Run("mp shared", arg));
            DebugBridge.Register("mp relays", arg => EntityCommands.Run("mp relays", arg));
            DebugBridge.Register("mp registry", arg => EntityCommands.Run("mp registry", arg));
            DebugBridge.Register("mp registryserver", arg => EntityCommands.Run("mp registryserver", arg));
            DebugBridge.Register("mp registrystore", arg => EntityCommands.Run("mp registrystore", arg));
            DebugBridge.Register("mp itement", arg => EntityCommands.Run("mp itement", arg));
            DebugBridge.Register("mp partposes", arg => EntityCommands.Run("mp partposes", arg));
            DebugBridge.Register("mp members", arg => EntityCommands.Run("mp members", arg));
            DebugBridge.Register("mp wrench", arg => EntityCommands.Run("mp wrench", arg));
            DebugBridge.Register("mp xdelete", arg => EntityCommands.Run("mp xdelete", arg));
            DebugBridge.Register("mp falloff", arg => EntityCommands.Run("mp falloff", arg));
            DebugBridge.Register("mp glue", arg => EntityCommands.Run("mp glue", arg));
            DebugBridge.Register("mp attachpose", arg => EntityCommands.Run("mp attachpose", arg));
            DebugBridge.Register("mp crashstop", arg => EntityCommands.Run("mp crashstop", arg));
            DebugBridge.Register("mp partin", arg => EntityCommands.Run("mp partin", arg));
            DebugBridge.Register("mp attachto", arg => EntityCommands.Run("mp attachto", arg));
            DebugBridge.Register("mp lookpick", arg => EntityCommands.Run("mp lookpick", arg));
            DebugBridge.Register("mp lookmount", arg => EntityCommands.Run("mp lookmount", arg));
            DebugBridge.Register("mp attachedparts", arg => EntityCommands.Run("mp attachedparts", arg));
            DebugBridge.Register("mp carslots", arg => EntityCommands.Run("mp carslots", arg));
            DebugBridge.Register("mp namecount", arg => EntityCommands.Run("mp namecount", arg));
            DebugBridge.Register("mp gameoff", arg => EntityCommands.Run("mp gameoff", arg));
            DebugBridge.Register("mp localoff", arg => EntityCommands.Run("mp localoff", arg));
            DebugBridge.Register("mp carparts", arg => EntityCommands.Run("mp carparts", arg));
            DebugBridge.Register("mp caps", arg => EntityCommands.Run("mp caps", arg));
            DebugBridge.Register("mp sent", arg => EntityCommands.Run("mp sent", arg));
            DebugBridge.Register("mp forceheld", arg => EntityCommands.Run("mp forceheld", arg));
            DebugBridge.Register("mp claim", arg => EntityCommands.Run("mp claim", arg));
            DebugBridge.Register("mp handcopy", arg => EntityCommands.Run("mp handcopy", arg));
            DebugBridge.Register("mp losecopy", arg => EntityCommands.Run("mp losecopy", arg));
            DebugBridge.Register("mp poitanks", arg => EntityCommands.Run("mp poitanks", arg));
            DebugBridge.Register("mp localstate", arg => EntityCommands.Run("mp localstate", arg));
            DebugBridge.Register("mp usecap", arg => EntityCommands.Run("mp usecap", arg));
            DebugBridge.Register("mp pcs", arg => EntityCommands.Run("mp pcs", arg));
            DebugBridge.Register("mp wheelpose", arg => EntityCommands.Run("mp wheelpose", arg));
            DebugBridge.Register("mp farrec", arg => EntityCommands.Run("mp farrec", arg));
            DebugBridge.Register("mp breakstate", arg => EntityCommands.Run("mp breakstate", arg));
            DebugBridge.Register("mp ragdollcycle", arg => EntityCommands.Run("mp ragdollcycle", arg));
            DebugBridge.Register("mp breakent", arg => EntityCommands.Run("mp breakent", arg));
            DebugBridge.Register("mp ownerlog", arg => EntityCommands.Run("mp ownerlog", arg));
            DebugBridge.Register("mp contacts", arg => EntityCommands.Run("mp contacts", arg));
            DebugBridge.Register("mp entstate", arg => EntityCommands.Run("mp entstate", arg));
            DebugBridge.Register("mp resyncdiff", arg => EntityCommands.Run("mp resyncdiff", arg));
            DebugBridge.Register("mp entitems", arg => EntityCommands.Run("mp entitems", arg));
            DebugBridge.Register("mp entpoke", arg => EntityCommands.Run("mp entpoke", arg));
            DebugBridge.Register("seatraw", arg => EntityCommands.Run("seatraw", arg));
            DebugBridge.Register("seatlook", arg => EntityCommands.Run("seatlook", arg));
            DebugBridge.Register("seats", arg => EntityCommands.Run("seats", arg));
            DebugBridge.Register("sitseat", arg => EntityCommands.Run("sitseat", arg));
            DebugBridge.Register("wheelinfo", arg => EntityCommands.Run("wheelinfo", arg));
            DebugBridge.Register("dropheld", arg => EntityCommands.Run("dropheld", arg));
            DebugBridge.Register("mount", arg => EntityCommands.Run("mount", arg));
            DebugBridge.Register("poiretype", arg => Run("poiretype", arg));
            DebugBridge.Register("poihide", arg => Run("poihide", arg));
            DebugBridge.Register("poiwait", arg => Run("poiwait", arg));
            DebugBridge.Register("pois", arg => Run("pois", arg));
            DebugBridge.Register("cinema", arg => Run("cinema", arg));
            DebugBridge.Register("replay", arg => Run("replay", arg));
            DebugBridge.Register("terrainhorizon", arg => Run("terrainhorizon", arg));
            DebugBridge.Register("terrainhorizonstats", arg => Run("terrainhorizonstats", arg));
            DebugBridge.Register("counters", arg => Run("counters", arg));
            DebugBridge.Register("repro", arg => Run("repro", arg));
            DebugBridge.Register("items", arg => Run("items", arg));
            DebugBridge.Register("physics", arg => Run("physics", arg));
            DebugBridge.Register("followdump", arg => Run("followdump", arg));
            DebugBridge.Register("render", arg => Run("render", arg));
            DebugBridge.Register("cameras", arg => Run("cameras", arg));
            DebugBridge.Register("renderinv", arg => Run("renderinv", arg));
            DebugBridge.Register("skinned", arg => Run("skinned", arg));
            DebugBridge.Register("rtoggle", arg => Run("rtoggle", arg));
            DebugBridge.Register("mirrors", arg => Run("mirrors", arg));
            DebugBridge.Register("particles", arg => Run("particles", arg));
            DebugBridge.Register("uievents", arg => Run("uievents", arg));
            DebugBridge.Register("uiclick", arg => Run("uiclick", arg));
            DebugBridge.Register("uihover", arg => Run("uihover", arg));
            DebugBridge.Register("uiscroll", arg => Run("uiscroll", arg));
            DebugBridge.Register("revampscroll", arg => Run("revampscroll", arg));
            DebugBridge.Register("settingsinst", arg => Run("settingsinst", arg));
            DebugBridge.Register("worldhash", arg => Run("worldhash", arg));
            DebugBridge.Register("gensettings", arg => Run("gensettings", arg));
            DebugBridge.Register("heightdbg", arg => Run("heightdbg", arg));
            DebugBridge.Register("stale", arg => Run("stale", arg));
            DebugBridge.Register("traffic", arg => Run("traffic", arg));
            DebugBridge.Register("aimlab", arg => Run("aimlab", arg));
            DebugBridge.Register("streamlab", arg => Run("streamlab", arg));
            DebugBridge.Register("vehlab", arg => Run("vehlab", arg));
            DebugBridge.Register("fakeplayers", arg => Run("fakeplayers", arg));
            DebugBridge.Register("uidump", arg => Run("uidump", arg));
            DebugBridge.Register("matshare", arg => Run("matshare", arg));
            DebugBridge.Register("instinfo", arg => Run("instinfo", arg));
            DebugBridge.Register("instancing", arg => Run("instancing", arg));
            DebugBridge.Register("visroots", arg => Run("visroots", arg));
            DebugBridge.Register("rendertime", arg => Run("rendertime", arg));
            DebugBridge.Register("camera", arg => Run("camera", arg));
            DebugBridge.Register("drivein", arg => Run("drivein", arg));
            DebugBridge.Register("driveout", arg => Run("driveout", arg));
            DebugBridge.Register("carsnear", arg => Run("carsnear", arg));
            DebugBridge.Register("roadpoint", arg => Run("roadpoint", arg));
            DebugBridge.Register("brakecar", arg => Run("brakecar", arg));
            DebugBridge.Register("nudge", arg => Run("nudge", arg));
            DebugBridge.Register("fuelup", arg => Run("fuelup", arg));
            DebugBridge.Register("poialign", arg => Run("poialign", arg));
            DebugBridge.Register("slotdestroys", arg => Run("slotdestroys", arg));
            DebugBridge.Register("spawncarg", arg => Run("spawncarg", arg));
            DebugBridge.Register("worldgen", arg => Run("worldgen", arg));
            DebugBridge.Register("alignstats", arg => Run("alignstats", arg));
            DebugBridge.Register("randbench", arg => Run("randbench", arg));
            DebugBridge.Register("allocprobe", arg => Run("allocprobe", arg));
            DebugBridge.Register("alloc", arg => Run("alloc", arg));
            DebugBridge.Register("carface", arg => Run("carface", arg));
            DebugBridge.Register("near", arg => Run("near", arg));
            DebugBridge.Register("autodrive", arg => Run("autodrive", arg));
            DebugBridge.Register("sleepcars", arg => Run("sleepcars", arg));
            DebugBridge.Register("carsleep", arg => Run("carsleep", arg));
            DebugBridge.Register("wheelbench", arg => Run("wheelbench", arg));
            DebugBridge.Register("awake", arg => Run("awake", arg));
            DebugBridge.Register("clearpile", arg => Run("clearpile", arg));
            DebugBridge.Register("crash", arg => Run("crash", arg));
            DebugBridge.Register("spawncar", arg => Run("spawncar", arg));
            DebugBridge.Register("spawnpile", arg => Run("spawnpile", arg));
            DebugBridge.Register("loadwatch", arg => Run("loadwatch", arg));
            DebugBridge.Register("loadtrace", arg => Run("loadtrace", arg));
            DebugBridge.Register("attachlog", arg => Run("attachlog", arg));
            DebugBridge.Register("modscan", arg => Run("modscan", arg));
            DebugBridge.Register("banner", arg => Run("banner", arg));
            DebugBridge.Register("mp pbot", arg => Run("mp pbot", arg));
            DebugBridge.Register("mp holdnet", arg => Run("mp holdnet", arg));
            DebugBridge.Register("mp walkat", arg => Run("mp walkat", arg));
            DebugBridge.Register("mp holdfill", arg => Run("mp holdfill", arg));
            DebugBridge.Register("mp debugv2", arg => Run("mp debugv2", arg));
            DebugBridge.Register("mp health", arg => Run("mp health", arg));
            DebugBridge.Register("mp ai", arg => Run("mp ai", arg));
            DebugBridge.Register("mp aiids", arg => Run("mp aiids", arg));
            DebugBridge.Register("mp aihead", arg => Run("mp aihead", arg));
            DebugBridge.Register("mp aicols", arg => Run("mp aicols", arg));
            DebugBridge.Register("mp aivision", arg => Run("mp aivision", arg));
            DebugBridge.Register("net pair", arg => Run("net pair", arg));
            DebugBridge.Register("net status", arg => Run("net status", arg));
            DebugBridge.Register("net relay", arg => Run("net relay", arg));
            DebugBridge.Register("net items", arg => Run("net items", arg));
            DebugBridge.Register("net ghost", arg => Run("net ghost", arg));
            DebugBridge.Register("net ghostall", arg => Run("net ghostall", arg));
            DebugBridge.Register("net links", arg => Run("net links", arg));
            DebugBridge.Register("net ownership", arg => Run("net ownership", arg));
            DebugBridge.Register("net compare", arg => Run("net compare", arg));
            DebugBridge.Register("net events", arg => Run("net events", arg));
            DebugBridge.Register("net stop", arg => Run("net stop", arg));
            DebugBridge.Register("net probe", arg => Run("net probe", arg));
            DebugBridge.Register("net lag", arg => Run("net lag", arg));
            DebugBridge.Register("net conditions", arg => Run("net conditions", arg));
        }
        private static string Run(string cmd, string arg)
        {
            var p = Plugin.Instance;
            var ma = arg.Split(' '); var na = ma; var mic = System.Globalization.CultureInfo.InvariantCulture;
            switch (cmd)
            {
                case "cinema": return Cinematic.CinematicBridge.Command(arg);
                case "replay": return Replay.ReplayBridge.Command(arg);
                // The horizon diagnostics (issue #37). The tool tools/terrainhorizon.py sends exactly this command,
                // so without the case the tool could never produce any evidence: the bridge answered "unknown
                // command" and the tool stopped. It is a pure read, so it needs no developer access.
                case "terrainhorizon": return Fixes.TerrainHorizonLab.Snapshot(arg);
                // This fix's own counters: what it drew, what it cost and how often it ran.
                case "terrainhorizonstats": return Fixes.TerrainHorizonLab.Stats();
                case "counters": return CounterProbe.Run(arg);
                case "repro": return ReproPlayback.Command(arg);
                case "items": return PhysicsLab.Items(arg);
                case "physics": return PhysicsLab.Stats();
                case "followdump": return PhysicsLab.FollowDump(arg.Length > 0 ? int.Parse(arg) : 6);
                case "render": return PhysicsLab.RenderStats();
                case "cameras": return PhysicsLab.Cameras();
                case "renderinv": return RenderLab.Inventory();
                case "skinned": return RenderLab.Skinned();
                case "rtoggle": { var tv = arg.Split(' '); return RenderLab.Toggle(tv[0], tv.Length > 1 ? tv[1] : ""); }
                case "mirrors": return RenderLab.Mirrors();
                case "particles": return RenderLab.Particles();
                case "uievents": return UiLab.Events(arg);
                case "uiclick": return UiLab.Click(arg);
                case "uihover": return UiLab.Hover(arg);
                case "uiscroll": return UiLab.Scroll(arg);
                case "revampscroll":
                {
                    // test: scroll the open Revamp tab (1 top, 0 bottom) — screenshots of its lower sections; "info V" scrolls
                    // the explanation panel instead
                    bool infoPanel = arg.StartsWith("info ", StringComparison.Ordinal);
                    if (infoPanel) arg = arg.Substring(5);
                    foreach (var sr in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.ScrollRect>())
                    {
                        var srPath = UiLab.PathOf(sr.transform);
                        if (srPath.Contains("TabRevamp") && srPath.Contains("RevampInfo") == infoPanel)
                        { sr.verticalNormalizedPosition = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture); return "{\"ok\":true}"; }
                    }
                    return "{\"error\":\"Revamp tab not open\"}";
                }
                case "settingsinst": return UiLab.SettingsInstances();
                case "worldhash":
                {
                    var wa = arg.Split(' ');
                    var ic = System.Globalization.CultureInfo.InvariantCulture;
                    return WorldHash.Hash(double.Parse(wa[0], ic), double.Parse(wa[1], ic), double.Parse(wa[2], ic), wa.Length > 3 && wa[3] == "detail");
                }
                case "gensettings": return WorldHash.GenSettings();
                case "heightdbg": { var ha = arg.Split(' '); var hic = System.Globalization.CultureInfo.InvariantCulture; return WorldHash.HeightDbg(double.Parse(ha[0], hic), double.Parse(ha[1], hic)); }
                case "stale": return WorldHash.Stale(arg == "detail");
                case "traffic": return AutopilotBridge.Status();
                case "aimlab":
                {
                    var al = arg.Trim().Split(' ');
                    return al[0] == "start" ? Diagnostics.AimLab.Start(uint.Parse(al[1])) : al[0] == "stop" ? Diagnostics.AimLab.Stop() : Diagnostics.AimLab.Status();
                }
                case "streamlab":
                {
                    var sa = arg.Split(' ');
                    if (sa[0] == "start") return Net.StreamLab.Start(sa.Length > 1 ? float.Parse(sa[1], System.Globalization.CultureInfo.InvariantCulture) : 0.5f);
                    return Net.StreamLab.Status();
                }
                case "vehlab":
                {
                    var va = arg.Split(' ');
                    switch (va[0])
                    {
                        case "start": return Net.VehicleLab.Start(va.Length > 1 ? float.Parse(va[1], System.Globalization.CultureInfo.InvariantCulture) : 6f);
                        case "status": return Net.VehicleLab.Status();
                        case "stop": return Net.VehicleLab.Stop();
                        default: return "{\"error\":\"vehlab start [offset] | status | stop\"}";
                    }
                }
                case "fakeplayers":
                {
                    var fa = arg.Split(' ');
                    var ic = System.Globalization.CultureInfo.InvariantCulture;
                    return MpLab.Set(int.Parse(fa[0]), fa.Length > 1 ? float.Parse(fa[1], ic) : 25f, fa.Length > 2 ? float.Parse(fa[2], ic) : 5000f);
                }
                case "uidump": { var ua = arg.Split(' '); return UiLab.Dump(ua[0], ua.Length > 1 ? int.Parse(ua[1]) : 3, ua.Length > 2 && ua[2] == "full"); }
                case "matshare": return RenderLab.MaterialSharing(arg);
                case "instinfo": return RenderLab.InstancingInfo();
                case "instancing": return RenderLab.InstancingOn();
                case "visroots": return RenderLab.VisibleByRoot(arg.Length > 0 ? int.Parse(arg) : 25);
                case "rendertime": return arg == "stop" ? RenderLab.TimingStop() : RenderLab.TimingStart();
                case "camera":
                {
                    var camArgs = arg.Split(' ');
                    return PhysicsLab.SetCamera(camArgs[0], camArgs[1] == "on");
                }
                case "drivein":
                {
                    if (arg.Length == 0) return DriveLab.GetIn();
                    var dv = arg.Split(' ');
                    var ic = System.Globalization.CultureInfo.InvariantCulture;
                    return DriveLab.GetIn(new Vector3(float.Parse(dv[0], ic), float.Parse(dv[1], ic), float.Parse(dv[2], ic)));
                }
                case "driveout": return DriveLab.GetOut();
                case "carsnear": return DriveLab.CarsNear(arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 300f);
                case "roadpoint": return DriveLab.RoadPoint();
                case "brakecar": return DriveLab.Brake();
                case "nudge": return DriveLab.Nudge(arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 12f);
                case "fuelup": return DriveLab.FuelUp();
                case "poialign": return Diagnostics.PoiAlignLog.Query(arg);
                case "slotdestroys": return "{\"count\":" + Diagnostics.SlotPartDestroyLog.Count + ",\"log\":" + Json.Str(Diagnostics.SlotPartDestroyLog.Log) + "}";
                case "spawncarg":
                {
                    // a test car ON the ground at a GLOBAL position (each machine converts with its own floating origin)
                    var sg = arg.Split(' ');
                    var uvc = mainscript.UnityPosFromGlobal(new Vector3d(double.Parse(sg[2], System.Globalization.CultureInfo.InvariantCulture), 0, double.Parse(sg[3], System.Globalization.CultureInfo.InvariantCulture)));
                    var ghc = StandHit(uvc.x, uvc.z, null);   // the road or ground, not a roof
                    if (ghc == null) return "{\"error\":\"no ground generated there\"}";
                    return PhysicsLab.SpawnCar(int.Parse(sg[0]), int.Parse(sg[1]), new Vector3(uvc.x, ghc.Value.point.y + 0.6f, uvc.z), sg.Length > 4 ? float.Parse(sg[4], System.Globalization.CultureInfo.InvariantCulture) : 0f);
                }
                case "worldgen": return arg == "reset" ? WorldGenLab.Reset() : WorldGenLab.Snapshot();
                case "alignstats": return WorldGenLab.AlignStats();
                case "randbench": return WorldGenLab.RandBench(arg.Length > 0 ? int.Parse(arg) : 20000);
                case "allocprobe": return WorldGenLab.AllocProbe();
                case "alloc": return WorldGenLab.AllocStats(arg == "reset");
                case "carface":
                {
                    var fv = arg.Split(' ');
                    var ic = System.Globalization.CultureInfo.InvariantCulture;
                    return DriveLab.FaceCar(new Vector3(float.Parse(fv[0], ic), float.Parse(fv[1], ic), float.Parse(fv[2], ic)), float.Parse(fv[3], ic));
                }
                case "near": return DriveLab.Near(arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 15f);
                case "autodrive":
                    if (arg.StartsWith("on")) return DriveLab.Start(arg.Length > 3 ? float.Parse(arg.Substring(3), System.Globalization.CultureInfo.InvariantCulture) : 20f);
                    if (arg == "off") return DriveLab.Stop();
                    return DriveLab.Stats();
                case "sleepcars": return PhysicsLab.SleepCars();
                case "carsleep": return PhysicsLab.CarSleepState();
                case "wheelbench": return PhysicsLab.WheelBench(arg.Length > 0 ? int.Parse(arg) : 100);
                case "awake": return PhysicsLab.Awake(arg.Length > 0 ? int.Parse(arg) : 25);
                case "clearpile": return PhysicsLab.ClearPile();
                case "crash":
                {
                    // collision tests: `crash rec start [radius]`, `crash rec stop`, `crash launch <m/s> [gx gz]`
                    var cra = arg.Split(' '); var cic = System.Globalization.CultureInfo.InvariantCulture;
                    if (cra[0] == "rec") return cra.Length > 1 && cra[1] == "stop" ? CrashLab.Stop(cra.Length > 2 ? float.Parse(cra[2], cic) : -1f) : CrashLab.Start(cra.Length > 2 ? float.Parse(cra[2], cic) : 80f);
                    if (cra[0] == "cars") return CrashLab.CarsNear(cra.Length > 1 ? float.Parse(cra[1], cic) : 40f);
                    if (cra[0] == "launch")
                    {
                        bool hold = cra[cra.Length - 1] == "hold"; int nargs = hold ? cra.Length - 1 : cra.Length;
                        return CrashLab.Launch(float.Parse(cra[1], cic), nargs > 3 ? new Vector3d(double.Parse(cra[2], cic), 0, double.Parse(cra[3], cic)) : (Vector3d?)null, hold);
                    }
                    return "{\"error\":\"crash rec start|stop, crash launch <m/s> [gx gz]\"}";
                }
                case "spawncar": { var sa = arg.Split(' '); return PhysicsLab.SpawnCar(int.Parse(sa[0]), int.Parse(sa[1]), new Vector3(float.Parse(sa[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(sa[3], System.Globalization.CultureInfo.InvariantCulture), float.Parse(sa[4], System.Globalization.CultureInfo.InvariantCulture)), sa.Length > 5 ? float.Parse(sa[5], System.Globalization.CultureInfo.InvariantCulture) : 0f); }
                case "spawnpile":
                {
                    // spawnpile <ids,comma> <count> <x> <y> <z> <cols> <spacing> <height> <seed>
                    var a = arg.Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    return PhysicsLab.SpawnPile(a[0], int.Parse(a[1]),
                        new Vector3(float.Parse(a[2], ci), float.Parse(a[3], ci), float.Parse(a[4], ci)),
                        int.Parse(a[5]), float.Parse(a[6], ci), float.Parse(a[7], ci), int.Parse(a[8]));
                }
                case "loadwatch": return Fixes.LoadWatch.Stats();
                case "loadtrace": return "{\"trace\":" + Json.Str(Fixes.LoadWatch.TraceText) + "}";
                case "attachlog": return Diagnostics.AttachLog.Stats;
                case "modscan":
                    // the TLD Loader mod scanner (LoaderScan): one DLL, or every mod in BepInEx/TLDLoaderMods
                    return string.IsNullOrWhiteSpace(arg) ? LoaderScan.ScanAll() : LoaderScan.ScanAndWrite(arg.Trim());
                case "banner":
                {
                    if (arg == "off") return Net.Banner.Show(0f, "");
                    int sp = arg.IndexOf(' ');
                    return Net.Banner.Show(float.Parse(arg.Substring(0, sp), System.Globalization.CultureInfo.InvariantCulture), arg.Substring(sp + 1));
                }

                case "mp pbot": return Net.PlayerBots.Command(ma.Skip(1).ToArray());   // player bots: a third player for ownership tests (Net/PlayerBot.cs)
                case "mp holdnet": return Net.FillLab.HoldNet(uint.Parse(ma[1]));
                case "mp walkat": return Net.FillLab.WalkAt(uint.Parse(ma[1]), float.Parse(ma[2], System.Globalization.CultureInfo.InvariantCulture));
                case "mp holdfill": return ma[1] == "stop" ? Net.FillLab.Stop() : ma[1] == "status" ? Net.FillLab.Status() : Net.FillLab.Start(uint.Parse(ma[1]), uint.Parse(ma[2]), int.Parse(ma[3]), ma.Length > 4 ? float.Parse(ma[4], System.Globalization.CultureInfo.InvariantCulture) : 0f, ma.Length > 5 ? float.Parse(ma[5], System.Globalization.CultureInfo.InvariantCulture) : 0.7f);
                case "mp debugv2": return DebugV2.Command(ma);
                case "mp health": return ItemHealth.Command(ma);
                case "mp ai": return Net.AiProbe.List();
                case "mp aiids": return Net.AiProbe.Ids();
                case "mp aihead": return Net.AiProbe.Heads();
                case "mp aicols": return Net.AiProbe.Cols(ma.Length > 1 ? ma[1] : "");
                case "mp aivision": return Net.AiProbe.Vision();
                case "net pair": return Net.NetLab.Pair(na.Length > 1 ? int.Parse(na[1]) : 1000, na.Length > 2 ? int.Parse(na[2]) : 64, na.Length > 3 && na[3] == "loopback");
                case "net status": return Net.NetLab.Status();
                case "net relay": return Net.NetLab.Relay(na.Length > 1 ? na[1] : "off");
                case "net items":
                    if (na.Length > 1 && (na[1] == "show" || na[1] == "hide")) { Net.ItemTraffic.SetVisible(na[1] == "show"); return "{\"show\":" + (Net.ItemTraffic.Show ? "true" : "false") + "}"; }
                    if (na.Length > 1 && na[1] == "labels") return ItemTrafficLabels.Json();
                    return Net.ItemTraffic.ListJson(20);
                case "net ownership": case "net ghost": case "net ghostall": case "net links":
                    return WorldOverlays.Command(na[0], na.Length > 1 ? na[1] : "state");
                case "net compare": return ObjectPanels.Command(true, na);
                case "net events": return ObjectPanels.Command(false, na);
                case "net stop": return Net.NetLab.Stop();
                case "net probe": return Net.NetLab.Probe(int.Parse(na[1]));
                case "net lag":
                {
                    var lic = System.Globalization.CultureInfo.InvariantCulture;
                    return Net.NetLab.Lag(int.Parse(na[1]), na.Length > 2 ? float.Parse(na[2], lic) : 0f, na.Length > 3 ? float.Parse(na[3], lic) : 0f, na.Length > 4 ? int.Parse(na[4]) : 0);
                }
                case "net conditions": return NetworkConditions.Command(string.Join(" ", na.Skip(1)));

                case "poiretype": { var pr = arg.Split(' '); return PoisProbe.PoiRetype(pr[0], int.Parse(pr[1])); }
                case "poihide": return PoisProbe.PoiHide(arg.Trim());
                case "poiwait": { var pw = arg.Split(' '); var ciw = System.Globalization.CultureInfo.InvariantCulture; return PoisProbe.PoiWait(double.Parse(pw[0], ciw), double.Parse(pw[1], ciw), double.Parse(pw[2], ciw)); }
                case "pois": { var pp = arg.Split(' '); var cip = System.Globalization.CultureInfo.InvariantCulture; return PoisProbe.Pois(double.Parse(pp[0], cip), double.Parse(pp[1], cip), double.Parse(pp[2], cip)); }
                default: return "{\"error\":\"unknown developer command\"}";
            }
        }
    }
}
