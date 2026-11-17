using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityCarDoorsTools
    {

        /// `mp opendoor <net> <i>`: the player's action on door i's usable — a click (Turn) if it turns, else a drag to
        /// the other end (slide 0↔1, then the game's sync call, as Slide does).
        public static string OpenDoor(uint net, int i)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            var ds = EntityDoors.DoorsOf(e);
            if (i < 0 || i >= ds.Count) return "{\"error\":\"no door " + i + " of " + ds.Count + "\"}";
            var u = ds[i].U;
            if (u.rotateAble && u.RT1 != null && !u.slideAble)
            {
                // a drag to the other end of the hinge (Rot with vr: the raw angle, as a VR hand moves it; it syncs)
                float dx = u.maxX - u.minX > 20f ? (u.xRot - u.minX < u.maxX - u.xRot ? u.maxX - u.xRot : u.minX - u.xRot) : 0f;
                float dy = u.maxY - u.minY > 20f ? (u.yRot - u.minY < u.maxY - u.yRot ? u.maxY - u.yRot : u.minY - u.yRot) : 0f;
                u.Rot(dx, dy, true, true);
            }
            else if (u.turnable) u.Turn();
            else { u.Refresh(u.slideValue > 0.5f ? 0f : 1f, u.xRot, u.yRot, u.currentTurnState); u.SyncMulti(true); }   // Refresh poses TDoor (as Slide's end does)
            return EntityDoors.CarDoors(net);
        }

        /// Test: cargo. `mp putincar <car> <item> <k>`: an item this machine owns onto the car's k-th rearmost seat
        /// (0.5 m above it), moving with the car. `mp relpos <car> <item…>`: each item where this machine shows it, in
        /// the car's frame (metres) — on a copy that is what the player sees of the cargo.
        public static string PutInCar(uint car, uint item, int k)
        {
            if (!ObservedEntities.TryGetValue(car, out var c) || c.Root == null || !ObservedEntities.TryGetValue(item, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            if (e.Proxy) return "{\"error\":\"not the owner\"}";
            var ct = c.Root.transform;
            var seats = new List<seatscript>(c.Root.GetComponentsInChildren<seatscript>(true));
            seats.Sort((a, b) => ct.InverseTransformPoint(a.transform.position).z.CompareTo(ct.InverseTransformPoint(b.transform.position).z));
            if (k < 0 || k >= seats.Count) return "{\"error\":\"no seat " + k + " of " + seats.Count + "\"}";
            var at = seats[k].transform.position + ct.up * 0.5f;
            var t = e.Root.transform; t.position = at; t.rotation = ct.rotation;
            var rb = e.Root.GetComponent<Rigidbody>(); var crb = c.Root.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.position = at; rb.velocity = crb != null ? crb.velocity : Vector3.zero; rb.angularVelocity = Vector3.zero; rb.WakeUp(); }
            Physics.SyncTransforms();
            return RelPos(car, new[] { item });
        }

        /// Test: the item (ours) at a point in the car's frame (m), upright with the car, at rest relative to it.
        public static string PutRel(uint car, uint item, float x, float y, float z, bool clear = false)
        {
            if (!ObservedEntities.TryGetValue(car, out var c) || c.Root == null || !ObservedEntities.TryGetValue(item, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            if (e.Proxy) return "{\"error\":\"not the owner\"}";
            var ct = c.Root.transform; var at = ct.TransformPoint(new Vector3(x, y, z));
            var t = e.Root.transform; t.position = at; t.rotation = ct.rotation;
            var rb = e.Root.GetComponent<Rigidbody>(); var crb = c.Root.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.position = at; rb.rotation = ct.rotation; rb.velocity = crb != null ? crb.velocity : Vector3.zero; rb.angularVelocity = Vector3.zero; rb.WakeUp(); }
            Physics.SyncTransforms();
            float lifted = clear ? LiftClear(t, ct.up, rb) : 0f;
            var r = RelPos(car, new[] { item });
            return clear ? r.Substring(0, r.Length - 1) + ",\"lifted\":" + lifted.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "}" : r;
        }

        /// Test placement `clear`: raised along `up` in 1 cm steps until none of its colliders overlaps another one (at
        /// most 0.6 m). A can set on a crate at a fixed height started inside it when the crate lay on another face, and
        /// the physics threw it out at 17.7 m/s, 40 m off the parked pickup (stackcargo, 2026-10-23).
        internal static float LiftClear(Transform t, Vector3 up, Rigidbody rb)
        {
            t.GetComponentsInChildren(false, _liftOwn);
            float lifted = 0f;
            for (int step = 0; step < 60; step++)
            {
                bool hit = false;
                foreach (var c in _liftOwn)
                {
                    if (c == null || !c.enabled || c.isTrigger) continue;
                    var b = c.bounds;
                    int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, _liftHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n && !hit; i++)
                    {
                        var o = _liftHits[i];
                        if (o == null || o.transform.IsChildOf(t)) continue;
                        hit = Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, o, o.transform.position, o.transform.rotation, out _, out _);
                    }
                    if (hit) break;
                }
                if (!hit) return lifted;
                t.position += up * 0.01f; lifted += 0.01f;
                if (rb != null && !rb.isKinematic) rb.position = t.position;
                Physics.SyncTransforms();
            }
            return lifted;
        }

        /// Test: what the cargo rules see of an item: its parent, its body, the surface under it and its speed on it.
        public static string CargoInfo(uint item)
        {
            if (!ObservedEntities.TryGetValue(item, out var e) || e.Root == null) return "{\"error\":\"no entity\"}";
            var rb = e.Root.GetComponent<Rigidbody>();
            var under = EntityCargoInteraction.SurfaceUnder(e, out var body);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "{\"net\":" + item + ",\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"parent\":" + Json.Str(e.Root.transform.parent != null ? e.Root.transform.parent.name : "") +
                   ",\"root\":" + Json.Str(e.Root.transform.root.name) + ",\"loose\":" + (EntityCargoInteraction.LooseItem(e) ? "true" : "false") +
                   ",\"rb\":" + (rb == null ? "null" : "{\"kinematic\":" + (rb.isKinematic ? "true" : "false") + ",\"sleeping\":" + (rb.IsSleeping() ? "true" : "false") +
                       ",\"collide\":" + (rb.detectCollisions ? "true" : "false") + ",\"speed\":" + rb.velocity.magnitude.ToString("F2", ci) +
                       ",\"ride\":" + EntityCargoInteraction.RideVelocity(e, rb).magnitude.ToString("F2", ci) + "}") +
                   ",\"moving\":" + (EntityCargoInteraction.MovingItemBody(e) != null ? "true" : "false") + ",\"under\":" + Json.Str(under != null ? under.name : "") +
                   ",\"underBody\":" + Json.Str(body != null ? body.name : "") + ",\"ghost\":" + (e.CargoGhost ? "true" : "false") + "}";
        }

        public static string RelPos(uint car, uint[] items)
        {
            if (!ObservedEntities.TryGetValue(car, out var c) || c.Root == null) return "{\"error\":\"no car\"}";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var ct = c.Root.transform;
            var sb = new System.Text.StringBuilder("{\"proxy\":" + (c.Proxy ? "true" : "false") + ",\"items\":{");
            int n = 0;
            foreach (var id in items)
            {
                if (!ObservedEntities.TryGetValue(id, out var e) || e.Root == null) continue;
                var l = ct.InverseTransformPoint(e.Root.transform.position);
                if (n++ > 0) sb.Append(',');
                sb.Append("\"" + id + "\":[" + l.x.ToString("F3", ci) + "," + l.y.ToString("F3", ci) + "," + l.z.ToString("F3", ci) + "]");
            }
            // as drawn: the same, read in LateUpdate (after every Update — the bridge runs before Mp.Tick places copies)
            sb.Append("},\"late\":{");
            n = 0;
            if (ObservedLateCar == car && ObservedLateItems != null && items.Length == ObservedLateItems.Length)
                for (int i = 0; i < ObservedLateItems.Length; i++)
                {
                    if (!ObservedLateHave[i]) continue;
                    if (n++ > 0) sb.Append(',');
                    var l = ObservedLateRel[i];
                    sb.Append("\"" + ObservedLateItems[i] + "\":[" + l.x.ToString("F3", ci) + "," + l.y.ToString("F3", ci) + "," + l.z.ToString("F3", ci) + "]");
                }
            ObservedLateCar = car; if (ObservedLateItems == null || ObservedLateItems.Length != items.Length) { ObservedLateItems = items; ObservedLateRel = new Vector3[items.Length]; ObservedLateHave = new bool[items.Length]; } else ObservedLateItems = items;
            return sb.Append("}}").ToString();
        }

        /// Test: our player stands on top of shared car `car` (its colliders' top, at the middle), `dy` above it, moving
        /// with the car.
        public static string StandOn(uint car, float dy, bool rear = false)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            if (!ObservedEntities.TryGetValue(car, out var c) || c.Root == null) return "{\"error\":\"no car\"}";
            if (rear)
            {
                // inside a body of the vehicle outside its hierarchy (the back of a bus): in the aisle by its first seat,
                // dy above the floor found by a ray down from seat height. (Before: the middle of all its colliders' bounds
                // at their lowest point + dy — the bottom of the wheels: the player stood on the road under the bus, and
                // every "standing in the back" run was invalid; the user, watching: "Laptop isn't even inside of the bus ever".)
                Transform body = null;
                if (c.Root.partslotscripts != null)
                    foreach (var sl in c.Root.partslotscripts) if (sl != null && sl.transform.root != c.Root.transform.root) { body = sl.transform.root; break; }
                if (body == null) return "{\"error\":\"no body outside the vehicle\"}";
                bool any2 = false; var b2 = new Bounds();
                foreach (var col in body.GetComponentsInChildren<Collider>()) { if (col.isTrigger || !col.enabled) continue; if (!any2) { b2 = col.bounds; any2 = true; } else b2.Encapsulate(col.bounds); }
                if (!any2) return "{\"error\":\"no colliders on " + body.name + "\"}";
                var seats = body.GetComponentsInChildren<seatscript>(true);
                if (seats.Length == 0) return "{\"error\":\"no seats on " + body.name + "\"}";
                var st = seats[seats.Length / 2].transform;
                // the aisle: the bus's centre line (its root's x = 0; the middle of the colliders' world box drifted half
                // a metre into a seat row depending on the bus's heading)
                // along it: the middle of the section's length (a seat's z hit the rear bench, which spans the width);
                // the floor is the nearest hit on the section that isn't a seat
                var rt = c.Root.transform; var ls = rt.InverseTransformPoint(st.position); var lm = rt.InverseTransformPoint(b2.center);
                var from = rt.TransformPoint(new Vector3(0f, ls.y, lm.z)) + Vector3.up * 0.5f;
                RaycastHit? floor = null;
                foreach (var h in Physics.RaycastAll(from, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.transform.root == body && h.collider.GetComponentInParent<seatscript>() == null && !h.collider.name.StartsWith("seat")
                        && (floor == null || h.distance < floor.Value.distance)) floor = h;
                if (floor == null) return "{\"error\":\"no floor of " + body.name + " under the aisle\"}";
                // the player's pivot isn't at its feet: the capsule's bottom goes dy above the floor (pivot at the floor
                // put the body half into it — pushed up into the roof and out of the bus)
                float feet = 0f;
                var cb = pl.CBody;
                if (cb != null) feet = pl.transform.position.y - (cb.transform.TransformPoint(cb.center).y - cb.height * 0.5f * cb.transform.lossyScale.y);
                var at = floor.Value.point + Vector3.up * (feet + dy);
                pl.transform.position = at;
                var prb2 = pl.GetComponent<Rigidbody>();
                var carrierBody = c.Root.GetComponent<Rigidbody>();
                if (prb2 != null) { prb2.position = at; if (!prb2.isKinematic) prb2.velocity = !c.Proxy && carrierBody != null ? carrierBody.velocity : c.LastVel; }
                return "{\"stood\":true,\"body\":" + Json.Str(body.name) + ",\"floor\":" + Json.Str(floor.Value.collider.name) + ",\"seat\":" + Json.Str(st.name) + ",\"feet\":" + feet.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"local\":" + V3f(c.Root.transform.InverseTransformPoint(at)) + "}";
            }
            var cols = c.Root.GetComponentsInChildren<Collider>();
            bool any = false; var b = new Bounds();
            foreach (var col in cols) { if (col.isTrigger || !col.enabled) continue; if (!any) { b = col.bounds; any = true; } else b.Encapsulate(col.bounds); }
            if (!any) return "{\"error\":\"no colliders\"}";
            var ct = c.Root.transform;
            float roofFeet = 0f;
            var capsule = pl.CBody;
            if (capsule != null) roofFeet = pl.transform.position.y - (capsule.transform.TransformPoint(capsule.center).y - capsule.height * 0.5f * capsule.transform.lossyScale.y);
            var top = new Vector3(ct.position.x, b.max.y + roofFeet + dy, ct.position.z);
            pl.transform.position = top;
            var crb = c.Root.GetComponent<Rigidbody>(); var prb = pl.GetComponent<Rigidbody>();
            if (prb != null) { prb.position = top; if (!prb.isKinematic) prb.velocity = !c.Proxy && crb != null ? crb.velocity : c.LastVel; }
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "{\"stood\":true,\"top\":" + b.max.y.ToString("F2", ci) + ",\"local\":" + V3f(ct.InverseTransformPoint(top)) + "}";
        }
        public static string Tow(string[] a)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            if (a.Length < 2 || a[1] == "off") { ObservedTowNet = 0; return "{\"tow\":false}"; }
            uint net = uint.Parse(a[1]);
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null || e.Root.car == null || e.Proxy) return "{\"error\":\"not a vehicle simulated here\"}";
            ObservedTowNet = net; ObservedTowSpeed = a.Length > 2 ? float.Parse(a[2], ic) : 3f; ObservedTowAccel = a.Length > 3 ? float.Parse(a[3], ic) : 2f;
            return "{\"tow\":" + net + ",\"speed\":" + ObservedTowSpeed.ToString(ic) + ",\"accel\":" + ObservedTowAccel.ToString(ic) + "}";
        }

        /// Test: where the players are in car `car`'s frame — ours and every remote body shown here; now and as drawn
        /// (LateUpdate, the frame before).
        public static string RelPlayers(uint car)
        {
            if (!ObservedEntities.TryGetValue(car, out var c) || c.Root == null) return "{\"error\":\"no car\"}";
            var ct = c.Root.transform;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var sb = new System.Text.StringBuilder("{\"me\":" + (pl != null ? V3f(ct.InverseTransformPoint(pl.transform.position)) : "null") + ",\"remotes\":{");
            int n = 0;
            foreach (var kv in RemotePlayers.ShownBodies()) { if (n++ > 0) sb.Append(','); sb.Append("\"" + kv.Key + "\":" + V3f(ct.InverseTransformPoint(kv.Value.position))); }
            sb.Append("},\"late\":{");
            n = 0;
            if (ObservedLatePlCar == car) foreach (var kv in ObservedLatePl) { if (n++ > 0) sb.Append(','); sb.Append("\"" + kv.Key + "\":" + V3f(kv.Value)); }
            ObservedLatePlCar = car;
            return sb.Append("}}").ToString();
        }

        /// Test: food. `mp bite`: one bite of what the player holds, the game's own way (fpscontroller's eat on a click:
        /// survival.Eat(edible.Eat(…))). `mp food <net>`: its bites here (index, hp, parts shown, gone).
        public static string Bite()
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || pl.pickedUp == null || pl.pickedUp.edible == null) return "{\"error\":\"holding no food\"}";
            var ed = pl.pickedUp.edible;
            pl.survival.Eat(ed.Eat(out float sh, out float hp, out float wa, true), sh, hp, wa);
            return "{\"index\":" + ed.index + ",\"hp\":" + ed.hp.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "}";
        }

        /// Test: how a copy moves on this screen, frame by frame (`mp copymotion <net> [reset]`): shown speed min / p10 /
        /// p50 / p90 / max (m/s) since the reset, and frames it all but stopped (< 30 % of the median).
        public static string CopyMotion(uint net, bool reset)
        {
            if (!ObservedEntities.TryGetValue(net, out var e)) return "{\"error\":\"no entity\"}";
            if (reset) { e.ShownN = 0; return "{\"reset\":true}"; }
            int n = Math.Min(e.ShownN, e.ShownSpeed.Length);
            if (n < 5) return "{\"n\":" + n + "}";
            var a = new float[n]; Array.Copy(e.ShownSpeed, a, n); Array.Sort(a);
            float med = a[n / 2]; int held = 0; foreach (var v in a) if (v < 0.3f * med) held++;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            System.Func<float, string> f = v => v.ToString("F2", ci);
            return "{\"n\":" + n + ",\"min\":" + f(a[0]) + ",\"p10\":" + f(a[n / 10]) + ",\"p50\":" + f(med) + ",\"p90\":" + f(a[n * 9 / 10]) + ",\"max\":" + f(a[n - 1]) + ",\"held\":" + held + "}";
        }
        internal static string V3f(Vector3 v)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "[" + v.x.ToString("F3", ci) + "," + v.y.ToString("F3", ci) + "," + v.z.ToString("F3", ci) + "]";
        }
        internal static readonly Collider[] _liftHits = new Collider[64];
        internal static readonly System.Collections.Generic.List<Collider> _liftOwn = new System.Collections.Generic.List<Collider>();
        internal static void LateSample()
        {
            TLDRevamp.Net.EntityFootingTools.StandSeriesSample();
            if (_latePlCar != 0 && ObservedEntities.TryGetValue(_latePlCar, out var pc) && pc.Root != null)
            {
                var pt = pc.Root.transform; _latePl.Clear();
                var pl = mainscript.s != null ? mainscript.s.player : null;
                if (pl != null) _latePl[-1] = pt.InverseTransformPoint(pl.transform.position);   // -1: ours
                foreach (var kv in RemotePlayers.ShownBodies()) _latePl[kv.Key] = pt.InverseTransformPoint(kv.Value.position);
            }
            if (_lateCar == 0 || _lateItems == null) return;
            if (!ObservedEntities.TryGetValue(_lateCar, out var c) || c.Root == null) return;
            var ct = c.Root.transform;
            for (int i = 0; i < _lateItems.Length; i++)
            {
                _lateHave[i] = ObservedEntities.TryGetValue(_lateItems[i], out var e) && e.Root != null;
                if (_lateHave[i]) _lateRel[i] = ct.InverseTransformPoint(e.Root.transform.position);
            }
        }

        /// Test (`mp tow <net> <m/s> [accel]` / `mp tow off`): drives a vehicle this machine simulates with nobody at the
        /// wheel — its body's forward speed ramped to `speed` at `accel` m/s² in the physics step, its joints (the back of
        /// a bus) pulling the rest as in the game. The same drive with a player standing in it on the owner (the game's
        /// physics: the baseline) and on a copy (busrear --stand --tow).
        internal static uint _towNet;
        internal static uint ObservedTowNet { get => _towNet; set => _towNet = value; }
        internal static float _towSpeed, _towAccel;
        internal static float ObservedTowAccel { get => _towAccel; set => _towAccel = value; }
        internal static float ObservedTowSpeed { get => _towSpeed; set => _towSpeed = value; }
        internal static void TowTick(float dt)
        {
            if (_towNet == 0) return;
            if (!ObservedEntities.TryGetValue(_towNet, out var e) || e.Root == null || e.Root.car == null || e.Proxy) { _towNet = 0; return; }
            var rb = e.Root.car.RB; if (rb == null || rb.isKinematic) return;
            var f = rb.transform.forward; f.y = 0f; f.Normalize();
            float v = Vector3.Dot(rb.velocity, f);
            float dv = Mathf.Clamp(_towSpeed - v, -_towAccel * dt, _towAccel * dt);
            rb.AddForce(f * dv, ForceMode.VelocityChange);
        }
        internal static uint _latePlCar;
        internal static uint ObservedLatePlCar { get => _latePlCar; set => _latePlCar = value; }
        internal static readonly Dictionary<int, Vector3> _latePl = new Dictionary<int, Vector3>();
        internal static System.Collections.Generic.IReadOnlyDictionary<int, UnityEngine.Vector3> ObservedLatePl => _latePl;

        internal static uint _lateCar;
        internal static uint ObservedLateCar { get => _lateCar; set => _lateCar = value; }
        internal static uint[] _lateItems;
        internal static uint[] ObservedLateItems { get => _lateItems; set => _lateItems = value; }
        internal static Vector3[] _lateRel;
        internal static Vector3[] ObservedLateRel { get => _lateRel; set => _lateRel = value; }
        internal static bool[] _lateHave;
        internal static bool[] ObservedLateHave { get => _lateHave; set => _lateHave = value; }
    }
}
