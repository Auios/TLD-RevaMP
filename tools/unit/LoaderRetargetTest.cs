using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TLDRevamp;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;

// A made-up game (neutral names: the repository holds no game code). The "public game" a mod was built against has an
// int key; this one has a uint. Its float, which the mod reads as a double, is a real mismatch and must stay one.
public class Saved { public uint key; public float size; }

/// The REAL src/TLDRevamp/LoaderRetarget.cs on a mod written with Mono.Cecil against the public shape of the made-up
/// game above. Before: calling the mod's method throws MissingFieldException, as a workshop mod reading idInSave does
/// on the beta. After Apply: the same method runs and reads the right value. A mismatch that is not just the sign,
/// and a mod that needs nothing, are left alone.
static class LoaderRetargetTest
{
    static int failures;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }

    static ModuleDefinition MakeMod(string name)
    {
        var mod = ModuleDefinition.CreateModule(name, ModuleKind.Dll);
        var game = typeof(Saved).Assembly.GetName();
        var scope = new AssemblyNameReference(game.Name, game.Version);
        mod.AssemblyReferences.Add(scope);
        var saved = new TypeReference("", "Saved", mod, scope, false);
        var code = new TypeDefinition("Fake", "ModCode", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, mod.TypeSystem.Object);
        mod.Types.Add(code);

        var read = new MethodDefinition("Read", MethodAttributes.Public | MethodAttributes.Static, mod.TypeSystem.Int32);
        read.Parameters.Add(new ParameterDefinition(saved));
        var il = read.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, new FieldReference("key", mod.TypeSystem.Int32, saved));   // int: the public game's
        il.Emit(OpCodes.Ret);
        code.Methods.Add(read);

        var size = new MethodDefinition("Size", MethodAttributes.Public | MethodAttributes.Static, mod.TypeSystem.Double);
        size.Parameters.Add(new ParameterDefinition(saved));
        il = size.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, new FieldReference("size", mod.TypeSystem.Double, saved));   // not a sign difference
        il.Emit(OpCodes.Ret);
        code.Methods.Add(size);
        return mod;
    }

    // as a real mod: read from its bytes (Cecil lists references only for a module it read)
    static ModuleDefinition Reread(ModuleDefinition mod) { var ms = new MemoryStream(); mod.Write(ms); ms.Position = 0; return ModuleDefinition.ReadModule(ms, new ReaderParameters { ReadingMode = ReadingMode.Immediate }); }

    static Assembly LoadBytes(ModuleDefinition mod) { var ms = new MemoryStream(); mod.Write(ms); return Assembly.Load(ms.ToArray()); }

    static bool Throws(Func<object> f) { try { f(); return false; } catch (TargetInvocationException) { return true; } catch (MissingMemberException) { return true; } }

    static object Call(Assembly a, string m, Saved s) => a.GetType("Fake.ModCode").GetMethod(m).Invoke(null, new object[] { s });

    static int Main()
    {
        Aliases();
        Compat();
        Registry();
        Parts();
        Generator();
        var gameAsm = typeof(Saved).Assembly;
        var sample = new Saved { key = 7, size = 1.5f };

        var before = LoadBytes(MakeMod("FakeModBefore"));
        Check("before: reading the int field fails", Throws(() => Call(before, "Read", sample)));

        var mod = Reread(MakeMod("FakeModAfter"));
        var changes = LoaderRetarget.Apply(mod, gameAsm);
        foreach (var c in changes) Console.WriteLine("  " + c);
        Check("one change: the int field is a uint now", changes.Count == 1 && changes[0] == "Saved.key Int32 -> UInt32");
        var after = LoadBytes(mod);
        Check("after: the field reads 7", (int)Call(after, "Read", sample) == 7);
        Check("a float read as a double stays missing", Throws(() => Call(after, "Size", sample)));
        Check("Apply again: no changes", LoaderRetarget.Apply(mod, gameAsm).Count == 0);

        string tmp = Path.Combine(Path.GetTempPath(), "retarget-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            // a mod that needs nothing is loaded from its file, as before
            var plain = ModuleDefinition.CreateModule("FakePlain", ModuleKind.Dll);
            plain.Types.Add(new TypeDefinition("Fake", "Plain", TypeAttributes.Public, plain.TypeSystem.Object));
            string plainPath = Path.Combine(tmp, "FakePlain.dll");
            plain.Write(plainPath);
            string logged = null;
            var asm = LoaderRetarget.Load(plainPath, gameAsm, new[] { tmp }, m => logged = m);
            Check("Load: a mod with nothing to change keeps its file", !string.IsNullOrEmpty(asm.Location) && logged == null);

            string modPath = Path.Combine(tmp, "FakeModFile.dll");
            MakeMod("FakeModFile").Write(modPath);
            asm = LoaderRetarget.Load(modPath, gameAsm, new[] { tmp }, m => logged = m);
            Check("Load: a mod that needed a change comes from bytes and logs it", string.IsNullOrEmpty(asm.Location) && logged != null && logged.Contains("Saved.key Int32 -> UInt32"));
            Check("Load: and runs", (int)Call(asm, "Read", new Saved { key = 9 }) == 9);

            File.WriteAllText(Path.Combine(tmp, "Broken.dll"), "not a dll");
            logged = null;
            bool threw = false;
            try { LoaderRetarget.Load(Path.Combine(tmp, "Broken.dll"), gameAsm, new[] { tmp }, m => logged = m); } catch (BadImageFormatException) { threw = true; }
            Check("Load: an unreadable file falls back to LoadFrom (its own error) and says so", threw && logged != null && logged.Contains("loaded unchanged"));
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }

        Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }

    /// The old save ids through a helper type (as TLDRevamp.LoaderCompat; TestCompat below): the spawner finds the first
    /// free id among the placed items and the records, and starts the item with it. Without the helpers the method stops
    /// at its first instruction, as the workshop mods' spawners did on the beta (issue 44).
    static void Compat()
    {
        string dll = Path.Combine(Path.GetDirectoryName(typeof(LoaderRetargetTest).Assembly.Location), "AliasMod.dll");
        var s = new savedatascript(); s.items[0] = new tosaveitemscript(); s.items[1] = new tosaveitemscript();
        TestCompat.Used.Clear(); TestCompat.Used.Add(2); TestCompat.Removed = false;
        using (var mod = ModuleDefinition.ReadModule(dll))
        {
            LoaderRetarget.Apply(mod, typeof(mainscript).Assembly);
            var t = new tosaveitemscript();
            bool threw = false;
            try { LoadBytes(mod).GetType("AliasMod").GetMethod("Spawn").Invoke(null, new object[] { s, t }); }
            catch (TargetInvocationException e) { threw = e.InnerException is MissingFieldException || e.InnerException is TypeLoadException; }
            Check("compat: without the helpers the spawner stops (before)", threw);
            threw = false;
            try { LoadBytes(mod).GetType("AliasMod").GetMethod("Holes").Invoke(null, new object[] { new object[0] }); }
            catch (TargetInvocationException e) { threw = e.InnerException is TypeLoadException; }
            Check("compat: without the helpers a method naming a dropped type stops (before: a car mod's dbLoad)", threw);
            threw = false;
            try { LoadBytes(mod).GetType("AliasMod").GetMethod("Ambiguous").Invoke(null, null); }
            catch (TargetInvocationException e) { threw = e.InnerException is AmbiguousMatchException; }
            Check("compat: without the helpers an ambiguous GetMethod throws (before)", threw);
        }
        using (var mod = ModuleDefinition.ReadModule(dll))
        {
            var changes = LoaderRetarget.Apply(mod, typeof(mainscript).Assembly, typeof(TestCompat));
            Check("compat: the old save members are listed", changes.Exists(c => c.Contains("toSaveStuff read -> TestCompat.ToSaveStuff")) &&
                  changes.Exists(c => c.Contains("ContainsKey -> TestCompat.RecordIdUsed")) && changes.Exists(c => c.Contains("FStart(System.Int32) -> TestCompat.FStart")) &&
                  changes.Exists(c => c.Contains("removeFromMemory write -> TestCompat.SetRemoveFromMemory")));
            var t = new tosaveitemscript();
            int got = (int)LoadBytes(mod).GetType("AliasMod").GetMethod("Spawn").Invoke(null, new object[] { s, t });
            Check("compat: the first id neither an item (0, 1) nor a record (2) has: 3", got == 3);
            Check("compat: FStart(int) starts the item with that id", t.Started == 3);
            Check("compat: removeFromMemory reaches the helper", TestCompat.Removed);
            Check("compat: a method's return of the other sign (GetNewKey: 5)", changes.Contains("savedatascript.GetNewKey() Int32 -> UInt32") &&
                  (int)LoadBytes(mod).GetType("AliasMod").GetMethod("NewKey").Invoke(null, new object[] { new savedatascript() }) == 5);
            Check("compat: Assembly.Location asks the helper (a rewritten mod has none)", changes.Contains("Assembly.Location -> the mod's own DLL") &&
                  (string)LoadBytes(mod).GetType("AliasMod").GetMethod("Where").Invoke(null, null) == "the mod's own DLL");
            Check("compat: a field the beta dropped is kept for the mod (tosaveitemscript.category: 7)",
                  changes.Contains("tosaveitemscript.category kept for the mod (the beta has none)") &&
                  (int)LoadBytes(mod).GetType("AliasMod").GetMethod("Category").Invoke(null, new object[] { new tosaveitemscript() }) == 7);
            var ts = new tosaveitemscript();
            Check("compat: a field the beta dropped with its type is kept as an object (a mod's interiorLights)",
                  changes.Contains("tosaveitemscript.holes kept for the mod (the beta has none, nor its type)") &&
                  (int)LoadBytes(mod).GetType("AliasMod").GetMethod("Lights").Invoke(null, new object[] { ts }) == 1);
            Check("compat: an array the beta keeps as a list (tosaveitemscript.randomScales)",
                  (int)LoadBytes(mod).GetType("AliasMod").GetMethod("Scales").Invoke(null, new object[] { ts }) == 2 && ts.randomScales.Count == 2);
            {
                // a mod's AccessTools.TypeByName (one mod asks every frame for mods that aren't there)
                var m = ModuleDefinition.CreateModule("TypeByNameMod", ModuleKind.Dll);
                var harmony = new AssemblyNameReference("0Harmony", new Version(2, 0)); m.AssemblyReferences.Add(harmony);
                var tbn = new MethodReference("TypeByName", m.ImportReference(typeof(Type)), new TypeReference("HarmonyLib", "AccessTools", m, harmony));
                tbn.Parameters.Add(new ParameterDefinition(m.TypeSystem.String));
                var holder = new TypeDefinition("", "Finder", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, m.TypeSystem.Object); m.Types.Add(holder);
                var find = new MethodDefinition("Find", MethodAttributes.Public | MethodAttributes.Static, m.ImportReference(typeof(Type))); holder.Methods.Add(find);
                var fil = find.Body.GetILProcessor(); fil.Emit(OpCodes.Ldstr, "System.String"); fil.Emit(OpCodes.Call, tbn); fil.Emit(OpCodes.Ret);
                var tc = LoaderRetarget.Apply(m, typeof(mainscript).Assembly, typeof(TestCompat));
                Check("compat: AccessTools.TypeByName asks the helper (remembered there)", tc.Contains("AccessTools.TypeByName -> TestCompat.TypeByName") &&
                      (Type)LoadBytes(m).GetType("Finder").GetMethod("Find").Invoke(null, null) == typeof(string) && TestCompat.TypeByNameCalls == 1);
            }
            var asm = LoadBytes(mod).GetType("AliasMod");
            Check("compat: GameObject.Find(\"Main\") gets mainscript's object", changes.Contains("GameObject.Find(\"Main\") -> TestCompat.FindMain") &&
                  ((UnityEngine.GameObject)asm.GetMethod("Main").Invoke(null, null)).Name == "the main object");
            var u = new usablescript { currentTurnState = 4 };
            asm.GetMethod("Refresh").Invoke(null, new object[] { u });
            Check("compat: usablescript.Refresh(slide, x, y) passes the usable's turn state (1+2+3+4)", u.Got == 10f);
            var l = new layerScript();
            asm.GetMethod("Layer").Invoke(null, new object[] { l });
            Check("compat: layerScript.FStart() starts it with its item", l.index == 0 && (string)l.p == "the item");
            TestCompat.Records.Clear();
            var s2 = new savedatascript();
            string first = (string)asm.GetMethod("Record").Invoke(null, new object[] { s2, "{\"rads\":3}" });
            string again = (string)asm.GetMethod("Record").Invoke(null, new object[] { s2, null });
            Check("compat: save_prefab is the helper's SaveRecord", changes.Contains("type save_prefab -> TestCompat.SaveRecord"));
            Check("compat: a mod's own record (a save template several mods share) is made once and read back",
                  first == "{\"rads\":3}" && again == first && TestCompat.Records.Count == 1 && TestCompat.Records[12345].categoryID == 3);
            menuhandler.s = new menuhandler();
            Check("compat: the main menu is menuhandler.s, its screens from the helper (mainmenuscript.mainmenu.SettingsScreenObj)",
                  changes.Contains("type mainmenuscript -> menuhandler") && changes.Contains("menuhandler.mainmenu -> menuhandler.s") &&
                  (string)asm.GetMethod("MenuScreens").Invoke(null, null) == "settings,save");
            var ufo = LoadBytes(mod).GetType("UfoMod+Sandstorms");
            Check("compat: a mod's field of a dropped game type is an object, and its class loads (a UFO mod)",
                  changes.Contains("UfoMod/Sandstorms.Temp temporaryTurnOffGeneration -> Object (the beta has no temporaryTurnOffGeneration)") &&
                  ufo.GetField("Temp").FieldType == typeof(object) && ((System.Type[])LoadBytes(mod).GetType("UfoMod").GetField("Types").GetValue(null)).Length == 1 &&
                  (int)ufo.GetField("Speed").GetValue(System.Activator.CreateInstance(ufo)) == 3);
            menuhandler.s.currentMainMap = new mapSettingScript();
            Check("compat: the road buildings are the helper's stand-in: no parent, the placed buildings (an AI traffic mod, a sandstorm mod)",
                  changes.Contains("type roadBuildingScript -> TestCompat.RoadBuildings") &&
                  changes.Contains("mapSettingScript.roadBuildingGeneration read -> TestCompat.RoadBuildingGeneration") &&
                  (string)asm.GetMethod("RoadBuildings").Invoke(null, new object[] { new UnityEngine.Transform() }) == "False,2" && TestCompat.RoadBuildingsMap == menuhandler.s.currentMainMap);
            Check("compat: another dropped game type is the helper's Gone, and a method naming it runs (a mod's wells: none found)",
                  changes.Contains("type digholescript2 -> TestCompat.Gone (the beta has none)") &&
                  (int)asm.GetMethod("Holes").Invoke(null, new object[] { new object[] { new object(), "x" } }) == 1);
            Check("compat: inputscript.forward as a bool asks the helper (the beta's is a float)",
                  changes.Contains("inputscript.forward read -> TestCompat.InputForward") &&
                  (bool)asm.GetMethod("Forward").Invoke(null, new object[] { new inputscript { forward = 0.5f } }) &&
                  !(bool)asm.GetMethod("Forward").Invoke(null, new object[] { new inputscript() }));
            Check("compat: an ambiguous GetMethod gets the fewest parameters, a string first (the real LooseMethod)",
                  changes.Contains("Type.GetMethod -> TestCompat.GetMethod") && (string)asm.GetMethod("Ambiguous").Invoke(null, null) == "String,Boolean");
            Check("compat: Invoke with the optional parameter left out gets its default (an AI traffic mod's slider text)",
                  changes.Contains("MethodBase.Invoke -> TestCompat.Invoke") && (string)asm.GetMethod("Invoked").Invoke(null, null) == "AITrue");
            var save = new savedatascript(); save.data.properties.driven = 5001f;
            Check("compat: savedatascript.properties is the save data's (an achievements mod's distance driven)",
                  changes.Contains("savedatascript.properties read -> TestCompat.SaveProperties") && (float)asm.GetMethod("Driven").Invoke(null, new object[] { save }) == 5001f);
            var mount = new mountStuff(); var item = new pickupable();
            asm.GetMethod("Remount").Invoke(null, new object[] { mount, item });
            Check("compat: mountStuff.Lock is the beta's Store (a modding toolkit's saved items in their mounts)",
                  changes.Contains("mountStuff.Lock(pickupable,System.Boolean) -> TestCompat.MountLock") && mount.Stored == item && mount.IgnoredRigidbody);
            var building = new poiScript { poigenid = 7 }; var copy = new poiScript(); var ground = new terrainHeightAlignToBuildingScript();
            var placed = (double)asm.GetMethod("Building").Invoke(null, new object[] { building, copy, ground });
            Check("buildings: pos and tipus are the beta's posID and poigenid, FStart(kind) starts with the place (a building mod)",
                  changes.Contains("poiScript.FStart(System.Int32) -> TestCompat.PoiFStart") && building.posID.x == 5 &&
                  copy.started && copy.poigenid == 7 && copy.posID.x == 5 && ground.Started == 1 && placed == 12);
            var onto = new tosaveitemscript();
            var swapped = (UnityEngine.Vector3)asm.GetMethod("Swap").Invoke(null, new object[] { new attachablescript(), onto });
            Check("compat: attachablescript.targetTosave written, pointLocalPos read and Load(pos) ask the helper (a tool mod's swap)",
                  changes.Contains("attachablescript.targetTosave write -> TestCompat.SetTargetTosave") &&
                  changes.Contains("attachablescript.pointLocalPos read -> TestCompat.PointLocalPos") &&
                  changes.Contains("attachablescript.Load(UnityEngine.Vector3) -> TestCompat.AttachableLoad") &&
                  changes.Contains("tosaveitemscript.Claim(System.Boolean) -> TestCompat.Claim") && TestCompat.Claimed == onto &&
                  TestCompat.Target == onto && TestCompat.LoadedAt.x == 7 && swapped.x == 7);
        }
    }

    /// TLD Loader 3.0.3's registry: on the beta a registered item's tosaveitemscript.id is its place in the item table
    /// (what the save keeps), and there is no category; the mod reads and writes its own numbers through the helper
    /// (RegistryCompat: as TLDRevamp.LoaderCompat with one mod's items 3 and 4 at places 20000 and 20001).
    static void Registry()
    {
        string dll = Path.Combine(Path.GetDirectoryName(typeof(LoaderRetargetTest).Assembly.Location), "AliasMod.dll");
        using (var mod = ModuleDefinition.ReadModule(dll))
        {
            var changes = LoaderRetarget.Apply(mod, typeof(mainscript).Assembly, typeof(TestCompat));
            Check("registry: a helper without the registry's members leaves tosaveitemscript.id alone (the beta has it)",
                  !changes.Exists(c => c.StartsWith("tosaveitemscript.id ")));
        }
        using (var mod = ModuleDefinition.ReadModule(dll))
        {
            var changes = LoaderRetarget.Apply(mod, typeof(mainscript).Assembly, typeof(RegistryCompat));
            Check("registry: id and category read, written and their addresses taken go to the helper",
                  System.Linq.Enumerable.All(new[] { "tosaveitemscript.id read -> RegistryCompat.ItemId", "tosaveitemscript.id write -> RegistryCompat.SetItemId",
                          "tosaveitemscript.id address -> RegistryCompat.ItemIdRef", "tosaveitemscript.category read -> RegistryCompat.ItemCategory",
                          "tosaveitemscript.category write -> RegistryCompat.SetItemCategory", "tosaveitemscript.category address -> RegistryCompat.ItemCategoryRef" }, x => changes.Contains(x)));
            var asm = LoadBytes(mod).GetType("AliasMod");
            var t = new tosaveitemscript { id = 20000 };
            Check("registry: a registered item reads as its mod's category and its own id, formatted too (99:3)",
                  (string)asm.GetMethod("Registered").Invoke(null, new object[] { t }) == "99:3:99:3" && (int)asm.GetMethod("Id").Invoke(null, new object[] { t }) == 3);
            Check("registry: a game item reads as it is (category 0, id 116)",
                  (string)asm.GetMethod("Registered").Invoke(null, new object[] { new tosaveitemscript { id = 116 } }) == "0:116:0:116");
            asm.GetMethod("Refile").Invoke(null, new object[] { t, 99, 4 });
            Check("registry: the mod's own id written goes to that item's place (4 -> 20001)", t.id == 20001);
            var g = new tosaveitemscript { id = 116 };
            asm.GetMethod("Refile").Invoke(null, new object[] { g, 0, 117 });
            Check("registry: a game item's id written as it is", g.id == 117);
        }
    }

    /// TerrainGenerator.TG...objTypes: a static of a type the beta lacks (the map from menuhandler.s), three steps that are
    /// the map itself, and the nested prefabClass for ObjClass.
    static void Generator()
    {
        string dll = Path.Combine(Path.GetDirectoryName(typeof(LoaderRetargetTest).Assembly.Location), "AliasMod.dll");
        var map = new mapSettingScript();
        for (int g = 0; g < 2; g++)
        {
            var gen = new objGenScript(); map.objGens.Add(gen);
            var cat = new objGenScript.categoryClass(); gen.categories.Add(cat);
            for (int i = 0; i < 3; i++) cat.prefabs.Add(new objGenScript.prefabClass { prefab = new UnityEngine.GameObject() });
        }
        menuhandler.s = new menuhandler { currentMainMap = map };
        using (var mod = ModuleDefinition.ReadModule(dll))
        {
            var changes = LoaderRetarget.Apply(mod, typeof(mainscript).Assembly, typeof(TestCompat));
            Check("generator: ObjClass is the nested objGenScript/prefabClass", changes.Contains("type ObjClass -> objGenScript/prefabClass"));
            Check("generator: the static TG is the current map", changes.Contains("mapSettingScript.TG -> menuhandler.s.currentMainMap"));
            int got = (int)LoadBytes(mod).GetType("AliasMod").GetMethod("Scattered").Invoke(null, null);
            Check("generator: every scattered prefab of every generator (6)", got == 6);
        }
    }

    /// partscript is the beta's attachablescript; the slot's part field its part() method; the car's slot array a list.
    static void Parts()
    {
        string dll = Path.Combine(Path.GetDirectoryName(typeof(LoaderRetargetTest).Assembly.Location), "AliasMod.dll");
        var car = new tosaveitemscript();
        for (int i = 0; i < 3; i++)
        {
            var slot = new partslotscript(); car.partslotscripts.Add(slot);
            if (i < 2) slot.Part = new attachablescript { tosave = car, slot = slot };
        }
        bool threw = false;
        try { System.Reflection.Assembly.Load(File.ReadAllBytes(dll)).GetType("AliasMod").GetMethod("Parts").Invoke(null, new object[] { car }); }
        catch (TargetInvocationException e) { threw = e.InnerException is MissingFieldException || e.InnerException is TypeLoadException; }
        catch (TypeLoadException) { threw = true; }
        Check("parts: unchanged, the walk stops at the dropped partscript (before)", threw);
        using (var mod = ModuleDefinition.ReadModule(dll))
        {
            var changes = LoaderRetarget.Apply(mod, typeof(mainscript).Assembly);
            Check("parts: the redirects are listed", changes.Contains("type partscript -> attachablescript") && changes.Contains("partslotscript.part -> partslotscript.part()") &&
                  changes.Exists(c => c.StartsWith("tosaveitemscript.partslotscripts read")) && changes.Exists(c => c.StartsWith("tosaveitemscript.partslotscripts write")) &&
                  changes.Contains("attachablescript.tosaveitem -> attachablescript.tosave"));
            int got = (int)LoadBytes(mod).GetType("AliasMod").GetMethod("Parts").Invoke(null, new object[] { car });
            Check("parts: two parts found through part() and tosave, one slot left after the store", got == 21);
            Check("parts: the store reached the game's list", car.partslotscripts.Count == 1);
            Check("parts: typeof(partscript) in an attribute is the beta's attachablescript (a mod's HarmonyPatch)",
                  changes.Contains("typeof() in TypeNoteAttribute -> the beta's type") &&
                  (System.Type)LoadBytes(mod).GetType("AliasMod").GetMethod("Noted").Invoke(null, null) == typeof(attachablescript));
            var world = new visszarako(); world.coord.x = 1000;
            Check("coordinates: doublecoord is the beta's UnityEngine.Vector3d (by address, another namespace)", changes.Contains("type doublecoord -> UnityEngine.Vector3d") &&
                  (double)LoadBytes(mod).GetType("AliasMod").GetMethod("GlobalX").Invoke(null, new object[] { world, 1500f }) == 500);
        }
    }

    static void Aliases()
    {
        string dll = Path.Combine(Path.GetDirectoryName(typeof(LoaderRetargetTest).Assembly.Location), "AliasMod.dll");
        byte[] original = File.ReadAllBytes(dll);
        using (var mod = ModuleDefinition.ReadModule(dll))
        {
            var changes = LoaderRetarget.Apply(mod, typeof(mainscript).Assembly);
            Check("all sixteen fields, eighteen method shapes, a building's start, the five part redirects, the coordinate type, the generator chain and its static map, the main menu, a return sign and an attribute's typeof are retargeted", changes.Count == 52);
            Check("aliases are listed with their beta targets (" + System.Linq.Enumerable.Count(LoaderRetarget.AliasDescriptions()) + ")", System.Linq.Enumerable.Count(LoaderRetarget.AliasDescriptions()) == 129);
            var rewritten = LoadBytes(mod).GetType("AliasMod");
            var p = new fpscontroller(); var r = new radioscript(); var c = new carscript();
            var m = new mainscript(); var menu = new menuhandler();
            var body = new UnityEngine.Transform(); var data = new DataFromMenuScript(); var settings = new settingsscript();
            var mode = new gamemodesettingsscript.gamemodesettings();
            gamemodesettingsscript.s = new gamemodesettingsscript();
            kaposztaleves.s = new kaposztaleves();
            object[] args = { p, r, c, m, menu, body, data, settings, mode };
            foreach (var operation in new[] { "Write", "Address" })
            {
                p.BodyRot = null; mainscript.s = null; r.Bvolume.tunedLerp = 0; c.lightUsable.currentTurnState = 0;
                c.debugCanvas = null;
                DataFromMenuScript.s = null; settingsscript.s = null; gamemodesettingsscript.s.S = null;
                kaposztaleves.s.settings = new kaposztaleves.devSettings();
                rewritten.GetMethod(operation).Invoke(null, args);
                Check(operation + ": Tb writes BodyRot", p.BodyRot == body);
                Check(operation + ": M writes the current singleton", mainscript.s == m);
                Check(operation + ": v writes the usable's volume", r.Bvolume.tunedLerp == 0.625f);
                Check(operation + ": lampak writes the usable's light state", c.lightUsable.currentTurnState == 2);
                Check(operation + ": KmhObj writes the speed display object", c.debugCanvas == (UnityEngine.GameObject)rewritten.GetField("Display").GetValue(null));
                Check(operation + ": DFMS writes the current singleton", DataFromMenuScript.s == data);
                Check(operation + ": settings writes the current singleton", settingsscript.s == settings);
                Check(operation + ": GSettings writes the live mode", gamemodesettingsscript.s.S == mode);
                Check(operation + ": godmode writes through the beta's struct", kaposztaleves.s.settings.god);
                Check(operation + ": Showkmh writes through the beta's struct", kaposztaleves.s.settings.kmh);
                Check(operation + ": every old field reads live beta storage", (bool)rewritten.GetMethod("Read").Invoke(null, args));
            }
            // Replace the beta's storage after the mod loaded: its reads must follow the new objects.
            body = new UnityEngine.Transform(); data = new DataFromMenuScript(); settings = new settingsscript();
            mode = new gamemodesettingsscript.gamemodesettings(); m = new mainscript();
            p.BodyRot = body; mainscript.s = m; DataFromMenuScript.s = data; settingsscript.s = settings;
            gamemodesettingsscript.s = new gamemodesettingsscript { S = mode };
            args = new object[] { p, r, c, m, menu, body, data, settings, mode };
            Check("reads follow replaced beta storage", (bool)rewritten.GetMethod("Read").Invoke(null, args));
            var h = new headlightscript(); var s = new survivalscript();
            rewritten.GetMethod("Calls").Invoke(null, new object[] { p, r, m, h, s });
            Check("RightHandRelease enables the beta's sync argument", p.Released);
            Check("UpdValues drops the unused public argument", r.Updates == 1);
            Check("PressedEscape uses the game's local input arguments", m.Screenshot && !m.MainMenu);
            Check("both SwitchState overloads call the beta and wrap", h.Calls == 2 && h.switchToState == 0 && h.Sender == null);
            Check("Damage preserves amount and show, supplies no direction", s.Lost == 3 && s.Shown && s.Direction.x == 0 && s.Direction.y == 0 && s.Direction.z == 0);
            Check("DamageInstant preserves amount and show, supplies no direction", s.InstantLost == 4 && !s.InstantShown && s.InstantDirection.x == 0 && s.InstantDirection.y == 0 && s.InstantDirection.z == 0);
            Check("ChGodMode writes the beta's god flag", !kaposztaleves.s.settings.god);
            Check("ChKmh writes the beta's speed display flag", !kaposztaleves.s.settings.kmh);
            Check("FoodCh calls the moved callback with its argument", kaposztaleves.s.Food == 1);
            Check("WaterCh calls the moved callback with its argument", kaposztaleves.s.Water == 2);
            Check("HpCh calls the moved callback with its argument", kaposztaleves.s.Hp == 3);
            Check("PissCh calls the moved callback with its argument", kaposztaleves.s.Piss == 4);
            Check("ShitCh calls the moved callback with its argument", kaposztaleves.s.Shit == 5);
            var slot = new partslotscript(); var battery = new attachablescript();
            rewritten.GetMethod("Craft").Invoke(null, new object[] { slot, battery });
            Check("Craft(part, bool) crafts the part into the slot with the beta's one argument", slot.Crafted == battery);
            var made = new UnityEngine.GameObject(); var spawner = new mainscript();
            rewritten.GetMethod("Spawns").Invoke(null, new object[] { spawner, made });
            Check("Spawn's returned object is dropped where the mod expects nothing; the painted one paints", spawner.Spawned == made && spawner.Painted);
            Check("the alias pass is idempotent", LoaderRetarget.Apply(mod, typeof(mainscript).Assembly).Count == 0);
            string logged = null;
            var loaded = LoaderRetarget.Load(dll, typeof(mainscript).Assembly, new[] { Path.GetDirectoryName(dll) }, line => logged = line);
            Check("Load logs the aliases used and loads the rewritten bytes", string.IsNullOrEmpty(loaded.Location) && logged != null && logged.Contains("radioscript.v -> radioscript.Bvolume.tunedLerp") && logged.Contains("mainscript.FoodCh(System.Single) -> kaposztaleves.s.PressedFoodCh(System.Single)"));
            Check("the public-shaped fixture stays unchanged on disk", System.Linq.Enumerable.SequenceEqual(original, File.ReadAllBytes(dll)));
        }
    }
}

/// Stands in for TLDRevamp.LoaderCompat against the made-up beta: the same names and shapes.
public static class TestCompat
{
    public static readonly System.Collections.Generic.HashSet<int> Used = new System.Collections.Generic.HashSet<int>();
    public static bool Removed;
    public static System.Collections.Generic.Dictionary<int, tosaveitemscript> ToSaveStuff(savedatascript s)
    {
        var d = new System.Collections.Generic.Dictionary<int, tosaveitemscript>();
        foreach (var kv in s.items) d[(int)kv.Key] = kv.Value;
        return d;
    }
    public class SaveRecord
    {
        public int categoryID, id; public save_rendszam rendszam;
        public SaveRecord(int category, int id, double x, double y, double z, float rx, float ry, float rz) { categoryID = category; this.id = id; }
    }
    public static readonly System.Collections.Generic.Dictionary<int, SaveRecord> Records = new System.Collections.Generic.Dictionary<int, SaveRecord>();
    public static System.Collections.Generic.Dictionary<int, SaveRecord> SavedRecords(savedata d) => Records;
    public static UnityEngine.GameObject MenuSettingsScreen(menuhandler m) => new UnityEngine.GameObject { Name = m == menuhandler.s ? "settings" : "?" };
    public static UnityEngine.GameObject MenuSaveScreen(menuhandler m) => new UnityEngine.GameObject { Name = "save" };
    public static UnityEngine.Transform MenuCanvas(menuhandler m) => null;
    public static bool RecordIdUsed(object records, int id) => Used.Contains(id);
    public static void FStart(tosaveitemscript t, int id) => t.FStart((uint)id);
    public static bool GetRemoveFromMemory(tosaveitemscript t) => Removed;
    public static void SetRemoveFromMemory(tosaveitemscript t, bool value) { Removed = value; }
    public static mapSettingScript Map(mapSettingScript m) => m;
    public class RoadBuildings { public UnityEngine.Transform parent; public System.Collections.Generic.Dictionary<int, UnityEngine.GameObject> placedBuildings; }
    public static mapSettingScript RoadBuildingsMap;
    public static RoadBuildings RoadBuildingGeneration(mapSettingScript m)
    {
        RoadBuildingsMap = m;
        return new RoadBuildings { placedBuildings = new System.Collections.Generic.Dictionary<int, UnityEngine.GameObject> { { 0, new UnityEngine.GameObject() }, { 1, new UnityEngine.GameObject() } } };
    }
    public class Gone { }
    public static bool InputForward(inputscript i) => i.forward > 0f;
    public static saveproperties SaveProperties(savedatascript s) => s.data.properties;
    public static object Invoke(System.Reflection.MethodBase m, object obj, object[] args) => LoaderRetarget.InvokeLoose(m, obj, args);
    public static void MountLock(mountStuff m, pickupable p, bool ignoreRigidbody) => m.Store(p, ignoreRigidbody);
    public static tosaveitemscript Target; public static UnityEngine.Vector3 LoadedAt;
    public static void SetTargetTosave(attachablescript a, tosaveitemscript t) => Target = t;
    public static UnityEngine.Vector3 PointLocalPos(attachablescript a) => new UnityEngine.Vector3 { x = 7 };
    public static void AttachableLoad(attachablescript a, UnityEngine.Vector3 pos) => LoadedAt = pos;
    public static tosaveitemscript Claimed; public static void Claim(tosaveitemscript t, bool b) => Claimed = t;
    public static System.Reflection.MethodInfo GetMethod(System.Type t, string name) =>
        LoaderRetarget.LooseMethod(t, name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
    public static System.Reflection.MethodInfo GetMethodFlags(System.Type t, string name, System.Reflection.BindingFlags flags) => LoaderRetarget.LooseMethod(t, name, flags);
    public static string AssemblyLocation(System.Reflection.Assembly a) => "the mod's own DLL";
    public static UnityEngine.GameObject FindMain(string name) => new UnityEngine.GameObject { Name = "the main object" };
    public static void RefreshUsable(usablescript u, float slide, float x, float y) => u.Refresh(slide, x, y, u.currentTurnState);
    public static void LayerFStart(layerScript l) => l.FStart("the item", 0);
    public static void PoiFStart(poiScript p, int kind) => p.FStart(kind, p.posID);
    static readonly System.Collections.Generic.Dictionary<string, object> KeptValues = new System.Collections.Generic.Dictionary<string, object>();
    public static int TypeByNameCalls;
    public static Type TypeByName(string name) { TypeByNameCalls++; return Type.GetType(name); }
    public static T Kept<T>(object owner, string field) => KeptValues.TryGetValue(field, out var v) ? (T)v : default(T);
    public static void Keep<T>(object owner, string field, T value) => KeptValues[field] = value;
    public static objGenScript.prefabClass[] ObjTypes(mapSettingScript m)
    {
        var all = new System.Collections.Generic.List<objGenScript.prefabClass>();
        foreach (var g in m.objGens) foreach (var c in g.categories) all.AddRange(c.prefabs);
        return all.ToArray();
    }
}

/// TLDRevamp.LoaderCompat's registry members over a made-up registry: category 99's items 3 and 4 at places 20000, 20001.
public static class RegistryCompat
{
    static bool Key(int slot, out int category, out int id) { category = 99; id = slot - 19997; return slot == 20000 || slot == 20001; }
    static int Slot(int category, int id) => category == 99 && (id == 3 || id == 4) ? 19997 + id : -1;
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<tosaveitemscript, object> Categories = new System.Runtime.CompilerServices.ConditionalWeakTable<tosaveitemscript, object>();
    public static int ItemId(tosaveitemscript t) => Key(t.id, out _, out int id) ? id : t.id;
    public static void SetItemId(tosaveitemscript t, int id) { int s = Slot(ItemCategory(t), id); t.id = s >= 0 ? s : id; }
    public static int ItemCategory(tosaveitemscript t) => Key(t.id, out int c, out _) ? c : Categories.TryGetValue(t, out var v) ? (int)v : 0;
    public static void SetItemCategory(tosaveitemscript t, int category) { Categories.AddOrUpdate(t, category); int s = Slot(category, ItemId(t)); if (s >= 0) t.id = s; }
    static int _copy;
    public static ref int ItemIdRef(tosaveitemscript t) { _copy = ItemId(t); return ref _copy; }
    public static ref int ItemCategoryRef(tosaveitemscript t) { _copy = ItemCategory(t); return ref _copy; }
}
