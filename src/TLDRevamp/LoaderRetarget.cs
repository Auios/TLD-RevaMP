using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TLDRevamp
{
    /// Public-game names with the same meaning on the beta. Only the mod's code changes. Field paths address the
    /// beta's own storage, including stores and managed addresses; there is no second copy to keep up to date.
    /// Kept free of Unity and BepInEx so the unit fixtures run the real rewriter against a made-up game.
    public static class LoaderRetarget
    {
        // old owner, old field, field type, beta root, beta field path. A different root starts at a static field.
        private static readonly string[][] Fields =
        {
            new[] { "fpscontroller", "Tb", "UnityEngine.Transform", "fpscontroller", "BodyRot" },
            new[] { "fpscontroller", "M", "mainscript", "mainscript", "s" },
            new[] { "radioscript", "v", "System.Single", "radioscript", "Bvolume.tunedLerp" },
            new[] { "carscript", "lampak", "System.Int32", "carscript", "lightUsable.currentTurnState" },
            new[] { "carscript", "KmhObj", "UnityEngine.GameObject", "carscript", "debugCanvas" },
            new[] { "menuhandler", "DFMS", "DataFromMenuScript", "DataFromMenuScript", "s" },
            new[] { "mainscript", "DFMS", "DataFromMenuScript", "DataFromMenuScript", "s" },
            new[] { "mainscript", "SettingObj", "settingsscript", "settingsscript", "s" },
            new[] { "menuhandler", "Settings", "settingsscript", "settingsscript", "s" },
            new[] { "mainscript", "GSettings", "gamemodesettingsscript/gamemodesettings", "gamemodesettingsscript", "s.S" },
            new[] { "mainscript", "godmode", "System.Boolean", "kaposztaleves", "s.settings.god" },
            new[] { "mainscript", "Showkmh", "System.Boolean", "kaposztaleves", "s.settings.kmh" },
            // partscript (now attachablescript, Types above)
            new[] { "attachablescript", "tosaveitem", "tosaveitemscript", "attachablescript", "tosave" },
            new[] { "attachablescript", "p", "pickupable", "attachablescript", "tosave.P" },
            new[] { "tosaveitemscript", "part", "attachablescript", "tosaveitemscript", "attachable" },
            new[] { "pickupable", "part", "attachablescript", "pickupable", "tosave.attachable" },
            new[] { "itemdatabase", "buildings", "UnityEngine.GameObject[]", "itemdatabase", "pois" },
            new[] { "mapSettingScript", "TG", "mapSettingScript", "menuhandler", "s.currentMainMap" },
            new[] { "mapSettingScript", "staticReference", "mapSettingScript", "menuhandler", "s.currentMainMap" },
            new[] { "mainscript", "terrainGenerationSettings", "mapSettingScript", "menuhandler", "s.currentMainMap" },
        };

        // The public game's parts: the beta folded partscript into attachablescript (the same slot, condition, tipus and
        // FallOFf). A type the beta lacks, renamed in the mod to the beta's (every use: fields, locals, GetComponent<T>).
        private static readonly string[][] Types =
        {
            new[] { "partscript", "attachablescript" },
            // buildings are points of interest (itemdatabase.pois, the same prefabs and names: kut, benzinkut01, ...)
            new[] { "buildingscript", "poiScript" },
            // a struct of three doubles, as Vector3d (visszarako.coord, the world offset: mainscript.world below)
            new[] { "doublecoord", "UnityEngine.Vector3d" },
            // the public game's world generator: TerrainGenerator.TG.manager.settings.objGeneration.objTypes[i].prefab, the
            // prefabs it scatters (cacti, rocks, tumbleweeds), is the current map's objGenScripts' prefabs on the beta. The
            // chain's four types are the map (mapSettingScript), each step to the next the map itself (CompatFields)
            new[] { "TerrainGenerator", "mapSettingScript" },
            new[] { "TerrainManager", "mapSettingScript" },
            new[] { "TerrainGenerationSettings", "mapSettingScript" },
            new[] { "ObjectGenerationScript", "mapSettingScript" },
            new[] { "ObjClass", "objGenScript/prefabClass" },
        };

        // Fields that became methods: old owner, old field, field type, beta method (no parameters, returns the field type)
        private static readonly string[][] Getters =
        {
            new[] { "partslotscript", "part", "attachablescript", "part" },
        };

        // Arrays the beta keeps as lists: old owner, old field, element type, beta field (a List`1 of it). Reads get a copy
        // (writes into the copy's elements don't reach the game), stores replace the list.
        private static readonly string[][] Lists =
        {
            new[] { "tosaveitemscript", "partslotscripts", "partslotscript", "partslotscripts" },
            new[] { "tosaveitemscript", "partconditions", "partconditionscript", "partconditions" },
            new[] { "tosaveitemscript", "tanks", "tankscript", "tanks" },
            new[] { "tosaveitemscript", "usables", "usablescript", "usables" },
            new[] { "tosaveitemscript", "tankcapscripts", "tankcapscript", "tankcapscripts" },
            new[] { "tosaveitemscript", "physlocks", "mountStuff", "physLocks" },
            new[] { "tosaveitemscript", "colors", "colorscript", "colors" },
            new[] { "tosaveitemscript", "door_rots", "door_rot", "door_rots" },
        };

        private static readonly string[][] Setters =
        {
            new[] { "ChGodMode", "godmode" }, new[] { "ChKmh", "Showkmh" },
        };

        private static readonly string[][] MovedMethods =
        {
            new[] { "FoodCh", "PressedFoodCh" }, new[] { "WaterCh", "PressedWaterCh" },
            new[] { "HpCh", "PressedHpCh" }, new[] { "PissCh", "PressedPissCh" }, new[] { "ShitCh", "PressedShitCh" },
        };

        // old owner, method, old parameter list, beta parameter list, beta arguments. Numbers name old arguments
        // (including this at zero); the other words supply the beta's ordinary local-call defaults.
        private static readonly string[][] Methods =
        {
            new[] { "fpscontroller", "RightHandRelease", "", "System.Boolean", "true" },
            new[] { "radioscript", "UpdValues", "System.Boolean", "", "" },
            new[] { "mainscript", "PressedEscape", "", "System.Boolean,System.Boolean", "true,false" },
            new[] { "headlightscript", "SwitchState", "System.Int32", "System.Int32,usablescript", "1,null" },
            new[] { "headlightscript", "SwitchState", "", "System.Int32,usablescript", "nextLight,null" },
            new[] { "survivalscript", "Damage", "System.Single,System.Boolean", "System.Single,UnityEngine.Vector3,System.Boolean", "1,zero,2" },
            new[] { "survivalscript", "DamageInstant", "System.Single,System.Boolean", "System.Single,UnityEngine.Vector3,System.Boolean", "1,zero,2" },
            new[] { "pickupable", "Drop", "System.Boolean", "", "" },
        };

        // The public game's save and attach members the beta changed or dropped, answered by a helper type the caller
        // passes (TLDRevamp.LoaderCompat; tests pass their own): old owner, member, field type, load helper, store helper.
        private static readonly string[][] CompatFields =
        {
            new[] { "savedatascript", "toSaveStuff", "System.Collections.Generic.Dictionary`2<System.Int32,tosaveitemscript>", "ToSaveStuff", "" },
            new[] { "savedata", "nearStuff", "System.Collections.Generic.Dictionary`2<System.Int32,save_prefab>", "SavedRecords", "" },
            new[] { "savedata", "farStuff", "System.Collections.Generic.Dictionary`2<System.Int32,save_prefab>", "SavedRecords", "" },
            new[] { "tosaveitemscript", "removeFromMemory", "System.Boolean", "GetRemoveFromMemory", "SetRemoveFromMemory" },
            new[] { "pickupable", "attach", "attachablescript", "GetAttach", "SetAttach" },
            new[] { "pickupable", "cols", "layerScript[]", "GetCols", "SetCols" },
            new[] { "pickupable", "cols2", "layerScript[]", "GetCols2", "SetCols2" },
            new[] { "attachablescript", "targetTosave", "tosaveitemscript", "TargetTosave", "" },
            new[] { "attachablescript", "C", "System.Collections.Generic.List`1<UnityEngine.Collider>", "GetC", "SetC" },
            new[] { "poiScript", "itemsSpawned", "System.Boolean", "ItemsSpawned", "" },
            new[] { "partslotscript", "part", "attachablescript", "", "SetSlotPart" },   // reads: part() (Getters)
            new[] { "tankcapscript", "TCap", "UnityEngine.Transform", "GetCapT", "SetCapT" },
            new[] { "tankcapscript", "TOpen", "UnityEngine.Transform", "GetCapOpen", "SetCapOpen" },
            new[] { "tankcapscript", "TClosed", "UnityEngine.Transform", "GetCapClosed", "SetCapClosed" },
            new[] { "carscript", "driverSeat", "seatscript", "DriverSeat", "" },
            new[] { "carscript", "ISwitches", "usablescript[]", "Switches", "" },
            new[] { "pickupable", "slots", "System.Collections.Generic.List`1<partslotscript>", "PickupSlots", "" },
            new[] { "tankcapscript", "GOpen", "System.Collections.Generic.List`1<UnityEngine.GameObject>", "GetCapGOpen", "SetCapGOpen" },
            new[] { "tankcapscript", "GClosed", "System.Collections.Generic.List`1<UnityEngine.GameObject>", "GetCapGClosed", "SetCapGClosed" },
            new[] { "mapSettingScript", "manager", "mapSettingScript", "Map", "" },
            new[] { "mapSettingScript", "settings", "mapSettingScript", "Map", "" },
            new[] { "mapSettingScript", "generator", "mapSettingScript", "Map", "" },
            new[] { "mapSettingScript", "objGeneration", "mapSettingScript", "Map", "" },
            new[] { "mapSettingScript", "objTypes", "objGenScript/prefabClass[]", "ObjTypes", "" },
        };
        // old owner, method, old parameter list, helper (the instance first, then the arguments)
        private static readonly string[][] CompatMethods =
        {
            new[] { "tosaveitemscript", "FStart", "System.Int32", "FStart" },
            new[] { "attachablescript", "Attach", "UnityEngine.Transform,UnityEngine.RaycastHit", "Attach" },
            new[] { "tankcapscript", "UpdCap", "System.Boolean", "UpdCap" },
        };

        public static IEnumerable<string> AliasDescriptions()
        {
            foreach (var a in Fields) yield return a[0] + "." + a[1] + " -> " + a[3] + "." + a[4] + " (read/write/address)";
            foreach (var a in Methods) yield return a[0] + "." + a[1] + "(" + a[2] + ") -> " + a[0] + "." + a[1] + "(" + a[3] + ") [" + a[4] + "]";
            foreach (var a in Types) yield return "type " + a[0] + " -> " + a[1];
            foreach (var a in Getters) yield return a[0] + "." + a[1] + " -> " + a[0] + "." + a[3] + "()";
            foreach (var a in Lists) yield return a[0] + "." + a[1] + " " + a[2] + "[] -> " + a[0] + "." + a[3] + " List<" + a[2] + ">";
            foreach (var a in Setters)
                foreach (var f in Fields)
                    if (f[0] == "mainscript" && f[1] == a[1]) yield return "mainscript." + a[0] + "(System.Boolean) -> store " + f[3] + "." + f[4];
            foreach (var a in MovedMethods) yield return "mainscript." + a[0] + "(System.Single) -> kaposztaleves.s." + a[1] + "(System.Single)";
            yield return "same-name integer fields -> beta integer sign (including tosaveitemscript.idInSave Int32 -> UInt32)";
            foreach (var a in CompatFields) yield return a[0] + "." + a[1] + " -> LoaderCompat." + a[3] + (a[4].Length > 0 ? "/" + a[4] : "");
            foreach (var a in CompatMethods) yield return a[0] + "." + a[1] + "(" + a[2] + ") -> LoaderCompat." + a[3];
            yield return "Dictionary<Int32,save_prefab>.ContainsKey -> LoaderCompat.RecordIdUsed; GetComponent<a type the beta lacks>() -> LoaderCompat.NoComponent";
        }

        /// Rewrites <paramref name="mod"/>'s member accesses into <paramref name="game"/> (matched by assembly name)
        /// and returns one line per change; empty when the mod needed nothing.
        public static List<string> Apply(ModuleDefinition mod, Assembly game, Type compat = null)
        {
            var changes = new List<string>();
            string gameName = game.GetName().Name;
            foreach (var tr in mod.GetTypeReferences())
                foreach (var a in Types)
                {
                    if (tr.DeclaringType != null || tr.Namespace != "" || tr.Name != a[0] || !(tr.Scope is AssemblyNameReference scope) || scope.Name != gameName) continue;
                    if (GameLookups.RuntimeType(game, a[0], false) != null || GameLookups.RuntimeType(game, a[1].Replace('/', '+'), false) == null) continue;   // the game has the old one, or not the new
                    int dot = a[1].LastIndexOf('.'), nest = a[1].IndexOf('/');
                    if (nest > 0)   // a nested type of the game's: objGenScript/prefabClass
                    {
                        tr.DeclaringType = new TypeReference("", a[1].Substring(0, nest), mod, tr.Scope);
                        tr.Namespace = ""; tr.Name = a[1].Substring(nest + 1);
                    }
                    else { tr.Namespace = dot < 0 ? "" : a[1].Substring(0, dot); tr.Name = a[1].Substring(dot + 1); }
                    changes.Add("type " + a[0] + " -> " + a[1]);
                }
            foreach (var mr in mod.GetMemberReferences())
            {
                if (!(mr is FieldReference fr)) continue;
                var owner = GameType(game, gameName, fr.DeclaringType);
                if (owner == null) continue;
                var fi = GameLookups.RuntimeField(owner, fr.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (fi == null) continue;
                var swap = OtherSign(fr.FieldType.MetadataType, fi.FieldType);
                if (swap == null) continue;
                string was = fr.FieldType.Name;
                fr.FieldType = swap(mod.TypeSystem);
                changes.Add(fr.DeclaringType.FullName + "." + fr.Name + " " + was + " -> " + fr.FieldType.Name);
            }
            // Snapshot the methods: adapters added below must not themselves be visited.
            var bodies = new List<MethodDefinition>();
            foreach (var t in mod.Types) Collect(t, bodies);
            TypeDefinition helpers = null;
            var adapters = new Dictionary<string, MethodDefinition>();
            foreach (var method in bodies)
            foreach (var ins in method.Body.Instructions)
            {
                var member = ins.Operand as MemberReference;
                if (member == null) continue;
                if (compat != null && !(ins.Previous != null && ins.Previous.OpCode.OpCodeType == OpCodeType.Prefix))
                {
                    var helper = CompatHelper(game, gameName, compat, ins, out string what);
                    if (helper != null)
                    {
                        ins.OpCode = OpCodes.Call;
                        ins.Operand = mod.ImportReference(helper);
                        if (!changes.Contains(what)) changes.Add(what);
                        continue;
                    }
                }
                if (GameType(game, gameName, member.DeclaringType) == null) continue;
                // A field prefix cannot prefix a call. Leave such unusual IL alone.
                if (ins.Previous != null && ins.Previous.OpCode.OpCodeType == OpCodeType.Prefix) continue;
                string key = ins.OpCode.Code + " " + member.FullName;
                MethodDefinition adapter;
                if (!adapters.TryGetValue(key, out adapter))
                {
                    string description;
                    adapter = FieldAdapter(mod, game, ins, out description) ?? GetterAdapter(mod, game, ins, out description) ??
                              ListAdapter(mod, game, ins, out description) ?? MethodAdapter(mod, game, ins, out description);
                    if (adapter == null) continue;
                    if (helpers == null)
                    {
                        string name = "__TLDRevampAliases";
                        while (mod.GetType(name) != null) name += "_";
                        helpers = new TypeDefinition("", name, Mono.Cecil.TypeAttributes.NotPublic | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed, mod.TypeSystem.Object);
                        mod.Types.Add(helpers);
                    }
                    adapter.Name = "Alias" + adapters.Count;
                    helpers.Methods.Add(adapter);
                    adapters.Add(key, adapter);
                    if (!changes.Contains(description)) changes.Add(description);
                }
                // Replacing the instruction in place preserves branches, switch targets and exception boundaries.
                ins.OpCode = OpCodes.Call;
                ins.Operand = adapter;
            }
            return changes;
        }

        /// The helper for an instruction on a public-game member the beta doesn't have (CompatFields, CompatMethods, the
        /// dropped save_prefab's ContainsKey, GetComponent of a dropped type), or null. A member the game has is left alone.
        private static MethodInfo CompatHelper(Assembly game, string gameName, Type compat, Instruction ins, out string what)
        {
            what = null;
            const BindingFlags Statics = BindingFlags.Public | BindingFlags.Static;
            if (ins.Operand is FieldReference fr && (ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Stfld))
            {
                var owner = GameType(game, gameName, fr.DeclaringType);
                if (owner == null) return null;
                // the game has it as the mod knows it: nothing to do. The same name with another type is not it (pickupable.cols
                // is a List here, attachablescript.C a private Collider[]): the mod's access stopped its whole method
                var has = GameLookups.RuntimeField(owner, fr.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (has != null && CecilName(has.FieldType) == fr.FieldType.FullName) return null;
                foreach (var a in CompatFields)
                {
                    if (fr.DeclaringType.FullName != a[0] || fr.Name != a[1] || fr.FieldType.FullName != a[2]) continue;
                    string name = ins.OpCode == OpCodes.Ldfld ? a[3] : a[4];
                    var m = name.Length > 0 ? compat.GetMethod(name, Statics) : null;
                    if (m == null) return null;
                    what = a[0] + "." + a[1] + (ins.OpCode == OpCodes.Ldfld ? " read" : " write") + " -> " + compat.Name + "." + name;
                    return m;
                }
                return null;
            }
            if (!(ins.Operand is MethodReference mr) || (ins.OpCode != OpCodes.Call && ins.OpCode != OpCodes.Callvirt)) return null;
            // Dictionary<int, save_prefab>.ContainsKey: the spawner's id test on the old records
            if (mr.Name == "ContainsKey" && mr.DeclaringType is GenericInstanceType git && git.ElementType.FullName == "System.Collections.Generic.Dictionary`2" &&
                git.GenericArguments.Count == 2 && git.GenericArguments[0].MetadataType == MetadataType.Int32 && Missing(game, gameName, git.GenericArguments[1]))
            {
                var m = compat.GetMethod("RecordIdUsed", Statics);
                if (m == null) return null;
                what = "Dictionary<Int32," + git.GenericArguments[1].Name + ">.ContainsKey -> " + compat.Name + ".RecordIdUsed";
                return m;
            }
            // GetComponent<T>() and its relatives with a type the beta dropped (partscript): there is none
            if (mr is GenericInstanceMethod gim && gim.GenericArguments.Count == 1 && gim.Parameters.Count == 0 && mr.HasThis &&
                (mr.Name == "GetComponent" || mr.Name == "GetComponentInChildren" || mr.Name == "GetComponentInParent") && Missing(game, gameName, gim.GenericArguments[0]))
            {
                var m = compat.GetMethod("NoComponent", Statics);
                if (m == null) return null;
                what = mr.Name + "<" + gim.GenericArguments[0].Name + "> -> " + compat.Name + ".NoComponent";
                return m;
            }
            var type = GameType(game, gameName, mr.DeclaringType);
            if (type == null || !mr.HasThis) return null;
            foreach (var a in CompatMethods)
            {
                if (mr.DeclaringType.FullName != a[0] || mr.Name != a[1] || Parameters(mr) != a[2]) continue;
                foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (candidate.Name == a[1] && string.Join(",", System.Linq.Enumerable.Select(candidate.GetParameters(), p => TypeName(p.ParameterType))) == a[2]) return null;   // the game has it
                var m = compat.GetMethod(a[3], Statics);
                if (m == null) return null;
                what = a[0] + "." + a[1] + "(" + a[2] + ") -> " + compat.Name + "." + a[3];
                return m;
            }
            return null;
        }

        /// A runtime type as Cecil writes it (List`1<UnityEngine.Collider>, layerScript[], Outer/Inner).
        private static string CecilName(Type t)
        {
            if (t.IsArray) return CecilName(t.GetElementType()) + "[]";
            if (t.IsGenericType && !t.IsGenericTypeDefinition)
                return TypeName(t.GetGenericTypeDefinition()) + "<" + string.Join(",", System.Linq.Enumerable.Select(t.GetGenericArguments(), CecilName)) + ">";
            return TypeName(t);
        }

        /// A type of the game's the beta doesn't have (save_prefab, partscript).
        private static bool Missing(Assembly game, string gameName, TypeReference tr) =>
            tr != null && !(tr is TypeSpecification) && tr.Scope is AssemblyNameReference scope && scope.Name == gameName && GameType(game, gameName, tr) == null;

        private static void Collect(TypeDefinition type, List<MethodDefinition> methods)
        {
            foreach (var m in type.Methods) if (m.HasBody) methods.Add(m);
            foreach (var t in type.NestedTypes) Collect(t, methods);
        }

        private const BindingFlags PublicFields = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        private static string TypeName(Type t) => t.FullName.Replace('+', '/');
        private static string Parameters(MethodReference m) => string.Join(",", System.Linq.Enumerable.Select(m.Parameters, p => p.ParameterType.FullName));

        private static MethodDefinition NewAdapter(ModuleDefinition mod, TypeReference owner, TypeReference result)
        {
            var m = new MethodDefinition("", Mono.Cecil.MethodAttributes.Assembly | Mono.Cecil.MethodAttributes.Static, result);
            m.Parameters.Add(new ParameterDefinition(mod.ImportReference(owner)));
            return m;
        }

        private static MethodDefinition FieldAdapter(ModuleDefinition mod, Assembly game, Instruction ins, out string description)
        {
            description = null;
            var old = ins.Operand as FieldReference;
            bool store = ins.OpCode == OpCodes.Stfld, address = ins.OpCode == OpCodes.Ldflda, statics = ins.OpCode == OpCodes.Ldsfld;
            if (old == null || (!store && !address && !statics && ins.OpCode != OpCodes.Ldfld)) return null;
            foreach (var a in Fields)
            {
                if (old.DeclaringType.FullName != a[0] || old.Name != a[1] || old.FieldType.FullName != a[2]) continue;
                if (game.GetType(a[0]).GetField(a[1], PublicFields) != null) return null; // a public build already has it
                var root = game.GetType(a[3]);
                if (root == null) return null;
                var path = new List<FieldInfo>();
                var current = root;
                foreach (var name in a[4].Split('.'))
                {
                    var f = current.GetField(name, PublicFields);
                    if (f == null || (path.Count > 0 && f.IsStatic)) return null;
                    path.Add(f); current = f.FieldType;
                }
                if (TypeName(current) != a[2] || (a[3] != a[0] && !path[0].IsStatic)) return null;
                var last = path[path.Count - 1];
                if ((store || address) && last.IsInitOnly) return null;
                var ft = mod.ImportReference(current);
                var m = NewAdapter(mod, old.DeclaringType, store ? mod.TypeSystem.Void : address ? new ByReferenceType(ft) : ft);
                if (statics) { if (!path[0].IsStatic) return null; m.Parameters.Clear(); }   // a static of the old owner: nothing to pass
                if (store) m.Parameters.Add(new ParameterDefinition(ft));
                var il = m.Body.GetILProcessor();
                if (!path[0].IsStatic) il.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < path.Count; i++)
                {
                    var f = path[i];
                    bool end = i == path.Count - 1;
                    if (end && store) il.Emit(OpCodes.Ldarg_1);
                    bool byAddress = end ? address : f.FieldType.IsValueType;
                    il.Emit(end && store ? (f.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld) : byAddress ? (f.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda) : (f.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld), mod.ImportReference(f));
                }
                il.Emit(OpCodes.Ret);
                description = a[0] + "." + a[1] + " -> " + a[3] + "." + a[4];
                return m;
            }
            return null;
        }

        /// A read of a field the beta replaced with a method of the same meaning (partslotscript.part -> part()).
        private static MethodDefinition GetterAdapter(ModuleDefinition mod, Assembly game, Instruction ins, out string description)
        {
            description = null;
            var old = ins.Operand as FieldReference;
            if (old == null || ins.OpCode != OpCodes.Ldfld) return null;
            foreach (var a in Getters)
            {
                if (old.DeclaringType.FullName != a[0] || old.Name != a[1] || old.FieldType.FullName != a[2]) continue;
                var owner = game.GetType(a[0]);
                if (owner == null || owner.GetField(a[1], PublicFields) != null) return null;
                var target = owner.GetMethod(a[3], BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (target == null || TypeName(target.ReturnType) != a[2]) return null;
                var m = NewAdapter(mod, old.DeclaringType, mod.ImportReference(target.ReturnType));
                var il = m.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, mod.ImportReference(target)); il.Emit(OpCodes.Ret);
                description = a[0] + "." + a[1] + " -> " + a[0] + "." + a[3] + "()";
                return m;
            }
            return null;
        }

        /// An array field the beta keeps as a List of the same elements (Lists): a read gets the list's copy as an array
        /// (null for none), a store a new list of the array's elements.
        private static MethodDefinition ListAdapter(ModuleDefinition mod, Assembly game, Instruction ins, out string description)
        {
            description = null;
            var old = ins.Operand as FieldReference;
            bool store = ins.OpCode == OpCodes.Stfld;
            if (old == null || (!store && ins.OpCode != OpCodes.Ldfld) || !(old.FieldType is ArrayType at)) return null;
            foreach (var a in Lists)
            {
                if (old.DeclaringType.FullName != a[0] || old.Name != a[1] || at.ElementType.FullName != a[2]) continue;
                var owner = game.GetType(a[0]);
                var f = owner?.GetField(a[3], BindingFlags.Public | BindingFlags.Instance);
                if (f == null || !f.FieldType.IsGenericType || f.FieldType.GetGenericTypeDefinition() != typeof(List<>) || TypeName(f.FieldType.GetGenericArguments()[0]) != a[2]) return null;
                if (a[1] == a[3] && f.FieldType.IsArray) return null;
                var list = f.FieldType;
                var m = NewAdapter(mod, old.DeclaringType, store ? mod.TypeSystem.Void : mod.ImportReference(list.GetGenericArguments()[0].MakeArrayType()));
                var il = m.Body.GetILProcessor();
                var none = Instruction.Create(OpCodes.Ldnull);
                if (store)
                {
                    m.Parameters.Add(new ParameterDefinition(mod.ImportReference(list.GetGenericArguments()[0].MakeArrayType())));
                    var set = Instruction.Create(OpCodes.Stfld, mod.ImportReference(f));
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Brfalse, none);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Newobj, mod.ImportReference(list.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(list.GetGenericArguments()[0]) })));
                    il.Emit(OpCodes.Br, set);
                    il.Append(none); il.Append(set); il.Emit(OpCodes.Ret);
                }
                else
                {
                    var done = Instruction.Create(OpCodes.Ret);
                    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, mod.ImportReference(f)); il.Emit(OpCodes.Dup);
                    var copy = Instruction.Create(OpCodes.Callvirt, mod.ImportReference(list.GetMethod("ToArray", Type.EmptyTypes)));
                    il.Emit(OpCodes.Brtrue, copy);
                    il.Emit(OpCodes.Pop); il.Append(none); il.Emit(OpCodes.Br, done);
                    il.Append(copy); il.Append(done);
                }
                description = a[0] + "." + a[1] + (store ? " write" : " read") + " -> " + a[0] + "." + a[3] + " (List<" + a[2] + ">)";
                return m;
            }
            return null;
        }

        private static MethodDefinition MethodAdapter(ModuleDefinition mod, Assembly game, Instruction ins, out string description)
        {
            description = null;
            var old = ins.Operand as MethodReference;
            if (old == null || !old.HasThis || old.ReturnType.MetadataType != MetadataType.Void || (ins.OpCode != OpCodes.Call && ins.OpCode != OpCodes.Callvirt)) return null;
            if (old.DeclaringType.FullName == "mainscript")
            {
                // These callbacks moved with the developer menu. Use its own methods and storage.
                foreach (var a in Setters)
                    if (old.Name == a[0] && Parameters(old) == "System.Boolean" && game.GetType("mainscript").GetMethod(a[0]) == null)
                    {
                        var setter = FieldAdapter(mod, game, Instruction.Create(OpCodes.Stfld, new FieldReference(a[1], mod.TypeSystem.Boolean, old.DeclaringType)), out description);
                        if (setter != null) description = "mainscript." + a[0] + "(System.Boolean) -> " + description;
                        return setter;
                    }
                foreach (var a in MovedMethods)
                    if (old.Name == a[0] && Parameters(old) == "System.Single" && game.GetType("mainscript").GetMethod(a[0]) == null)
                    {
                        var root = game.GetType("kaposztaleves");
                        var singleton = root?.GetField("s", PublicFields);
                        var target = root?.GetMethod(a[1], new[] { typeof(float) });
                        if (singleton == null || !singleton.IsStatic || singleton.FieldType != root || target == null || target.IsStatic || target.ReturnType != typeof(void)) return null;
                        var m = NewAdapter(mod, old.DeclaringType, mod.TypeSystem.Void);
                        m.Parameters.Add(new ParameterDefinition(mod.TypeSystem.Single));
                        var il = m.Body.GetILProcessor();
                        il.Emit(OpCodes.Ldsfld, mod.ImportReference(singleton)); il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Callvirt, mod.ImportReference(target)); il.Emit(OpCodes.Ret);
                        description = "mainscript." + a[0] + "(System.Single) -> kaposztaleves.s." + a[1] + "(System.Single)";
                        return m;
                    }
            }
            foreach (var a in Methods)
            {
                if (old.DeclaringType.FullName != a[0] || old.Name != a[1] || Parameters(old) != a[2]) continue;
                var owner = game.GetType(a[0]);
                MethodInfo target = null;
                foreach (var candidate in owner.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (candidate.Name != a[1]) continue;
                    string args = string.Join(",", System.Linq.Enumerable.Select(candidate.GetParameters(), p => TypeName(p.ParameterType)));
                    if (args == a[2]) return null;
                    if (args == a[3] && candidate.ReturnType == typeof(void)) target = candidate;
                }
                if (target == null) return null;
                var nextLight = owner.GetField("switchToState", PublicFields);
                var states = owner.GetField("states", PublicFields);
                var count = states?.FieldType.GetProperty("Count")?.GetGetMethod();
                if (a[4].Contains("nextLight") && (nextLight == null || nextLight.FieldType != typeof(int) || count == null || count.ReturnType != typeof(int))) return null;
                var m = NewAdapter(mod, old.DeclaringType, mod.TypeSystem.Void);
                foreach (var p in old.Parameters) m.Parameters.Add(new ParameterDefinition(mod.ImportReference(p.ParameterType)));
                var il = m.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                var argsToPass = a[4].Length == 0 ? new string[0] : a[4].Split(',');
                for (int i = 0; i < argsToPass.Length; i++)
                {
                    switch (argsToPass[i])
                    {
                        case "true": il.Emit(OpCodes.Ldc_I4_1); break;
                        case "false": il.Emit(OpCodes.Ldc_I4_0); break;
                        case "null": il.Emit(OpCodes.Ldnull); break;
                        case "nextLight":
                            var next = new VariableDefinition(mod.TypeSystem.Int32);
                            m.Body.Variables.Add(next);
                            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, mod.ImportReference(nextLight));
                            il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc, next);
                            il.Emit(OpCodes.Ldloc, next); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, mod.ImportReference(states));
                            il.Emit(OpCodes.Callvirt, mod.ImportReference(count));
                            var wrapped = Instruction.Create(OpCodes.Ldloc, next);
                            il.Emit(OpCodes.Blt, wrapped); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stloc, next);
                            il.Append(wrapped); break;
                        case "zero":
                            var local = new VariableDefinition(mod.ImportReference(target.GetParameters()[i].ParameterType));
                            m.Body.Variables.Add(local); m.Body.InitLocals = true;
                            il.Emit(OpCodes.Ldloca, local); il.Emit(OpCodes.Initobj, local.VariableType); il.Emit(OpCodes.Ldloc, local); break;
                        default: il.Emit(OpCodes.Ldarg, m.Parameters[int.Parse(argsToPass[i])]); break;
                    }
                }
                il.Emit(OpCodes.Callvirt, mod.ImportReference(target));
                il.Emit(OpCodes.Ret);
                description = a[0] + "." + a[1] + "(" + a[2] + ") -> " + a[0] + "." + a[1] + "(" + a[3] + ") [" + a[4] + "]";
                return m;
            }
            return null;
        }

        /// Reads the mod's DLL, rewrites it if it needs it and loads it: from the bytes when something changed (the
        /// assembly then has no Location), as before with LoadFrom when nothing did or the rewrite failed.
        public static Assembly Load(string dll, Assembly game, IEnumerable<string> searchDirs, Action<string> log, Type compat = null)
        {
            try
            {
                var resolver = new DefaultAssemblyResolver();   // writing may resolve the mod's references (enum arguments of attributes)
                foreach (var d in searchDirs) if (Directory.Exists(d)) resolver.AddSearchDirectory(d);
                using (var mod = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(dll)),
                           new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate }))
                {
                    var changes = Apply(mod, game, compat);
                    if (changes.Count > 0)
                    {
                        var ms = new MemoryStream();
                        mod.Write(ms);
                        var asm = Assembly.Load(ms.ToArray());
                        log(Path.GetFileName(dll) + " retargeted to the beta: " + string.Join(", ", changes.ToArray()));
                        return asm;
                    }
                }
            }
            catch (Exception e) { log(Path.GetFileName(dll) + ": retargeting failed, loaded unchanged (" + e.Message + ")"); }
            return Assembly.LoadFrom(dll);
        }

        /// The game's runtime type for a reference into it (nested types too), or null for anything else.
        private static Type GameType(Assembly game, string gameName, TypeReference tr)
        {
            if (tr == null || tr is TypeSpecification) return null;
            var root = tr;
            while (root.DeclaringType != null) root = root.DeclaringType;
            if (!(root.Scope is AssemblyNameReference scope) || scope.Name != gameName) return null;
            string name = tr.Name;
            for (var d = tr.DeclaringType; d != null; d = d.DeclaringType) name = d.Name + "+" + name;
            if (!string.IsNullOrEmpty(root.Namespace)) name = root.Namespace + "." + name;
            return GameLookups.RuntimeType(game, name, false);
        }

        private static Func<TypeSystem, TypeReference> OtherSign(MetadataType mine, Type real)
        {
            switch (mine)
            {
                case MetadataType.Int32: return real == typeof(uint) ? ts => ts.UInt32 : (Func<TypeSystem, TypeReference>)null;
                case MetadataType.UInt32: return real == typeof(int) ? ts => ts.Int32 : (Func<TypeSystem, TypeReference>)null;
                case MetadataType.Int64: return real == typeof(ulong) ? ts => ts.UInt64 : (Func<TypeSystem, TypeReference>)null;
                case MetadataType.UInt64: return real == typeof(long) ? ts => ts.Int64 : (Func<TypeSystem, TypeReference>)null;
                case MetadataType.Int16: return real == typeof(ushort) ? ts => ts.UInt16 : (Func<TypeSystem, TypeReference>)null;
                case MetadataType.UInt16: return real == typeof(short) ? ts => ts.Int16 : (Func<TypeSystem, TypeReference>)null;
                case MetadataType.SByte: return real == typeof(byte) ? ts => ts.Byte : (Func<TypeSystem, TypeReference>)null;
                case MetadataType.Byte: return real == typeof(sbyte) ? ts => ts.SByte : (Func<TypeSystem, TypeReference>)null;
                default: return null;
            }
        }
    }
}
