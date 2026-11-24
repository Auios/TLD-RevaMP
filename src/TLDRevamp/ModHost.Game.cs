using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TLDRevamp
{
    /// ModHost's game-only parts: TLD Loader's loading panel and its Events, raised from the game's own calls.
    public static partial class ModHost
    {
        /// The beta's item database has empty slots (items taken out of the game keep their ids); the public game's had none.
        /// Mods go through every item in DbLoad (Nitrous gives every car its nitrous system, Attach Everything makes every
        /// item attachable) and stopped at the first empty one: no nitrous spawners added, nothing attachable (issue 44).
        /// During DbLoad and OnLoad each empty slot holds an inactive placeholder; afterwards the slots are empty again (also in the
        /// array a mod made by appending to it), so no item id moves. A/B: false = before 2026-11-23.
        private static GameObject _slotPlaceholder;
        private static readonly List<int> _holes = new List<int>();
        static partial void CompatType(ref Type type) { type = typeof(LoaderCompat); }
        static partial void FillHoles()
        {
            var db = itemdatabase.s;
            var holes = _holes; holes.Clear();
            if (db == null || db.items == null) return;
            for (int i = 0; i < db.items.Length; i++)
                if (db.items[i] == null)
                {
                    if (_slotPlaceholder == null)
                    {
                        _slotPlaceholder = new GameObject("(empty item slot)") { hideFlags = HideFlags.HideAndDontSave };
                        _slotPlaceholder.SetActive(false);
                    }
                    db.items[i] = _slotPlaceholder; holes.Add(i);
                }
            EmptySlotsFilled = holes.Count;
        }
        static partial void RestoreHoles()
        {
            var db = itemdatabase.s; var holes = _holes;
            if (db != null && db.items != null)
                foreach (int i in holes) if (i < db.items.Length && ReferenceEquals(db.items[i], _slotPlaceholder)) db.items[i] = null;
            if (_slotPlaceholder != null) { UnityEngine.Object.Destroy(_slotPlaceholder); _slotPlaceholder = null; }   // with what mods added to it
            holes.Clear();
        }


        /// TLD Loader's loading panel (ModLoader.loading): an inactive object, so a mod's `loading.activeSelf` is false.
        static partial void MakeLoadingPanel()
        {
            var f = Loader?.GetField("loading", BindingFlags.Public | BindingFlags.Static);
            if (f == null || f.GetValue(null) != null) return;
            var go = new GameObject("TLDLoader loading (RevaMP)") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(go);
            f.SetValue(null, go);
        }

        static partial void BindEvents(Assembly stub) => Events.Bind(stub);

        /// A/B (bridge `set`): false = before 2026-11-11 (mainscript.M.seed/load/napszak exist but stay 0/false/null).
        public static bool GameAliasesOn = true;
        private static bool _fromSave, _loadPending, _aliasWasUp;
        private static FieldInfo _fSeed, _fLoad, _fNapszak, _fLoaded, _fMulti, _fWorld, _fMainWorld;
        private static UnityEngine.Transform _worldAnchor;
        private static visszarako _mainWorld;
        private static bool _aliasLooked;
        private static bool _loadSignal;   // a save was chosen since the last look (the flag may be set and cleared between looks)

        // The two game calls that set DataFromMenuScript.load: the save screen's Load (an existing file) and the menu's
        // Continue. Not SetLoad itself: a one-line setter the JIT inlines into them, so a patch on it never runs (2026-11-11:
        // a reloaded save read load=false with it).
        [HarmonyLib.HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.Load))]
        private static class LoadChosen { [HarmonyLib.HarmonyPrefix] private static void Prefix(string _name) { if (File.Exists(_name)) _loadSignal = true; } }

        [HarmonyLib.HarmonyPatch(typeof(menuhandler), nameof(menuhandler.AutoContinue))]
        private static class ContinueChosen { [HarmonyLib.HarmonyPrefix] private static void Prefix() => _loadSignal = true; }

        /// The public game's mainscript.seed / load / napszak (the preloader adds them, NotSerialized) set from the beta's
        /// world seed, whether the world came from a save, and its day/night cycle.
        static partial void GameAliases(bool worldUp)
        {
            if (!GameAliasesOn) return;
            if (!_aliasLooked)
            {
                _aliasLooked = true;
                const BindingFlags f = BindingFlags.Public | BindingFlags.Instance;
                _fSeed = GameLookups.ReflectionField(typeof(mainscript), "seed", f); _fLoad = GameLookups.ReflectionField(typeof(mainscript), "load", f); _fNapszak = GameLookups.ReflectionField(typeof(mainscript), "napszak", f);
                _fLoaded = GameLookups.ReflectionField(typeof(mainscript), "loaded", f); _fMulti = GameLookups.ReflectionField(typeof(mainscript), "multi", f); _fWorld = GameLookups.ReflectionField(typeof(mainscript), "world", f);
                _fMainWorld = GameLookups.ReflectionField(typeof(mainscript), "mainWorld", f);
            }
            var dm = DataFromMenuScript.s;
            bool signal = _loadSignal; _loadSignal = false;
            bool load = FromSaveLatch(_aliasWasUp, worldUp, (dm != null && dm.load) || signal, Net.Mp.Client != null, ref _loadPending, ref _fromSave);
            _aliasWasUp = worldUp;
            var m = mainscript.s;
            if (m == null) return;
            try
            {
                if (_fSeed != null && dm != null) _fSeed.SetValue(m, dm.seed);
                if (_fLoad != null) _fLoad.SetValue(m, load);
                if (_fNapszak != null && !ReferenceEquals(_fNapszak.GetValue(m), napszakvaltakozas.s)) _fNapszak.SetValue(m, napszakvaltakozas.s);
                // before this frame's OnLoad: false through it and its frame's Update, true from the next frame
                if (_fLoaded != null) _fLoaded.SetValue(m, worldUp && _gameLoadedCalled);
                if (_fMulti != null) _fMulti.SetValue(m, Net.Entities.InSession);
                if ((_fWorld != null || _fMainWorld != null) && worldUp)
                {
                    if (_worldAnchor == null)
                    {
                        _worldAnchor = new UnityEngine.GameObject("RevaMP mainscript.world (TLD Loader mods)").transform;
                        // the public game's mainWorld: its coord is the world offset. Started already, so the game never
                        // lists it among the objects it moves back (this anchor follows the offset below)
                        _mainWorld = _worldAnchor.gameObject.AddComponent<visszarako>();
                        _mainWorld.started = true; _mainWorld.dont = true; _mainWorld.dontlookUnderMap = true;
                    }
                    if (_fWorld != null && !ReferenceEquals(_fWorld.GetValue(m), _worldAnchor)) _fWorld.SetValue(m, _worldAnchor);
                    if (_fMainWorld != null && !ReferenceEquals(_fMainWorld.GetValue(m), _mainWorld)) _fMainWorld.SetValue(m, _mainWorld);
                    var o = m.visszarakva;   // global = unity - offset: world.InverseTransformPoint(p) = p - offset
                    _worldAnchor.position = new UnityEngine.Vector3((float)o.x, (float)o.y, (float)o.z);
                    _mainWorld.coord = o;
                }
            }
            catch (Exception e) { GameAliasesOn = false; Plugin.Log.LogWarning("ModHost: mainscript.seed/load/napszak/loaded/multi/world/mainWorld: " + e.Message); }
        }

        public static string AliasStatus()
        {
            var aliases = new System.Collections.Generic.List<string>();
            foreach (var a in new[] { "mainscript.M -> mainscript.s (host mirror)", "itemdatabase.d -> itemdatabase.s (host mirror)",
                                     "savedatascript.d -> savedatascript.s (host mirror)", "mainscript.seed -> DataFromMenuScript.s.seed (host snapshot)",
                                     "mainscript.load -> from-save latch (host snapshot)", "mainscript.napszak -> napszakvaltakozas.s (host mirror)",
                                     "mainscript.loaded -> world up and OnLoad done", "mainscript.multi -> RevaMP session", "mainscript.world -> a transform at the world offset",
                                     "mainscript.mainWorld -> a visszarako whose coord is the world offset" })
                aliases.Add(Json.Str(a));
            foreach (var a in LoaderRetarget.AliasDescriptions()) aliases.Add(Json.Str(a));
            string list = "\"aliases\":[" + string.Join(",", aliases.ToArray()) + "],\"rewrites\":" + (Retarget ? "true" : "false") + ",";
            var m = mainscript.s;
            if (m == null) return "{" + list + "\"world\":false}";
            return "{" + list + "\"seed\":" + (_fSeed?.GetValue(m) ?? "null") + ",\"load\":" + ((_fLoad?.GetValue(m) as bool?) == true ? "true" : "false") +
                   ",\"napszak\":" + (_fNapszak?.GetValue(m) != null ? "true" : "false") + ",\"gameSeed\":" + (DataFromMenuScript.s?.seed.ToString() ?? "null") + "}";
        }

        /// UseHarmony, safely: all of a mod's patches or none. TLD Loader runs PatchAll on the mod's assembly; on the beta a
        /// mod written for the public game breaks the game itself that way:
        /// - a patch whose code names a class that can't load here throws inside the game's own method on every call
        ///   (Car Batteries: ~9,600 exceptions in carscript.Electronics/LEDUPD, enginescript.Update,
        ///   headlightscript.RefreshLights in one test run);
        /// - PatchAll stops at the first patch whose target is gone and leaves the ones before it applied; patching the rest
        ///   class by class instead (tried 2026-11-11) applied a set its author never ran together and World Tweaker's hung
        ///   the game at start-up, every time.
        /// So nothing is applied until every patch class passes: its methods compile (a missing type or member fails here,
        /// not inside the game) and its target, where the attributes name one, exists on the beta. One failure: none applied,
        /// and the log names it. Then all are applied; if one still throws, all are undone.
        static partial void PatchModSafely(Loaded l)
        {
            if (l.BrokenTypes > 0) { Refuse(l, l.BrokenTypes + " of its types can't load on the beta"); return; }
            var asm = l.Mod.GetType().Assembly;
            var classes = new System.Collections.Generic.List<Type>();
            foreach (var t in HarmonyLib.AccessTools.GetTypesFromAssembly(asm))
            {
                if (t == null) continue;
                System.Collections.Generic.List<HarmonyLib.HarmonyMethod> attrs;
                try { attrs = HarmonyLib.HarmonyMethodExtensions.GetFromType(t); } catch (Exception e) { Refuse(l, t.FullName + ": " + e.Message); return; }
                if (attrs == null || attrs.Count == 0) continue;
                string why = Check(t, HarmonyLib.HarmonyMethod.Merge(attrs));
                if (why != null) { Refuse(l, t.FullName + ": " + why); return; }
                classes.Add(t);
            }
            var h = new HarmonyLib.Harmony("com." + l.Id.ToLowerInvariant());
            try { foreach (var t in classes) h.CreateClassProcessor(t).Patch(); }
            catch (Exception e)
            {
                try { h.UnpatchSelf(); } catch (System.Exception quietError) { Diagnostics.QuietErrors.Once("ModHost.RollbackPatches", quietError); }
                Refuse(l, "patching threw, all undone: " + (e.InnerException ?? e).Message);
                return;
            }
            l.Mod.GetType().BaseType?.GetField("HarmonyInstance")?.SetValue(l.Mod, h);
            HarmonyModsPatched++; HarmonyClassesPatched += classes.Count;
            Plugin.Log.LogInfo("ModHost: " + l.Id + ": Harmony (UseHarmony): all " + classes.Count + " patch class(es) applied");
        }

        public static int HarmonyModsPatched, HarmonyClassesPatched, HarmonyModsRefused;

        private static void Refuse(Loaded l, string why)
        {
            HarmonyModsRefused++;
            Plugin.Log.LogWarning("ModHost: " + l.Id + " asks for Harmony (UseHarmony); none of its patches applied: " + why);
        }

        /// null = fine: the class's methods compile, and the target its attributes name exists here.
        private static string Check(Type t, HarmonyLib.HarmonyMethod m)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            try
            {
                foreach (var me in t.GetMethods(all))
                    if (!me.IsAbstract && !me.ContainsGenericParameters) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(me.MethodHandle);
            }
            catch (Exception e) { return "its code doesn't compile here: " + (e.InnerException ?? e).Message; }
            // a class that finds its target itself (TargetMethod/TargetMethods) is checked by patching it
            if (m == null || m.declaringType == null || HarmonyLib.AccessTools.Method(t, "TargetMethod") != null || HarmonyLib.AccessTools.Method(t, "TargetMethods") != null) return null;
            try
            {
                var dt = m.declaringType; var args = m.argumentTypes;
                object target;
                switch (m.methodType ?? HarmonyLib.MethodType.Normal)
                {
                    case HarmonyLib.MethodType.Getter: target = GameLookups.RuntimeHarmonyGetter(dt, m.methodName); break;
                    case HarmonyLib.MethodType.Setter: target = GameLookups.RuntimeHarmonySetter(dt, m.methodName); break;
                    case HarmonyLib.MethodType.Constructor: target = GameLookups.RuntimeHarmonyConstructor(dt, args); break;
                    case HarmonyLib.MethodType.StaticConstructor: target = dt.TypeInitializer; break;
                    default: target = m.methodName == null ? (object)dt : GameLookups.RuntimeHarmonyMethod(dt, m.methodName, args); break;
                }
                return target == null ? "its target " + dt.Name + "." + m.methodName + " doesn't exist on the beta" : null;
            }
            catch (Exception e) { return "its target can't be found: " + e.Message; }
        }

        /// TLD Loader's Events, raised from the same game calls TLD Loader patches: sitting down / standing up
        /// (seatscript.GetIn/GetOut), saving, loading, deleting a save, and the pause menu (mainscript.SetCursorVisible).
        internal static class Events
        {
            private static MethodInfo _sit, _stand, _save, _load, _delete, _pause, _unpause;
            public static long Raised;
            internal static void Bind(Assembly stub)
            {
                var t = stub.GetType("TLDLoader.Events");
                if (t == null) return;
                const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Static;
                _sit = t.GetMethod("RaiseOnSit", f); _stand = t.GetMethod("RaiseOnStand", f); _save = t.GetMethod("RaiseOnSave", f);
                _load = t.GetMethod("RaiseOnSaveLoad", f); _delete = t.GetMethod("RaiseOnSaveDelete", f);
                _pause = t.GetMethod("RaiseOnPause", f); _unpause = t.GetMethod("RaiseOnUnpause", f);
            }
            private static void Raise(MethodInfo m, params object[] a)
            {
                if (m == null || Mods.Count == 0) return;
                try { m.Invoke(null, a); Raised++; } catch (Exception e) { Plugin.Log.LogWarning("ModHost: TLD Loader event " + m.Name + ": " + (e.InnerException ?? e).Message); }
            }

            [HarmonyLib.HarmonyPatch(typeof(seatscript), nameof(seatscript.GetIn))]
            private static class Sit { [HarmonyLib.HarmonyPrefix] private static void Prefix(seatscript __instance) => Raise(_sit, __instance); }

            [HarmonyLib.HarmonyPatch(typeof(seatscript), nameof(seatscript.GetOut))]
            private static class Stand { [HarmonyLib.HarmonyPrefix] private static void Prefix(seatscript __instance) => Raise(_stand, __instance); }

            [HarmonyLib.HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.Save))]
            private static class Save { [HarmonyLib.HarmonyPostfix] private static void Postfix(string _text, bool __runOriginal) { if (__runOriginal) Raise(_save, _text); } }

            [HarmonyLib.HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.Load))]
            private static class Load { [HarmonyLib.HarmonyPostfix] private static void Postfix(string _name) { if (File.Exists(_name)) Raise(_load, _name); } }

            [HarmonyLib.HarmonyPatch(typeof(newSaveScreenScript), nameof(newSaveScreenScript.PressedDeleteYes))]
            private static class Delete
            {
                [HarmonyLib.HarmonyPrefix]
                private static void Prefix(newSaveScreenScript __instance)
                {
                    if (_delete == null || Mods.Count == 0) return;
                    try
                    {
                        // the beta's own choice in PressedDeleteYes: an autosave slot, or the selected save
                        var which = GameLookups.FieldValue<bool>(typeof(newSaveScreenScript), "BautoSaveSelected", __instance)
                            ? __instance.AutoSaveReferences[__instance.selectedAutoSaveIndex]
                            : __instance.LSaveReferences[GameLookups.FieldValue<int>(typeof(newSaveScreenScript), "CurrentSelected", __instance)];
                        if (which != null) Raise(_delete, which.name);
                    }
                    catch (System.Exception quietError) { Diagnostics.QuietErrors.Once("ModHost.DeleteSave", quietError); }
                }
            }

            /// The pause menu opening and closing, once each. mainscript.Update calls SetCursorVisible every frame, so raising
            /// on each call fired OnUnpause ~60 times a second while playing (OnPause while paused); its argument isn't the
            /// pause either (FreeCursorScreens frees the cursor under the join screen). The game's own pauseMenuOpen is read
            /// there instead, and only a change raises. A new world starts from its own state, with nothing raised.
            private static mainscript _pauseWorld;
            private static bool _paused;
            internal static long PauseChecks;

            [HarmonyLib.HarmonyPatch(typeof(mainscript), nameof(mainscript.SetCursorVisible))]
            private static class Pause
            {
                [HarmonyLib.HarmonyPrefix]
                private static void Prefix(mainscript __instance)
                {
                    PauseChecks++;
                    bool now = __instance.pauseMenuOpen;
                    if (!ReferenceEquals(__instance, _pauseWorld)) { _pauseWorld = __instance; _paused = now; return; }
                    if (now == _paused) return;
                    _paused = now;
                    Raise(now ? _pause : _unpause);
                }
            }
        }

    }
}
