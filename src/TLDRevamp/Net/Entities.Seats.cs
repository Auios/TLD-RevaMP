using System;
using System.Collections.Generic;
using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    internal static partial class EntitySeats
    {
        public static long ItemSeatsSent;
        private static readonly Dictionary<Transform, seatscript[]> _seats = new Dictionary<Transform, seatscript[]>();
        internal static seatscript[] VehicleSeats(tosaveitemscript vehicle)
        {
            var cache = _seats;
            var root = vehicle.transform;
            if (cache.TryGetValue(root, out var have) && have.Length > 0 && have[0] != null) return have;
            if (cache.Count > 256) cache.Clear();   // destroyed vehicles' entries
            var list = new List<seatscript>(root.GetComponentsInChildren<seatscript>(true));
            list.RemoveAll(st => st == null || InLockedItem(st.transform, root));
            if (vehicle.partslotscripts != null)
            {
                var extra = new List<Transform>();
                foreach (var sl in vehicle.partslotscripts)
                    if (sl != null && (!sl.transform.IsChildOf(root)) && !extra.Contains(sl.transform.root)) extra.Add(sl.transform.root);
                extra.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
                foreach (var t in extra) list.AddRange(t.GetComponentsInChildren<seatscript>(true));
            }
            var arr = list.ToArray();
            cache[root] = arr;
            return arr;
        }

        /// Below `top`, is `t` in an item the game has locked into a mount of it (the item parented to the mountStuff)?
        private static bool InLockedItem(Transform t, Transform top)
        {
            for (var p = t; p != null && p != top; p = p.parent)
                if (p.parent != null && p.parent.GetComponent<mountStuff>() != null && p.GetComponent<tosaveitemscript>() != null) return true;
            return false;
        }

        /// The shared object a seat belongs to: the nearest one above it that isn't a part (the car for its own seats, the
        /// stool for a stool's, wherever the stool is).
        private static Ent SeatObject(seatscript seat)
        {
            return Entities.SeatObjectAbove(seat.transform);
        }

        /// The local player's seat, if it belongs to a shared object: (network id, seat index), else net 0.
        internal static void LocalSeat(out uint net, out int idx)
        {
            net = 0; idx = 0;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null || !pl.Bsitting || pl.seat == null) return;
            if (SeatObject(pl.seat) is Ent so)
            {
                int k = Array.IndexOf(VehicleSeats(so.Root), pl.seat);
                if (k >= 0)
                {
                    net = so.NetId; idx = k;
                    if (so.Root.car == null) ItemSeatsSent++;
                    return;
                }
            }
            var root = pl.seat.transform.root;
            foreach (var e in Entities.PlacedSeatObjects())
            {
                if (e.Root == null || e.PartIndex >= 0) continue;
                // only the vehicle itself: an item in the seated player's hand is in the car's hierarchy too (hand →
                // player → seat → car), and VehicleSeats of it is the car's seats — a lamp held in the right hand was
                // sent as the seat, the others found no seat 1 on a lamp and drew the passenger trailing the car on the
                // road (heldincar.py, 2026-10-08)
                if (e.Root.car == null && e.Root.transform != root) continue;
                var seats = e.Root.transform.root == root ? VehicleSeats(e.Root) : null;
                if (seats == null)
                {
                    // a seat on a body outside the vehicle's hierarchy (the back of a bus): one of its slot bodies
                    if (e.Root.partslotscripts == null || e.Root.car == null) continue;
                    bool mine = false;
                    foreach (var sl in e.Root.partslotscripts) if (sl != null && sl.transform.root == root) { mine = true; break; }
                    if (!mine) continue;
                    seats = VehicleSeats(e.Root);
                }
                int i = Array.IndexOf(seats, pl.seat);
                if (i < 0) return;
                net = e.NetId; idx = i;
                return;
            }
        }

        /// Where a player seated in shared object `net`, seat `idx`, sits on this machine (null if not here).
        /// Per frame for every seated remote player: the seat found once per (car copy, seat) and kept. Searching the
        /// car's hierarchy every frame cost the laptop 223 ms/s at 16 seated players (~5 ms a frame; spread load test,
        /// v0.57.89) — before the bots sat in their cars no test had seated players in numbers.
        internal static Transform SeatTransformCached(uint net, int idx, ref Transform carRoot, ref Transform seat)
        {
            if (!Entities.TryGetEntity(net, out var e) || (e.Root == null && !Entities.ResolveEntity(e))) return null;
            var root = e.Root.transform.root;
            if (seat != null && carRoot == root) return seat;
            carRoot = root;
            seat = SeatTransform(net, idx);
            return seat;
        }

        /// The seat itself (SeatTransform gives its sit point, a separate object without the seatscript).
        internal static seatscript SeatScript(uint net, int idx, out carscript car)
        {
            car = null;
            if (!Entities.TryGetEntity(net, out var e) || e.Root == null) return null;
            var seats = VehicleSeats(e.Root);
            if (idx < 0 || idx >= seats.Length || seats[idx] == null) return null;
            car = seats[idx].Car != null ? seats[idx].Car : e.Root.car;
            return seats[idx];
        }

        internal static Transform SeatTransform(uint net, int idx)
        {
            if (!Entities.TryGetEntity(net, out var e) || !Entities.ResolveEntity(e)) return null;
            var seats = VehicleSeats(e.Root);
            if (idx < 0 || idx >= seats.Length) return null;
            return seats[idx].sitPos != null ? seats[idx].sitPos : seats[idx].transform;
        }

        /// Items of shared object `net` here: loaded or stored, how attached, how far from the root.
        /// Why would SeatTransform(net, idx) fail right now (the seat-flicker probe).
        internal static string SeatWhy(uint net, int idx)
        {
            if (!Entities.TryGetEntity(net, out var e)) return "{\"why\":\"net absent from ByNet\"}";
            if (!Entities.ResolveEntity(e)) return "{\"why\":\"Resolve failed (dormant)\"}";
            if (e.Root == null) return "{\"why\":\"root null\"}";
            var seats = e.Root.transform.root.GetComponentsInChildren<seatscript>(true);
            return "{\"why\":\"idx " + idx + "/" + seats.Length + "\"}";
        }
    }
}
