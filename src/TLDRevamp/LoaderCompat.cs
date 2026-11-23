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

        /// save_prefab, the public game's record of a far item, as the mods use it (LoaderRetarget points the type here): the
        /// M_ mods and RUNDEN's keep their settings and progress in one, a string in a fake plate record,
        /// `farStuff.Add(Mathf.Abs(ID.GetHashCode()), new save_prefab(plate, ...) { rendszam = new save_rendszam { S = json } })`.
        public class SaveRecord
        {
            public int categoryID, id;
            public save_rendszam rendszam;
            public SaveRecord(int category, int id, double x, double y, double z, float rx, float ry, float rz) { categoryID = category; this.id = id; }
        }

        /// The beta's save has its plate strings by item id (itemData.rendszam); a mod's record is one of them under this
        /// base + its key. Item ids count up from 0, and the beta renumbers only the ids it knows (a construction's).
        public const uint RecordIdBase = 0x80000000u;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<savedata, Dictionary<int, SaveRecord>> _records =
            new System.Runtime.CompilerServices.ConditionalWeakTable<savedata, Dictionary<int, SaveRecord>>();

        /// savedata.nearStuff / farStuff: the mods' own records of this save (both: a mod reads either, then adds to far).
        /// The spawner's ContainsKey on them comes to RecordIdUsed.
        public static Dictionary<int, SaveRecord> SavedRecords(savedata d)
        {
            Calls++;
            if (d == null) return new Dictionary<int, SaveRecord>();
            if (_records.TryGetValue(d, out var map)) return map;
            map = ReadRecords(d.itemData?.rendszam);
            _records.Add(d, map);
            return map;
        }

        public static Dictionary<int, SaveRecord> ReadRecords(List<save_rendszam> plates)
        {
            var map = new Dictionary<int, SaveRecord>();
            if (plates != null)
                foreach (var plate in plates)
                    if (plate != null && plate.id >= RecordIdBase) map[(int)(plate.id - RecordIdBase)] = new SaveRecord(0, 0, 0, 0, 0, 0, 0, 0) { rendszam = plate };
            return map;
        }

        /// The records as they are now, one plate string each, in place of the ones there.
        public static void WriteRecords(Dictionary<int, SaveRecord> map, List<save_rendszam> plates)
        {
            plates.RemoveAll(p => p != null && p.id >= RecordIdBase);
            foreach (var kv in map)
                if (kv.Key >= 0 && kv.Value?.rendszam != null)
                    plates.Add(new save_rendszam { id = RecordIdBase + (uint)kv.Key, S = kv.Value.rendszam.S });
        }

        /// Into the save being written (a copy of savedatascript.s.data, made before the mods' last changes reach it).
        [HarmonyLib.HarmonyPatch(typeof(savedatascript), nameof(savedatascript.SaveToData))]
        private static class KeepRecords
        {
            [HarmonyLib.HarmonyPrefix]
            private static void Prefix(savedata _data)
            {
                var from = savedatascript.s != null ? savedatascript.s.data : null;
                if (from == null || _data?.itemData?.rendszam == null || !_records.TryGetValue(from, out var map) || map.Count == 0) return;
                try { WriteRecords(map, _data.itemData.rendszam); }
                catch (System.Exception e) { Diagnostics.QuietErrors.Once("LoaderCompat.KeepRecords", e); }
            }
        }

        /// Any record or placed item with this id: the beta keeps far items as records in data.itemData.items.
        public static bool RecordIdUsed(object records, int id)
        {
            Calls++;
            if (records is Dictionary<int, SaveRecord> own && own.ContainsKey(id)) return true;
            var s = savedatascript.s;
            if (s == null) return false;
            uint key = unchecked((uint)id);
            if (s.items != null && s.items.ContainsKey(key)) return true;
            var data = s.data;
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

        // ------------------------------------------------------------------ the main menu

        /// mainmenuscript.mainmenu's screens (menuhandler.s here), active while open: mods hide their own menu buttons then
        /// (Achievements, M-ultiTool, World Tweaker). The beta's settings and save screens switch their own object on.
        public static GameObject MenuSettingsScreen(menuhandler m) { Calls++; return settingsscript.s != null ? settingsscript.s.gameObject : null; }
        public static GameObject MenuSaveScreen(menuhandler m)
        {
            Calls++;
            var save = m != null && m.saveScreen != null ? m.saveScreen : savedatascript.s != null ? savedatascript.s.saveScreen : null;
            return save != null ? save.gameObject : null;
        }
        public static Transform MenuCanvas(menuhandler m) { Calls++; return m != null && m.canv != null ? m.canv.transform : null; }

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

        // ------------------------------------------------------------------ fields the beta dropped

        /// A field of the public game the beta has no counterpart for (LoaderRetarget's last resort): what the mod stored,
        /// per object (static fields: owner null), so the methods that name it run instead of stopping at their first line.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<string, object>> _kept =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<string, object>>();
        private static readonly Dictionary<string, object> _keptStatic = new Dictionary<string, object>();
        public static T Kept<T>(object owner, string field)
        {
            var d = owner == null ? _keptStatic : _kept.TryGetValue(owner, out var x) ? x : null;
            return d != null && d.TryGetValue(field, out var v) && v is T t ? t : default(T);
        }
        public static void Keep<T>(object owner, string field, T value)
        {
            Calls++;
            (owner == null ? _keptStatic : _kept.GetValue(owner, _ => new Dictionary<string, object>()))[field] = value;
        }

        /// attachablescript.Refresh(): the public game refreshed the attach point's save data; the beta keeps none there.
        public static void RefreshAttachable(attachablescript a) { Calls++; }

        /// usablescript.Refresh(slide, xRot, yRot): the beta's Refresh also takes the turn state; the usable keeps its own.
        public static void RefreshUsable(usablescript u, float slide, float xRot, float yRot) { Calls++; u.Refresh(slide, xRot, yRot, u.currentTurnState); }

        /// layerScript.FStart(): the beta's takes the item and the collider's place in its list (nothing reads that index).
        public static void LayerFStart(layerScript l) { Calls++; if (l != null) l.FStart(l.p != null ? l.p : l.GetComponentInParent<pickupable>(), 0); }

        /// Harmony.PatchAll() in the mod's own code: as the mod asked, but a patch that fails on the beta undoes the ones
        /// applied before it (all or none, as ModHost's UseHarmony), and the mod's code goes on after the call.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void PatchAll(HarmonyLib.Harmony h) => PatchAllFrom(h, new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().ReflectedType.Assembly);
        public static void PatchAllFrom(HarmonyLib.Harmony h, System.Reflection.Assembly a)
        {
            Calls++;
            try { h.PatchAll(a); }
            catch (System.Exception e)
            {
                try { h.UnpatchSelf(); } catch (System.Exception quietError) { Diagnostics.QuietErrors.Once("LoaderCompat.PatchAll", quietError); }
                PatchAllRefused++;
                Plugin.Log.LogWarning("ModHost: " + a.GetName().Name + ": its Harmony patches can't all apply on the beta, none applied: " + (e.InnerException ?? e).Message);
            }
        }
        public static int PatchAllRefused;

        /// GameObject.Find("Main"): the public game's object with mainscript on it. The beta's has another name, and an
        /// object named Main is something else; in the menu (no mainscript) the name is looked up as the mod asked.
        public static GameObject FindMain(string name) { Calls++; return mainscript.s != null ? mainscript.s.gameObject : GameObject.Find(name); }

        /// Assembly.Location: for a rewritten mod (loaded from bytes, no location) the DLL it came from.
        public static string AssemblyLocation(System.Reflection.Assembly a)
        {
            if (a == null) return null;
            lock (LoaderRetarget.Origins) if (LoaderRetarget.Origins.TryGetValue(a, out var p)) return p;
            return a.Location;
        }

        // ------------------------------------------------------------------ fuel caps

        /// tankcapscript.TCap/TOpen/TClosed and UpdCap(bool): the public game moved the cap between two transforms; the
        /// beta's cap turns by its usable between closedRot and openRot. Engine mods (DIESEL_V8ENGINE and nine more) set
        /// these in the middle of the method that builds the whole engine, which stopped at them: kept here for the mod to
        /// read back, the beta's own cap left as it is.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<tankcapscript, Transform[]> _caps =
            new System.Runtime.CompilerServices.ConditionalWeakTable<tankcapscript, Transform[]>();
        private static Transform CapGet(tankcapscript c, int i) => c != null && _caps.TryGetValue(c, out var t) ? t[i] : null;
        private static void CapSet(tankcapscript c, int i, Transform v) { Calls++; if (c != null) _caps.GetValue(c, _ => new Transform[3])[i] = v; }
        public static Transform GetCapT(tankcapscript c) => CapGet(c, 0) ?? (c != null ? c.transform : null);
        public static void SetCapT(tankcapscript c, Transform v) => CapSet(c, 0, v);
        public static Transform GetCapOpen(tankcapscript c) => CapGet(c, 1);
        public static void SetCapOpen(tankcapscript c, Transform v) => CapSet(c, 1, v);
        public static Transform GetCapClosed(tankcapscript c) => CapGet(c, 2);
        public static void SetCapClosed(tankcapscript c, Transform v) => CapSet(c, 2, v);
        public static void UpdCap(tankcapscript c, bool sound) { Calls++; }
        // GOpen/GClosed: what the public game showed open or closed (lists of objects), kept the same way
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<tankcapscript, List<GameObject>[]> _capObjs =
            new System.Runtime.CompilerServices.ConditionalWeakTable<tankcapscript, List<GameObject>[]>();
        private static List<GameObject> CapObjs(tankcapscript c, int i) => c == null ? new List<GameObject>() : _capObjs.GetValue(c, _ => new[] { new List<GameObject>(), new List<GameObject>() })[i];
        public static List<GameObject> GetCapGOpen(tankcapscript c) => CapObjs(c, 0);
        public static void SetCapGOpen(tankcapscript c, List<GameObject> v) { Calls++; if (c != null) _capObjs.GetValue(c, _ => new[] { new List<GameObject>(), new List<GameObject>() })[0] = v ?? new List<GameObject>(); }
        public static List<GameObject> GetCapGClosed(tankcapscript c) => CapObjs(c, 1);
        public static void SetCapGClosed(tankcapscript c, List<GameObject> v) { Calls++; if (c != null) _capObjs.GetValue(c, _ => new[] { new List<GameObject>(), new List<GameObject>() })[1] = v ?? new List<GameObject>(); }

        /// pickupable.slots: the item's part slots, the beta's tosaveitemscript list (the same list).
        public static List<partslotscript> PickupSlots(pickupable p) { var t = SaveOf(p); return t != null ? t.partslotscripts : null; }

        // ------------------------------------------------------------------ cars

        /// carscript.driverSeat: the beta marks the seat itself (driverSeat0).
        public static seatscript DriverSeat(carscript c)
        {
            Calls++;
            var seats = c != null && c.tosave != null ? c.tosave.seats : null;
            if (seats != null) foreach (var s in seats) if (s != null && s.driverSeat0) return s;
            return null;
        }

        /// carscript.ISwitches: the car's switches, the usables the beta lists on its tosaveitemscript (a copy).
        public static usablescript[] Switches(carscript c) => c != null && c.tosave != null && c.tosave.usables != null ? c.tosave.usables.ToArray() : null;

        // ------------------------------------------------------------------ the world generator

        /// TerrainGenerator.TG.manager.settings.objGeneration: on the beta each step is the current map.
        public static mapSettingScript Map(mapSettingScript m) => m;

        /// ObjectGenerationScript.objTypes: every prefab the current map's object generators scatter, in their order (the
        /// same prefab objects: a mod's change to one is the game's).
        public static objGenScript.prefabClass[] ObjTypes(mapSettingScript m)
        {
            Calls++;
            var all = new List<objGenScript.prefabClass>();
            if (m != null && m.objGens != null)
                foreach (var g in m.objGens)
                    if (g != null && g.categories != null)
                        foreach (var c in g.categories)
                            if (c != null && c.prefabs != null) foreach (var p in c.prefabs) if (p != null && p.prefab != null) all.Add(p);
            return all.ToArray();
        }

        // ------------------------------------------------------------------ buildings

        /// buildingscript.itemsSpawned (buildingscript is the beta's poiScript): the place has started, which is when the
        /// beta spawns its items. The Advanced Modding Toolkit puts its mods' items into a building when this turns true.
        public static bool ItemsSpawned(poiScript p) => p != null && p.started;

        /// attachablescript.Attach(Transform, RaycastHit): attached where the ray hit, as the beta's Attach(RaycastHit).
        public static void Attach(attachablescript a, Transform target, RaycastHit hit) { Calls++; if (a != null && hit.collider != null) a.Attach(hit); }
    }
}
