using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityPushTools
    {
        public static string TestPush(uint net, float secs)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || e.Root == null) return "{\"error\":\"no such object\"}";
            ObservedTestPushNet = net; ObservedTestPushUntil = Time.time + secs;
            return "{\"pushing\":" + net + ",\"proxy\":" + (e.Proxy ? "true" : "false") + ",\"pushables\":" + e.Root.GetComponentsInChildren<pushablescript>(true).Length + "}";
        }

        // ---- test hook (bridge): this player pushes the nearest pushable of an object for a while, through the game's
        // own Push, every frame — what holding the push key on it does
        internal static uint _testPushNet;
        internal static uint ObservedTestPushNet { get => _testPushNet; set => _testPushNet = value; }
        internal static float _testPushUntil;
        internal static float ObservedTestPushUntil { get => _testPushUntil; set => _testPushUntil = value; }

        internal static void TestPushTick()
        {
            if (_testPushNet == 0) return;
            if (Time.time > _testPushUntil || !ObservedEntities.TryGetValue(_testPushNet, out var e) || e.Root == null) { _testPushNet = 0; return; }
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return;
            var p = e.Root.GetComponentInChildren<pushablescript>();
            if (p == null) return;
            var from = pl.transform.position;
            var col = p.GetComponentInChildren<Collider>();
            var at = col != null ? col.ClosestPoint(from) : p.transform.position;
            var d = at - from; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            p.Push(d.normalized, at, true);
        }
    }
}
