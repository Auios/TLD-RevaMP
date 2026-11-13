using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using static TLDRevamp.Net.NetMeter;

namespace TLDRevamp.Net
{
    internal static class NetMeterTools
    {
        /// The panel: the busiest message types of the last second.
        public static string PanelText(int rows, bool compact = false)
        {
            Rates();
            Scratch.Clear();
            float outT = 0, inT = 0, msT = 0;
            for (int i = 0; i < 256; i++)
            {
                outT += ObservedROutB[i]; inT += ObservedRInB[i]; msT += ObservedRMs[i];
                if (ObservedROutB[i] + ObservedRInB[i] > 0 || ObservedRMs[i] > 0.001f) Scratch.Add(i);
            }
            Scratch.Sort((a, b) => (ObservedROutB[b] + ObservedRInB[b]).CompareTo(ObservedROutB[a] + ObservedRInB[a]));
            float fps = Mathf.Max(1f, ObservedRateFrames);
            var sb = new System.Text.StringBuilder();
            if (compact)
            {
                sb.Append($"Up {outT / 1024f:F1} KB/s · Down {inT / 1024f:F1} KB/s · {msT:F2} ms/s\n");
                sb.Append("Type              Up/s   KB/s  Down/s   KB/s   ms/s\n");
            }
            else
            {
                sb.Append($"<b>Network packets</b>  up {outT / 1024f:F1} KB/s ({outT / fps:F0} B/frame)  down {inT / 1024f:F1} KB/s ({inT / fps:F0} B/frame)  {msT:F2} ms/s\n");
                sb.Append("<b>type               up msg/s  up KB/s  down msg/s  down KB/s  ms/s</b>\n");
            }
            for (int k = 0; k < Scratch.Count && k < rows; k++)
            {
                int i = Scratch[k];
                string nm = Name((byte)i);
                if (compact)
                {
                    if (nm.Length > 16) nm = nm.Substring(0, 15) + "…";
                    sb.Append(nm.PadRight(17)).Append(ObservedROutM[i].ToString("F1").PadLeft(5)).Append((ObservedROutB[i] / 1024f).ToString("F2").PadLeft(7))
                        .Append(ObservedRInM[i].ToString("F1").PadLeft(8)).Append((ObservedRInB[i] / 1024f).ToString("F2").PadLeft(7)).Append(ObservedRMs[i].ToString("F2").PadLeft(7)).Append('\n');
                    continue;
                }
                if (nm.Length > 18) nm = nm.Substring(0, 18);
                sb.Append(nm.PadRight(19)).Append(ObservedROutM[i].ToString("F1").PadLeft(9)).Append((ObservedROutB[i] / 1024f).ToString("F2").PadLeft(9))
                  .Append(ObservedRInM[i].ToString("F1").PadLeft(12)).Append((ObservedRInB[i] / 1024f).ToString("F2").PadLeft(11)).Append(ObservedRMs[i].ToString("F2").PadLeft(7)).Append('\n');
            }
            if (Scratch.Count == 0) sb.Append("(nothing in the last second)\n");
            return sb.ToString();
        }

        private struct Rec { public float T; public bool Out; public byte Type; public int Bytes; public uint Peer; }
        private static readonly Rec[] Ring = new Rec[512];
        private static int _ringAt;
        private static long _ringN;
        internal static void Note(bool isOut, byte type, int bytes, uint peer)
        {
            Ring[_ringAt] = new Rec { T = Time.realtimeSinceStartup, Out = isOut, Type = type, Bytes = bytes, Peer = peer };
            _ringAt = (_ringAt + 1) % Ring.Length; _ringN++;
        }
        internal static void ResetHistory() { _ringAt = 0; _ringN = 0; }
        private static readonly List<int> Scratch = new List<int>();
        public static bool Show;
        internal static string[][] PanelRows(int rows)
        {
            Rates(); Scratch.Clear();
            float outT = 0, inT = 0, msT = 0;
            for (int i = 0; i < 256; i++)
            {
                outT += TLDRevamp.Net.NetMeter.ObservedROutB[i]; inT += TLDRevamp.Net.NetMeter.ObservedRInB[i]; msT += TLDRevamp.Net.NetMeter.ObservedRMs[i];
                if (TLDRevamp.Net.NetMeter.ObservedROutB[i] + TLDRevamp.Net.NetMeter.ObservedRInB[i] > 0 || TLDRevamp.Net.NetMeter.ObservedRMs[i] > .001f) Scratch.Add(i);
            }
            Scratch.Sort((a, b) => (TLDRevamp.Net.NetMeter.ObservedROutB[b] + TLDRevamp.Net.NetMeter.ObservedRInB[b]).CompareTo(TLDRevamp.Net.NetMeter.ObservedROutB[a] + TLDRevamp.Net.NetMeter.ObservedRInB[a]));
            int count = Math.Min(rows, Scratch.Count);
            var result = new string[2 + Math.Max(1, count)][];
            result[0] = new[] { $"Up {outT / 1024f:F1} KB/s · Down {inT / 1024f:F1} KB/s · {msT:F2} ms/s" };
            result[1] = new[] { "Type", "Up/s", "KB/s", "Down/s", "KB/s", "ms/s" };
            for (int k = 0; k < count; k++)
            {
                int i = Scratch[k];
                result[k + 2] = new[] { Name((byte)i), TLDRevamp.Net.NetMeter.ObservedROutM[i].ToString("F1"), (TLDRevamp.Net.NetMeter.ObservedROutB[i] / 1024f).ToString("F2"),
                    TLDRevamp.Net.NetMeter.ObservedRInM[i].ToString("F1"), (TLDRevamp.Net.NetMeter.ObservedRInB[i] / 1024f).ToString("F2"), TLDRevamp.Net.NetMeter.ObservedRMs[i].ToString("F2") };
            }
            if (count == 0) result[2] = new[] { "(nothing in the last second)" };
            return result;
        }

        /// `net log [n]`: the last n messages (newest last), each with when, which way, type, bytes and connection.
        public static string Log(int n)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            n = (int)Math.Min(Math.Min(n, Ring.Length), _ringN);
            var rows = new List<string>();
            for (int k = n; k >= 1; k--)
            {
                var r = Ring[(_ringAt - k + Ring.Length) % Ring.Length];
                rows.Add("{\"t\":" + r.T.ToString("F3", ic) + ",\"dir\":\"" + (r.Out ? "out" : "in") + "\",\"type\":" + TLDRevamp.Json.Str(Name(r.Type)) + ",\"bytes\":" + r.Bytes + ",\"conn\":" + r.Peer + "}");
            }
            return "{\"messages\":[" + string.Join(",", rows) + "]}";
        }
    }
}
