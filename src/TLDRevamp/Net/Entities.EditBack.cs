using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal static class EntityEditBack
    {
        public static long RefusedEditsAsked, RefusedEditsResent;
        private const int EditBackMax = 256;
        private static readonly Dictionary<ulong, float> _editBack = new Dictionary<ulong, float>();
        private static readonly List<ulong> _editBackDue = new List<ulong>();

        internal static void EditBackLater(uint net, int idx, bool malformed)
        {
            if (malformed || idx < 0) return;
            EditBackAfterHold(net, idx);
        }

        internal static void EditBackAfterHold(uint net, int idx)
        {
            ulong key = (ulong)net << 32 | (uint)idx;
            if (_editBack.Count < EditBackMax || _editBack.ContainsKey(key)) _editBack[key] = Time.realtimeSinceStartup + Entities.EditBackHold + 0.2f;
        }

        internal static void EditBackTick()
        {
            if (_editBack.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            _editBackDue.Clear();
            foreach (var kv in _editBack) if (now >= kv.Value) _editBackDue.Add(kv.Key);
            foreach (ulong key in _editBackDue)
            {
                _editBack.Remove(key);
                if (!Entities.RequestEditBack((uint)(key >> 32), (uint)key)) continue;
                RefusedEditsAsked++;
            }
        }
    }

    public static partial class Entities
    {
        private static void EditBackLater(SEnt se, int idx, bool malformed) => EntityEditBack.EditBackLater(se.NetId, idx, malformed);
        private static void EditBackAfterHold(SEnt se, int idx) => EntityEditBack.EditBackAfterHold(se.NetId, idx);
    }
}
