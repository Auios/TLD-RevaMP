using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityAiTools
    {
        private static readonly RaycastHit[] ProbeHits = new RaycastHit[32];
        internal static readonly NetWriter W = new NetWriter();

        /// Test hook (bridge `call TLDRevamp.Net.EntityAiTools.TestObjectHit break|explode|shotgun net bi kind`): send the host
        /// what a hit, a set-off or a passenger's controls on object `net` would (tools/objecthits.py). Returns how many
        /// breakables the object has here.
        public static string TestObjectHit(string what, uint net, int bi, int kind)
        {
            W.Reset();
            if (what == "break") { W.U8(BreakHit); W.U32(net); W.VarU32((uint)bi); W.U8((byte)kind); W.F32(1f); W.F32(0f); W.F32(0f); W.F32(0f); }
            else if (what == "explode") { W.U8(ExplodeReq); W.U32(net); W.U8(2); W.VarU32((uint)bi); W.F32(0f); W.Bool(false); }
            else if (what == "shotgun") { W.U8(ShotgunIn); W.U32(net); W.U8(0); W.U8(0); W.U16(0); W.U16(0); }
            else return "break, explode or shotgun";
            ToServer(W, true);
            int n = ObservedEntities.TryGetValue(net, out var e) && Resolve(e) && e.Root != null ? e.Root.transform.root.GetComponentsInChildren<breakablescript>(true).Length : -1;
            return "sent; breakables here " + n;
        }

        /// Test (`mp breakables <r> [name]`): the breakables within r m of our player (how many of each kind, root/gib), the
        /// nearest 20 whose root or gib name has `name` in it - what it is, the
        /// shared object it belongs to (0: none, a piece of the world), whether it is broken, its gib.
        public static string Breakables(float radius, string filter)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var found = new List<KeyValuePair<float, string>>();
            var kinds = new SortedDictionary<string, int>();
            foreach (var br in Object.FindObjectsOfType<breakablescript>())
            {
                if (br == null) continue;
                float d = Vector3.Distance(br.transform.position, pl.transform.position);
                if (d > radius) continue;
                string kind = br.transform.root.name + "/" + (br.Gib != null ? br.Gib.name : "");
                kinds[kind] = kinds.TryGetValue(kind, out int kn) ? kn + 1 : 1;
                if (!string.IsNullOrEmpty(filter) && kind.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var e = EntOfRoot(br.transform.root); var g = mainscript.GlobalFromUnityPos(br.transform.position);
                found.Add(new KeyValuePair<float, string>(d, "{\"name\":" + Json.Str(br.name) + ",\"root\":" + Json.Str(br.transform.root.name) + ",\"net\":" + (e != null ? e.NetId : 0) +
                    ",\"destroyed\":" + (br.destroyed ? "true" : "false") + ",\"gib\":" + Json.Str(br.Gib != null ? br.Gib.name : "") + ",\"health\":" + br.health.ToString("F1", ci) +
                    ",\"pos\":[" + g.x.ToString("F2", ci) + "," + g.y.ToString("F2", ci) + "," + g.z.ToString("F2", ci) + "],\"d\":" + d.ToString("F1", ci) + "}"));
            }
            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            if (found.Count > 20) found.RemoveRange(20, found.Count - 20);
            var kl = new List<string>(); foreach (var kv in kinds) kl.Add(Json.Str(kv.Key) + ":" + kv.Value);
            return "{\"kinds\":{" + string.Join(",", kl) + "},\"breakables\":[" + string.Join(",", found.ConvertAll(x => x.Value)) + "]}";
        }

        /// Test (`mp breaknear <name> <r>`): a blow hard enough to break the nearest unbroken breakable whose root name has
        /// `name` in it, through the game's TryBreak (a melee hit's call: the katana's).
        public static string BreakNear(string filter, float radius)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            breakablescript best = null; float bd = radius;
            foreach (var br in Object.FindObjectsOfType<breakablescript>())
            {
                if (br == null || br.destroyed || (br.transform.root.name + "/" + (br.Gib != null ? br.Gib.name : "")).IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                float d = Vector3.Distance(br.transform.position, pl.transform.position);
                if (d < bd) { bd = d; best = br; }
            }
            if (best == null) return "{\"error\":\"nothing to break\"}";
            var g = mainscript.GlobalFromUnityPos(best.transform.position);
            string name = best.transform.root.name;
            best.TryBreak(Mathf.Max(best.health, 1f) * 2f + 1000f);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "{\"broke\":" + Json.Str(name) + ",\"destroyed\":" + (best != null && best.destroyed ? "true" : "false") +
                   ",\"pos\":[" + g.x.ToString("F2", ci) + "," + g.y.ToString("F2", ci) + "," + g.z.ToString("F2", ci) + "]}";
        }

        public static string AiSee()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var rows = new List<string>();
            foreach (var a in Object.FindObjectsOfType<newAiScript>())
            {
                if (a.head == null) continue;
                foreach (var t in RemotePlayers.AiTargets())
                {
                    var pv = t.body.position - a.transform.position;
                    float ang = Vector3.Angle(a.head.forward, pv);
                    float tc = PlayerCombat.RayCapsule(new Ray(a.head.position, pv), t.feet, t.top, EntityAI.BodyR);
                    string block = "";
                    int n = Physics.RaycastNonAlloc(a.head.position, pv, ProbeHits, tc > 0 ? tc : a.range, a.lm);
                    float bd = float.MaxValue;
                    for (int i = 0; i < n; i++)
                        if (ProbeHits[i].distance < bd && ProbeHits[i].collider.gameObject.layer != 23) { bd = ProbeHits[i].distance; block = ProbeHits[i].collider.transform.root.name + "/" + ProbeHits[i].collider.name + " L" + ProbeHits[i].collider.gameObject.layer + " @" + bd.ToString("F2", ci); }
                    // how far the eye ray passes from the body's axis, and the head's offset from the creature's root
                    float missBy = PlayerCombat.RayCapsule(new Ray(a.head.position, pv), t.feet, t.top, 50f) < 0f ? -1f : RayMiss(new Ray(a.head.position, pv), t.feet, t.top);
                    var ho = a.head.position - a.transform.position; ho.y = 0f;
                    rows.Add("{\"ai\":" + Json.Str(DescribeRoot(a.transform.root)) + ",\"player\":" + t.id + ",\"dist\":" + pv.magnitude.ToString("F2", ci) +
                             ",\"missBy\":" + missBy.ToString("F2", ci) + ",\"headOff\":" + ho.magnitude.ToString("F2", ci) +
                             ",\"angle\":" + ang.ToString("F0", ci) + ",\"fov\":" + a.fov.ToString("F0", ci) + ",\"rayAtBody\":" + tc.ToString("F2", ci) +
                             ",\"headY\":" + a.head.position.y.ToString("F2", ci) + ",\"feetY\":" + t.feet.y.ToString("F2", ci) + ",\"topY\":" + t.top.y.ToString("F2", ci) +
                             ",\"rootDy\":" + pv.y.ToString("F2", ci) + ",\"blockedBy\":" + Json.Str(block) + ",\"seenAt\":" +
                             EntityAI.SeenAt(a, a.head.position, pv, a.range, t.feet, t.top, false).ToString("F2", ci) + ",\"kin\":" + (a.rb != null && a.rb.isKinematic ? "true" : "false") + "}");
                }
            }
            return "{\"see\":[" + string.Join(",", rows) + "]}";
        }

        /// Test readout (bridge `mp aisee`): every creature's view of every remote player, step by step as the owner
        /// tests it — angle vs fov, where its eye ray (vanilla's direction: root to root, from the head) meets the body,
        /// the first thing in front of it.
        /// Closest distance between a ray and a segment (diagnostics).
        internal static float RayMiss(Ray ray, Vector3 a, Vector3 b)
        {
            float best = float.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                var p = Vector3.Lerp(a, b, i / 20f);
                float s = Mathf.Max(0f, Vector3.Dot(p - ray.origin, ray.direction));
                best = Mathf.Min(best, (ray.origin + ray.direction * s - p).magnitude);
            }
            return best;
        }
    }
}
