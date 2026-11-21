using System;
using System.Linq;
using UnityEngine;
using static TLDRevamp.DebugBridge;

namespace TLDRevamp
{
    internal static class EntityCommands
    {
        internal static string Run(string command, string arg)
        {
            var ma = arg.Split(' ');
            var mic = System.Globalization.CultureInfo.InvariantCulture;
            switch (command)
            {
                case "mp recsize": return Net.EntityDebug.RecordSize();
                case "mp copies": return Net.EntityDebug.CopiesToggle(ma[1], ma.Length > 2 && ma[2] == "on");
                case "mp sitobj": return Net.EntityDebug.SitObj(uint.Parse(ma[1]));
                case "mp entstats": return Net.EntityDebug.ResetStats();
                case "mp jumps": return Net.EntityDebug.JumpLog();
                case "mp entdebug": return Net.EntityDebug.Debug();
                case "mp worldcar": return Net.EntityDebug.WorldCar();
                case "mp pickupnear": return Net.EntityDebug.PickupNear(ma.Length > 1 ? float.Parse(ma[1], System.Globalization.CultureInfo.InvariantCulture) : 4f);
                case "mp drop": return Net.EntityDebug.DropHeld();
                case "mp throwpoint": return Net.EntityDebug.ThrowAtPoint(double.Parse(ma[1], System.Globalization.CultureInfo.InvariantCulture), double.Parse(ma[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ma[3], System.Globalization.CultureInfo.InvariantCulture));
                case "mp throwat": return Net.EntityDebug.ThrowAt(uint.Parse(ma[1]), float.Parse(ma[2], System.Globalization.CultureInfo.InvariantCulture));
                case "mp sitpassenger": return Net.EntityDebug.SitPassenger(uint.Parse(ma[1]));
                case "mp freezeroot":
                {
                    // test: freeze the root body of <net> here as the game would (isKinematic), optionally marked buried
                    // (the game's own unfreeze then skips it for good)
                    if (!Net.Entities.ObservedEntities.TryGetValue(uint.Parse(ma[1]), out var ze) || ze.Root == null) return "{\"error\":\"no such object here\"}";
                    var zrb = ze.Root.GetComponent<Rigidbody>();
                    if (zrb == null) return "{\"error\":\"no body\"}";
                    if (ma.Length > 2 && ma[2] == "unbury") { ze.Root.UnBury(); return "{\"unburied\":true}"; }   // the game's own, as a shovel ends it
                    zrb.isKinematic = true;
                    if (ma.Length > 2 && ma[2] == "buried") ze.Root.buried = true;
                    int zm = 0;   // "all": every member with a body of its own too (a door or hood on a joint)
                    if (ma.Length > 2 && ma[2] == "all")
                        foreach (var zi in ze.Items)
                        {
                            var zmb = zi != null && zi != ze.Root ? zi.GetComponent<Rigidbody>() : null;
                            if (zmb != null && !zmb.isKinematic) { zmb.isKinematic = true; zm++; }
                        }
                    return "{\"kinematic\":true,\"buried\":" + (ze.Root.buried ? "true" : "false") + ",\"members\":" + zm + ",\"proxy\":" + (ze.Proxy ? "true" : "false") + "}";
                }
                case "mp ownerhistory": return Net.Entities.OwnerHistoryJson(uint.Parse(ma[1]));
                case "mp forgetowner":
                {
                    // test: this machine forgets that it owns <net> (its copy role and owner back to [owner], default the
                    // host) while the server keeps it as ours — what a lost or out-of-order Owner message leaves behind
                    if (!Net.Entities.ObservedEntities.TryGetValue(uint.Parse(ma[1]), out var fe)) return "{\"error\":\"no such object here\"}";
                    fe.OwnerId = ma.Length > 2 ? int.Parse(ma[2]) : 0;
                    Net.Entities.SetProxy(fe, true);
                    return "{\"proxy\":" + (fe.Proxy ? "true" : "false") + ",\"owner\":" + fe.OwnerId + "}";
                }
                case "mp carbreak": return Net.EntityDebug.CarBreak(uint.Parse(ma[1]));
                case "mp interp": return Net.EntityDebug.InterpDump(uint.Parse(ma[1]));
                case "mp copystate": return Net.EntityDebug.CopyState(uint.Parse(ma[1]));
                case "mp paint":
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    UnityEngine.Color? pc = ma.Length >= 5 ? new UnityEngine.Color(float.Parse(ma[2], ci), float.Parse(ma[3], ci), float.Parse(ma[4], ci)) : (UnityEngine.Color?)null;
                    return Net.EntityDebug.PaintLab(uint.Parse(ma[1]), pc);
                }
                case "mp fluids": return Net.EntityDebug.Fluids(uint.Parse(ma[1]));
                case "mp tankexplode": return Net.EntityDebug.TankExplode(uint.Parse(ma[1]), int.Parse(ma[2]), ma.Length > 3 ? float.Parse(ma[3], System.Globalization.CultureInfo.InvariantCulture) : 1000f);
                case "mp fill": return Net.EntityDebug.Fill(uint.Parse(ma[1]), int.Parse(ma[2]), ma[3], float.Parse(ma[4], System.Globalization.CultureInfo.InvariantCulture));
                case "mp pour": return ma[1] == "stop" ? Net.EntityDebug.Pour(0, 0, 0, false) : ma[1] == "status" ? Net.EntityDebug.PourStatus() : Net.EntityDebug.Pour(uint.Parse(ma[1]), uint.Parse(ma[2]), int.Parse(ma[3]), true);
                case "mp sitdriver": return Net.EntityDebug.SitDriver(uint.Parse(ma[1]));
                case "mp seats": return Net.EntityDebug.SeatDump(uint.Parse(ma[1]));
                case "mp takeout": { var cic8 = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.TakeOut(uint.Parse(ma[1]), float.Parse(ma[2], cic8), float.Parse(ma[3], cic8)); }
                case "mp lights": return Net.EntityDebug.Lights(uint.Parse(ma[1]));
                case "mp switch": return Net.EntityDebug.TurnSwitch(uint.Parse(ma[1]));
                case "mp wheels": return Net.EntityDebug.WheelsInfo(uint.Parse(ma[1]));
                case "mp wheelover": return Net.EntityDebug.WheelOver(uint.Parse(ma[1]), uint.Parse(ma[2]));
                case "mp moveitem": { var cic5 = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.MoveItem(uint.Parse(ma[1]), float.Parse(ma[2], cic5), float.Parse(ma[3], cic5), ma.Length > 4 && ma[4] == "ground"); }
                case "mp saverecords": return Net.EntityDebug.SaveRecords(ma.Length > 1 ? ma[1] : "");
                case "mp farstray": return Net.EntityDebug.FarStray(uint.Parse(ma[1]), double.Parse(ma[2], System.Globalization.CultureInfo.InvariantCulture));
                case "mp farstore": { var cic7 = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.FarStore(double.Parse(ma[1], cic7), double.Parse(ma[2], cic7), double.Parse(ma[3], cic7)); }
                case "mp sitestate": { var cic6 = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.SiteState(double.Parse(ma[1], cic6), double.Parse(ma[2], cic6), double.Parse(ma[3], cic6)); }
                case "mp itemslist": { var cic4 = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.ItemsList(double.Parse(ma[1], cic4), double.Parse(ma[2], cic4), double.Parse(ma[3], cic4)); }
                case "mp itemsnear": { var cic3 = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.ItemsNear(double.Parse(ma[1], cic3), double.Parse(ma[2], cic3), double.Parse(ma[3], cic3)); }
                case "mp carsnear": { var cic2 = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.CarsNear(double.Parse(ma[1], cic2), double.Parse(ma[2], cic2), double.Parse(ma[3], cic2)); }
                case "mp detachpart": return Net.EntityDebug.DetachPart(int.Parse(ma[1]), ma.Length > 2 ? uint.Parse(ma[2]) : 0);
                case "mp telemetrydesync": return Net.EntityDebug.TelemetryDesync(uint.Parse(ma[1]), int.Parse(ma[2]));
                case "mp pickupnet": return Net.EntityDebug.PickupNet(uint.Parse(ma[1]), ma.Length > 2 && ma[2] == "here");
                case "mp fakeinput": return Net.EntityFakeInput.FakeInput(ma);
                case "mp track": return Net.EntityTrack.Track(ma);
                case "mp myhands": return Net.EntityFakeInput.MyHands();
                case "mp itemcheck": return Net.EntityItemCheck.ItemCheck(uint.Parse(ma[1]), ma.Length > 2 ? ma[2] : "");
                case "mp physlocks": { var ci = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.PhysLocks(double.Parse(ma[1], ci), double.Parse(ma[2], ci), ma.Length > 3 ? double.Parse(ma[3], ci) : 60); }
                case "mp sleepall": { var ci = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.SleepAll(double.Parse(ma[1], ci), double.Parse(ma[2], ci), ma.Length > 3 ? double.Parse(ma[3], ci) : 60); }
                case "mp awake": { var ci = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.Awake(double.Parse(ma[1], ci), double.Parse(ma[2], ci), ma.Length > 3 ? double.Parse(ma[3], ci) : 60); }
                case "mp shared": return Net.EntityDebug.SharedList(ma.Length > 1 ? ma[1] : "");
                case "mp relays": return Net.EntityDebug.RelayList();
                case "mp registry": return Net.EntityDebug.RegistryList();
                case "mp registryserver": return Net.EntityDebug.RegistryServerList();
                case "mp registrystore": return Net.EntityDebug.RegistryStoreList(ma.Length > 1 ? ma[1] : "");
                case "mp itement": { var ci = System.Globalization.CultureInfo.InvariantCulture; return Net.EntityDebug.ItemEnt(double.Parse(ma[1], ci), double.Parse(ma[2], ci), double.Parse(ma[3], ci)); }
                case "mp partposes": return Net.EntityDebug.PartPoses(uint.Parse(ma[1]));
                case "mp members": return Net.EntityDebug.Members(uint.Parse(ma[1]));
                case "mp wrench": return Net.EntityDebug.Wrench(uint.Parse(ma[1]), int.Parse(ma[2]), ma.Length > 3 && ma[3] == "force");
                case "mp xdelete": return Net.EntityDebug.XDelete(uint.Parse(ma[1]), ma.Length > 2 ? int.Parse(ma[2]) : -1);
                case "mp falloff": return Net.EntityDebug.FallOff(uint.Parse(ma[1]), int.Parse(ma[2]));
                case "mp glue": return ma.Length > 2 ? Net.EntityDebug.GlueTo(uint.Parse(ma[1]), uint.Parse(ma[2]), ma.Length > 3 ? float.Parse(ma[3], System.Globalization.CultureInfo.InvariantCulture) : 0f, ma.Length > 4 && ma[4] == "keep") : Net.EntityDebug.Glueables();
                case "mp attachpose": return Net.EntityDebug.AttachPoseOf(uint.Parse(ma[1]));
                case "mp crashstop": return Net.EntityDebug.CrashStop(uint.Parse(ma[1]));
                case "mp partin": return Net.EntityDebug.PartIn(uint.Parse(ma[1]), uint.Parse(ma[2]));
                case "mp attachto": return Net.EntityDebug.AttachTo(uint.Parse(ma[1]), uint.Parse(ma[2]), int.Parse(ma[3]));
                case "mp lookpick": return Net.EntityDebug.LookPick(uint.Parse(ma[1]), ma.Length > 2 && ma[2] == "pick");
                case "mp lookmount": return Net.EntityDebug.LookMount(uint.Parse(ma[1]), int.Parse(ma[2]));
                case "mp attachedparts": return Net.EntityDebug.AttachedParts(uint.Parse(ma[1]));
                case "mp carslots": return Net.EntityDebug.CarSlots(uint.Parse(ma[1]));
                case "mp namecount": return Net.EntityDebug.NameCount(ma[1]);
                case "mp gameoff": return Net.EntityDebug.GameOff(ma[1]);
                case "mp localoff": return Net.EntityDebug.LocalOff(uint.Parse(ma[1]), int.Parse(ma[2]));
                case "mp carparts": return Net.EntityDebug.CarParts(uint.Parse(ma[1]));
                case "mp caps": return Net.EntityDebug.Caps(uint.Parse(ma[1]));
                case "mp sent": return Net.EntityDebug.ServerEntity(uint.Parse(ma[1]));
                case "mp forceheld": return Net.EntityDebug.SetForceHeld(uint.Parse(ma[1]));
                case "mp claim": return Net.EntityDebug.ClaimTest(uint.Parse(ma[1]));
                case "mp handcopy": return Net.EntityDebug.TestHandCopy(uint.Parse(ma[1]));
                case "mp losecopy": return Net.EntityDebug.TestLoseCopy(uint.Parse(ma[1]));
                case "mp poitanks": return Net.EntityDebug.TestPoiTanks(float.Parse(ma[1], System.Globalization.CultureInfo.InvariantCulture));
                case "mp localstate": return Net.EntityDebug.LocalState(uint.Parse(ma[1]), int.Parse(ma[2]));
                case "mp usecap": return Net.EntityDebug.UseCap(uint.Parse(ma[1]), int.Parse(ma[2]));
                case "mp pcs": return Net.EntityDebug.PartConds(uint.Parse(ma[1]));
                case "mp wheelpose": return Net.EntityDebug.WheelPose(uint.Parse(ma[1]));
                case "mp farrec": return Net.EntityDebug.FarRec(uint.Parse(ma[1]));
                case "mp breakstate": return Net.EntityDebug.BreakState(uint.Parse(ma[1]));
                case "mp ragdollcycle": return Net.EntityDebug.RagdollCycle(uint.Parse(ma[1]));
                case "mp breakent": return Net.EntityDebug.BreakEnt(uint.Parse(ma[1]), ma.Length > 2 ? float.Parse(ma[2], System.Globalization.CultureInfo.InvariantCulture) : 1e6f);
                case "mp ownerlog": return Net.EntityDebug.OwnerLogJson(ma.Length > 1 ? uint.Parse(ma[1]) : 0u);
                case "mp contacts": return Net.EntityContactCensus.Contacts(ma.Length > 1 ? ma[1] : "");
                case "mp entstate": return Net.EntityDebug.EntState(uint.Parse(ma[1]));
                case "mp resyncdiff": return Net.EntityDebug.ResyncDiff(uint.Parse(ma[1]));
                case "mp entitems": return Net.EntityDebug.EntItems(uint.Parse(ma[1]));
                case "mp entpoke": return Net.EntityDebug.EntPoke(uint.Parse(ma[1]), ma[2], ma.Length > 4 ? int.Parse(ma[3]) : 0, float.Parse(ma[ma.Length - 1], System.Globalization.CultureInfo.InvariantCulture));
                case "seatraw": return Net.EntityDebug.SeatRaw();
                case "seatlook": return Net.EntityDebug.SeatLook();
                case "seats": return Net.EntityDebug.SeatList(uint.Parse(arg.Trim()));
                case "sitseat":
                {
                    var sp = arg.Split(' ');
                    // sitseat <idx> [redirect] [net=<id>]: a seat of the nearest car, or of shared vehicle <id>
                    uint sn = 0; foreach (var w in sp) if (w.StartsWith("net=")) sn = uint.Parse(w.Substring(4));
                    return Net.EntityDebug.SitSeat(int.Parse(sp[0]), System.Array.IndexOf(sp, "redirect") > 0, sn);
                }
                case "wheelinfo": return Net.EntityDebug.WheelInfo(!string.IsNullOrEmpty(arg) ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 30f);
                case "dropheld": return Net.EntityDebug.DropHeld();
                case "mount": return Net.EntityMountLab.MountLab(arg);
                default: return "{\"error\":\"unknown developer command\"}";
            }
        }
    }
}
