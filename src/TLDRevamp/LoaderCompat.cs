using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp
{
    /// What TLD Loader mods use of the public game's save and attach code that the beta changed or dropped. LoaderRetarget
    /// points the mod's own instructions here (by name: it stays free of the game's types); nothing in the game changes.
    ///
    /// The spawner most of RUNDEN's and M_'s workshop mods share (issue 44: dingus' cat, Nitrous' bottle): pick the first
    /// free save id with `!savedatascript.d.toSaveStuff.ContainsKey(i) && !data.farStuff.ContainsKey(i) &&
    /// !data.nearStuff.ContainsKey(i)`, instantiate, `tosaveitemscript.FStart(i)`. On the beta those fields and the type
    /// save_prefab are gone: the whole method stopped at its first call, so nothing was made, and the spawn menu's holder
    /// (the dingus cat model with its collider switched off) stayed in the air where it was spawned.
    public static class LoaderCompat
    {
        public static long Calls;

        // ------------------------------------------------------------------ the old save ids

        private static Dictionary<int, tosaveitemscript> _toSave;
        private static int _toSaveFrame = -1;
        /// savedatascript.toSaveStuff: the placed items by id (the beta keys them by uint). One copy per frame: the spawner
        /// loop reads it once per id it tries.
        public static Dictionary<int, tosaveitemscript> ToSaveStuff(savedatascript s)
        {
            Calls++;
            if (_toSave != null && _toSaveFrame == Time.frameCount) return _toSave;
            _toSave = new Dictionary<int, tosaveitemscript>();
            var items = s != null ? s.items : null;
            if (items != null) foreach (var kv in items) _toSave[unchecked((int)kv.Key)] = kv.Value;
            _toSaveFrame = Time.frameCount;
            return _toSave;
        }

        /// savedata.nearStuff / farStuff: the records it kept per id. Their type (save_prefab) is gone too, so the only use
        /// the spawner makes of them, ContainsKey, comes to RecordIdUsed with this token instead.
        public static object SavedRecords(savedata d) { Calls++; return d; }

        /// Any record or placed item with this id: the beta keeps far items as records in data.itemData.items.
        public static bool RecordIdUsed(object records, int id)
        {
            Calls++;
            var s = savedatascript.s;
            if (s == null) return false;
            uint key = unchecked((uint)id);
            if (s.items != null && s.items.ContainsKey(key)) return true;
            var data = records as savedata ?? s.data;
            return data != null && data.itemData != null && savedatascript.IndexOfID(data.itemData.items, key, out _);
        }

        /// tosaveitemscript.FStart(int): the beta's takes a uint.
        public static void FStart(tosaveitemscript t, int id) { Calls++; if (t != null) t.FStart(unchecked((uint)id)); }

        /// tosaveitemscript.removeFromMemory: the public game's "not an item of its own" for display copies of a game prefab
        /// (Nitrous' gauge face is a compass inside the bottle). One never started isn't saved on the beta either, but the
        /// beta takes every item out of its parent the frame after it starts (NextFrameUnparent): the compass fell out and
        /// the gauge, its own colliders already removed, fell through the world. Kept where the mod put it.
        public static bool GetRemoveFromMemory(tosaveitemscript t) => t != null && t.dontUnparent;
        public static void SetRemoveFromMemory(tosaveitemscript t, bool value) { Calls++; if (t != null && value) t.dontUnparent = true; }

        /// GetComponent&lt;T&gt;() of a type the beta doesn't have (partscript): there is none.
        public static Component NoComponent(object owner) { Calls++; return null; }

        // ------------------------------------------------------------------ attaching

        private static tosaveitemscript SaveOf(pickupable p) => p == null ? null : p.tosave != null ? p.tosave : p.GetComponent<tosaveitemscript>();

        /// pickupable.attach: the beta keeps the attach script on the item's tosaveitemscript only.
        public static attachablescript GetAttach(pickupable p) { var t = SaveOf(p); return t != null ? t.attachable : null; }
        public static void SetAttach(pickupable p, attachablescript a) { Calls++; var t = SaveOf(p); if (t != null) t.attachable = a; }

        /// pickupable.cols / cols2: arrays there, lists here.
        public static layerScript[] GetCols(pickupable p) => p != null && p.cols != null ? p.cols.ToArray() : null;
        public static void SetCols(pickupable p, layerScript[] v) { Calls++; if (p != null) p.cols = v != null ? new List<layerScript>(v) : null; }
        public static layerScript[] GetCols2(pickupable p) => p != null && p.cols2 != null ? p.cols2.ToArray() : null;
        public static void SetCols2(pickupable p, layerScript[] v) { Calls++; if (p != null) p.cols2 = v != null ? new List<layerScript>(v) : null; }

        /// attachablescript.targetTosave: what it is attached to.
        public static tosaveitemscript TargetTosave(attachablescript a) => a != null ? a.AttachedToTosave() : null;

        /// attachablescript.C: the item's own colliders, a public list there, a private array here.
        private static readonly System.Reflection.FieldInfo _c = GameLookups.Field(typeof(attachablescript), "C");
        public static List<Collider> GetC(attachablescript a)
        {
            if (a == null) return null;
            var arr = _c != null && _c.FieldType == typeof(Collider[]) ? (Collider[])_c.GetValue(a) : null;
            return new List<Collider>(arr ?? a.GetComponentsInChildren<Collider>());
        }
        public static void SetC(attachablescript a, List<Collider> v) { Calls++; if (a != null && _c != null && _c.FieldType == typeof(Collider[])) _c.SetValue(a, v != null ? v.ToArray() : null); }

        /// partslotscript.part = a: the public game's slot held one part; the beta's keeps a list (part() is its first).
        public static void SetSlotPart(partslotscript s, attachablescript a)
        {
            Calls++;
            if (s == null) return;
            if (s.parts == null) s.parts = new List<attachablescript>(); else s.parts.Clear();
            if (a != null) s.AddPart(a);
        }

        // ------------------------------------------------------------------ buildings

        /// buildingscript.itemsSpawned (buildingscript is the beta's poiScript): the place has started, which is when the
        /// beta spawns its items. The Advanced Modding Toolkit puts its mods' items into a building when this turns true.
        public static bool ItemsSpawned(poiScript p) => p != null && p.started;

        /// attachablescript.Attach(Transform, RaycastHit): attached where the ray hit, as the beta's Attach(RaycastHit).
        public static void Attach(attachablescript a, Transform target, RaycastHit hit) { Calls++; if (a != null && hit.collider != null) a.Attach(hit); }
    }
}
