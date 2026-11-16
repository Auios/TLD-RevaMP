using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;
using KF = KernelFactsFakes;

static partial class EntitySplitTest
{
    static void KernelFactsChecks()
    {
        var rows = new List<string>();
        void Row(string name, object value) => rows.Add(name + "|" + value);
        foreach (float n in new[] { 0f, -0f, float.Epsilon, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity }) Row("float-" + BitConverter.SingleToInt32Bits(n), EntityNumbers.Finite(n));
        foreach (double n in new[] { 0d, -0d, double.Epsilon, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Row("double-" + BitConverter.DoubleToInt64Bits(n), EntityNumbers.Finite(n));
        foreach (float n in new[] { 1f, float.NaN, float.PositiveInfinity })
        {
            Row("v3-" + n, EntityNumbers.Finite(new Vector3(1, n, 3)));
            Row("v3d-" + n, EntityNumbers.Finite(new Vector3d(1, 2, n)));
            Row("q-" + n, EntityNumbers.Finite(new Quaternion(n, 0, 0, 1)));
        }
        var a = new KF.Tank("a"); var b = new KF.Tank("b");
        Row("tank-order", string.Join(",", EntityLookup.TanksOf(new List<KF.Item> { null, new KF.Item("none"), new KF.Item("one") { tanks = new List<KF.Tank> { a, null, b, a } } }).Select(t => t?.name ?? "null")));
        KF.Main.s = null; KF.Save.s = null; Row("nearest-no-world", EntityLookup.NearestCar() == null);
        KF.Save.s = new KF.Save(); KF.Main.s = new KF.Main(); Row("nearest-no-player", EntityLookup.NearestCar() == null);
        KF.Main.s.player = new KF.Player();
        var far = new KF.Item("far") { car = new object() }; far.transform.position = new Vector3(8, 0, 0);
        var first = new KF.Item("first") { car = new object() }; first.transform.position = new Vector3(2, 0, 0);
        var tie = new KF.Item("tie") { car = new object() }; tie.transform.position = new Vector3(-2, 0, 0);
        var child = new KF.Item("child") { car = new object() }; child.transform.parent = first.transform;
        KF.Save.s.items[1] = far; KF.Save.s.items[2] = null; KF.Save.s.items[3] = first; KF.Save.s.items[4] = tie; KF.Save.s.items[5] = child;
        Row("nearest-tie-and-parent", EntityLookup.NearestCar().name);

        KF.Kernel.ByNet.Clear(); KF.Kernel.Shares.Clear();
        KF.Kernel.ByNet[8] = new KF.Kernel.Ent { NetId = 8, Root = first };
        KF.Kernel.ByNet[9] = new KF.Kernel.Ent { NetId = 9, Root = first };
        KF.Kernel.ByNet[10] = new KF.Kernel.Ent { NetId = 10, Root = far };
        KF.Kernel.ByNet[11] = new KF.Kernel.Ent { NetId = 11 };
        var leaf = new KF.GameObject(); leaf.transform.parent = child.transform;
        Row("mod-null", EntityModApi.NetIdOf(null)); Row("mod-nearest-shared-ancestor", EntityModApi.NetIdOf(leaf));
        Row("mod-missing", EntityModApi.GameObjectOf(12) == null); Row("mod-dormant", EntityModApi.GameObjectOf(11) == null);
        Row("mod-find", ReferenceEquals(EntityModApi.GameObjectOf(8), first.gameObject));
        KF.Kernel.InSession = false; Row("mod-share-no-session", EntityModApi.ShareGameObject(leaf));
        KF.Kernel.InSession = true; Row("mod-share-null", EntityModApi.ShareGameObject(null)); Row("mod-share-no-saveable", EntityModApi.ShareGameObject(new KF.GameObject()));
        Row("mod-share-car", EntityModApi.ShareGameObject(leaf)); first.car = null; Row("mod-share-item", EntityModApi.ShareGameObject(leaf));
        KF.Kernel.Result = "{\"error\":\"fixture\"}"; Row("mod-share-error", EntityModApi.ShareGameObject(leaf));
        Row("mod-share-order", string.Join(",", KF.Kernel.Shares));

        var d = new KF.Data(); d.items.AddRange(new object[] { new KF.Row(7, "a"), null, new KF.Row(8, "b"), new KF.Row(7, "c") });
        d.rows.AddRange(new object[] { null, new KF.Row(7, "leading-null") }); d.wrong.Add(new KF.WrongRow { id = 7 }); d.other.Add(new KF.OtherRow { idInSave = 7 });
        KF.Lookups.Calls.Clear(); Row("save-removed", EntityPlayerSave.DropFromSave(d, new HashSet<uint> { 7 }));
        Row("save-survivors", string.Join(",", d.items.Select(x => x is KF.Row r ? r.id + ":" + r.value : "null")));
        Row("save-untouched", d.rows.Count + ":" + d.wrong.Count + ":" + d.other.Count);
        Row("save-lookups", string.Join(",", KF.Lookups.Calls)); Row("save-repeat", EntityPlayerSave.DropFromSave(d, new HashSet<uint> { 7 }));

        var slot = new KF.Slot(); var pickup = new KF.Pickup(); var part = new KF.Part { transform = first.transform };
        var player = new KF.Player(); var text = new StringBuilder();
        EntityMountFacts.MountFacts(slot, part, pickup, player, text, -1); Row("mount-no-colliders", text);
        var target = new KF.Collider { name = "target", Slot = slot }; var wall = new KF.Collider { name = "wall" }; wall.gameObject.layer = 3;
        var off = new KF.Collider { name = "off", enabled = false, isTrigger = true }; off.gameObject.layer = 4;
        part.Colliders = new[] { off, target }; part.cover_CantCraftIfCovered = new KF.Cover { Has = true }; pickup.crafted = true; part.attached = true;
        KF.Physics.Hits.Clear(); KF.Physics.Hits.Enqueue(wall); KF.Physics.Hits.Enqueue(target); KF.Physics.Rays = 0;
        text.Clear(); EntityMountFacts.MountFacts(slot, part, pickup, player, text, 3); Row("mount-rays", text); Row("mount-ray-count", KF.Physics.Rays);
        Row("ground-default", EntityGround.GroundLayers);

        string path = "tools/unit/EntityKernelFactsRows.txt";
        if (Environment.GetEnvironmentVariable("CAPTURE_KERNEL_HEAD") == "1") File.WriteAllText(path, string.Join("\r\n", rows) + "\r\n");
        Check(rows.SequenceEqual(File.ReadAllLines(path)), "slice 30 fixed rows equal the pre-move HEAD capture");
        if (Environment.GetEnvironmentVariable("CAPTURE_KERNEL_HEAD") == "1") return;
        CoreMembers.Register();
        foreach (var owner in new[] { typeof(EntityLookup), typeof(EntityNumbers), typeof(EntityMountFacts), typeof(EntityGround), typeof(EntityModApi), typeof(EntityPlayerSave) })
        {
            Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, owner.Name + " is internal static");
            foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
            {
                bool retained = typeof(Entities).GetMember(member.Name, Static).Length > 0;
                foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." }) Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "kernel helper alias " + prefix + member.Name);
            }
        }
        Check(BridgeReflect.Set("Entities.GroundLayers", "road,sand") == "\"road,sand\"" && EntityGround.GroundLayers == "road,sand", "historical ground setting writes the sole owner");
        EntityGround.GroundLayers = "sand,sand2,sand3,road";
        KernelBoundaryChecks();
    }

    static void KernelBoundaryChecks()
    {
        Check(File.ReadAllLines("tools/unit/EntityKernelFactsSourceRows.txt").SequenceEqual(File.ReadAllLines("tools/unit/obj/EntitySplitTest/KernelFactsActualRows.txt")), "helper bodies and retained kernel files equal HEAD after named substitutions");
        string[] files = Directory.GetFiles("src/TLDRevamp/Net", "Entities*.cs");
        foreach (string name in new[] { "Tick", "FixedTick", "Reset", "ServerOnJoin", "ServerOnLeave", "ClientReceive", "ServerReceive", "ToServer", "SendToServer", "ServerSendTo", "ServerSendAll", "ServerForwardRaw", "Deliver" })
            Check(files.Any(f => Regex.IsMatch(File.ReadAllText(f).Split("public static partial class Entities").Last(), @"(?:public|internal|private) static [^\r\n]*\b" + name + @"\(")), name + " remains declared on Entities");
        foreach (var helper in new[] { ("Lookup", "EntityLookup"), ("MountFacts", "EntityMountFacts"), ("Ground", "EntityGround"), ("ModApi", "EntityModApi"), ("PlayerSave", "EntityPlayerSave"), ("Push", "EntityNumbers") })
        {
            string text = File.ReadAllText("src/TLDRevamp/Net/Entities." + helper.Item1 + ".cs");
            string owner = text.Split("    internal static class " + helper.Item2)[1].Split("\n    }")[0];
            string code = Regex.Replace(owner, @"//[^\r\n]*", "");
            Check(!text.Contains("using static") && !Regex.IsMatch(code, @"\b(?:ByNet|Server|SEnt|W|WS|LookupSet|Placed)\b"), helper.Item2 + " cannot access kernel tables or writers");
            foreach (Match call in Regex.Matches(code, @"Entities\.(\w+)")) Check(new[] { "TryModRootNet", "ModObject", "ModShareRoot", "InSession", "MountRootNet" }.Contains(call.Groups[1].Value), "narrow helper dependency " + call.Value);
        }
        // Pin actual outside references, including raw reflection; new source files are scanned too.
        var actual = new List<string>();
        foreach (string folder in new[] { "src", "tools" })
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Split('/').Any(p => p == "obj" || p == "bin")))
            {
                string path = file.Replace('\\', '/');
                if (path.StartsWith("src/TLDRevamp/Net/Entities") || path.StartsWith("src/TLDRevamp.Dev/Net/Entities")) continue;
                bool importsKernel = Regex.IsMatch(File.ReadAllText(file), @"using\s+static\s+(?:TLDRevamp\.Net\.)?Entities\s*;");
                foreach (string row in File.ReadLines(file))
                {
                    if (Regex.IsMatch(row, @"\bEntities\.(?:ByNet|Server|W|WS)\b|GetField\(\""(?:ByNet|Server|W|WS)\""") || importsKernel && Regex.IsMatch(row, @"(?<!\.)\b(?:ByNet|Server|W|WS)\b")) actual.Add(path + "|" + row.Trim());
                }
            }
        Check(actual.OrderBy(x => x).SequenceEqual(File.ReadAllLines("tools/unit/EntityKernelOutsideRows.txt").OrderBy(x => x)), "no new outside table or writer references");
    }
}
