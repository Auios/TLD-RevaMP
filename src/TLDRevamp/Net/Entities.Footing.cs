using UnityEngine;

namespace TLDRevamp.Net
{
    /// Players standing on a shared object — a car's roof or bed, the back of the bus — ride in its frame (protocol 23):
    /// the stander sends the object and where on it they stand, every machine puts the body there in its own view of it
    /// (RemotePlayers.Tick). Their world position came from their own, late view of the car and was late again on the
    /// others: 3 m behind the roof at 30 km/h (standride.py, 2026-10-08). Seated players have their seat instead.
    internal static partial class EntityPhysics
    {
        public static long StandInterpFrames;
        private static bool _standInterp;
        /// The other way to draw both at one pose: the copy we stand on drawn at its physics
        /// pose, like our player (AboardDrawnAtPhysics does it for a seat). Off: the world outside then moves in physics
        /// steps (frame-to-frame speed p90 ~50% off its median, against 6-28% interpolated) and the camera still jumped
        /// now and then (up to 410 mm along, 228 mm up; stand3.py).
        internal static bool StandCopyAtPhysics;

        /// Runner.FixedUpdate (FixedTick), after the copies moved: the footing is LocalFooting's, from the last pose sent.
        internal static void StandInterpTick()
        {
            DevHooks.StandSeriesStep?.Invoke();
            var pl = mainscript.s != null ? mainscript.s.player : null;
            var rb = pl != null ? pl.RB : null;
            if (rb == null) { _standInterp = false; return; }
            bool want = Entities.InSession && !pl.Bsitting && !rb.isKinematic && Entities.PhysicsFootingNet != 0 &&
                        Entities.TryPhysicsStand(Entities.PhysicsFootingNet, out var e) && e.Proxy && e.MovedByBody && e.Root != null &&
                        e.Root.RB != null && e.Root.RB.interpolation == RigidbodyInterpolation.Interpolate;
            if (want)
            {
                if (rb.interpolation != RigidbodyInterpolation.Interpolate) rb.interpolation = RigidbodyInterpolation.Interpolate;
                _standInterp = true; StandInterpFrames++;
            }
            else if (_standInterp) StandInterpOff(rb);
        }

        internal static void StandInterpOff(Rigidbody rb)
        {
            _standInterp = false;
            if (rb != null) rb.interpolation = mainscript.RBI();
        }

        internal static bool StandsOnAtPhysics(Entities.Ent e)
        {
            if (!StandCopyAtPhysics || Entities.PhysicsFootingNet != e.NetId) return false;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            return pl != null && !pl.Bsitting;
        }

        // Session cleanup.
        internal static void ResetStandInterpolation()
        {
            if (_standInterp) StandInterpOff(mainscript.s != null && mainscript.s.player != null ? mainscript.s.player.RB : null);
        }
    }

    public static partial class Entities
    {
        public static long FootingSent;
        private static uint _telemetryFooting;

        /// Our player: the shared object under its feet (the game's ground sensor, fpscontroller.grounded), if any.
        public static void LocalFooting(out Protocol.Footing f)
        {
            f = default;
            _telemetryFooting = 0;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || pl.Bsitting) { FootWhy = "seated"; return; }
            // what is under the feet: the game's ground sensor, else a short ray down (the sensor is a trigger that keeps
            // whichever collider it touched last)
            Transform under = pl.grounded != null && pl.grounded.sensed && pl.grounded.T != null ? pl.grounded.T : null;
            string via = "sensor";
            if (under == null || ProxyOf(under.root) == null && OwnedRoot(under.root) == null)
            {
                var from = pl.transform.position + Vector3.up * 0.5f;
                var hits = Physics.RaycastAll(from, Vector3.down, 2.0f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue; Transform bt = null;
                foreach (var h in hits)
                {
                    var hr = h.collider.transform.root;
                    if (hr == pl.transform.root) continue;   // our own body
                    if (pl.pickedUp != null && hr == pl.pickedUp.transform.root || pl.inHandP != null && hr == pl.inHandP.transform.root) continue;   // what we hold
                    if (h.distance < best) { best = h.distance; bt = h.collider.transform; }
                }
                if (bt != null) { under = bt; via = "ray"; }
            }
            if (under == null) { FootWhy = "nothing under"; return; }
            var root = under.root;
            // placed objects only (Entities.Placed): on plain ground both loops below ran through every shared object
            foreach (var e in Placed())
            {
                if (e.Root == null || e.PartIndex >= 0 || e.Root.transform != root) continue;
                // only what moves: a vehicle, or a loose object in motion. Stepping over a tyre on the ground flipped the
                // footing on and off, each time a fresh interpolator on the other machines
                if (e.Root.car == null)
                {
                    var rb = e.Root.GetComponent<Rigidbody>();
                    var v = rb == null ? Vector3.zero : e.Proxy || rb.isKinematic ? e.LastVel : rb.velocity;
                    if (v.sqrMagnitude < 0.5f * 0.5f) { FootWhy = "on " + e.NetId + ": not moving"; return; }
                }
                f.Net = e.NetId;
                _telemetryFooting = e.NetId;
                f.Local = root.InverseTransformPoint(pl.transform.position);
                f.Yaw = Mp.LocalYaw() - root.eulerAngles.y;
                FootingSent++; FootWhy = "on " + e.NetId + " (" + via + ")";
                return;
            }
            // a body of a vehicle outside its hierarchy (the back of a bus: its own body on a joint, as for its seats,
            // LocalSeat): the position in the vehicle's frame
            foreach (var e in Placed())
            {
                if (e.Root == null || e.PartIndex >= 0 || e.Root.car == null || e.Root.partslotscripts == null) continue;
                bool mine = false;
                foreach (var sl in e.Root.partslotscripts) if (sl != null && sl.transform.root == root) { mine = true; break; }
                if (!mine) continue;
                var vt = e.Root.transform;
                f.Net = e.NetId;
                _telemetryFooting = e.NetId;
                f.Local = vt.InverseTransformPoint(pl.transform.position);
                f.Yaw = Mp.LocalYaw() - vt.eulerAngles.y;
                FootingSent++; FootWhy = "on " + e.NetId + " (" + via + ", its body " + root.name + ")";
                return;
            }
            FootWhy = "under " + under.name + " / root " + root.name + " (" + via + "): not shared";
        }
        public static string FootWhy = "";

        private static Ent OwnedRoot(Transform root)
        {
            var e = PlacedOfTransform(root);   // 20 times a second on plain ground: it walked every shared object
            return e != null && e.PartIndex < 0 ? e : null;
        }

        /// The shared object `net`'s transform here (null when it isn't loaded).
        public static Transform FootRootOf(uint net) => ByNet.TryGetValue(net, out var e) && e.Root != null ? e.Root.transform : null;
    }
}
