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

    public static void Calls(fpscontroller p, radioscript r, mainscript m, headlightscript h, survivalscript s)
    {
        p.RightHandRelease(); r.UpdValues(true); m.PressedEscape();
        h.SwitchState(2); h.SwitchState(); s.Damage(3f, true); s.DamageInstant(4f, false);
        m.ChGodMode(false); m.ChKmh(false);
        m.FoodCh(1); m.WaterCh(2); m.HpCh(3); m.PissCh(4); m.ShitCh(5);
    }
}
