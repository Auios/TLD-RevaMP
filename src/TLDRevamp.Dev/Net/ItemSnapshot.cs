using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static TLDRevamp.Net.ItemSnapshot;

namespace TLDRevamp.Net
{
    internal static class ItemSnapshotTools
    {

        internal static long IdOf(object o) => o is iid2 b ? ((long)b.id << 16) + b.ind : o is iid a ? (long)a.id << 16 : 0;

        internal static string Normalised(itemDataClass d)
        {
            foreach (var it in d.items) it.transform = new posRotd(new Vector3d(0, 0, 0), Vector3.zero);
            foreach (var f in ListFields)
                if (f.GetValue(d) is IList list && list.Count > 1)
                {
                    var arr = new List<object>(); foreach (var o in list) arr.Add(o);
                    arr.Sort((x, y) => IdOf(x).CompareTo(IdOf(y)));
                    list.Clear(); foreach (var o in arr) list.Add(o);
                }
            return JsonUtility.ToJson(d);
        }

        /// Round trip of the group of the nearest car (bridge `itemsnap group`).
        /// Bridge `itemsnap groupdiag [r]`: the nearest car's group, and every item within r m that isn't in it — with
        /// how it's held (transform parent, attachable.attached/point/slot, the point's tosave parent, joint body).
        public static string GroupDiag(float r)
        {
            if (savedatascript.s == null || mainscript.s == null || mainscript.s.player == null) return "{\"error\":\"not in game\"}";
            var p = mainscript.s.player.transform.position;
            tosaveitemscript car = null; float best = float.MaxValue;
            foreach (var it in savedatascript.s.items.Values)
                if (it != null && it.car != null && it.transform.parent == null) { float dd = (it.transform.position - p).sqrMagnitude; if (dd < best) { best = dd; car = it; } }
            if (car == null) return "{\"error\":\"no car\"}";
            var group = Group(car);
            var inG = new HashSet<tosaveitemscript>(group);
            var outRows = new List<string>();
            foreach (var it in savedatascript.s.items.Values)
            {
                if (it == null || inG.Contains(it) || (it.transform.position - car.transform.position).magnitude > r) continue;
                var a = it.attachable;
                string Path(Transform t) { var parts = new List<string>(); for (int n = 0; t != null && n < 6; n++, t = t.parent) parts.Add(t.name); return string.Join("<", parts); }
                var pt = a != null ? a.point : null;
                var ptHost = pt != null ? pt.GetComponentInParent<tosaveitemscript>() : null;
                var j = it.GetComponent<Joint>();
                outRows.Add("{\"name\":" + Json.Str(it.name) + ",\"id\":" + it.idInSave + ",\"parent\":" + Json.Str(Path(it.transform.parent)) +
                            ",\"attachable\":" + (a != null ? "true" : "false") + ",\"attached\":" + (a != null && a.attached ? "true" : "false") +
                            ",\"point\":" + Json.Str(pt != null ? Path(pt) : "") + ",\"pointHost\":" + Json.Str(ptHost != null ? ptHost.name + "#" + ptHost.idInSave : "") +
                            ",\"slot\":" + Json.Str(a != null && a.slot != null ? Path(a.slot.transform) : "") +
                            ",\"lastRoot\":" + Json.Str(a != null && a.lastRoot != null ? a.lastRoot.name : "") +
                            ",\"joint\":" + Json.Str(j != null && j.connectedBody != null ? j.connectedBody.name : "") +
                            ",\"active\":" + (it.gameObject.activeInHierarchy ? "true" : "false") + "}");
            }
            // the roots the outside items hang under: their components, joints, and which fields of the car point at them
            var foreignRoots = new HashSet<Transform>();
            foreach (var it in savedatascript.s.items.Values)
                if (it != null && !inG.Contains(it) && (it.transform.position - car.transform.position).magnitude <= r) foreignRoots.Add(it.transform.root);
            var rootRows = new List<string>();
            foreach (var fr in foreignRoots)
            {
                var comps = new List<string>(); foreach (var c in fr.GetComponents<Component>()) if (c != null) comps.Add(c.GetType().Name);
                var joints = new List<string>(); foreach (var jj in fr.GetComponentsInChildren<Joint>(true)) joints.Add(jj.name + "->" + (jj.connectedBody != null ? jj.connectedBody.name : "null"));
                var refs = new List<string>();
                foreach (var mb in car.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    foreach (var f in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        object v; try { v = f.GetValue(mb); } catch { continue; }
                        Transform tt = v is Component cc && cc != null ? cc.transform : v is GameObject go && go != null ? go.transform : null;
                        if (tt != null && tt.root == fr && tt.root != car.transform.root) refs.Add(mb.GetType().Name + "(" + mb.name + ")." + f.Name + "=" + tt.name);
                        if (refs.Count > 40) break;
                    }
                }
                rootRows.Add("{\"root\":" + Json.Str(fr.name) + ",\"comps\":" + Json.Str(string.Join(" ", comps)) + ",\"joints\":" + Json.Str(string.Join(" ", joints)) +
                             ",\"refsFromCar\":" + Json.Str(string.Join(" | ", refs)) + "}");
            }
            var names = new List<string>(); foreach (var g in group) names.Add(g.name + "#" + g.idInSave);
            return "{\"car\":" + Json.Str(car.name + "#" + car.idInSave) + ",\"group\":" + group.Count + ",\"members\":" + Json.Str(string.Join(" ", names)) +
                   ",\"outside\":[" + string.Join(",", outRows) + "],\"roots\":[" + string.Join(",", rootRows) + "]}";
        }

        public static string TestGroup()
        {
            if (savedatascript.s == null || mainscript.s == null || mainscript.s.player == null) return "{\"error\":\"not in game\"}";
            var p = mainscript.s.player.transform.position;
            tosaveitemscript car = null; float best = float.MaxValue;
            foreach (var it in savedatascript.s.items.Values)
                if (it != null && it.car != null && it.transform.parent == null) { float dd = (it.transform.position - p).sqrMagnitude; if (dd < best) { best = dd; car = it; } }
            if (car == null) return "{\"error\":\"no car\"}";
            var group = Group(car);
            byte[] b0 = RecordCodec.Encode(CaptureGroup(group));
            var d = RecordCodec.Decode(b0);
            Move(d, new Vector3d(0, 0, 6));
            var map = SpawnGroup(d);
            var back = new Dictionary<uint, uint>();
            foreach (var kv in map) back[kv.Value] = kv.Key;
            var copies = new List<tosaveitemscript>();
            foreach (var kv in map) if (savedatascript.s.items.TryGetValue(kv.Value, out var c)) copies.Add(c);
            // compare right away, records normalised: ids mapped back, positions zeroed, lists sorted by id
            var dc = CaptureGroup(copies); Remap(dc, back);
            var d0 = RecordCodec.Decode(b0);
            string a = Normalised(d0), bb = Normalised(dc);
            int k = 0; while (k < a.Length && k < bb.Length && a[k] == bb[k]) k++;
            string res = "{\"car\":" + Json.Str(car.name) + ",\"items\":" + group.Count + ",\"spawned\":" + copies.Count + ",\"bytes\":" + b0.Length +
                         ",\"identical\":" + (a == bb ? "true" : "false") +
                         (a == bb ? "" : ",\"diffAt\":" + k + ",\"orig\":" + Json.Str(a.Substring(Math.Max(0, k - 80), Math.Min(200, a.Length - Math.Max(0, k - 80)))) +
                                         ",\"copy\":" + Json.Str(bb.Substring(Math.Max(0, k - 80), Math.Min(200, bb.Length - Math.Max(0, k - 80))))) + "}";
            KeepCopy = copies;
            if (Full) res = res.Substring(0, res.Length - 1) + ",\"origJson\":" + a + ",\"copyJson\":" + bb + "}";
            return res;
        }

        public static string Test(int n)
        {
            if (savedatascript.s == null || mainscript.s == null || mainscript.s.player == null) return "{\"error\":\"not in game\"}";
            var p = mainscript.s.player.transform.position;
            var near = new List<tosaveitemscript>();
            foreach (var it in savedatascript.s.items.Values)
                if (it != null && it.gameObject.activeInHierarchy && it.transform.parent == null) near.Add(it);
            near.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
            var rows = new List<string>();
            int same = 0, diff = 0, failed = 0;
            for (int i = 0; i < near.Count && i < n; i++)
            {
                var orig = near[i];
                string name = orig.name.Replace("(Clone)", "");
                try
                {
                    byte[] b0 = RecordCodec.Encode(Capture(orig));
                    var d = RecordCodec.Decode(b0);
                    if (d == null) { failed++; rows.Add(Json.Str(name + ": decode failed")); continue; }
                    uint nid = savedatascript.s.GetNewKey();
                    Rekey(d, orig.idInSave, nid);
                    Move(d, new Vector3d(3, 0.5, 0));
                    var copy = Spawn(d, nid);
                    if (copy == null) { failed++; rows.Add(Json.Str(name + ": spawn failed")); continue; }
                    // compare right away (before physics moves the copy): records with id/position aligned
                    var dc = Capture(copy);
                    Rekey(dc, nid, orig.idInSave);
                    var d0 = RecordCodec.Decode(b0);
                    foreach (var it in dc.items) it.transform = new posRotd(new Vector3d(0, 0, 0), Vector3.zero);
                    foreach (var it in d0.items) it.transform = new posRotd(new Vector3d(0, 0, 0), Vector3.zero);
                    var ba = RecordCodec.Encode(d0); var bb = RecordCodec.Encode(dc);
                    bool eq = ba.Length == bb.Length;
                    for (int q = 0; eq && q < ba.Length; q++) if (ba[q] != bb[q]) eq = false;
                    if (eq) { same++; rows.Add(Json.Str(name + ": identical (" + b0.Length + " B)")); }
                    else
                    {
                        diff++;
                        string a = ToJson(d0), bj = ToJson(dc);
                        int k = 0; while (k < a.Length && k < bj.Length && a[k] == bj[k]) k++;
                        int s0 = Math.Max(0, k - 60);
                        rows.Add(Json.Str(name + ": DIFF (json view at " + k + ") orig …" + a.Substring(s0, Math.Min(140, a.Length - s0)) + " | copy …" + bj.Substring(s0, Math.Min(140, bj.Length - s0))));
                    }
                    UnityEngine.Object.Destroy(copy.gameObject);
                    savedatascript.s.items.Remove(nid);
                }
                catch (Exception e) { failed++; rows.Add(Json.Str(name + ": " + e.GetType().Name + " " + e.Message)); }
            }
            return "{\"schema\":\"" + RecordCodec.SchemaHash(typeof(itemDataClass)).ToString("X16") + "\",\"unsupported\":" + Json.Str(RecordCodec.Unsupported) + ",\"tested\":" + (same + diff + failed) + ",\"identical\":" + same + ",\"different\":" + diff + ",\"failed\":" + failed + ",\"rows\":[" + string.Join(",", rows) + "]}";
        }
    }
}
