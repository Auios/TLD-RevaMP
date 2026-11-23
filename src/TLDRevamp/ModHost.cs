using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TLDRevamp
{
    /// Runs TLDLoader mods under BepInEx on the beta (TLDLoader itself doesn't work there). The mods' DLLs go in
    /// BepInEx/TLDLoaderMods/; the stub TLDLoader.dll next to our plugin provides the API they reference (Mod, its
    /// logger, the config folder). Lifecycle as TLDLoader's: OnMenuLoad in the main menu (mods with LoadInMenu), OnLoad
    /// once a game world is up (the player exists), OnUnload when that world ends, Config for the mod's own settings
    /// window, and Update/FixedUpdate/OnGUI for mods that override them.
    ///
    /// Everything goes through reflection: TLDRevamp has no compile-time reference to the stub, so a missing stub or a
    /// broken mod can never stop TLDRevamp's own types (and patches) from loading.
    public static partial class ModHost
    {
        private sealed class Loaded
        {
            public object Mod; public string Id, Name, Version, File, Hash;   // Hash: SHA-256 of the DLL (the join check)
            public MethodInfo OnMenuLoad, OnLoad, OnUnload, Config, Update, FixedUpdate, OnGUI;   // null: not overridden
            public MethodInfo DbLoad, DbLoadOld;   // with LoadInDB: DbLoad and the older dbLoad, both as TLDLoader calls them
            public string LastError;
            public bool Menu;   // LoadInMenu: its Update and OnGUI run in the menu too
            public int BrokenTypes;   // types in its DLL that failed to load on the beta (UseHarmony is not applied then)
        }

        private static readonly List<Loaded> Mods = new List<Loaded>();
        private static bool _started, _worldUp, _gameLoadedCalled, _menuCalled, _unloadCalled;
        private static object _worldObj;
        public static int WorldsReplaced;
        public static string Info = "not started";
        public static int DbLoadRuns;
        private static itemdatabase _dbFor;
        private static object _aliasM, _aliasD, _aliasSd;

        public static string ModsDir => Path.Combine(BepInEx.Paths.BepInExRootPath, "TLDLoaderMods");

        public static void Start()
        {
            if (_started) return;
            _started = true;
            try
            {
                if (!Directory.Exists(ModsDir)) { Info = "no " + ModsDir; return; }
                var dlls = Directory.GetFiles(ModsDir, "*.dll");
                if (dlls.Length == 0) { Info = "no mods in " + ModsDir; return; }
                string stubPath = Path.Combine(Path.GetDirectoryName(typeof(ModHost).Assembly.Location), "TLDLoader.dll");
                if (!File.Exists(stubPath)) { Info = "stub TLDLoader.dll missing next to TLDRevamp.dll"; Plugin.Log.LogWarning("ModHost: " + Info); return; }
                var stub = Assembly.LoadFrom(stubPath);
                var modBase = stub.GetType("TLDLoader.Mod");
                stub.GetType("TLDLoader.ModLoader").GetField("ConfigRoot").SetValue(null, Path.Combine(BepInEx.Paths.ConfigPath, "TLDLoaderMods"));
                stub.GetType("TLDLoader.ModLoader").GetField("ModsFolder")?.SetValue(null, ModsDir);   // assets: <ModsDir>/Assets/<ID>
                var hostLogger = stub.GetType("TLDLoader.HostLogger");
                Loader = stub.GetType("TLDLoader.ModLoader");
                var gameLog = BepInEx.Logging.Logger.CreateLogSource("TLD Loader mods");
                Loader.GetField("Echo")?.SetValue(null, (Action<string, int>)((m, lv) => { if (lv == 2) gameLog.LogError(m); else if (lv == 1) gameLog.LogWarning(m); else gameLog.LogInfo(m); }));
                MakeLoadingPanel();
                BindEvents(stub);
                foreach (var dll in dlls)
                {
                    // A DLL whose types partly fail to load (one names a game type the beta lacks) still offers the rest:
                    // before 2026-11-11 the whole DLL was skipped, and a type whose check threw stopped every mod after it
                    // (Backpack Mod: "Could not load type of field 'Amt.ModItemsSpawner:_building'" - no Loader mod loaded).
                    Type[] types; int broken = 0;
                    try { types = Initialized(LoadMod(dll), dll).GetTypes(); }
                    catch (ReflectionTypeLoadException e)
                    {
                        types = Array.FindAll(e.Types ?? new Type[0], x => x != null);
                        broken += (e.Types?.Length ?? 0) - types.Length;
                        Plugin.Log.LogWarning("ModHost: " + Path.GetFileName(dll) + ": " + ((e.Types?.Length ?? 0) - types.Length) + " type(s) failed to load, the other " + types.Length + " are used: " + (e.LoaderExceptions.Length > 0 ? e.LoaderExceptions[0]?.Message : ""));
                    }
                    catch (Exception e) { Plugin.Log.LogError("ModHost: " + Path.GetFileName(dll) + " failed to load: " + e.Message); continue; }
                    // every type checked first, so a mod knows how many of its DLL's types failed before it starts
                    var modTypes = new List<Type>();
                    foreach (var t in types)
                    {
                        try { if (!t.IsAbstract && modBase.IsAssignableFrom(t)) modTypes.Add(t); }
                        catch (Exception e) { broken++; Plugin.Log.LogWarning("ModHost: " + Path.GetFileName(dll) + ": type " + t.FullName + " skipped: " + e.Message); }
                    }
                    foreach (var t in modTypes)
                    {
                        try
                        {
                            var mod = Activator.CreateInstance(t);
                            var l = new Loaded { Mod = mod, File = Path.GetFileName(dll), BrokenTypes = broken, Hash = FileHash(dll) };
                            l.Id = (string)modBase.GetProperty("ID").GetValue(mod);
                            l.Name = (string)modBase.GetProperty("Name").GetValue(mod);
                            l.Version = (string)modBase.GetProperty("Version").GetValue(mod);
                            var log = BepInEx.Logging.Logger.CreateLogSource(l.Id);
                            // UseLogger: the mod's own log file (TLDLoader.Logger, echoed to BepInEx's log); otherwise BepInEx's
                            object logger = null;
                            if (Flag(modBase, mod, "UseLogger"))
                                try { logger = Activator.CreateInstance(stub.GetType("TLDLoader.Logger"), mod); }
                                catch (Exception e) { Plugin.Log.LogWarning("ModHost: " + l.Id + ": its log file: " + (e.InnerException ?? e).Message); }
                            modBase.GetProperty("Logger").SetValue(mod, logger ?? Activator.CreateInstance(hostLogger,
                                (Action<string>)(m => log.LogInfo(m)), (Action<string>)(m => log.LogWarning(m)), (Action<string>)(m => log.LogError(m))));
                            Loader.GetMethod("HostAdd", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, new[] { mod });
                            bool menu = (bool)modBase.GetProperty("LoadInMenu").GetValue(mod);
                            // Every lifecycle method is bound the same way: the mod's own
                            // override, or nothing.  (OnMenuLoad additionally needs the mod
                            // to ask for it with LoadInMenu.)  The base class's methods are
                            // empty, so calling them would be a no-op; not binding them
                            // keeps "did the mod override it" answerable.
                            l.Menu = menu;
                            l.OnMenuLoad = menu ? Overridden(t, modBase, "OnMenuLoad") : null;
                            l.OnLoad = Overridden(t, modBase, "OnLoad") ?? modBase.GetMethod("OnLoad");
                            l.OnUnload = Overridden(t, modBase, "OnUnload");
                            l.Config = Overridden(t, modBase, "Config");
                            l.Update = Overridden(t, modBase, "Update");
                            l.FixedUpdate = Overridden(t, modBase, "FixedUpdate");
                            l.OnGUI = Overridden(t, modBase, "OnGUI");
                            if (modBase.GetProperty("LoadInDB") is PropertyInfo inDb && (bool)inDb.GetValue(mod))
                            {
                                l.DbLoad = Overridden(t, modBase, "DbLoad");
                                l.DbLoadOld = Overridden(t, modBase, "dbLoad");
                            }
                            Mods.Add(l);
                            Plugin.Log.LogInfo($"ModHost: loaded {l.Name} {l.Version} ({l.Id}) from {l.File}");
                            StartLikeLoader(l, modBase);
                        }
                        catch (Exception e) { Plugin.Log.LogError("ModHost: creating " + t.FullName + " failed: " + (e.InnerException ?? e)); }
                    }
                }
                Info = Mods.Count + " mod(s)";
                DevHooks.ModsLoaded?.Invoke();   // test tooling: the autopilot port made aware of other players
            }
            catch (Exception e) { Info = "failed: " + e.Message; Plugin.Log.LogError("ModHost: " + e); }
        }

        /// A/B (config Mods.RetargetLoaderMods, read before the mods load): false = before 2026-11-11, every mod loaded
        /// as it is, without LoaderRetarget's integer-sign fixes or public-name adapters.
        public static bool Retarget = true;
        /// A/B (config Mods.LoaderCompat, read before the mods load): false = before 2026-11-23, no LoaderCompat helpers for
        /// the public game's save ids and attach members (issue 44: dingus, Nitrous, Attach Everything).
        public static bool Compat = true;
        public static int Retargeted;
        static partial void CompatType(ref Type type);   // ModHost.Game.cs: LoaderCompat
        private static Type CompatHelpers() { Type t = null; if (Compat) CompatType(ref t); return t; }

        private static Assembly LoadMod(string dll)
        {
            if (!Retarget) return Assembly.LoadFrom(dll);
            var asm = LoaderRetarget.Load(dll, typeof(mainscript).Assembly,
                new[] { ModsDir, BepInEx.Paths.ManagedPath, BepInEx.Paths.BepInExAssemblyDirectory, Path.GetDirectoryName(typeof(ModHost).Assembly.Location) },
                m => Plugin.Log.LogInfo("ModHost: " + m), CompatHelpers());
            if (string.IsNullOrEmpty(asm.Location)) Retargeted++;
            return asm;
        }
        /// The DLL's module initializer first. Mods built with Costura carry their libraries inside (Newtonsoft.Json, NLayer:
        /// M-'s Achievements, Better Radio, The Long Who, World Tweaker, M-ultiTool) and hook the resolver there; the type
        /// check below otherwise found their types unloadable before any of the mod's code ran, and they stayed so.
        private static Assembly Initialized(Assembly asm, string dll)
        {
            try { System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(asm.ManifestModule.ModuleHandle); }
            catch (Exception e) { Plugin.Log.LogWarning("ModHost: " + Path.GetFileName(dll) + ": its module initializer: " + (e.InnerException ?? e).Message); }
            return asm;
        }
        private static Type Loader;
        private static MethodInfo _hostScene;
        public static int HarmonyPatched, ConfigsAtLoad, ConfigsAtLoadFailed;

        private static bool Flag(Type modBase, object mod, string name)
        {
            try { return modBase.GetProperty(name) is PropertyInfo p && (bool)p.GetValue(mod); }
            catch { return false; }
        }

        /// What TLD Loader does with a mod right after creating it, in its order: Config() once with the settings window
        /// closed (a SettingAPI mod reads its saved values there; a mod that draws directly throws, as under TLD Loader,
        /// and is told once), then UseHarmony (ModHost.Game.cs: guarded, not TLD Loader's plain PatchAll). OnMenuLoad comes later.
        private static void StartLikeLoader(Loaded l, Type modBase)
        {
            var pass = Loader.GetMethod("HostConfigPass", BindingFlags.NonPublic | BindingFlags.Static);
            if (l.Config != null && pass != null)
            {
                pass.Invoke(null, new[] { l.Mod, (object)false });
                try { l.Config.Invoke(l.Mod, null); ConfigsAtLoad++; }
                catch (Exception e) { ConfigsAtLoadFailed++; Plugin.Log.LogInfo("ModHost: " + l.Id + ": its Config() at load (settings not read there): " + (e.InnerException ?? e).Message); }
                finally { pass.Invoke(null, new[] { l.Mod, (object)true }); }
            }
            if (Flag(modBase, l.Mod, "UseHarmony")) PatchModSafely(l);   // ModHost.Game.cs: per patch class, checked first
        }

        // the game-only parts (ModHost.Game.cs; not in the unit test's build): the loading panel and TLD Loader's events
        static partial void MakeLoadingPanel();
        static partial void BindEvents(Assembly stub);
        static partial void PatchModSafely(Loaded l);
        static partial void GameAliases(bool worldUp);

        /// Was the world now up loaded from a save? The game's own flag (DataFromMenuScript.load) is set when a save is
        /// chosen - while the old world is still up when loading from inside one - and cleared on the new world's first
        /// frame (mapSettingScript.Update), which can be before the mods' OnLoad, where they read mainscript.M.load to skip
        /// their new-world work: a save would get that work twice. So a load seen at any time (the flag, or the game's
        /// save screen's Load or the menu's Continue since the last look) is pending until the next world comes up, and decides that world: true from
        /// then until it ends. A joiner's world is the host's, never new: always true there.
        internal static bool FromSaveLatch(bool wasUp, bool worldUp, bool loadSeen, bool joiner, ref bool pending, ref bool fromSave)
        {
            if (loadSeen) pending = true;
            if (wasUp && !worldUp) fromSave = false;
            if (!wasUp && worldUp) { fromSave = pending; pending = false; }
            return joiner || fromSave;
        }

        private static void Scene(bool inWorld)
        {
            if (Loader == null) return;
            if (_hostScene == null) _hostScene = Loader.GetMethod("HostScene", BindingFlags.NonPublic | BindingFlags.Static);
            try { _hostScene?.Invoke(null, new object[] { inWorld }); } catch (System.Exception quietError) { Diagnostics.QuietErrors.Once("ModHost.Scene", quietError); }
        }

        private static MethodInfo Overridden(Type t, Type modBase, string name)
        {
            // A lookup that throws (an odd declaration) must not cost the mod its
            // whole registration: the caller is inside a try, but a throw here would
            // skip the rest of the mod's setup.
            try
            {
                var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                return m != null && m.DeclaringType != modBase ? m : null;
            }
            catch { return null; }
        }

        private static void Call(Loaded l, MethodInfo m, string what)
        {
            if (m == null) return;
            try { m.Invoke(l.Mod, null); }
            catch (Exception e)
            {
                var inner = e.InnerException ?? e;
                string err = what + ": " + inner.Message;
                // once, not per frame; the stack says which line of the mod (the error alone doesn't)
                if (err != l.LastError) { l.LastError = err; Plugin.Log.LogError("ModHost: " + l.Id + " " + err + "\n" + inner.StackTrace); }
            }
        }

        /// Per frame (Runner.Update): menu/game transitions, then the mods' own Update.
        /// The beta's item database has empty slots: filled for the mods' DbLoad (ModHost.Game.cs). A/B: false = before 2026-11-23.
        public static bool FillEmptySlots = true;
        public static int EmptySlotsFilled;
        static partial void FillHoles();
        static partial void RestoreHoles();

        public static void Tick()
        {
            if (Mods.Count == 0) return;
            PublicNames();
            // DbLoad: once per item database (a new one comes with each scene load), before a world can place items —
            // the database is up in the menu already. Mods change the game's item prefabs or add their own there. The
            // order is the mods' IDs (ordinal), the same on every machine: what one mod does can depend on another's.
            if (itemdatabase.s != null && itemdatabase.s != _dbFor)
            {
                _dbFor = itemdatabase.s;
                DbLoadRuns++;
                var order = new List<Loaded>(Mods);
                order.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                if (FillEmptySlots) FillHoles();
                try { foreach (var l in order) { Call(l, l.DbLoad, "DbLoad"); Call(l, l.DbLoadOld, "dbLoad"); } }
                finally { if (FillEmptySlots) RestoreHoles(); }
            }
            // The world's own signal is the PLAYER, not the scene name: the game loads
            // scenes for a restart and for a new game, and the active scene is briefly
            // something else while it does. Keying the unload on the scene would fire
            // OnUnload for a scene change that does not end the world. The player
            // appears with the world and is gone when it ends, which is what OnLoad
            // and OnUnload are about.
            bool worldUp = mainscript.s != null && mainscript.s.player != null;
            // Another world in this one's place between two ticks: a save loaded from inside a world (the save screen's
            // Load reloads the scene, and the new world's player exists before the next tick). The player check alone
            // saw no change: before 2026-11-11 the mods got no OnUnload for the old world and no OnLoad for the new one.
            object worldNow = worldUp ? (object)mainscript.s : null;
            bool replaced = worldUp && _worldUp && !ReferenceEquals(worldNow, _worldObj);
            _worldObj = worldNow;
            if (replaced)
            {
                WorldsReplaced++;
                _unloadCalled = true; foreach (var l in Mods) Call(l, l.OnUnload, "OnUnload");
                _gameLoadedCalled = false; _menuCalled = false;
                GameAliases(false);   // the old world ended (for mainscript.M.load)
            }
            if (worldUp != _worldUp)
            {
                bool wasUp = _worldUp;
                _worldUp = worldUp;
                Scene(worldUp);   // ModLoader.isOnMainMenu / GetCurrentScene
                _gameLoadedCalled = false;
                _menuCalled = false;
                // Leaving the world unloads the mod: TLDLoader calls OnUnload when the
                // game it loaded into goes away, so the mod can undo what it did.
                if (wasUp && !worldUp) { _unloadCalled = true; foreach (var l in Mods) Call(l, l.OnUnload, "OnUnload"); }
            }
            // The menu is the game's own main-menu scene; the world is up whenever the
            // player exists. In the menu the player does not exist, so a mod that asked
            // for the menu gets its OnMenuLoad there.
            GameAliases(worldUp);   // ModHost.Game.cs: mainscript.M.seed/load/napszak, before the mods' OnLoad below
            bool inMenu = !worldUp && !_gameLoadedCalled && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != menuhandler.sceneName;
            if (inMenu && !_menuCalled) { _menuCalled = true; foreach (var l in Mods) Call(l, l.OnMenuLoad, "OnMenuLoad"); }
            // OnLoad once the world is actually up (the player exists), as TLDLoader calls it after the game loaded
            if (worldUp && !_gameLoadedCalled)
            {
                _gameLoadedCalled = true;
                _unloadCalled = false;
                foreach (var l in Mods) Call(l, l.OnLoad, "OnLoad");
            }
            // TLDLoader runs a mod's Update in a world only, unless it asked for the menu (LoadInMenu): mods read
            // mainscript.M.player there with no null check (FastPedal, Fugitive). Before 2026-11-23 every mod's ran in the menu.
            foreach (var l in Mods) if (worldUp || l.Menu) Call(l, l.Update, "Update");
            DevHooks.ModsTick?.Invoke();
        }

        /// The public game's names for the singletons (TLDRevamp.Preloader.LoaderAliases adds the fields) pointed at the
        /// beta's, whenever one of them is a new object. Without the preloader the fields don't exist: nothing to do.
        private static void PublicNames()
        {
            Alias(typeof(mainscript), "M", mainscript.s, ref _aliasM);
            Alias(typeof(itemdatabase), "d", itemdatabase.s, ref _aliasD);
            Alias(typeof(savedatascript), "d", savedatascript.s, ref _aliasSd);
        }
        private static void Alias(Type type, string name, object now, ref object last)
        {
            if (ReferenceEquals(now, last)) return;
            last = now;
            try { type.GetField(name, BindingFlags.Public | BindingFlags.Static)?.SetValue(null, now); }
            catch (Exception e) { Plugin.Log.LogWarning("ModHost: " + type.Name + "." + name + ": " + e.Message); }
        }

        /// The game is quitting or the plugin is going away: the world ends without a
        /// scene change, so OnUnload has to be called here or the mods never get to
        /// undo what they did.
        public static void Shutdown()
        {
            if (!_worldUp) return;
            _worldUp = false;
            _gameLoadedCalled = false;
            _unloadCalled = true;
            foreach (var l in Mods) Call(l, l.OnUnload, "OnUnload");
        }

        /// As TLDLoader: FixedUpdate once the mods' OnLoad ran in a world, OnGUI then too or in the menu for a LoadInMenu mod.
        public static void FixedTick() { if (!ModsLoaded()) return; foreach (var l in Mods) Call(l, l.FixedUpdate, "FixedUpdate"); }
        public static void Gui() { bool loaded = ModsLoaded(); foreach (var l in Mods) if (loaded || l.Menu) Call(l, l.OnGUI, "OnGUI"); }
        private static bool ModsLoaded() => _worldUp && _gameLoadedCalled;

        /// Set by the settings screen while it is open: a Loader mod's Config() draws
        /// its own window, and it must only be called then.  Unity runs the IMGUI
        /// pass several times per frame, so calling Config() unconditionally would
        /// run a mod's settings code many times a second for as long as the game is
        /// open - and for a mod that does one-time work in Config() (creating
        /// objects, reading files) that is a real bug, not a detail.
        public static bool ConfigScreenOpen;

        /// The mod's own settings window.  TLDLoader mods draw it with IMGUI
        /// (GUI.*) inside Config(), so the host calls it in its own IMGUI pass, and
        /// only while the settings screen is open.  The mod draws where it wants; a
        /// mod that ignores ConfigScrollHeight still works, because the host does
        /// not reserve an area for it.
        public static void DrawConfigs()
        {
            if (!ConfigScreenOpen) return;
            foreach (var l in Mods) Call(l, l.Config, "Config");
        }

        /// The settings screen may be closed without a tab press (the back button),
        /// so the flag is refreshed from the screen's own state every frame instead
        /// of only when a tab is pressed.  A Loader mod's Config() must not run
        /// while no settings screen is open.
        public static void RefreshConfigScreen()
        {
            bool open = false;
            try
            {
                var s = settingsscript.s;
                if (s != null)
                {
                    var root = s.transform.Find("NewSettings");
                    if (root != null)
                    {
                        var tab = root.Find("TabRevamp");
                        open = tab != null && tab.gameObject.activeInHierarchy;
                    }
                }
            }
            catch (System.Exception quietError) { Diagnostics.QuietErrors.Once("ModHost.ConfigScreen", quietError); }
            ConfigScreenOpen = open;
        }

        /// Whether any mod wants a settings window of its own (so the tab can make room).
        public static bool AnyConfig => Mods.Exists(l => l.Config != null);

        /// Whether OnUnload has run since the last OnLoad (the status line reports it).
        public static bool UnloadCalled => _unloadCalled;

        /// Host: refuse joiners whose TLD Loader mods differ (config Mods.LoaderModsMustMatch).
        public static bool MustMatch = true;
        public static int JoinsRefused, JoinsAdmittedDiffering;

        /// Every TLD Loader mod running here, as the join check compares them (Net/LoaderModCheck).
        public static List<Net.LoaderModCheck.Entry> JoinList()
        {
            var list = new List<Net.LoaderModCheck.Entry>();
            foreach (var l in Mods) list.Add(new Net.LoaderModCheck.Entry(l.Id, l.Name, l.Version, l.Hash));
            return list;
        }

        private static readonly Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string FileHash(string path)
        {
            if (Hashes.TryGetValue(path, out var h)) return h;
            try { using (var sha = System.Security.Cryptography.SHA256.Create()) h = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
            catch (Exception e) { h = ""; Plugin.Log.LogWarning("ModHost: hashing " + Path.GetFileName(path) + ": " + e.Message); }
            return Hashes[path] = h;
        }

        public static string Status()
        {
            var rows = new List<string>();
            foreach (var l in Mods) rows.Add("{\"id\":" + Json.Str(l.Id) + ",\"name\":" + Json.Str(l.Name) + ",\"version\":" + Json.Str(l.Version) + ",\"hash\":" + Json.Str(l.Hash ?? "") + ",\"lastError\":" + Json.Str(l.LastError ?? "") + "}");
            return "{\"info\":" + Json.Str(Info) + ",\"retargeted\":" + Retargeted + ",\"mustMatch\":" + (MustMatch ? "true" : "false") +
                   ",\"joinsRefused\":" + JoinsRefused + ",\"joinsAdmittedDiffering\":" + JoinsAdmittedDiffering + ",\"inGame\":" + (_worldUp ? "true" : "false") + ",\"onLoadCalled\":" + (_gameLoadedCalled ? "true" : "false") +
                   ",\"unloadedSinceLastLoad\":" + (_unloadCalled ? "true" : "false") + ",\"hasConfig\":" + (AnyConfig ? "true" : "false") + ",\"mods\":[" + string.Join(",", rows) + "]}";
        }
    }
}
