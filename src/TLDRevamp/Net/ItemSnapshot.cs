using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// An item's network snapshot = its save record (docs/MULTIPLAYER-ARCHITECTURE.md §4b): savedatascript.SaveToDictionary
    /// into a fresh itemDataClass, sent as JSON (JsonUtility: data only — never BinaryFormatter, which the save files use,
    /// because deserialising it from another player's bytes can run their code). Spawned on the other side through the
    /// game's own path (SpawnItem + InitAfterSpawn + LoadStuff), the same one that places saved items near the player.
    ///
    /// Bridge `itemsnap test [n]`: round trip of the n items nearest to the player (capture → JSON → spawn a copy with a
    /// new id 3 m away → capture the copy → compare the records with id/position aligned). Differences mean state the
    /// save record doesn't carry, or JSON loses.
    public static class ItemSnapshot
    {
        public static itemDataClass Capture(tosaveitemscript item)
        {
            var d = new itemDataClass();
            savedatascript.SaveToDictionary(item, d);
            return d;
        }

        public static string ToJson(itemDataClass d) => JsonUtility.ToJson(d);
        public static itemDataClass FromJson(string s) => JsonUtility.FromJson<itemDataClass>(s);

        internal static readonly FieldInfo[] ListFields = typeof(itemDataClass).GetFields(BindingFlags.Public | BindingFlags.Instance);

        /// Every record in the snapshot that belongs to `from` gets id `to` (records of other items are left alone),
        /// including id-carrying objects nested inside records (e.g. a part condition's colour).
        public static void Rekey(itemDataClass d, uint from, uint to)
        {
            foreach (var f in ListFields)
            {
                if (!(f.GetValue(d) is IList list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e == null) continue;
                    var boxed = RekeyObj(e, from, to, 0);
                    if (e.GetType().IsValueType) list[i] = boxed;
                }
            }
        }

        private static object RekeyObj(object o, uint from, uint to, int depth)
        {
            if (o is iid a && a.id == from) { a.id = to; o = a; }
            else if (o is iid2 b && b.id == from) { b.id = to; o = b; }
            if (depth > 3) return o;
            foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var v = f.GetValue(o);
                if (v == null || v is string || f.FieldType.IsPrimitive || f.FieldType.IsEnum) continue;
                if (v is iid || v is iid2)
                {
                    var nv = RekeyObj(v, from, to, depth + 1);
                    if (f.FieldType.IsValueType) f.SetValue(o, nv);
                }
                else if (v is IList inner)
                    for (int i = 0; i < inner.Count; i++)
                    {
                        var e = inner[i];
                        if (e == null || e is string || e.GetType().IsPrimitive) continue;
                        var ne = RekeyObj(e, from, to, depth + 1);
                        if (e.GetType().IsValueType) inner[i] = ne;
                    }
            }
            return o;
        }

        public static void Move(itemDataClass d, Vector3d delta)
        {
            foreach (var it in d.items) it.transform = new posRotd(it.transform.pos + delta, it.transform.rot.Load());
        }

        public static tosaveitemscript Spawn(itemDataClass d, uint id)
        {
            var sd = new savedata { itemData = d };
            var go = savedatascript.s.SpawnItem(d.items[0]);
            if (go == null) return null;
            savedatascript.s.InitAfterSpawn(go, id);
            savedatascript.s.LoadStuff(sd, id, true);
            return savedatascript.s.items.TryGetValue(id, out var t) ? t : null;
        }

        // ------------------------------------------------------------------ groups (a car + its parts + locked items)

        /// The item, everything attached/parented under it, the parts in its part slots, and items locked in it
        /// (physLocks), closed over those links.
        /// Part slots count on their own: a slot can sit on a body outside the item's hierarchy. Bus01's rear section
        /// (BusBack: its own Rigidbody, a ConfigurableJoint to BusFront, no tosaveitemscript) carries the rear wheel slots,
        /// listed in BusFront's partslotscripts — the wheels there are in no transform under BusFront. Before v0.64.3 a
        /// shared bus went out with 6 of its 8 wheel sets (busprobe --mp: host record 24 items, spawner 28; groupdiag).
        /// The local player sits under the car's seat while seated (fpscontroller.GetIn parents it to the sit point): what it
        /// wears, holds or carries is its own shared object, not the car's — taken into a car shared then (sitting down in
        /// an unshared car shares it), the other machines got a second hat in the car. Skipped (A/B: false = before v0.68.2).
        public static bool SkipPlayersThings = true;
        public static long PlayersThingsSkipped;

        public static List<tosaveitemscript> Group(tosaveitemscript root)
        {
            var me = SkipPlayersThings && mainscript.s != null ? mainscript.s.player : null;
            if (me != null && root != null && root.transform.IsChildOf(me.transform)) me = null;   // a thing of the player's itself: as before
            var list = new List<tosaveitemscript>();
            var seen = new HashSet<uint>();
            var queue = new Queue<tosaveitemscript>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var it = queue.Dequeue();
                if (it == null || !seen.Add(it.idInSave)) continue;
                list.Add(it);
                foreach (var ch in it.GetComponentsInChildren<tosaveitemscript>(true))
                {
                    if (seen.Contains(ch.idInSave)) continue;
                    if (ch != root && ch.attachable != null && ch.attachable.utanfuto != null) continue;
                    if (me != null && ch.transform.IsChildOf(me.transform)) { PlayersThingsSkipped++; continue; }
                    queue.Enqueue(ch);
                }
                if (it.partslotscripts != null)
                    foreach (var slot in it.partslotscripts)
                        if (slot != null && slot.parts != null)
                            foreach (var part in slot.parts)
                                if (part != null && part.utanfuto == null && part.tosave != null && !seen.Contains(part.tosave.idInSave)) queue.Enqueue(part.tosave);
                var d = Capture(it);
                foreach (var pl in d.physLocks)
                    if (pl.IDs != null)
                        foreach (var lid in pl.IDs)
                            if (savedatascript.s.items.TryGetValue(lid, out var locked) && !seen.Contains(lid)) queue.Enqueue(locked);
            }
            return list;
        }

        public static itemDataClass CaptureGroup(List<tosaveitemscript> items)
        {
            var d = new itemDataClass();
            foreach (var it in items) savedatascript.SaveToDictionary(it, d);
            return d;
        }

        /// CaptureGroup, keeping each item's own record too (same lists, same order: SaveToDictionary only appends).
        public static itemDataClass CaptureGroup(List<tosaveitemscript> items, List<itemDataClass> perItem)
        {
            var d = new itemDataClass();
            foreach (var it in items)
            {
                var di = it != null ? Capture(it) : null;
                perItem.Add(di);
                if (di == null) continue;
                foreach (var f in ListFields)
                    if (f.GetValue(di) is IList src && f.GetValue(d) is IList dst)
                        foreach (var x in src) dst.Add(x);
            }
            return d;
        }

        /// Every id-valued field (uint, or uint in a list) found in `map` is replaced — the records' own ids and their
        /// references to each other (attachable parentid, physLock IDs, …) move together.
        public static void Remap(itemDataClass d, Dictionary<uint, uint> map)
        {
            foreach (var f in ListFields)
                if (f.GetValue(d) is IList list)
                    for (int i = 0; i < list.Count; i++)
                        if (list[i] != null) { var o = RemapObj(list[i], map, 0); if (list[i].GetType().IsValueType) list[i] = o; }
        }

        private static object RemapObj(object o, Dictionary<uint, uint> map, int depth)
        {
            if (depth > 4) return o;
            foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.IsNotSerialized) continue;
                var v = f.GetValue(o);
                if (v == null) continue;
                if (f.FieldType == typeof(uint)) { if (map.TryGetValue((uint)v, out var nv)) f.SetValue(o, nv); }
                else if (v is List<uint> lu) { for (int i = 0; i < lu.Count; i++) if (map.TryGetValue(lu[i], out var nv)) lu[i] = nv; }
                else if (f.FieldType == typeof(uint[]) && v is uint[] au) { for (int i = 0; i < au.Length; i++) if (map.TryGetValue(au[i], out var nv)) au[i] = nv; }
                else if (v is IList inner) { for (int i = 0; i < inner.Count; i++) if (inner[i] != null && !(inner[i] is string) && !inner[i].GetType().IsPrimitive) { var ne = RemapObj(inner[i], map, depth + 1); if (inner[i].GetType().IsValueType) inner[i] = ne; } }
                else if (!f.FieldType.IsPrimitive && !f.FieldType.IsEnum && !(v is string) && (v is iid || v is iid2 || f.FieldType.IsDefined(typeof(System.SerializableAttribute), false)))
                { var nv2 = RemapObj(v, map, depth + 1); if (f.FieldType.IsValueType) f.SetValue(o, nv2); }
            }
            return o;
        }

        public static long ForeignRefsDropped;

        /// A record set's references to items OUTSIDE it (an attachment's parent, a container's contents) are the
        /// sender's ids: here they would name whatever local item has that number — a box pulled a car's wheels into
        /// itself (farworld run 15). Before the remap, such references are dropped: the item loads loose / the box
        /// without that content, never on or in a stranger. Counted and logged (first 30).
        public static int DropForeignRefs(itemDataClass d, Dictionary<uint, uint> group, string what)
        {
            int n = 0;
            if (d.attachable != null)
                n += d.attachable.RemoveAll(a =>
                {
                    if (a == null || a.attachType != (int)save_attachable.attachTypeEmu.toItem || group.ContainsKey(a.parentid)) return false;
                    if (ForeignRefsDropped + 1 <= 30) Plugin.Log.LogWarning($"{what}: attachment #{a.id} → #{a.parentid} outside its group dropped");
                    ForeignRefsDropped++;
                    return true;
                });
            if (d.physLocks != null)
                foreach (var pl in d.physLocks)
                {
                    if (pl == null || pl.IDs == null) continue;
                    var keep = new List<int>();
                    for (int i = 0; i < pl.IDs.Length; i++) if (group.ContainsKey(pl.IDs[i])) keep.Add(i);
                    if (keep.Count == pl.IDs.Length) continue;
                    int lost = pl.IDs.Length - keep.Count;
                    if (ForeignRefsDropped + 1 <= 30) Plugin.Log.LogWarning($"{what}: container #{pl.id} lock {pl.ind}: {lost} of {pl.IDs.Length} contents outside its group dropped");
                    ForeignRefsDropped += lost; n += lost;
                    var ids = new uint[keep.Count]; var ps = pl.poss != null ? new vector3[keep.Count] : null; var rs = pl.rots != null ? new vector3[keep.Count] : null;
                    for (int j = 0; j < keep.Count; j++)
                    {
                        ids[j] = pl.IDs[keep[j]];
                        if (ps != null && keep[j] < pl.poss.Length) ps[j] = pl.poss[keep[j]];
                        if (rs != null && keep[j] < pl.rots.Length) rs[j] = pl.rots[keep[j]];
                    }
                    pl.IDs = ids; pl.poss = ps; pl.rots = rs;
                }
            return n;
        }

        /// Spawn a group through the game's own path: all items first, then all state (so parts find their parents).
        /// Returns old id → new local id.
        public static Dictionary<uint, uint> SpawnGroup(itemDataClass d) => SpawnGroup(d, null);

        /// `order`: the new local id of every record, in record order (= the owner's group order: parts are identified
        /// across machines by their index in it).
        public static Dictionary<uint, uint> SpawnGroup(itemDataClass d, List<uint> order)
        {
            var map = new Dictionary<uint, uint>();
            foreach (var it in d.items) { var k = savedatascript.s.GetNewKey(); map[it.id] = k; order?.Add(k); }
            DropForeignRefs(d, map, "spawn group");
            Remap(d, map);
            var sd = new savedata { itemData = d };
            var records = new List<save_item>(d.items); // LoadStuff edits the lists it reads
            foreach (var it in records)
            {
                var go = savedatascript.s.SpawnItem(it);
                if (go != null) savedatascript.s.InitAfterSpawn(go, it.id);
            }
            var slotted = new List<save_attachable>();   // LoadStuff edits the lists it reads
            foreach (var at in d.attachable) if (at.attachType == 1 && at.indexType == 2) slotted.Add(new save_attachable(at));
            foreach (var it in records)
                if (savedatascript.s.items.ContainsKey(it.id)) savedatascript.s.LoadStuff(sd, it.id, true);
            CheckSlotted(slotted, "spawn group");
            return map;
        }

        /// Parts a group record puts in a part slot that aren't attached once it's loaded (busdrive.py: a bus copy came up
        /// with its two rear wheels — slots on BusBack — out of their slots, sometimes; the friend driving it couldn't move).
        public static long SlotMisses; public static string SlotMissLog = "";
        internal static void CheckSlotted(List<save_attachable> slotted, string what)
        {
            foreach (var at in slotted)
            {
                if (!savedatascript.s.items.TryGetValue(at.id, out var part) || part == null || part.attachable == null) continue;
                if (part.attachable.attached) continue;
                SlotMisses++;
                string why;
                if (!savedatascript.s.items.TryGetValue(at.parentid, out var parent) || parent == null) why = "parent " + at.parentid + " not loaded";
                else if (parent.partslotscripts == null || at.index < 0 || at.index >= parent.partslotscripts.Count) why = "slot " + at.index + " out of range (" + (parent.partslotscripts != null ? parent.partslotscripts.Count : -1) + ")";
                else
                {
                    var sl = parent.partslotscripts[at.index];
                    why = "slot " + at.index + " " + (sl != null ? sl.name + " on " + sl.transform.root.name + " active " + sl.gameObject.activeInHierarchy +
                          " holds " + (sl.hasPart() ? sl.part().name : "nothing") : "null");
                }
                var line = what + ": " + part.name + " (" + at.id + ") not in its slot: " + why;
                if (SlotMissLog.Length < 4000) SlotMissLog += line + "\n";
                if (SlotMisses <= 20) Plugin.Log.LogWarning("slot miss — " + line);
            }
        }

        public static List<tosaveitemscript> KeepCopy;
        public static bool Full;
    }
}
