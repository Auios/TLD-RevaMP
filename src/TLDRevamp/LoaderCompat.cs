using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp
{
    /// What TLD Loader mods use of the public game's save and attach code that the beta changed or dropped. LoaderRetarget
    /// points the mod's own instructions here (by name: it stays free of the game's types); nothing in the game changes.
    ///
    /// The spawner many workshop mods share (issue 44: a pet mod's cat, a nitrous mod's bottle): pick the first
    /// free save id with `!savedatascript.d.toSaveStuff.ContainsKey(i) && !data.farStuff.ContainsKey(i) &&
    /// !data.nearStuff.ContainsKey(i)`, instantiate, `tosaveitemscript.FStart(i)`. On the beta those fields and the type
    /// save_prefab are gone: the whole method stopped at its first call, so nothing was made, and the spawn menu's holder
    /// (the pet mod's cat model with its collider switched off) stayed in the air where it was spawned.
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

        /// save_prefab, the public game's record of a far item, as the mods use it (LoaderRetarget points the type here): many
        /// workshop mods keep their settings and progress in one, a string in a fake plate record,
        /// `farStuff.Add(Mathf.Abs(ID.GetHashCode()), new save_prefab(plate, ...) { rendszam = new save_rendszam { S = json } })`.
        public class SaveRecord
        {
            public int categoryID, id;
            public save_rendszam rendszam;
            public save_meterscript meter;   // one author's save helper (in a car mod) keeps its numbers in one
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
                    if (plate != null && plate.id >= RecordIdBase)
                    {
                        var r = new SaveRecord(0, 0, 0, 0, 0, 0, 0, 0) { rendszam = plate };
                        int cut = plate.S != null ? plate.S.IndexOf(MeterMark) : -1;
                        if (cut >= 0)
                        {
                            r.meter = ReadMeter(plate.S.Substring(cut + 1));
                            r.rendszam = new save_rendszam { id = plate.id, S = plate.S.Substring(0, cut) };
                        }
                        map[(int)(plate.id - RecordIdBase)] = r;
                    }
            return map;
        }

        /// A record's meter goes into its plate string after this mark: its readings (mutatok), then its dials
        /// (szamlapok), each "name\tvalue" on a line, the two lists apart by an empty line.
        private const char MeterMark = '\u0001';
        private static string WriteMeter(save_meterscript m)
        {
            var b = new System.Text.StringBuilder();
            void List(save_meterscript.save_meter[] ms)
            {
                if (ms != null) foreach (var x in ms) if (x != null) b.Append(x.tipus).Append('\t').Append(x.value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            }
            List(m.mutatok); b.Append('\n'); List(m.szamlapok);
            return b.ToString();
        }
        private static save_meterscript ReadMeter(string text)
        {
            var lists = new[] { new List<save_meterscript.save_meter>(), new List<save_meterscript.save_meter>() };
            int which = 0;
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0) { if (which == 0) which = 1; continue; }
                int tab = line.LastIndexOf('\t');
                if (tab < 0) continue;
                float.TryParse(line.Substring(tab + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
                lists[which].Add(new save_meterscript.save_meter { tipus = line.Substring(0, tab), value = v });
            }
            return new save_meterscript { mutatok = lists[0].ToArray(), szamlapok = lists[1].ToArray() };
        }

        /// The records as they are now, one plate string each, in place of the ones there.
        public static void WriteRecords(Dictionary<int, SaveRecord> map, List<save_rendszam> plates)
        {
            plates.RemoveAll(p => p != null && p.id >= RecordIdBase);
            foreach (var kv in map)
                if (kv.Key >= 0 && (kv.Value?.rendszam != null || kv.Value?.meter != null))
                    plates.Add(new save_rendszam
                    {
                        id = RecordIdBase + (uint)kv.Key,
                        S = (kv.Value.rendszam?.S ?? "") + (kv.Value.meter != null ? MeterMark + WriteMeter(kv.Value.meter) : "")
                    });
        }

        /// A new save_rendszam's string is empty, as the public game's was; the beta's is null. A wheel position mod
        /// makes its record's and reads it back as its saved XML: "" meant none yet, null threw (StringReader).
        [HarmonyLib.HarmonyPatch(typeof(save_rendszam), HarmonyLib.MethodType.Constructor, new System.Type[0])]
        private static class EmptyPlateString
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(save_rendszam __instance) { if (__instance.S == null) __instance.S = ""; }
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

        /// savedatascript.properties: the save's (its date, seed, the distance driven) are the data's on the beta.
        public static saveproperties SaveProperties(savedatascript s) => s != null && s.data != null ? s.data.properties : null;

        /// tosaveitemscript.removeFromMemory: the public game's "not an item of its own" for display copies of a game prefab
        /// (a nitrous mod's gauge face is a compass inside the bottle). One never started isn't saved on the beta either, but the
        /// beta takes every item out of its parent the frame after it starts (NextFrameUnparent): the compass fell out and
        /// the gauge, its own colliders already removed, fell through the world. Kept where the mod put it.
        public static bool GetRemoveFromMemory(tosaveitemscript t) => t != null && t.dontUnparent;
        public static void SetRemoveFromMemory(tosaveitemscript t, bool value) { Calls++; if (t != null && value) t.dontUnparent = true; }

        /// GetComponent&lt;T&gt;() of a type the beta doesn't have (partscript): there is none.
        public static Component NoComponent(object owner) { Calls++; return null; }

        // ------------------------------------------------------------------ the main menu

        /// mainmenuscript.mainmenu's screens (menuhandler.s here), active while open: mods hide their own menu buttons then
        /// (three of the workshop's mods). The beta's settings and save screens switch their own object on.
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
        public static tosaveitemscript TargetTosave(attachablescript a)
        {
            if (a == null) return null;
            var t = a.AttachedToTosave();
            return t != null ? t : _loadTargets.TryGetValue(a, out var box) ? box.T : null;
        }
        /// attachablescript.targetTosave = t: what its next Load(pos) attaches it to (a tool mod's swap of an item for another).
        private sealed class TargetBox { public tosaveitemscript T; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<attachablescript, TargetBox> _loadTargets =
            new System.Runtime.CompilerServices.ConditionalWeakTable<attachablescript, TargetBox>();
        public static void SetTargetTosave(attachablescript a, tosaveitemscript t)
        {
            Calls++;
            if (a == null) return;
            _loadTargets.Remove(a);
            _loadTargets.Add(a, new TargetBox { T = t });
        }

        /// attachablescript.pointLocalPos: where it is attached, in its target's space. Detach destroys the point at the end
        /// of the frame, so it is still there for the read right after one.
        public static Vector3 PointLocalPos(attachablescript a)
        {
            if (a == null || a.point == null) return Vector3.zero;
            var t = a.point.GetComponentInParent<tosaveitemscript>();
            return t != null ? t.transform.InverseTransformPoint(a.point.position) : a.point.localPosition;
        }

        /// attachablescript.Load(localPos): attached to its target (targetTosave) at that place, as a save load did.
        public static void AttachableLoad(attachablescript a, Vector3 localPos)
        {
            Calls++;
            var t = TargetTosave(a);
            if (t == null) return;
            a.Attach(a.NewPoint(t.transform, t.transform.TransformPoint(localPos), a.transform.rotation));
        }

        /// tosaveitemscript.Claim(b): the public game's multiplayer claim on an item; the beta has none (a tool mod claims
        /// the part it swapped in). Nothing to do.
        public static void Claim(tosaveitemscript t, bool b) { Calls++; }

        /// mountStuff.Lock(item, ignoreRigidbody): the beta's Store (a modding toolkit's saved items put back in their mounts).
        public static void MountLock(mountStuff m, pickupable p, bool ignoreRigidbody) { Calls++; if (m != null) m.Store(p, ignoreRigidbody); }

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
        /// beta's cap turns by its usable between closedRot and openRot. Engine mods (ten in the workshop) set
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

        // ------------------------------------------------------------------ the buildings along the road

        /// roadBuildingScript, the public game's generator of the buildings along the road
        /// (TerrainGenerationSettings.roadBuildingGeneration): the beta places them as points of interest, each a root of its
        /// own. Made without its native half, it equals null in Unity's sense, so mods that test it first skip it (two
        /// driving mods); parent is null (a tool mod places a building at the root, as the beta does, and an AI traffic
        /// mod finds no building under it); placedBuildings is the current map's placed points of interest (a sandstorm
        /// mod starts its storms at one).
        public class RoadBuildings : MonoBehaviour
        {
            public Transform parent;
            public Dictionary<int, GameObject> placedBuildings;
        }

        private static RoadBuildings _roadBuildings;
        private static float _roadBuildingsAt = float.MinValue;
        public static RoadBuildings RoadBuildingGeneration(mapSettingScript m)
        {
            Calls++;
            if ((object)_roadBuildings == null)
            {
                _roadBuildings = (RoadBuildings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(RoadBuildings));
                _roadBuildings.placedBuildings = new Dictionary<int, GameObject>();
            }
            // an AI traffic mod asks per car while it drives: the list is made again at most once a second
            if (Time.unscaledTime - _roadBuildingsAt < 1f) return _roadBuildings;
            _roadBuildingsAt = Time.unscaledTime;
            var placed = _roadBuildings.placedBuildings;
            placed.Clear();
            if (m != null && m.poiGens != null)
                foreach (var g in m.poiGens)
                    if (g != null && g.chunks != null)
                        foreach (var c in g.chunks.Values)
                            if (c != null && c.pois != null)
                                foreach (var poi in c.pois.Values)
                                    if (poi != null && poi.pobj != null && poi.pobj.gameObject.activeInHierarchy) placed[placed.Count] = poi.pobj.gameObject;
            return _roadBuildings;
        }

        // ------------------------------------------------------------------ what the beta dropped

        /// A game type the beta has no counterpart for (digholescript2, flashlightscript): nothing in the world has
        /// one, so finding one finds none.
        public class Gone : MonoBehaviour { }

        /// inputscript.forward: a float on the beta, a bool on the public game.
        public static bool InputForward(inputscript i) => i != null && i.forward > 0f;

        /// Type.GetMethod(name) and (name, flags) where the beta's overloads make the name ambiguous (LoaderRetarget.LooseMethod).
        public static System.Reflection.MethodInfo GetMethod(System.Type t, string name) =>
            LoaderRetarget.LooseMethod(t, name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
        public static System.Reflection.MethodInfo GetMethodFlags(System.Type t, string name, System.Reflection.BindingFlags flags) =>
            LoaderRetarget.LooseMethod(t, name, flags);
        /// MethodBase.Invoke(obj, args), with the optional parameters the args leave out at their defaults.
        public static object Invoke(System.Reflection.MethodBase m, object obj, object[] args)
        {
            if (m != null && obj != null && m.Name == "PatchAll" && m.DeclaringType?.Name == "Harmony" && args?.Length == 1 && args[0] is System.Reflection.Assembly a &&
                PatchEachClass(obj, a)) return null;
            return LoaderRetarget.InvokeLoose(m, obj, args);
        }

        /// HarmonyLib.AccessTools.TypeByName in a mod: the same answer, remembered; a miss until another assembly loads. Road
        /// Save Guard looks for seven other mods every frame: each miss searched every loaded type and HarmonyX logged a
        /// warning for it, and the game slowed until the test car stood still.
        public static System.Type TypeByName(string name)
        {
            if (name == null) return HarmonyLib.AccessTools.TypeByName(name);
            lock (_typesByName)
            {
                if (!_typesWatched) { _typesWatched = true; System.AppDomain.CurrentDomain.AssemblyLoad += (s, e) => { lock (_typesByName) _typesByName.Clear(); }; }
                if (_typesByName.TryGetValue(name, out var known)) return known;
            }
            var t = HarmonyLib.AccessTools.TypeByName(name);
            lock (_typesByName) _typesByName[name] = t;
            return t;
        }
        private static readonly Dictionary<string, System.Type> _typesByName = new Dictionary<string, System.Type>();
        private static bool _typesWatched;

        /// Harmony's PatchAll(assembly) called by reflection (a mod with its own copy of Harmony): each patch class on
        /// its own, as PatchAll does, but one whose target the beta dropped (mainscript.PauseRBs, setcenterofmass.Pause)
        /// is left out. PatchAll stopped there: the classes after it (that mod's wheels, sandstorm, pouring) never applied.
        public static int PatchClassesSkipped;
        private static bool PatchEachClass(object harmony, System.Reflection.Assembly a)
        {
            var create = harmony.GetType().GetMethod("CreateClassProcessor", new[] { typeof(System.Type) });
            if (create == null) return false;
            Calls++;
            System.Type[] types;
            try { types = a.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = System.Array.FindAll(e.Types, t => t != null); }
            var skipped = new List<string>();
            foreach (var t in types)
            {
                try
                {
                    var processor = create.Invoke(harmony, new object[] { t });
                    processor?.GetType().GetMethod("Patch", System.Type.EmptyTypes)?.Invoke(processor, null);
                }
                catch (System.Exception e) { skipped.Add(t.Name + " (" + ((e.InnerException ?? e).InnerException ?? e.InnerException ?? e).Message + ")"); }
            }
            if (skipped.Count > 0)
            {
                PatchClassesSkipped += skipped.Count;
                Plugin.Log.LogWarning("ModHost: " + a.GetName().Name + ": " + skipped.Count + " of its Harmony patch classes left out, their targets not on the beta: " + string.Join("; ", skipped));
            }
            return true;
        }

        /// A tumbleweed mod destroys the tumbleweed spawners (thumbleweedMain) it finds, as on the public game, where nothing
        /// else held them. The beta's map keeps them in a list and updates each every frame: the destroyed one threw there,
        /// the rest of the map's Update never ran and the world never finished loading. Destroyed ones leave the list.
        [HarmonyLib.HarmonyPatch(typeof(mapSettingScript), "Update")]
        private static class DropDestroyedTumbleweeds
        {
            [HarmonyLib.HarmonyPrefix]
            internal static void Prefix(mapSettingScript __instance)
            {
                var list = __instance.thumbleweeds;
                if (list == null) return;
                for (int i = list.Count - 1; i >= 0; i--) if (list[i] == null) list.RemoveAt(i);
            }
        }
        /// RestartMap numbers them first, at world load: the destroyed one threw there, before the sky was set up.
        [HarmonyLib.HarmonyPatch(typeof(mapSettingScript), nameof(mapSettingScript.RestartMap))]
        private static class DropDestroyedTumbleweedsAtRestart
        {
            [HarmonyLib.HarmonyPrefix]
            private static void Prefix(mapSettingScript __instance) => DropDestroyedTumbleweeds.Prefix(__instance);
        }

        /// A copy of a tank cap that had started (a helicopter mod clones the car06 cap it made in DbLoad) has the beta's
        /// started flag set, its particle module not (Unity copies the one, not the other): FStart skipped, the cap's Update
        /// threw every frame ("Do not create your own module instances", x2807 in one run). Such a copy starts as its own.
        /// A/B: false = before 2026-11-24.
        public static bool RestartCopiedTankCaps = true;
        public static int TankCapsRestarted;
        [HarmonyLib.HarmonyPatch(typeof(tankcapscript), "Start")]
        private static class RestartCopiedTankCap
        {
            private static readonly System.Reflection.FieldInfo Owner = HarmonyLib.AccessTools.Field(typeof(ParticleSystem.CollisionModule), "m_ParticleSystem");
            [HarmonyLib.HarmonyPrefix]
            private static void Prefix(tankcapscript __instance)
            {
                if (!RestartCopiedTankCaps || !__instance.started || __instance.ps == null || Owner == null) return;
                if (ReferenceEquals(Owner.GetValue(__instance.psc), __instance.ps)) return;   // started here (FStart before Start)
                __instance.started = false;
                TankCapsRestarted++;
            }
        }

        // ------------------------------------------------------------------ buildings

        /// buildingscript.itemsSpawned (buildingscript is the beta's poiScript): the place has started, which is when the
        /// beta spawns its items. A modding toolkit puts its mods' items into a building when this turns true.
        public static bool ItemsSpawned(poiScript p) => p != null && p.started;

        /// attachablescript.Attach(Transform, RaycastHit): attached where the ray hit, as the beta's Attach(RaycastHit).
        public static void Attach(attachablescript a, Transform target, RaycastHit hit) { Calls++; if (a != null && hit.collider != null) a.Attach(hit); }
    }
}
