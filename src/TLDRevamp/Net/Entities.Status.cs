using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static Vector3d StoredPos(uint id)
        {
            var data = savedatascript.s != null ? savedatascript.s.data : null;
            return data != null && data.itemData != null && id != 0 && savedatascript.IndexOfID(data.itemData.items, id, out int k) ? data.itemData.items[k].transform.pos : new Vector3d(0, 0, 0);
        }

        /// What this machine thinks the entity's velocity is: its own body (simulated here), the owner's last (a copy).
        private static string Vel(Ent e)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var rb = e.Root.GetComponent<Rigidbody>();
            var v = e.Proxy ? e.LastVel : rb != null ? rb.velocity : Vector3.zero;
            return "[" + v.x.ToString("F2", ic) + "," + v.y.ToString("F2", ic) + "," + v.z.ToString("F2", ic) + "]";
        }

        internal static string V3(Vector3d v)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            return "[" + v.x.ToString("F2", ic) + "," + v.y.ToString("F2", ic) + "," + v.z.ToString("F2", ic) + "]";
        }

        private static float Dist2(Ent e, Vector3 from) => e.Root != null ? (e.Root.transform.position - from).sqrMagnitude : float.MaxValue;

        /// limit: only that many entities in the list, the nearest to the player first (the counters are always complete).
        /// A report in a big world listed 4,330 of them, 2.3 MB built on the main thread: a 1.3 s freeze when the report was taken.
        internal static string Status(int limit = int.MaxValue)
        {
            var rows = new List<string>();
            IEnumerable<Ent> listed = ByNet.Values;
            if (ByNet.Count > limit)
            {
                var from = mainscript.s != null && mainscript.s.player != null ? mainscript.s.player.transform.position : Vector3.zero;
                var nearest = new List<Ent>(ByNet.Values);
                nearest.Sort((a, b) => Dist2(a, from).CompareTo(Dist2(b, from)));
                nearest.RemoveRange(limit, nearest.Count - limit);
                listed = nearest;
            }
            foreach (var e in listed)
            {
                var g = e.Root != null ? mainscript.GlobalFromUnityPos(e.Root.transform.position) : StoredPos(e.RootId);
                var q = e.Root != null ? e.Root.transform.rotation : Quaternion.identity;
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                int attached = 0;
                foreach (var it in e.Items) if (it != null && it.attachable != null && it.attachable.attached) attached++;
                rows.Add("{\"net\":" + e.NetId + ",\"saveId\":" + e.RootId + ",\"car\":" + (e.Root != null && e.Root.car != null ? "true" : "false") + ",\"owner\":" + e.OwnerId + ",\"name\":" + Json.Str(e.Root != null ? e.Root.name : "") + ",\"parent\":" + e.ParentNet + ",\"part\":" + e.PartIndex + ",\"attached\":" + attached + ",\"owner\":" + e.OwnerId + ",\"epoch\":" + e.Epoch + ",\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"upy\":" + (e.Root != null ? e.Root.transform.up.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "1") +
                         ",\"items\":" + e.Items.Count + ",\"driven\":" + (e.Driven ? "true" : "false") + ",\"held\":" + (e.Proxy ? (e.Held ? "true" : "false") : (e.Root != null && IsHeld(e.Root) ? "true" : "false")) +
                         ",\"dormant\":" + (e.Root == null ? "true" : "false") + ",\"stored\":" + (e.Proxy ? (e.Stored ? "true" : "false") : (IsStored(e.Root) ? "true" : "false")) +
                         ",\"shown\":" + (e.Root != null && e.Root.P != null && e.Root.P.disableThisWhenStored != null ? (e.Root.P.disableThisWhenStored.gameObject.activeSelf ? "true" : "false") : "null") + ",\"pos\":[" + g.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + g.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + g.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "]" +
                         ",\"ghost\":" + (e.CargoGhost ? "true" : "false") +
                         ",\"rot\":[" + q.x.ToString("F6", ic) + "," + q.y.ToString("F6", ic) + "," + q.z.ToString("F6", ic) + "," + q.w.ToString("F6", ic) + "]" +
                         (e.Ip != null ? ",\"delayMs\":" + e.Ip.DelayMs.ToString("F0") + ",\"jitterMs\":" + e.Ip.JitterMs.ToString("F0") + ",\"extrapolating\":" + (e.Ip.Extrapolating ? "true" : "false") : "") +
                         // desync in the owner's clock (twocars): an owner's row says when (its state stamps' clock), a copy's row
                         // which moment of the owner's clock it shows — the bridge call's own timing drops out
                         (!e.Proxy ? ",\"clk\":" + (Time.unscaledTimeAsDouble - (Time.timeAsDouble - Time.fixedTimeAsDouble)).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                                   : e.Ip != null && e.Ip.Ready ? ",\"rt\":" + e.Ip.RenderTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) : "") +
                         (e.Root != null ? ",\"yaw\":" + e.Root.transform.eulerAngles.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"vel\":" + Vel(e) : "") +
                         (e.Root != null && e.Root.car != null && e.Root.car.Engine != null
                             ? ",\"engRunning\":" + (e.Root.car.Engine.running ? "true" : "false") + ",\"engStart\":" + (e.Root.car.Engine.start ? "true" : "false") + ",\"starterSounds\":" + ProxyStarterSounds + ",\"stopSounds\":" + ProxyStopSounds + ",\"engRpm\":" + e.Root.car.Engine.rpm.ToString("F0") +
                               ",\"engTemp\":" + e.Root.car.Engine.temp.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                               ",\"boil\":" + (e.Root.car.CoolantBoil != null && e.Root.car.CoolantBoil.isPlaying ? "true" : "false") +
                               ",\"steam\":" + (e.Root.car.CoolantSteam != null && e.Root.car.CoolantSteamEmission.enabled ? e.Root.car.CoolantSteamEmission.rateOverTime.constant : 0f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                               ",\"coolant\":" + (e.Root.car.coolant != null && e.Root.car.coolant.coolant != null && e.Root.car.coolant.coolant.F != null ? e.Root.car.coolant.coolant.F.GetAmount() : -1f).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                               ",\"fuel\":" + (e.Root.car.Tank != null && e.Root.car.Tank.F != null ? e.Root.car.Tank.F.GetAmount().ToString("F4", System.Globalization.CultureInfo.InvariantCulture) : "-1")
                             : "") +
                         ",\"frames\":" + e.Frames + ",\"extrapFrames\":" + e.ExtrapFrames + ",\"jumps\":" + e.Jumps + ",\"maxDevCm\":" + e.MaxDevCm.ToString("F1") +
                         ",\"physical\":" + (e.Physical ? "true" : "false") + ",\"partsColOff\":" + (e.PartsColOff ? "true" : "false") + ",\"byBody\":" + (e.MovedByBody ? "true" : "false") +
                         ",\"seqOut\":" + e.SeqOut + ",\"recv\":" + e.Recv + ",\"why\":[" + e.WhyAwake + "," + e.WhyPos + "," + e.WhyRot + "," + e.WhyStored + "]" + ",\"sentRest\":" + (e.SentAtRest ? "true" : "false") +
                         ",\"lastSent\":" + V3(e.LastSentPos) + ",\"lastIn\":" + (e.Ip != null && e.Ip.Ready ? V3(e.Ip.LastPos) : "null") + "}");
            }
            return "{\"me\":" + MyId + ",\"resync\":{\"sent\":" + ResyncsSent + ",\"applied\":" + ResyncsApplied + ",\"itemsLoaded\":" + ResyncItemsLoaded + ",\"unchanged\":" + ResyncSkipped + ",\"partStates\":" + PartStatesChanged + ",\"bytes\":" + ResyncBytes + ",\"itemsSent\":" + ResyncItemsSent + ",\"captureMsMax\":" + ResyncCaptureMsMax.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"captureMsTotal\":" + ResyncCaptureMsTotal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",\"held\":" + ResyncHeld + ",\"budgetFrames\":" + ResyncBudgetFrames + ",\"listsApplied\":" + StateListsApplied + ",\"listsSame\":" + StateListsSame + ",\"attachReapplied\":" + AttachReapplied + ",\"splitSkipped\":" + ResyncSplitSkipped + ",\"foreignAttachDropped\":" + EntityResync.ResyncForeignAttachDropped + ",\"handoversSent\":" + HandoversSent + ",\"handoversApplied\":" + HandoversApplied + ",\"handoversLate\":" + HandoversLate + ",\"handoversCrashSkipped\":" + HandoversCrashSkipped + ",\"attachesKept\":" + AttachesKept + ",\"ragdollLimbsRestored\":" + RagdollLimbsRestored + ",\"attachesReplayed\":" + AttachesReplayed + ",\"handoverLateWhy\":" + Json.Str(HandoverLateWhy) + ",\"handoverDamageSuppressed\":" + HandoverDamageSuppressed + "},\"edits\":{\"marked\":" + EditsMarked + ",\"sent\":" + EditsSent + ",\"applied\":" + EditsApplied + ",\"rejected\":" + EditsRejected + ",\"marks\":" + Json.Str(EditMarks) + "},\"rebound\":" + Rebound + ",\"membersRebound\":" + MembersRebound + ",\"farRecordUpdates\":" + FarRecordUpdates + ",\"farAdds\":" + FarAdds + ",\"farResyncs\":" + FarResyncs + ",\"farResyncItems\":" + FarResyncItems + ",\"farRechunked\":" + FarRechunked + ",\"farAttachedMoved\":" + FarAttachedMoved + ",\"reshared\":" + Reshared + ",\"orphansCleared\":" + OrphansCleared + ",\"leaseKeys\":" + Json.Str(string.Join(" | ", LeaseAsked)) + ",\"leasesDone\":" + LeasesDone + ",\"leasesFreed\":" + LeasesFreed + ",\"leaseRefusedWhy\":" + LeaseRefusalsJson() + ",\"builtInReEnabled\":" + BuiltInReEnabled + ",\"spawnShared\":" + SpawnShared + ",\"contactSent\":" + ContactSent + ",\"contactRelayed\":" + ContactRelayed + ",\"contactApplied\":" + ContactApplied + ",\"count\":" + ByNet.Count + ",\"pendingShare\":" + PendingShare.Count + ",\"serverEnts\":" + Server.Count + ",\"claimsSent\":" + ClaimsSent + ",\"claimsRefused\":" + ClaimsRefused + ",\"refusedWhy\":{\"crash\":" + RefusedCrash + ",\"held\":" + RefusedHeld + ",\"stored\":" + RefusedStored + ",\"driven\":" + RefusedDriven + "}" + ",\"partsReported\":" + PartsReported + ",\"partsApplied\":" + PartsApplied + ",\"partsAppliedStored\":" + PartsAppliedStored + ",\"partRejects\":" + Json.Str(PartRejects) + ",\"suppressedDamage\":" + SuppressedDamage + ",\"suppressedFallOff\":" + SuppressedFallOff + ",\"attach\":{\"sent\":" + AttachesSent + ",\"applied\":" + AttachesApplied + ",\"same\":" + AttachesSame + ",\"failed\":" + AttachesFailed + ",\"fails\":" + Json.Str(AttachFails.Length > 300 ? AttachFails.Substring(AttachFails.Length - 300) : AttachFails) + ",\"detachSyncsSent\":" + DetachSyncsSent + ",\"detachSyncsApplied\":" + DetachSyncsApplied + ",\"detachReqsSent\":" + DetachReqsSent + ",\"detachReqsApplied\":" + DetachReqsApplied + ",\"autoDetachesIgnored\":" + AutoDetachesIgnored + ",\"cargoClaimed\":" + CargoClaimed + ",\"cargoNear\":" + CargoClaimedNear + ",\"cargoGhostsMade\":" + CargoGhostsMade + ",\"ghostNets\":[" + string.Join(",", CargoGhostNets) + "]" + ",\"itemNear\":" + ItemClaimedNear + ",\"itemOnContact\":" + ItemClaimedOnContact + ",\"gameFrozenReleased\":" + GameFrozenReleased + ",\"cargoOnContact\":" + CargoClaimedOnContact + ",\"heldShown\":" + HeldShown + ",\"contents\":{\"claimed\":" + ContentsClaimed + ",\"onOwner\":" + ContentsOnOwner + ",\"onPlace\":" + ContentsOnPlace + ",\"stored\":" + ContentsStored + "}}" + ",\"statesSent\":" + StatesSent + ",\"statesIn\":" + StatesIn + ",\"stale\":" + StatesStale + ",\"spawned\":" + Spawned +
                   ",\"addRejects\":" + AddRejects + ",\"addRejectLast\":" + Json.Str(AddRejectLast) + ",\"ownerChanges\":" + OwnerChanges + ",\"silentTakeovers\":" + SilentTakeovers + ",\"pushes\":[" + PushesSent + "," + PushesApplied + "," + PushFrames + "]" + ",\"entities\":[" + string.Join(",", rows) + "]}";
        }
    }
}
