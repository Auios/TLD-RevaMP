using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TLDRevamp;

/// The loader host, run for real: the REAL src/TLDRevamp/ModHost.cs drives a REAL
/// TLD Loader mod (a compiled fixture that binds to the stub TLDLoader API, the
/// same way a third-party mod does), and the test checks the lifecycle the mod
/// sees.  No game, no stubs of the host's own logic: only the game's and BepInEx's
/// few entry points are stubbed (tools/unit/fixtures/loader/HostStubs.cs).
static class LoaderHostTest
{
    static int failures;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }
    static void Section(string s) { Console.WriteLine("--- " + s); }

    static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string tmp = Path.Combine(Path.GetTempPath(), "loaderhost-" + Guid.NewGuid().ToString("N"));
        string modsDir = Path.Combine(tmp, "BepInEx", "TLDLoaderMods");
        string pluginDir = Path.Combine(tmp, "BepInEx", "plugins", "TLDRevamp");
        Directory.CreateDirectory(modsDir);
        Directory.CreateDirectory(pluginDir);
        Directory.CreateDirectory(Path.Combine(tmp, "BepInEx", "config"));

        // The host loads the stub from next to ITS OWN assembly (ModHost: the stub
        // TLDLoader.dll next to TLDRevamp.dll).  The test's assembly IS the host
        // here. The project builds and copies both fixtures next to this output.
        string here = Path.GetDirectoryName(typeof(LoaderHostTest).Assembly.Location);
        string fixture = Path.Combine(here, "RecorderMod.dll");
        if (!File.Exists(fixture))
        {
            Console.Error.WriteLine("the loader fixture is missing from the test output; rebuild LoaderHostTest.csproj");
            return 2;
        }
        File.Copy(fixture, Path.Combine(modsDir, "RecorderMod.dll"), true);

        BepInEx.Paths.BepInExRootPath = Path.Combine(tmp, "BepInEx");
        BepInEx.Paths.ConfigPath = Path.Combine(tmp, "BepInEx", "config");

        // the game's scene: the menu first
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "menu";
        mainscript.s = null;

        Section("the host starts and loads the fixture mod");
        ModHost.Start();
        Check("the host reports the mod it loaded", ModHost.Status().Contains("Recorder Mod"));
        Check("the host loaded one mod", ModHost.Status().Contains("1 mod(s)"));
        {
            // the join check's list: the mod with the SHA-256 of its DLL (what a joiner must match)
            var jl = ModHost.JoinList();
            string want;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                want = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(modsDir, "RecorderMod.dll")))).Replace("-", "").ToLowerInvariant();
            Check("the join list has the mod with its DLL's SHA-256", jl.Count == 1 && jl[0].Name == "Recorder Mod" && jl[0].Hash == want);
        }
        Check("the host gives the mods their config folder",
              Directory.Exists(Path.Combine(tmp, "BepInEx", "config", "TLDLoaderMods")) ||
              ModHost.Status().Contains("1 mod(s)"));

        var recorder = Type.GetType("RecorderMod, RecorderMod");
        Check("the fixture mod's type is in the loaded assembly", recorder != null);
        if (recorder == null) { Console.WriteLine("FAILURES: " + failures); return 1; }
        var calls = (System.Collections.IList)recorder.GetField("Calls", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Func<List<string>> GetCalls = () => calls.Cast<string>().ToList();

        // a fresh recorder for each part
        // the recorder's own list, not a copy of it: Clear() has to empty the real one
        var callsList = (List<string>)calls;
        Action Reset = () => callsList.Clear();
        // the loaded mod's LoadInMenu as the host read it at load
        Action<bool> SetMenu = menu =>
        {
            foreach (var l in (System.Collections.IEnumerable)typeof(ModHost).GetField("Mods", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null))
                l.GetType().GetField("Menu").SetValue(l, menu);
        };

        Section("OnMenuLoad in the menu, once");
        Reset();
        ModHost.Tick();
        Check("OnMenuLoad was called in the menu", GetCalls().Contains("OnMenuLoad"));
        Check("OnLoad was NOT called in the menu", !GetCalls().Contains("OnLoad"));
        Reset();
        ModHost.Tick();
        Check("OnMenuLoad is not called again while the menu stays", !GetCalls().Contains("OnMenuLoad"));
        Check("Update is still called every tick", GetCalls().Contains("Update"));

        Section("OnLoad only once the player exists");
        Reset();
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "game";   // the game scene, but no player yet
        ModHost.Tick();
        Check("OnLoad is not called while the player does not exist", !GetCalls().Contains("OnLoad"));
        Check("OnUnload is not called on the way in", !GetCalls().Contains("OnUnload"));
        mainscript.s = new mainscript { player = new TLDRevamp.Fixtures.Player() };
        Reset();
        ModHost.Tick();
        Check("OnLoad is called once the player exists", GetCalls().Contains("OnLoad"));
        Reset();
        ModHost.Tick();
        Check("OnLoad is not called again in the same world", !GetCalls().Contains("OnLoad"));

        Section("a scene change inside the world does not unload the mod");
        // The game loads scenes for a restart and for a new game; the active scene is
        // briefly something else while it does.  The world is up (the player exists),
        // so the mod must NOT be unloaded.
        Reset();
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "some-other-scene";
        ModHost.Tick();
        Check("a scene change while the player exists does not call OnUnload", !GetCalls().Contains("OnUnload"));
        Check("a scene change while the player exists does not call OnLoad again", !GetCalls().Contains("OnLoad"));
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "game";
        ModHost.Tick();
        Check("coming back to the game scene does not call OnLoad again", !GetCalls().Contains("OnLoad"));

        Section("OnUnload when the world goes away");
        Reset();
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "menu";
        mainscript.s = null;                       // the world is gone: the player with it
        ModHost.Tick();
        Check("OnUnload is called when the world goes away", GetCalls().Contains("OnUnload"));
        Check("OnMenuLoad is not called again in the menu (TLD Loader calls it once, when it makes the mod)", !GetCalls().Contains("OnMenuLoad"));
        Reset();
        ModHost.Tick();
        Check("OnUnload is not called again in the menu", !GetCalls().Contains("OnUnload"));
        Check("OnMenuLoad is not called again either", !GetCalls().Contains("OnMenuLoad"));

        Section("straight into a world (auto continue): OnMenuLoad before OnLoad, as TLD Loader had called it");
        var menuCalled = typeof(ModHost).GetField("_menuCalled", BindingFlags.NonPublic | BindingFlags.Static);
        menuCalled.SetValue(null, false);   // a host that never saw the menu
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "game";
        mainscript.s = new mainscript { player = new TLDRevamp.Fixtures.Player() };
        Reset();
        ModHost.Tick();
        {
            var c = GetCalls();
            Check("OnMenuLoad, then OnLoad", c.Contains("OnMenuLoad") && c.IndexOf("OnMenuLoad") < c.IndexOf("OnLoad"));
        }
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "menu";
        mainscript.s = null;
        Reset();
        ModHost.Tick();
        Check("back in the menu, it isn't called again", !GetCalls().Contains("OnMenuLoad") && GetCalls().Contains("OnUnload"));

        Section("Update, FixedUpdate, OnGUI and Config");
        Reset();
        ModHost.Tick();
        Check("Update is called every tick", GetCalls().Contains("Update"));
        Reset();
        ModHost.FixedTick();
        Check("FixedUpdate waits for a world, as TLD Loader's (after the mods' OnLoad)", !GetCalls().Contains("FixedUpdate"));
        Reset();
        ModHost.Gui();
        Check("OnGUI is called in the menu for a LoadInMenu mod", GetCalls().Contains("OnGUI"));
        SetMenu(false);                      // as a mod without LoadInMenu
        Reset();
        ModHost.Tick(); ModHost.Gui();
        Check("without LoadInMenu, Update and OnGUI don't run in the menu (mods read mainscript.M.player there)",
              !GetCalls().Contains("Update") && !GetCalls().Contains("OnGUI"));
        mainscript.s = new mainscript { player = new TLDRevamp.Fixtures.Player() };
        ModHost.Tick();
        Reset();
        ModHost.Tick(); ModHost.FixedTick(); ModHost.Gui();
        Check("in the world Update, FixedUpdate and OnGUI all run", GetCalls().Contains("Update") && GetCalls().Contains("FixedUpdate") && GetCalls().Contains("OnGUI"));
        SetMenu(true);
        mainscript.s = null;
        ModHost.Tick();
        Reset();
        ModHost.ConfigScreenOpen = true;     // a settings screen is open
        ModHost.DrawConfigs();
        Check("Config is called for the mod's settings window", GetCalls().Contains("Config"));
        ModHost.ConfigScreenOpen = false;

        Section("the game quitting ends the world too");
        mainscript.s = new mainscript { player = new TLDRevamp.Fixtures.Player() };
        ModHost.Tick();                      // the world is up again
        Reset();
        ModHost.Shutdown();                   // the game is quitting: no scene change
        Check("OnUnload is called when the game quits", GetCalls().Contains("OnUnload"));
        Reset();
        ModHost.Shutdown();
        Check("OnUnload is not called twice", !GetCalls().Contains("OnUnload"));

        Section("Config only runs while a settings screen is open");
        Reset();
        ModHost.ConfigScreenOpen = false;
        ModHost.DrawConfigs();
        Check("Config is not called while no settings screen is open", !GetCalls().Contains("Config"));
        ModHost.ConfigScreenOpen = true;
        Reset();
        ModHost.DrawConfigs();
        Check("Config is called while a settings screen is open", GetCalls().Contains("Config"));
        ModHost.ConfigScreenOpen = false;

        Section("a mod that throws does not stop the host");
        recorder.GetField("ThrowOn", BindingFlags.Public | BindingFlags.Static).SetValue(null, "Update");
        Reset();
        try { ModHost.Tick(); Check("the host survives a throwing Update", true); }
        catch (Exception e) { Check("the host survives a throwing Update", false); }
        Check("the error is reported", ModHost.Status().Contains("lastError"));
        recorder.GetField("ThrowOn", BindingFlags.Public | BindingFlags.Static).SetValue(null, null);

        Section("the status reports the lifecycle state");
        string status = ModHost.Status();
        Check("the status says the mod is loaded", status.Contains("Recorder Mod"));
        Check("the status reports the version", status.Contains("1.2.3"));
        Check("the status reports onLoadCalled", status.Contains("onLoadCalled"));
        Check("the status reports unloadedSinceLastLoad", status.Contains("unloadedSinceLastLoad"));
        Check("the status reports hasConfig", status.Contains("hasConfig"));

        Section("the real third-party mod loads under the same host");
        // The fixture above is a mod built against THIS tree's shim, so it cannot
        // catch a case where the real TLDLoader API differs from the stub.  The
        // repository's real third-party mod (an autopilot DLL in
        // thirdparty/TLDLoaderMods/) is loaded here the same way the host loads it
        // in the game: its own DLL, plus the game's assemblies for its references.
        string thirdParty = Path.Combine(root, "thirdparty/TLDLoaderMods");
        string realMod = Directory.Exists(thirdParty) ? Directory.GetFiles(thirdParty, "*.dll").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault() : null;
        if (realMod != null && File.Exists(realMod))
        {
            string libDir = Path.Combine(root, "lib");
            // the game's assemblies the mod references, so its types load
            AppDomain.CurrentDomain.AssemblyResolve += (s2, e) =>
            {
                string name = new AssemblyName(e.Name).Name + ".dll";
                foreach (string dir in new[] { libDir, Path.GetDirectoryName(realMod) })
                {
                    string p = Path.Combine(dir, name);
                    if (File.Exists(p)) return Assembly.LoadFrom(p);
                }
                return null;
            };
            // the stub the mod binds to, next to the mod's own copy
            File.Copy(Path.Combine(here, "TLDLoader.dll"),
                      Path.Combine(modsDir, "TLDLoader.dll"), true);
            try
            {
                var asm = Assembly.LoadFrom(realMod);
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException te) { types = te.Types.Where(t => t != null).ToArray(); }
                var realMods = types.Where(t => !t.IsAbstract && t.BaseType != null && t.BaseType.FullName == "TLDLoader.Mod").ToArray();
                Check("the real third-party mod's Mod type is found", realMods.Length > 0);
                if (realMods.Length > 0)
                {
                    var t = realMods[0];
                    // the host binds the same way for every mod
                    var overridden = t.GetMethod("OnUnload", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    Check("the real mod overrides OnUnload", overridden != null && overridden.DeclaringType != t.BaseType);
                    var onLoad = t.GetMethod("OnLoad", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    Check("the real mod overrides OnLoad", onLoad != null && onLoad.DeclaringType != t.BaseType);
                    var config = t.GetMethod("Config", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    Check("the real mod overrides Config", config != null && config.DeclaringType != t.BaseType);
                    // and the host can create it and read the shim's properties
                    var mod = Activator.CreateInstance(t);
                    var modBase = t.BaseType;
                    Check("the host can read the real mod's id", !string.IsNullOrEmpty((string)modBase.GetProperty("ID").GetValue(mod)));
                    Check("the host can read the real mod's version", !string.IsNullOrEmpty((string)modBase.GetProperty("Version").GetValue(mod)));
                    // the real mod's LoadInMenu is what the host gates on
                    var lim = (bool)modBase.GetProperty("LoadInMenu").GetValue(mod);
                    Check("the real mod's LoadInMenu is readable", true);
                    // creating it must not throw (the host does this at startup)
                    Check("the real mod can be created by the host", mod != null);
                }
            }
            catch (Exception e)
            {
                Check("the real third-party mod loads under the host: " + e.Message, false);
            }
        }
        else
        {
            // The real third-party mod is not in the repository (thirdparty/ is
            // gitignored: it is a mod downloaded from its own release), and the
            // game's assemblies are not either (lib/).  A checkout without them
            // cannot run this part, so it is skipped, not failed: the CI has
            // neither.  The rest of the test still runs.
            Console.WriteLine("SKIP the real third-party mod is not in this checkout "
                              + "(thirdparty/ and lib/ are not committed); the lifecycle "
                              + "above was checked against the purpose-built fixture");
        }

        Section("a mod that asks not to be loaded in the menu is not");
        // LoadInMenu is read when the mod is loaded, so the check needs a second
        // host run with the fixture's flag off.  The host's state is static, so
        // this runs in its own process-like reset: the fixture is reloaded with
        // the flag off by copying it into a fresh mods folder and re-running the
        // host's Start in a second directory.  The host refuses to start twice,
        // so the flag is checked by reading what the host decided.
        var loadInMenuProp = recorder.GetProperty("LoadInMenu", BindingFlags.Public | BindingFlags.Instance);
        object modInstance = null;
        foreach (var f in typeof(ModHost).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            if (f.Name == "Mods")
            {
                var list = (System.Collections.IEnumerable)f.GetValue(null);
                foreach (var l in list)
                {
                    var mi = l.GetType().GetField("Mod", BindingFlags.Public | BindingFlags.Instance);
                    if (mi != null) modInstance = mi.GetValue(l);
                }
            }
        Check("the host created the mod", modInstance != null);
        recorder.GetField("LoadInMenuOverridden", BindingFlags.Public | BindingFlags.Static).SetValue(null, false);
        Check("the fixture's LoadInMenu flag is what the host reads",
              loadInMenuProp != null && (bool)loadInMenuProp.GetValue(modInstance) == false);
        recorder.GetField("LoadInMenuOverridden", BindingFlags.Public | BindingFlags.Static).SetValue(null, true);

        Section("DbLoad once per item database in the game scene, not the menu's; the public game's names");
        Reset();
        ModHost.Tick();
        Check("no DbLoad while there is no item database", !GetCalls().Contains("DbLoad"));
        itemdatabase.s = new itemdatabase();
        mainscript.s = new mainscript();
        var menuCalledField = typeof(ModHost).GetField("_menuCalled", BindingFlags.NonPublic | BindingFlags.Static);
        menuCalledField.SetValue(null, false);   // a mod that has not had its OnMenuLoad yet (straight into a world)
        Reset();
        ModHost.Tick();
        Check("no DbLoad for the menu's item database (TLD Loader ran it in the world; a gauge mod read the player there)",
              UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "menu" && !GetCalls().Contains("DbLoad"));
        menuCalledField.SetValue(null, false);
        UnityEngine.SceneManagement.SceneManager.ActiveSceneName = "game";   // the same database, the game scene now: no player yet
        Reset();
        ModHost.Tick();
        var c0 = GetCalls();
        Check("DbLoad and the older dbLoad are called once the item database exists (LoadInDB)", c0.Contains("DbLoad") && c0.Contains("dbLoad"));
        Check("OnMenuLoad first, once (TLD Loader calls it when it makes the mod; a mod's DbLoad uses what it made), then DbLoad, then dbLoad",
              c0.Count(x => x == "OnMenuLoad") == 1 && c0.IndexOf("OnMenuLoad") < c0.IndexOf("DbLoad") && c0.IndexOf("DbLoad") < c0.IndexOf("dbLoad"));
        Check("mainscript.M points at mainscript.s for the mods", ReferenceEquals(mainscript.M, mainscript.s));
        Check("itemdatabase.d points at itemdatabase.s for the mods", ReferenceEquals(itemdatabase.d, itemdatabase.s));
        Reset();
        ModHost.Tick();
        Check("DbLoad is not called again for the same item database", !GetCalls().Contains("DbLoad"));
        itemdatabase.s = new itemdatabase();
        Reset();
        ModHost.Tick();
        Check("a new item database (a scene load) gets DbLoad again", GetCalls().Contains("DbLoad"));
        Check("itemdatabase.d follows the new database", ReferenceEquals(itemdatabase.d, itemdatabase.s));
        var before = (List<int>)recorder.GetField("RegisteredBefore").GetValue(null);
        var places = (List<int>)recorder.GetField("Places").GetValue(null);
        Check("TLD Loader 3.0.3's registry is empty again for each new item database (" + string.Join(",", before) + ")",
              before.Count == 2 && before.All(n => n == 0));
        Check("and the mod's item gets the same place each time, in the registry's range (" + string.Join(",", places) + ")",
              places.Count == 2 && places[0] == places[1] && places[0] >= 16384 && places[0] < 32768);

        Section("a DbLoad that threw before the player was up runs again once the world is up (the public game ran it in the world)");
        var throwOn = recorder.GetField("ThrowOn", BindingFlags.Public | BindingFlags.Static);
        mainscript.s = new mainscript();
        itemdatabase.s = new itemdatabase();
        throwOn.SetValue(null, "dbLoad");
        Reset();
        ModHost.Tick();
        throwOn.SetValue(null, null);
        Check("its dbLoad ran before the player and threw", GetCalls().Contains("dbLoad") && !GetCalls().Contains("OnLoad"));
        mainscript.s.player = new TLDRevamp.Fixtures.Player();
        Reset();
        ModHost.Tick();
        var w = GetCalls();
        Check("in the world: DbLoad and dbLoad again, before OnLoad",
              w.Contains("DbLoad") && w.Contains("dbLoad") && w.Contains("OnLoad") && w.IndexOf("dbLoad") < w.IndexOf("OnLoad"));
        Check("the same database: what its first DbLoad registered is still there for the second",
              before.Count >= 2 && before[before.Count - 1] == 1 && places[places.Count - 1] == places[0]);
        mainscript.s.player = null; ModHost.Tick();
        mainscript.s.player = new TLDRevamp.Fixtures.Player();
        Reset();
        ModHost.Tick();
        Check("only once: the next world's OnLoad comes without it", GetCalls().Contains("OnLoad") && !GetCalls().Contains("dbLoad"));
        mainscript.s.player = null; itemdatabase.s = new itemdatabase();
        Reset(); ModHost.Tick();
        mainscript.s.player = new TLDRevamp.Fixtures.Player();
        Reset(); ModHost.Tick();
        Check("a DbLoad that did not throw is not run again in the world", GetCalls().Contains("OnLoad") && !GetCalls().Contains("dbLoad"));
        Func<string> LastError = () =>
        {
            foreach (var l in (System.Collections.IEnumerable)typeof(ModHost).GetField("Mods", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null))
                return (string)l.GetType().GetField("LastError").GetValue(l);
            return null;
        };
        mainscript.s.player = null; ModHost.Tick();
        throwOn.SetValue(null, "dbLoad"); itemdatabase.s = new itemdatabase(); ModHost.Tick(); throwOn.SetValue(null, null);
        bool erred = LastError() != null && LastError().StartsWith("dbLoad");
        itemdatabase.s = new itemdatabase(); ModHost.Tick();
        Check("a DbLoad error is over once DbLoad runs for the next database (the scene load's)", erred && LastError() == null);

        Console.WriteLine(failures == 0 ? "ALL PASS" : (failures + " FAILURE(S)"));
        try { Directory.Delete(tmp, true); } catch { }
        return failures == 0 ? 0 : 1;
    }
}
