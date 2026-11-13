using System;
using System.Text;

namespace TLDRevamp
{
    /// In memory only, with the bridge on only: never in a player's game or in a report.
    internal static class ObjectTrail
    {
        internal const int Capacity = 60000;
        public static bool On;
        private static TelemetryEvent[] _rows;
        private static int _head;
        private static long _total;
        public static void Add(TelemetryEvent row)
        {
            if (!On) return;
            if (_rows == null) _rows = new TelemetryEvent[Capacity];
            _rows[_head] = row; _head = (_head + 1) % Capacity; _total++;
        }
        public static void Clear() { if (_rows != null) Array.Clear(_rows, 0, Capacity); _head = 0; _total = 0; }
        public static string Rows(uint net)
        {
            var sb = new StringBuilder("{\"total\":" + _total + ",\"lost\":" + Math.Max(0, _total - Capacity) + ",\"rows\":[");
            int count = (int)Math.Min(_total, Capacity); bool first = true;
            for (int i = 0; i < count; i++)
            {
                var e = _rows[(_head - count + i + Capacity) % Capacity];
                if (e.Net != net && e.Other != net) continue;
                if (!first) sb.Append(','); first = false;
                sb.Append("{\"st\":" + Json.Num(e.St) + ",\"kind\":" + Json.Str(e.Kind) + ",\"net\":" + e.Net + ",\"other\":" + e.Other +
                    ",\"epoch\":" + e.Epoch + ",\"item\":" + e.Item + ",\"slot\":" + e.Slot + ",\"from\":" + e.From + ",\"to\":" + e.To + ",\"result\":" + Json.Str(e.Result) + "}");
            }
            return sb.Append("]}").ToString();
        }
    }

}
