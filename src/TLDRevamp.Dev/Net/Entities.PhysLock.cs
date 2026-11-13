using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPhysLockTools
    {

        /// Test (bridge `mp lockdiag <net>`): how this machine treats a copy that a container may hold — which branch of the
        /// copy-lock rules it is in and the numbers they compare.
        public static string LockDiag(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e)) return "{\"error\":\"no such object here\"}";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("F3", ci);
            string V(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
            bool resolved = Resolve(e);
            var p = e.Root != null ? e.Root.P : null;
            var sb = new System.Text.StringBuilder("{\"net\":" + net + ",\"resolved\":" + (resolved ? "true" : "false") + ",\"proxy\":" + (e.Proxy ? "true" : "false") +
                ",\"owner\":" + e.OwnerId + ",\"held\":" + (e.Held ? "true" : "false") + ",\"stored\":" + (e.Stored ? "true" : "false") + ",\"carriedBy\":" + e.CarriedBy +
                ",\"physical\":" + (e.Physical ? "true" : "false") + ",\"movedByBody\":" + (e.MovedByBody ? "true" : "false") + ",\"kinematic\":" + (e.Root != null && e.Root.GetComponent<Rigidbody>() != null && e.Root.GetComponent<Rigidbody>().isKinematic ? "true" : "false") + ",\"part\":" + e.PartIndex + ",\"items\":" + e.Items.Count +
                ",\"recv\":" + e.Recv + ",\"lockOffSince\":" + e.LockOffSince.ToString("F1", ci) + ",\"ripReady\":" + (e.Ip != null && e.Ip.Ready ? "true" : "false"));
            if (e.Root != null)
            {
                var t = e.Root.transform;
                var rb = e.Root.GetComponent<Rigidbody>();
                sb.Append(",\"parent\":" + Json.Str(t.parent != null ? t.parent.name : "") + ",\"rootOf\":" + Json.Str(t.root.name) + ",\"body\":" + Json.Str(rb == null ? "none" : rb.isKinematic ? "kin" : "free") +
                          ",\"ridesParent\":" + (RidesParent(e) ? "true" : "false") + ",\"worn\":" + (WornRides(e) ? "true" : "false"));
                if (e.Ip != null && e.Ip.Ready) sb.Append(",\"ownerPoseOff\":" + F(Vector3.Distance(mainscript.UnityPosFromGlobal(e.Ip.LastPos), t.position)));
            }
            if (p != null && p.physlock != null)
            {
                var cr = p.physlock.transform.root;
                var ce = ProxyOf(cr);
                bool outNow = OwnerHasItOut(e, p);
                sb.Append(",\"lock\":{\"container\":" + Json.Str(cr.name) + ",\"mount\":" + Json.Str(p.physlock.name) + ",\"self\":" + (e.Root != null && cr == e.Root.transform ? "true" : "false") +
                          ",\"containerNet\":" + (ce != null ? ce.NetId : 0) + ",\"containerOwner\":" + (ce != null ? ce.OwnerId : -1) + ",\"containerIpReady\":" + (ce != null && ce.Ip != null && ce.Ip.Ready ? "true" : "false") +
                          ",\"ownerHasItOut\":" + (outNow ? "true" : "false") + ",\"lockedAway\":" + (LockedAway(e) ? "true" : "false") + ",\"rides\":" + (RidesOwnersContainer(e) ? "true" : "false") +
                          ",\"local\":" + V(TLDRevamp.Net.Entities.ObservedDiagLocal) + ",\"boxMin\":" + V(TLDRevamp.Net.Entities.ObservedDiagBox.min) + ",\"boxMax\":" + V(TLDRevamp.Net.Entities.ObservedDiagBox.max));
                if (e.Ip != null && e.Ip.Ready) sb.Append(",\"offMount\":" + F(Vector3.Distance(mainscript.UnityPosFromGlobal(e.Ip.LastPos), p.physlock.transform.position)));
                sb.Append("}");
            }
            else sb.Append(",\"lock\":null");
            return sb.Append("}").ToString();
        }

        /// Test hook (bridge `mp copystore <item> <car>`): our copy of the item stored into a container of our copy of the
        /// car, this machine only — what a copy's own scan did by mistake.
        public static string TestCopyStore(uint item, uint car)
        {
            if (!ObservedEntities.TryGetValue(item, out var ie) || !ie.Proxy || !Resolve(ie) || ie.Root == null || ie.Root.P == null) return "{\"error\":\"no copy of the item here\"}";
            if (!ObservedEntities.TryGetValue(car, out var ce) || !Resolve(ce) || ce.Root == null) return "{\"error\":\"no car here\"}";
            mountStuff best = null;
            foreach (var m in ce.Root.transform.root.GetComponentsInChildren<mountStuff>(true)) if (m != null) { best = m; if (m.sType.ToString().ToLower().Contains("trunk") || m.sType.ToString().ToLower().Contains("car")) break; }
            if (best == null) return "{\"error\":\"no container on the car\"}";
            ie.Root.transform.position = best.transform.position + Vector3.up * 0.2f;
            best.Store(ie.Root.P, true);
            return "{\"stored\":" + Json.Str(ie.Root.name) + ",\"in\":" + Json.Str(best.name + " (" + best.sType + ")") + ",\"physlock\":" + (ie.Root.P.physlock != null ? "true" : "false") + ",\"ipReady\":" + (ie.Ip != null && ie.Ip.Ready ? "true" : "false") +
                   ",\"physical\":" + (ie.Physical ? "true" : "false") + ",\"byBody\":" + (ie.MovedByBody ? "true" : "false") + ",\"carriedBy\":" + ie.CarriedBy + "}";
        }

        /// Diagnostics (bridge `mp physlock`): the local setting, the host's, what the game's checks use here, and for the
        /// nearest containers of each kind whether they would hold an item now (the game's own CanStore).
        public static string PhysLockStatus(double gx, double gz, float radius)
        {
            int local = settingsscript.s != null && settingsscript.s.S != null ? settingsscript.s.S.IPhysicsLock : -1;
            int used = settingsscript.s != null && settingsscript.s.S != null ? PhysicsLockSetting(settingsscript.s.S) : -1;
            var kinds = new SortedDictionary<string, string>();
            foreach (var m in Object.FindObjectsOfType<mountStuff>())
            {
                if (m == null) continue;
                var g = mainscript.GlobalFromUnityPos(m.transform.position);
                if ((g.x - gx) * (g.x - gx) + (g.z - gz) * (g.z - gz) > radius * radius) continue;
                string k = m.sType.ToString();
                if (kinds.ContainsKey(k)) continue;
                bool holds = false;
                try { holds = m.CanStore(); } catch { }
                kinds[k] = holds ? "true" : "false";
            }
            var rows = new List<string>();
            foreach (var kv in kinds) rows.Add(Json.Str(kv.Key) + ":" + kv.Value);
            return "{\"local\":" + local + ",\"host\":" + HostPhysicsLock + ",\"used\":" + used + ",\"patched\":" + PhysLockFromHost.Patched +
                   ",\"sent\":" + PhysLockSent + ",\"applied\":" + PhysLockApplied + ",\"reads\":" + PhysLockReads + ",\"storesRefused\":" + ContainerStoresRefused + ",\"copyLocksReleased\":" + CopyLocksReleased + ",\"startFreezesSkipped\":" + CopyStartFreezesSkipped +
                   ",\"holds\":{" + string.Join(",", rows) + "}}";
        }

        [HarmonyPatch]
        internal static class PhysLockFromHost
        {
            private static readonly FieldInfo Field = AccessTools.Field(typeof(settingsscript.setting), nameof(settingsscript.setting.IPhysicsLock));
            private static readonly MethodInfo Replacement = AccessTools.Method(typeof(Entities), nameof(PhysicsLockSetting));
            public static int Patched;

            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(mountStuff), nameof(mountStuff.CanStore));
                yield return AccessTools.Method(typeof(mountStuff), "Update");
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var ins in instructions)
                {
                    if (ins.LoadsField(Field)) { ins.opcode = OpCodes.Call; ins.operand = Replacement; Patched++; }   // stack: setting -> int
                    yield return ins;
                }
            }
        }
    }
}
