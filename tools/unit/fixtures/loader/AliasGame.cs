// Public and beta shapes for the rewriter tests. No game code is used here.
namespace UnityEngine
{
    public class Transform { }
    public class GameObject { public string Name; public static GameObject Find(string name) => null; }
    public struct Vector3 { public float x, y, z; }
    public struct Color { public float r, g, b, a; }
#if !PUBLIC_GAME
    public struct Vector3d { public double x, y, z; }
#endif
}
public class DataFromMenuScript { public static DataFromMenuScript s; }   // test stub, not game code
public class settingsscript { public static settingsscript s; }
public class gamemodesettingsscript
{
    public class gamemodesettings { }
    public static gamemodesettingsscript s;
    public gamemodesettings S;
}
public class usablescript
{
    public float tunedLerp; public int currentTurnState;
#if PUBLIC_GAME
    public void Refresh(float slide, float x, float y) { }
#else
    public float Got;
    public void Refresh(float slide, float x, float y, int turn) { Got = slide + x + y + turn; }
#endif
}
public class layerScript
{
#if PUBLIC_GAME
    public void FStart() { }
#else
    public object p; public int index = -1;
    public void FStart(object _p, int _index) { p = _p; index = _index; }
#endif
}
public class fpscontroller
{
#if PUBLIC_GAME
    public UnityEngine.Transform Tb;
    public mainscript M;
    public void RightHandRelease() { }
#else
    public UnityEngine.Transform BodyRot;
    public bool Released;
    public void RightHandRelease(bool sync) { Released = sync; }
#endif
}
public class radioscript
{
#if PUBLIC_GAME
    public float v;
    public void UpdValues(bool multi) { }
#else
    public usablescript Bvolume = new usablescript();
    public int Updates;
    public void UpdValues() { Updates++; }
#endif
}
public class carscript   // test stub, not game code
{
#if PUBLIC_GAME
    public int lampak;
    public UnityEngine.GameObject KmhObj;
#else
    public usablescript lightUsable = new usablescript();
    public UnityEngine.GameObject debugCanvas;
#endif
}
public class mainscript   // test stub, not game code
{
    public static mainscript s;
#if PUBLIC_GAME
    public DataFromMenuScript DFMS;
    public settingsscript SettingObj;
    public gamemodesettingsscript.gamemodesettings GSettings;
    public bool godmode, Showkmh;
    public void ChGodMode(bool value) { }
    public void ChKmh(bool value) { }
    public void FoodCh(float value) { }
    public void WaterCh(float value) { }
    public void HpCh(float value) { }
    public void PissCh(float value) { }
    public void ShitCh(float value) { }
    public void PressedEscape() { }
    public void Spawn(UnityEngine.GameObject g, int rtype) { }
    public void Spawn(UnityEngine.GameObject g, UnityEngine.Color c, int s, int rtype) { }
#else
    public UnityEngine.GameObject Spawned; public bool Painted;
    public UnityEngine.GameObject Spawn(UnityEngine.GameObject g, int rtype) { Spawned = g; return g; }
    public UnityEngine.GameObject Spawn(UnityEngine.GameObject g, UnityEngine.Color c, int s, int rtype, bool paint) { Spawned = g; Painted = paint; return g; }
    public bool Screenshot, MainMenu;
    public void PressedEscape(bool screenshot, bool mainMenu) { Screenshot = screenshot; MainMenu = mainMenu; }
#endif
}
public class kaposztaleves   // test stub, not game code
{
    public struct devSettings { public bool god, kmh; }
    public static kaposztaleves s;
    public devSettings settings;
    public float Food, Water, Hp, Piss, Shit;
    public void PressedFoodCh(float value) { Food = value; }
    public void PressedWaterCh(float value) { Water = value; }
    public void PressedHpCh(float value) { Hp = value; }
    public void PressedPissCh(float value) { Piss = value; }
    public void PressedShitCh(float value) { Shit = value; }
}
public class menuhandler   // test stub, not game code
{
#if PUBLIC_GAME
    public DataFromMenuScript DFMS;
    public settingsscript Settings;
#else
    public static menuhandler s;
    public mapSettingScript currentMainMap;
#endif
}
#if PUBLIC_GAME
// the public game's main menu; the beta's is menuhandler
public class mainmenuscript { public static mainmenuscript mainmenu; public UnityEngine.GameObject SettingsScreenObj, SaveScreenObj; }
#endif

// The world generator's scattered prefabs: the public game's chain of four types, the beta's map and its generators.
#if PUBLIC_GAME
public class ObjClass { public UnityEngine.GameObject prefab; }
public class ObjectGenerationScript { public ObjClass[] objTypes; }
public class TerrainGenerationSettings
{
    public ObjectGenerationScript objGeneration;
    public static TerrainGenerationSettings staticReference;
    public roadBuildingScript roadBuildingGeneration;
}
// the generator of the buildings along the road; the beta places them as points of interest
public class roadBuildingScript { public UnityEngine.Transform parent; public System.Collections.Generic.Dictionary<int, UnityEngine.GameObject> placedBuildings; }
public class TerrainManager { public TerrainGenerationSettings settings; }
public class TerrainGenerator { public static TerrainGenerator TG; public TerrainManager manager; }
#else
public class objGenScript   // test stub, not game code
{
    public class prefabClass { public UnityEngine.GameObject prefab; }
    public class categoryClass { public System.Collections.Generic.List<prefabClass> prefabs = new System.Collections.Generic.List<prefabClass>(); }
    public System.Collections.Generic.List<categoryClass> categories = new System.Collections.Generic.List<categoryClass>();
}
public class mapSettingScript { public System.Collections.Generic.List<objGenScript> objGens = new System.Collections.Generic.List<objGenScript>(); }
#endif
public class headlightscript
{
    public int switchToState;
    public System.Collections.Generic.List<int> states = new System.Collections.Generic.List<int> { 0, 1, 2 };
#if PUBLIC_GAME
    public void SwitchState() { }
    public void SwitchState(int state) { }
#else
    public usablescript Sender;
    public int Calls;
    public void SwitchState(int state, usablescript sender) { switchToState = System.Math.Min(state, states.Count - 1); Sender = sender; Calls++; }
#endif
}
public class survivalscript
{
#if PUBLIC_GAME
    public void Damage(float amount, bool show) { }
    public void DamageInstant(float amount, bool show) { }
#else
    public float Lost, InstantLost;
    public bool Shown, InstantShown;
    public UnityEngine.Vector3 Direction, InstantDirection;
    public void Damage(float amount, UnityEngine.Vector3 direction, bool show) { Lost = amount; Direction = direction; Shown = show; }
    public void DamageInstant(float amount, UnityEngine.Vector3 direction, bool show) { InstantLost = amount; InstantDirection = direction; InstantShown = show; }
#endif
}

// The old save ids the TLD Workshop's spawners use (issue 44), and what the beta has instead.
public class tosaveitemscript   // test stub, not game code
{
    public int id;   // both: the public game's item number; the beta's, its place in the item table
#if PUBLIC_GAME
    public bool removeFromMemory;
    public void FStart(int key) { }
    public partslotscript[] partslotscripts;
    public int category;   // nothing on the beta: kept for the mod
    public System.Collections.Generic.List<digholescript2> holes;   // the field and its type: nothing on the beta (one mod's interiorLights)
    public randomScaleScript[] randomScales;   // the beta's is a list
    public void Claim(bool b) { }   // the public game's multiplayer claim; the beta has none
#else
    public uint Started; public bool dontUnparent;
    public void FStart(uint key) { Started = key; }
    public System.Collections.Generic.List<partslotscript> partslotscripts = new System.Collections.Generic.List<partslotscript>();
    public attachablescript attachable;
    public System.Collections.Generic.List<randomScaleScript> randomScales;
#endif
}
public class randomScaleScript { }

// The world offset's coordinate: the public game's doublecoord, the beta's UnityEngine.Vector3d (another namespace).
#if PUBLIC_GAME
public struct doublecoord { public double x, y, z; }
public class visszarako { public doublecoord coord; }
#else
public class visszarako { public UnityEngine.Vector3d coord; }
#endif

// The public game's parts (partscript, its slot's part field) and the beta's: parts are attachablescripts, the slot
// answers part().
#if PUBLIC_GAME
public class partscript { public tosaveitemscript tosaveitem; public partslotscript slot; }
public class attachablescript { public tosaveitemscript targetTosave; public UnityEngine.Vector3 pointLocalPos; public void Load(UnityEngine.Vector3 pos) { } }
public class partslotscript { public partscript part; public void Craft(partscript p, bool b) { } }
#else
public class attachablescript { public tosaveitemscript tosave; public partslotscript slot; }
public class partslotscript
{
    public attachablescript Part; public attachablescript part() => Part;
    public attachablescript Crafted; public void Craft(attachablescript p) { Crafted = p; }
}
#endif
// a mount for items: the public game's Lock, the beta's Store
public class pickupable { }
public class mountStuff
{
#if PUBLIC_GAME
    public void Lock(pickupable p, bool ignoreRigidbody) { }
#else
    public pickupable Stored; public bool IgnoredRigidbody; public void Store(pickupable p, bool ignoreRigidbody) { Stored = p; IgnoredRigidbody = ignoreRigidbody; }
#endif
}
public class save_rendszam { public uint id; public string S; }
#if PUBLIC_GAME
public class save_prefab
{
    public save_rendszam rendszam; public int categoryID;
    public save_prefab(int category, int id, double x, double y, double z, float rx, float ry, float rz) { categoryID = category; }
}
// the menu's switch for the world's spawners (a UFO mod's sandstorm UFO keeps one); the beta has none
public class temporaryTurnOffGeneration { }
// a hole dug in the ground (a mod's wells look for them); the beta has none
public class digholescript2 { public int depth; }
#endif
public class inputscript
{
#if PUBLIC_GAME
    public bool forward;
#else
    public float forward;
#endif
}
public class saveproperties { public float driven; }
public class savedata
{
#if PUBLIC_GAME
    public System.Collections.Generic.Dictionary<int, save_prefab> nearStuff, farStuff;
#else
    public saveproperties properties = new saveproperties();
#endif
}
public class savedatascript
{
    public savedata data = new savedata();
#if PUBLIC_GAME
    public saveproperties properties;
#endif
#if PUBLIC_GAME
    public int GetNewKey() => 0;
#else
    public uint GetNewKey() => 5;
#endif
#if PUBLIC_GAME
    public System.Collections.Generic.Dictionary<int, tosaveitemscript> toSaveStuff;
#else
    public System.Collections.Generic.Dictionary<uint, tosaveitemscript> items = new System.Collections.Generic.Dictionary<uint, tosaveitemscript>();
#endif
}
