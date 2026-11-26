using System;
using System.Collections.Generic;
using System.Linq;
using TLDRevamp.Fixes;

/// The REAL src/TLDRevamp/Fixes/SaveListNames.cs: of a Saves folder holding saves with RevaMP's side files next to them,
/// only the saves are saves, and the load screen's list (built the game's way: path contains "TLDsaveData_" and ".tlds",
/// name = file name without its last extension) loses exactly the side-file entries.
static class SaveListNamesTest
{
    static int failures;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }

    /// The game's newSaveScreenScript.ReadFiles rule, for the non-autosave entries.
    static List<string> GameList(IEnumerable<string> paths) => paths
        .Where(p => p.Contains("TLDsaveData_") && p.Contains(".tlds"))
        .Select(p => WindowsNameWithoutExtension(p).Substring("TLDsaveData_".Length))
        .Where(n => n != "autosave1" && n != "autosave2" && n != "autosave3")
        .ToList();

    /// Path.GetFileNameWithoutExtension as the game sees it (Windows, or Proton): '\' is a separator also on Linux runners.
    static string WindowsNameWithoutExtension(string p)
    {
        string f = p.Substring(p.LastIndexOfAny(new[] { '/', '\\' }) + 1);
        int dot = f.LastIndexOf('.');
        return dot < 0 ? f : f.Substring(0, dot);
    }

    static int Main()
    {
        const string dir = @"D:\Games\TheLongDrive\Saves\";
        var files = new List<string>();
        foreach (var save in new[] { "26_11_20_1840", "26_11_20_1939", "autosave1" })
        {
            files.Add(dir + "TLDsaveData_" + save + ".tlds");
            foreach (var side in new[] { ".leases", ".players", ".players.bak", ".players.tmp", ".tune" }) files.Add(dir + "TLDsaveData_" + save + ".tlds" + side);
            files.Add(dir + "TLDsaveProp_" + save + ".tldsp");
            files.Add(dir + "TLDsaveThumb_" + save + ".jpg");
        }

        Check("a save is a save", SaveListNames.SaveNameOf(dir + "TLDsaveData_26_11_20_1840.tlds") == "26_11_20_1840");
        Check("forward slashes too", SaveListNames.SaveNameOf("C:/x/Saves/TLDsaveData_a b.tlds") == "a b");
        Check("side files are no saves", new[] { ".leases", ".players", ".players.bak", ".players.tmp", ".tune" }
            .All(s => SaveListNames.SaveNameOf(dir + "TLDsaveData_x.tlds" + s) == null));
        Check("properties and thumbnails are no saves", SaveListNames.SaveNameOf(dir + "TLDsaveProp_x.tldsp") == null && SaveListNames.SaveNameOf(dir + "TLDsaveThumb_x.jpg") == null);
        Check("empty name is no save", SaveListNames.SaveNameOf(dir + "TLDsaveData_.tlds") == null && SaveListNames.SaveNameOf(null) == null && SaveListNames.SaveNameOf("") == null);
        Check("a save named with a dot keeps it", SaveListNames.SaveNameOf(dir + "TLDsaveData_trip.v2.tlds") == "trip.v2");

        var listed = GameList(files);
        Check("the game's rule lists the side files (the bug)", listed.Count(n => n == "26_11_20_1840.tlds") == 3 && listed.Contains("26_11_20_1840.tlds.players"));
        var saves = SaveListNames.SaveNames(files);
        var drop = SaveListNames.Drop(listed, saves);
        var kept = listed.Where((n, i) => !drop.Contains(i)).ToList();
        Check("after the fix: only the two saves, in the game's order", kept.SequenceEqual(new[] { "26_11_20_1840", "26_11_20_1939" }));
        Check("autosave side files would land in the list too, and go", listed.Contains("autosave1.tlds") && !kept.Contains("autosave1.tlds"));

        // someone named a save "x.tlds": its own file is TLDsaveData_x.tlds.tlds; save "x"'s side files share that name.
        var clash = new List<string> { dir + "TLDsaveData_x.tlds", dir + "TLDsaveData_x.tlds.leases", dir + "TLDsaveData_x.tlds.tlds", dir + "TLDsaveData_x.tlds.tlds.tune" };
        var clashListed = GameList(clash);
        var clashDrop = SaveListNames.Drop(clashListed, SaveListNames.SaveNames(clash));
        var clashKept = clashListed.Where((n, i) => !clashDrop.Contains(i)).ToList();
        Check("save \"x.tlds\" stays once, save \"x\" stays, side files go", clashKept.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(new[] { "x", "x.tlds" }));

        Check("nothing to drop in a folder with no side files", SaveListNames.Drop(GameList(files.Where(f => f.EndsWith(".tlds"))), saves).Count == 0);
        Check("null names are dropped", SaveListNames.Drop(new string[] { null, "26_11_20_1840" }, saves).SequenceEqual(new[] { 0 }));

        Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
