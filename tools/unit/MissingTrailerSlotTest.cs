using System;
using System.Collections.Generic;
using TLDRevamp.Fixes;

static class MissingTrailerSlotTest
{
    static int fails;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }
    static bool Load(attachablescript part, int index = 3, int type = 2, int target = 1) => MissingTrailerSlot.Prefix(part, type, target, 42, index);
    static int Main()
    {
        var parent = new TrailerSaveStub { car = new object(), partslotscripts = new List<object> { new object(), new object(), new object() } };
        itemPlaceRemoveScript.s = new itemPlaceRemoveScript { Parent = parent };
        var part = new attachablescript { utanfuto = new object(), tosave = new TrailerSaveStub { P = new PickupStub { crafted = true } } };
        Check("existing stock slot follows game load", Load(part, 2));
        Check("unknown appended slot bypasses generic attachment fallback", !Load(part) && !part.attached && !part.tosave.P.crafted && part.CraftedCalls == 1);
        Check("load leaves movement guard cleared", !part.tempDontMoveAtAttach);
        part.attached = true;
        Check("already attached drawbar detached without removing its item", !Load(part, 10) && part.DetachCalls == 1 && part.tosave != null && part.utanfuto != null);
        part.utanfuto = null;
        Check("non-trailer unknown slots retain game's fallback", Load(part));
        part.utanfuto = new object();
        Check("collider and POI records unchanged", Load(part, type: 1) && Load(part, target: 2));
        Check("negative index unchanged", Load(part, -1));
        parent.car = null;
        Check("non-car parent unchanged", Load(part));
        parent.car = new object();
        itemPlaceRemoveScript.s.Parent = null;
        Check("not-yet-loaded parent unchanged", Load(part));
        Check("null drawbar unchanged", Load(null));
        itemPlaceRemoveScript.s.Parent = parent;
        parent.partslotscripts.Add(new object());
        Check("reinstalled receiver follows normal slot load", Load(part));
        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
        return fails == 0 ? 0 : 1;
    }
}

// Minimal load-boundary doubles; the game's implementation is never copied into the repository.
public sealed class attachablescript
{
    public object utanfuto;
    public bool attached, tempDontMoveAtAttach = true;
    public TrailerSaveStub tosave;
    public int CraftedCalls, DetachCalls;
    public void Detach() { attached = false; DetachCalls++; }
    public void Crafted(bool mounted) { CraftedCalls++; }
}
public sealed class TrailerSaveStub { public object car; public List<object> partslotscripts; public PickupStub P; }
public sealed class PickupStub { public bool crafted; public void Crafted() { } }
public sealed class itemPlaceRemoveScript
{
    public static itemPlaceRemoveScript s;
    public TrailerSaveStub Parent;
    public bool GetOne(uint id, out TrailerSaveStub parent) { parent = Parent; return parent != null && id == 42; }
}
public sealed class save_attachable { public static void Load() { } }
namespace UnityEngine { public struct Vector3d { } }
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method, Type[] args) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPrefix : Attribute { }
}
