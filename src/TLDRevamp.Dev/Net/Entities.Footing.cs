using System.Linq;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityFootingTools
    {

        /// Test (`mp footprobe`): what our player stands on — the game's ground sensor, the ray down, the feet's IK targets
        /// against the surface (a stander's legs went through the roof on the other screen).
        public static string FootProbe()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            System.Func<float, string> F = x => x.ToString("F3", ci);
            var gs = pl.grounded;
            string sensor = gs == null ? "null" : "{\"sensed\":" + (gs.sensed ? "true" : "false") + ",\"T\":" + Json.Str(gs.T != null ? gs.T.name : "") +
                            ",\"layer\":" + gs.gameObject.layer + ",\"trigger\":" + (gs.selfCol != null && gs.selfCol.isTrigger ? "true" : "false") +
                            ",\"rb\":" + Json.Str(gs.GetComponentInParent<Rigidbody>() != null ? (gs.GetComponentInParent<Rigidbody>().isKinematic ? "kinematic" : "dynamic") : "none") + "}";
            var from = pl.transform.position + Vector3.up * 0.5f;
            string ray = "null"; float surf = float.NaN;
            if (Physics.Raycast(from, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                surf = hit.point.y;
                var rrb = hit.collider.attachedRigidbody;
                ray = "{\"hit\":" + Json.Str(hit.collider.name) + ",\"root\":" + Json.Str(hit.collider.transform.root.name) + ",\"layer\":" + hit.collider.gameObject.layer +
                      ",\"rb\":" + Json.Str(rrb == null ? "none" : rrb.isKinematic ? "kinematic" : "dynamic") + ",\"belowPlayer\":" + F(pl.transform.position.y - hit.point.y) + "}";
            }
            var a = pl.anim;
            string feet = a == null || a.LLTarget == null ? "null" : "{\"left\":" + F(a.LLTarget.position.y - (float.IsNaN(surf) ? 0 : surf)) + ",\"right\":" + F(a.RLTarget.position.y - (float.IsNaN(surf) ? 0 : surf)) + "}";
            return "{\"sensor\":" + sensor + ",\"ray\":" + ray + ",\"feetAboveSurface\":" + feet + ",\"why\":" + Json.Str(FootWhy) + "}";
        }

        /// Test (`mp outsidebodies <net>`): a vehicle's bodies outside its hierarchy on this machine (Bus01's BusBack) —
        /// how they are moved: body flags, the joint, the parent, every script on them.
        public static string OutsideBodies(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no such vehicle here\"}";
            var root = e.Root.transform.root; var rows = new System.Collections.Generic.List<string>();
            var seen = new System.Collections.Generic.HashSet<Transform>();
            if (e.Root.partslotscripts != null)
                foreach (var sl in e.Root.partslotscripts)
                {
                    if (sl == null) continue;
                    var br = sl.transform.root;
                    if (br == root || !seen.Add(br)) continue;
                    var rb = br.GetComponent<Rigidbody>(); var j = br.GetComponent<Joint>();
                    var scripts = new System.Collections.Generic.List<string>();
                    foreach (var mb in br.GetComponents<MonoBehaviour>()) if (mb != null) scripts.Add(mb.GetType().Name);
                    rows.Add("{\"name\":" + Json.Str(br.name) + ",\"rb\":" + (rb == null ? "null" : "{\"kin\":" + (rb.isKinematic ? "true" : "false") + ",\"interp\":" + Json.Str(rb.interpolation.ToString()) +
                             ",\"constraints\":" + Json.Str(rb.constraints.ToString()) + ",\"sleeping\":" + (rb.IsSleeping() ? "true" : "false") + ",\"madeKinByUs\":" + (e.MadeKinematic.Contains(rb) ? "true" : "false") + "}") +
                             ",\"joint\":" + (j == null ? "null" : "{\"type\":" + Json.Str(j.GetType().Name) + ",\"to\":" + Json.Str(j.connectedBody != null ? j.connectedBody.name : "") +
                             ",\"toKin\":" + (j.connectedBody != null && j.connectedBody.isKinematic ? "true" : "false") + "}") +
                             ",\"parent\":" + Json.Str(br.parent != null ? br.parent.name : "") + ",\"scripts\":[" + string.Join(",", scripts.ConvertAll(Json.Str)) + "]}");
                }
            // every wheel mesh on the vehicle and on those bodies: its spin angle now, and whether the copy's wheel sync
            // drives it (ProxyWheelOwner) - sampled twice while driving, a wheel that doesn't turn is a frozen wheel
            var wrows = new System.Collections.Generic.List<string>(); var whosts = new System.Collections.Generic.List<Transform> { root }; whosts.AddRange(seen);
            foreach (var hr in whosts)
                foreach (var wg in hr.GetComponentsInChildren<wheelgraphicsscript>(true))
                {
                    var vis = wg.T != null ? wg.T : wg.transform; var lq = Quaternion.Inverse(root.rotation) * vis.rotation;
                    var ic2 = System.Globalization.CultureInfo.InvariantCulture;
                    wrows.Add("[" + Json.Str(hr.name + "/" + wg.name) + "," + (ProxyWheelOwner.ContainsKey(wg) ? "true" : "false") + ",[" +
                              lq.x.ToString("F4", ic2) + "," + lq.y.ToString("F4", ic2) + "," + lq.z.ToString("F4", ic2) + "," + lq.w.ToString("F4", ic2) + "]]");
                }
            return "{\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"wheels(name,synced,rotInVehicle)\":[" + string.Join(",", wrows) + "]" + ",\"ragdollCopy\":" + (TLDRevamp.Net.Entities.ObservedRagdollCopies.Contains(e) ? "true" : "false") + ",\"limbsByBody\":" + (LimbsByBody(e) ? "true" : "false") +
                   ",\"limbs\":" + (Limbs(e) is Transform[] ls ? ls.Length : 0) + ",\"bodies\":[" + string.Join(",", rows) + "]}";
        }
        public static string StandSeries(string[] a)
        {
            if (a.Length > 2)
            {
                ObservedSeriesNet = uint.Parse(a[1]); ObservedSeriesLeft = int.Parse(a[2]); ObservedSeries.Length = 0; ObservedSeriesSteps = 0;
                return "{\"started\":" + ObservedSeriesLeft + "}";
            }
            return "{\"left\":" + ObservedSeriesLeft + ",\"cols\":[\"t\",\"dt\",\"steps\",\"alpha\",\"speed\",\"camX\",\"camY\",\"camZ\",\"bodyX\",\"bodyY\",\"bodyZ\",\"interp\",\"worldX\",\"worldZ\"],\"rows\":[" + ObservedSeries + "]}";
        }
        internal static void Step() => _seriesSteps++;

        /// Test (`mp standseries <net> <frames>` to start, `mp standseries` to read): per drawn frame, our camera and body in
        /// the frame of the vehicle `net` as drawn (mm), its speed, the physics steps since the last frame and the
        /// interpolation point, the camera in the world: what standing in it looks like, at a finer grain than the black box's cm.
        internal static uint _seriesNet;
        internal static uint ObservedSeriesNet { get => _seriesNet; set => _seriesNet = value; }
        internal static int _seriesLeft, _seriesSteps;
        internal static int ObservedSeriesSteps { get => _seriesSteps; set => _seriesSteps = value; }
        internal static int ObservedSeriesLeft { get => _seriesLeft; set => _seriesLeft = value; }
        internal static readonly System.Text.StringBuilder _series = new System.Text.StringBuilder();
        internal static System.Text.StringBuilder ObservedSeries => _series;
        internal static void StandSeriesSample()
        {
            if (_seriesLeft <= 0 || !ObservedEntities.TryGetValue(_seriesNet, out var e) || e.Root == null) return;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var cam = pl == null ? null : pl.Cam != null ? pl.Cam.transform : pl.mainCam != null ? pl.mainCam.transform : null;
            if (cam == null) return;
            var t = e.Root.transform; var c = t.InverseTransformPoint(cam.position) * 1000f; var b = t.InverseTransformPoint(pl.transform.position) * 1000f;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            float alpha = Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime));
            if (_series.Length > 0) _series.Append(',');
            _series.Append('[').Append(Time.realtimeSinceStartupAsDouble.ToString("F4", ci)).Append(',').Append((Time.deltaTime * 1000f).ToString("F1", ci)).Append(',')
                   .Append(_seriesSteps).Append(',').Append(alpha.ToString("F2", ci)).Append(',').Append(e.LastVel.magnitude.ToString("F2", ci)).Append(',')
                   .Append(c.x.ToString("F1", ci)).Append(',').Append(c.y.ToString("F1", ci)).Append(',').Append(c.z.ToString("F1", ci)).Append(',')
                   .Append(b.x.ToString("F1", ci)).Append(',').Append(b.y.ToString("F1", ci)).Append(',').Append(b.z.ToString("F1", ci)).Append(',')
                   .Append((pl.RB != null && pl.RB.interpolation == RigidbodyInterpolation.Interpolate ? 1 : 0) +
                           (e.Root.RB != null && e.Root.RB.interpolation == RigidbodyInterpolation.Interpolate ? 2 : 0)).Append(',');   // 1 ours, 2 the vehicle's
            var w = mainscript.GlobalFromUnityPos(cam.position);   // the camera in the world (mm): how evenly the world goes by
            _series.Append((w.x * 1000).ToString("F0", ci)).Append(',').Append((w.z * 1000).ToString("F0", ci)).Append(']');
            _seriesSteps = 0; _seriesLeft--;
        }
    }
}
