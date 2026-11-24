// Compiled against the public shape, then loaded with the beta shape by LoaderRetargetTest.
public static class AliasMod
{
    public static UnityEngine.GameObject Display = new UnityEngine.GameObject();
    public static void Write(fpscontroller p, radioscript r, carscript c, mainscript m, menuhandler menu,
                             UnityEngine.Transform body, DataFromMenuScript data, settingsscript settings,
                             gamemodesettingsscript.gamemodesettings mode)
    {
        p.Tb = body; p.M = m; r.v = 0.625f; c.lampak = 2;
        menu.DFMS = data; m.DFMS = data; m.SettingObj = settings; menu.Settings = settings; m.GSettings = mode;
        m.godmode = true; m.Showkmh = true;
        c.KmhObj = Display;
    }

    public static bool Read(fpscontroller p, radioscript r, carscript c, mainscript m, menuhandler menu,
                            UnityEngine.Transform body, DataFromMenuScript data, settingsscript settings,
                            gamemodesettingsscript.gamemodesettings mode)
    {
        return p.Tb == body && p.M == m && r.v == 0.625f && c.lampak == 2 &&
               menu.DFMS == data && m.DFMS == data && m.SettingObj == settings && menu.Settings == settings && m.GSettings == mode && m.godmode && m.Showkmh && c.KmhObj == Display;
    }

    static void Set<T>(ref T field, T value) { field = value; }
    public static void Address(fpscontroller p, radioscript r, carscript c, mainscript m, menuhandler menu,
                               UnityEngine.Transform body, DataFromMenuScript data, settingsscript settings,
                               gamemodesettingsscript.gamemodesettings mode)
    {
        Set(ref p.Tb, body); Set(ref p.M, m); Set(ref r.v, 0.625f); Set(ref c.lampak, 2);
        Set(ref menu.DFMS, data); Set(ref m.DFMS, data); Set(ref m.SettingObj, settings);
        Set(ref menu.Settings, settings); Set(ref m.GSettings, mode);
        Set(ref m.godmode, true); Set(ref m.Showkmh, true);
        Set(ref c.KmhObj, Display);
    }

    /// The spawner RUNDEN's and M_'s workshop mods share: the first id no item and no record has, then FStart(int).
    /// The Advanced Modding Toolkit's global position: a Unity position minus the world offset's coordinate.
    public static double GlobalX(visszarako world, float x) => 0.0 - world.coord.x + x;

    /// Remove-cactus/rock mods: every prefab the world generator scatters, counted.
    public static int Scattered()
    {
        int n = 0;
        foreach (var o in TerrainGenerator.TG.manager.settings.objGeneration.objTypes) if (o.prefab != null) n++;
        return n;
    }

    /// A field the beta dropped, stored and read back (tosaveitemscript.category).
    public static int Category(tosaveitemscript t) { t.category = 7; return t.category; }

    /// A game method whose return changed sign (savedatascript.GetNewKey: int there, uint here).
    public static int NewKey(savedatascript s) => s.GetNewKey();

    /// The public game's main object by name, a usable's three-value Refresh and a collider's FStart() (CompanyCruiser,
    /// Better AM Radio, Badger's Rotary).
    public static UnityEngine.GameObject Main() => UnityEngine.GameObject.Find("Main");
    public static void Refresh(usablescript u) => u.Refresh(1f, 2f, 3f);
    public static void Layer(layerScript l) => l.FStart();

    /// Where the mod's DLL is (the Advanced Modding Toolkit finds its assets next to it).
    public static string Where() => typeof(AliasMod).Assembly.Location;

    /// The workshop's part walk (M_VehicleDeterioration, SpeedOrBoom): every slot's part that knows its car and slot,
    /// then the slots replaced by the first one. Tens: the parts found, ones: the slots left.
    public static int Parts(tosaveitemscript car)
    {
        int n = 0;
        foreach (var slot in car.partslotscripts)
            if (slot.part != null && slot.part.tosaveitem == car && slot.part.slot == slot) n++;
        car.partslotscripts = new[] { car.partslotscripts[0] };
        return n * 10 + car.partslotscripts.Length;
    }

    public static int Spawn(savedatascript s, tosaveitemscript t)
    {
        int i;
        for (i = 0; s.toSaveStuff.ContainsKey(i) || s.data.farStuff.ContainsKey(i) || s.data.nearStuff.ContainsKey(i); i++) { }
        t.FStart(i);
        t.removeFromMemory = true;
        return i;
    }

    /// M-'s save template (M_TR_Radiation, M-ultiTool, The Long Who): the mod's data as a string in a fake plate record
    /// under its ID's hash, made the first time.
    public static string Record(savedatascript s, string input)
    {
        int key = 12345;
        save_rendszam val = null;
        if ((s.data.farStuff.TryGetValue(key, out var value) || s.data.nearStuff.TryGetValue(key, out value)) && value.rendszam != null) val = value.rendszam;
        if (val == null)
        {
            var rec = new save_prefab(3, 4, double.MaxValue, double.MaxValue, double.MaxValue, 0f, 0f, 0f);
            rec.rendszam = new save_rendszam();
            val = rec.rendszam;
            val.S = string.Empty;
            s.data.farStuff.Add(key, rec);
        }
        if (!string.IsNullOrEmpty(input)) val.S = input;
        return val.S;
    }

    /// Achievements' menu button: shown unless the menu's settings or save screen is open.
    public static string MenuScreens() => mainmenuscript.mainmenu.SettingsScreenObj.Name + "," + mainmenuscript.mainmenu.SaveScreenObj.Name;

    /// RUNDEN's AI traffic: is a collider's root the road buildings' parent; Stronger Sandstorms: the buildings placed.
    public static string RoadBuildings(UnityEngine.Transform root)
    {
        var g = TerrainGenerationSettings.staticReference.roadBuildingGeneration;
        return (root == g.parent) + "," + g.placedBuildings.Count;
    }

    /// KARL's wells: the dig holes among what was found (the beta has none), in a list of them. One more than that.
    public static int Holes(object[] found)
    {
        var holes = new System.Collections.Generic.List<digholescript2>();
        foreach (var o in found) if (o is digholescript2 h) holes.Add(h);
        return holes.Count + 1;
    }

    /// Stronger Sandstorms: the forward key held.
    public static bool Forward(inputscript i) => i.forward;

    /// SpawnerTLD: a spawn, plain and painted (the beta's return what they made).
    public static void Spawns(mainscript m, UnityEngine.GameObject g) { m.Spawn(g, -1); m.Spawn(g, new UnityEngine.Color(), 0, -1); }

    /// RUNDEN's car battery into its slot.
    public static void Craft(partslotscript s, partscript p) => s.Craft(p, true);

    /// RUNDEN's AI traffic names a text's SetText by name; the beta's text has more than one.
    /// (TextMeshPro's: no SetText(string) alone, one with an optional flag), and invokes it with the text only.
    public class Texty { public string Text; public void SetText(System.Text.StringBuilder b) { Text = "builder"; } public void SetText(string s, bool b = true) { Text = s + b; } }
    public static string Ambiguous() => string.Join(",", System.Linq.Enumerable.Select(typeof(Texty).GetMethod("SetText").GetParameters(), p => p.ParameterType.Name));
    public static string Invoked() { var t = new Texty(); typeof(Texty).GetMethod("SetText").Invoke(t, new object[] { "AI" }); return t.Text; }

    /// Achievements: the distance driven in this save.
    public static float Driven(savedatascript s) => s.properties.driven;

    /// AMT: a saved item back in its mount.
    public static void Remount(mountStuff m, pickupable p) => m.Lock(p, true);

    /// M-ultiTool's swap of an item for another: what was attached to the old one goes onto the new one, where it was.
    public static UnityEngine.Vector3 Swap(attachablescript a, tosaveitemscript to) { a.targetTosave = to; a.Load(a.pointLocalPos); return a.pointLocalPos; }

    public static void Calls(fpscontroller p, radioscript r, mainscript m, headlightscript h, survivalscript s)
    {
        p.RightHandRelease(); r.UpdValues(true); m.PressedEscape();
        h.SwitchState(2); h.SwitchState(); s.Damage(3f, true); s.DamageInstant(4f, false);
        m.ChGodMode(false); m.ChKmh(false);
        m.FoodCh(1); m.WaterCh(2); m.HpCh(3); m.PissCh(4); m.ShitCh(5);
    }
}

/// More UFOs: its list of UFO types names one that keeps a game type the beta dropped.
public static class UfoMod
{
    public static readonly System.Type[] Types = { typeof(Sandstorms) };
    public class Sandstorms { public temporaryTurnOffGeneration Temp; public int Speed = 3; }
}
