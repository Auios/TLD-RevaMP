using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TLDRevamp.Net;
using UnityEngine;

public partial class attachablescript { public void FallOFf() { } }
public partial class save_attachable { public uint id; }
public class cheatedStuff : Component { public void Delete() { } }
public partial class tosaveitemscript { public object useThisForDistance; public void FStart() { } }
public partial class fpscontroller { public void usePickedUpStuff() { } }
public partial class PoseInterpolator { public double Hold, Target; public double OwnTarget(double now) => Target; }
public partial class mountStuff { public List<tosaveitemscript> storedcars = new List<tosaveitemscript>(); }
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public sealed partial class Ent { public bool FarDirty; }
        private static bool PartIsProxy(attachablescript at) => at?.tosave != null && ByNet.Values.Any(e => e.Proxy && e.Items.Contains(at.tosave));
    }
}

namespace LifetimeStreamingFakes
{
    internal static class Kernel
    {
        internal const byte MemberGone = 86;
        internal static bool LifetimeInSession = true, Proxy;
        internal static long LifetimeSpawnCount, Suppressed;
        internal static Action<GameObject> DuringShare;
        internal static readonly List<GameObject> Shared = new List<GameObject>();
        internal static readonly Dictionary<Transform, Entities.Ent> Copies = new Dictionary<Transform, Entities.Ent>();
        internal static bool LifetimePartIsProxy(attachablescript at) => Proxy;
        internal static void RecordLifetimeFallOff() => Suppressed++;
        internal static Entities.Ent ProxyForLinkedDelay(Transform root) => Copies.TryGetValue(root, out var e) ? e : null;
        internal static void ShareLifetimeSpawn(GameObject go) { Shared.Add(go); DuringShare?.Invoke(go); if (go != null && !go.Destroyed) LifetimeSpawnCount++; }
    }
    internal sealed class Save
    {
        internal static Save s;
        internal sealed class Data { internal itemDataClass itemData = new itemDataClass(); }
        internal Data data = new Data();
        internal Dictionary<uint, tosaveitemscript> items = new Dictionary<uint, tosaveitemscript>();
        internal static bool IndexOfID(List<int> rows, uint id, out int index) { index = rows.IndexOf((int)id); return index >= 0; }
    }
    internal sealed class Menu
    {
        internal static Menu s;
        internal Map currentMainMap;
        internal sealed class Map { internal List<Center> genArounds; }
        internal sealed class Center { internal Vector3 upos; }
    }
    internal sealed class Placer { internal static Placer s; internal double itemRemoveDist; }
    internal static class Snapshot { internal static FieldInfo[] ListFields = typeof(itemDataClass).GetFields(); }
    internal static class Lookups
    {
        internal static FieldInfo SaveIdField(Type type, string name, Type row) => row.GetField(name);
        internal static MethodInfo Method(Type type, string name, Type[] args = null) => args == null ? type.GetMethod(name) : type.GetMethod(name, args);
    }
    internal static class Rotation
    {
        internal static float Angle(Quaternion a, Quaternion b)
        {
            double dot = Math.Min(Math.Abs(a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w), 1);
            return dot > .999999 ? 0 : (float)(Math.Acos(dot) * 360 / Math.PI);
        }
    }
    internal sealed class Row { public uint id; public string value; public Row(uint key, string text) { id = key; value = text; } }
    internal static class TrailerGrip
    {
        internal static readonly List<Grip> All = new List<Grip>();
        internal sealed class Grip { internal tosaveitemscript tosaveitem; }
    }
}
