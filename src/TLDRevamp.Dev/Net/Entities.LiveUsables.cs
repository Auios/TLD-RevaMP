using System.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityLiveUsablesTools
    {
        public static string Usables(uint net)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !Resolve(e)) return "{\"error\":\"entity is not loaded here\"}";
            var rows = new List<string>();
            for (int i = 0; i < e.Items.Count; i++)
            {
                var it = e.Items[i];
                if (it == null || (e.Split != null && e.Split.Contains(i))) continue;
                var us = EntityUsables.ItemUsables(it);
                for (int j = 0; j < us.Length; j++)
                {
                    var u = us[j]; if (u == null) continue;
                    string kind = (u.rotateAble ? "rot " : "") + (u.slideAble ? "slide " : "") + (u.turnable ? "turn " : "") + (u.tuneAble ? "tune " : "");
                    string path = u.name;
                    for (var t = u.transform.parent; t != null && t != it.transform; t = t.parent) path = t.name + "/" + path;
                    var g = mainscript.GlobalFromUnityPos(u.transform.position);
                    rows.Add("{\"item\":" + i + ",\"usable\":" + j + ",\"itemName\":" + Json.Str(it.name) + ",\"name\":" + Json.Str(path) +
                        ",\"kind\":" + Json.Str(kind.Trim()) + ",\"value\":" + EntityUsables.UsableValuesJson(EntityUsables.UsableState(u)) + ",\"at\":[" + EntityUsables.UN(g.x) + "," + EntityUsables.UN(g.y) + "," + EntityUsables.UN(g.z) + "]}");
                }
            }
            return "{\"net\":" + net + ",\"owner\":" + e.OwnerId + ",\"st\":" + EntityUsables.UN(SessionLog.St) + ",\"usables\":[" + string.Join(",", rows) + "]}";
        }
        public static string DragUsable(uint net, int item, int usable, float seconds, float hz)
        {
            if (!EntityNumbers.Finite(seconds) || !EntityNumbers.Finite(hz) || seconds <= 0 || seconds > 60 || hz < 1 || hz > 240 || !TLDRevamp.Net.EntityDoorAttachmentsTools.UsableArgs(net, item, usable, out _, out var u))
                return "{\"error\":\"loaded net/item/usable, seconds (0,60], hz [1,240] required\"}";
            TelemetryTouch(net);
            return StartUsableDrag(u, seconds, hz);
        }
        internal static string StartUsableDrag(usablescript u, float seconds, float hz)
        {
            if (u == null || !EntityNumbers.Finite(seconds) || !EntityNumbers.Finite(hz) || seconds <= 0 || seconds > 60 || hz < 1 || hz > 240)
                return "{\"error\":\"loaded usable, seconds (0,60], hz [1,240] required\"}";
            if (!(u.rotateAble || u.slideAble || u.turnable || u.tuneAble)) return "{\"error\":\"usable has no input action\"}";
            u.FStart();
            double start = SessionLog.St;
            ObservedDragUsable = new UsableDrag { U = u, Start = start, End = start + seconds, Next = start, Hz = hz };
            ObservedDragUsable.From = EntityUsables.UsableState(u);
            ObservedDragUsable.To = new PoiState { XRot = u.xRot < (u.minX + u.maxX) * .5f ? u.maxX : u.minX,
                YRot = u.yRot < (u.minY + u.maxY) * .5f ? u.maxY : u.minY, Slide = u.slideValue < .5f ? 1 : 0,
                Tuned = u.tunedFloat < (u.tunedFloatMin + u.tunedFloatMax) * .5f ? u.tunedFloatMax : u.tunedFloatMin };
            return "{\"start\":" + EntityUsables.UN(start) + ",\"end\":" + EntityUsables.UN(start + seconds) + ",\"hz\":" + EntityUsables.UN(hz) + "}";
        }
        /// Monotonic motion makes the sender/receiver traces unambiguous when measuring delay offline.
        internal static void DragUsableTick()
        {
            var d = ObservedDragUsable;
            if (d == null) return;
            if (d.U == null) { ObservedDragUsable = null; return; }
            double now = SessionLog.St;
            if (now < d.Next && now < d.End) return;
            var u = d.U; float t = Mathf.Clamp01((float)((now - d.Start) / (d.End - d.Start)));
            float value = 0.15f + 0.7f * t;
            if (u.rotateAble && u.RT1 != null)
            {
                float x = Mathf.Lerp(u.minX, u.maxX, value), y = Mathf.Lerp(u.minY, u.maxY, value);
                if (u.setidPoi) { x = Mathf.Lerp(d.From.XRot, d.To.XRot, t); y = Mathf.Lerp(d.From.YRot, d.To.YRot, t); }
                float sens = mainscript.s.rotSens;
                if (Mathf.Abs(sens) > 1e-6f) u.Rot((x - u.xRot) / (u.invX ? sens : -sens), (y - u.yRot) / (u.invY ? -sens : sens), true);
            }
            else if (u.slideAble)
            {
                if (u.setidPoi) value = Mathf.Lerp(d.From.Slide, d.To.Slide, t);
                float speed = u.slideSpeed * mainscript.s.slideSens, delta = value - u.slideValue;
                float mx = u.slideMultiplier.x == 0 ? 1 : u.slideMultiplier.x, my = u.slideMultiplier.y == 0 ? 1 : u.slideMultiplier.y, mz = u.slideMultiplier.z == 0 ? 1 : u.slideMultiplier.z;
                if (Mathf.Abs(speed) > 1e-6f)
                {
                    if (u.slideX != u.slideXi) u.Slide(delta / (speed * mx * (u.slideX ? 1 : -1)), 0, 0, true);
                    else if (u.slideY != u.slideYi) u.Slide(0, delta / (speed * my * (u.slideY ? 1 : -1)), 0, true);
                    else if (u.slideZ != u.slideZi) u.Slide(0, 0, delta / (speed * mz * (u.slideZ ? 1 : -1)), true);
                }
            }
            // from the hand's own (unsnapped) position, as a mouse turn moves it: from the snapped tunedFloat, a stepped
            // knob (radio volume) overshot to the next step and came back every frame
            else if (u.tuneAble) u.Tuned((Mathf.Lerp(u.tunedFloatMin, u.tunedFloatMax, value) - u.tempTunedFloat) / (u.tuneSpeed == 0 ? 1 : u.tuneSpeed), true);
            else if (u.turnable) u.Turn();
            d.Next = Math.Max(now, d.Next + 1.0 / d.Hz);
            if (now >= d.End) ObservedDragUsable = null;
        }
        /// Sampling runs in Entities.Tick, independent of bridge polling, for both the old and live paths.
        public static string ProbeUsable(uint net, int item, int usable, float seconds)
        {
            if (!EntityNumbers.Finite(seconds) || seconds <= 0 || seconds > 90 || !TLDRevamp.Net.EntityDoorAttachmentsTools.UsableArgs(net, item, usable, out _, out var u))
                return "{\"error\":\"loaded net/item/usable and seconds (0,90] required\"}";
            TelemetryTouch(net);
            return StartUsableProbe(u, seconds);
        }
        internal static string StartUsableProbe(usablescript u, float seconds)
        {
            if (u == null || !EntityNumbers.Finite(seconds) || seconds <= 0 || seconds > 90) return "{\"error\":\"loaded usable and seconds (0,90] required\"}";
            ObservedUsableProbe = new UsableProbe { U = u, Start = SessionLog.St, End = SessionLog.St + seconds, Last = EntityUsables.UsableBits(u) };
            return "{\"start\":" + EntityUsables.UN(ObservedUsableProbe.Start) + ",\"end\":" + EntityUsables.UN(ObservedUsableProbe.End) + "}";
        }
        internal static void UsableProbeTick()
        {
            var p = ObservedUsableProbe;
            if (p == null || p.U == null || SessionLog.St > p.End) return;
            p.Frames++; var v = EntityUsables.UsableBits(p.U);
            if (!v.Equals(p.Last)) { p.Updates++; p.Last = v; }
            if (p.Samples.Count < 8192)
            {
                var sample = new UsableSample { St = SessionLog.St, Value = EntityUsables.UsableState(p.U) };
                if (p.U.setidPoi)
                {
                    var key = PoiLiveKey(p.U);
                    if (TLDRevamp.Net.Entities.ObservedPoiObserved.TryGetValue(key.Poi, out var observed))
                    { sample.ReceiveHz = observed.Received.Hz(Time.realtimeSinceStartup); sample.Received = observed.ReceivedTotal; }
                    sample.Interpolating = TLDRevamp.Net.Entities.ObservedRemoteUsables.ContainsKey(key);
                }
                p.Samples.Add(sample);
            }
            else p.Truncated = true;
        }
        public static string UsableProbeJson()
        {
            var p = ObservedUsableProbe;
            if (p == null) return "{\"error\":\"start mp usableprobe net item usable seconds first\"}";
            var rows = new List<string>();
            foreach (var s in p.Samples) rows.Add("[" + EntityUsables.UN(s.St) + "," + EntityUsables.UsableValuesJson(s.Value) +
                (p.U != null && p.U.setidPoi ? "," + s.ReceiveHz + "," + (s.Interpolating ? "true" : "false") + "," + s.Received : "") + "]");
            double elapsed = Math.Max(1e-3, Math.Min(SessionLog.St, p.End) - p.Start);
            return "{\"start\":" + EntityUsables.UN(p.Start) + ",\"end\":" + EntityUsables.UN(p.End) + ",\"frames\":" + p.Frames + ",\"updates\":" + p.Updates + ",\"updatesPerS\":" + EntityUsables.UN(p.Updates / elapsed) +
                ",\"truncated\":" + (p.Truncated ? "true" : "false") + ",\"samples\":[" + string.Join(",", rows) + "]}";
        }
        internal static void Reset() { _dragUsable = null; _usableProbe = null; }
        internal static bool Held(usablescript usable) => _dragUsable != null && _dragUsable.U == usable;

        // ---------------------------------------------------------------- bridge: real input calls, per-frame observations in the session clock
        internal sealed class UsableDrag { public usablescript U; public double Start, End, Next; public float Hz; public PoiState From, To; }
        internal static UsableDrag _dragUsable;
        internal static UsableDrag ObservedDragUsable { get => _dragUsable; set => _dragUsable = value; }
        internal struct UsableSample { public double St; public PoiState Value; public int ReceiveHz; public long Received; public bool Interpolating; }
        internal sealed class UsableProbe
        {
            public usablescript U; public double Start, End; public int Frames, Updates; public bool Truncated;
            public UsableValue Last; public readonly List<UsableSample> Samples = new List<UsableSample>();
        }
        internal static UsableProbe _usableProbe;
        internal static UsableProbe ObservedUsableProbe { get => _usableProbe; set => _usableProbe = value; }
    }
}
