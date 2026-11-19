using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public partial class fpscontroller
{
    public LayerMask useLayer, pickupLayer, attachLayer;
    public float FrayRange = 2;
}
public partial class partslotscript { public bool disableCollidersOnCraft, cantCraft, cantUnCraft, attachTarget; public pickupable pick; }
public partial class tosaveitemscript { public List<DiagRandom> randoms; }
public sealed class DiagRandom { public int rtipus; }
public partial class attachablescript { public Transform T; public DiagCondition condition; }
public sealed class DiagCondition { public int state, state2, state3; }
public class utanfutoscript : Component { }
namespace UnityEngine
{
    public partial struct LayerMask { public int value; }
    public static partial class Physics { public static RaycastHit[] RaycastAll(Ray ray, float reach, int mask) => Hits; }
}
namespace TLDRevamp
{
    internal interface IHistoryRow { double LocalTime { get; } }
    internal static partial class Json
    {
        internal static string Num(double value, string format = "G") => double.IsNaN(value) || double.IsInfinity(value) ? "null" : value.ToString(format, CultureInfo.InvariantCulture);
    }
    internal static partial class TelemetryPrivacy
    {
        internal static string Str(string value) => Json.Str(value);
        internal static string Player(int player) => "player" + player;
    }
    internal static partial class DevHooks { internal static Func<attachablescript, IReadOnlyList<Renderer>> PartRenderers; }
    internal static partial class SessionLog
    {
        internal static partial class History
        {
            internal static partial class Trace
            {
                internal static readonly List<MotionRow> Rows = new List<MotionRow>();
                internal static bool Contains(uint net) => Nets.Contains(net);
                internal static void Add(MotionRow row) => Rows.Add(row);
                internal static void Write(TextWriter writer, double now, bool privateNames) => writer.Write("fixture trace");
            }
        }
    }
}
namespace TLDRevamp.Net
{
    internal static class DiagMountFactsFixture
    {
        internal static void MountFacts(partslotscript slot, attachablescript part, pickupable pickup, fpscontroller player, System.Text.StringBuilder text, int index) => text.Append(",\"fixtureMount\":true");
    }
    public static partial class Entities
    {
        public sealed partial class Ent { public float TelemetryInterval; public double TelemetryReceived; }
        internal static IDictionary<uint, Ent> DiagMap => ByNet;
        internal static void DiagPose(Ent entity, int kind, double clock, Vector3d position, Quaternion rotation, Vector3 velocity, int item = -1, int slot = -1, int mode = 0, float correction = 0)
            => EntityTelemetry.TelemetryPose(entity, kind, clock, position, rotation, velocity, item, slot, mode, correction);
        internal static int DiagCategory(tosaveitemscript item) => EntityTelemetry.TelemetryRootCategory(item);
        internal static void DiagSlot(TelemetryState state, Ent car, int item, int index, partslotscript slot, attachablescript part) => CaptureSlot(state, car, item, index, slot, part);
        internal static void DiagStations(TelemetryState state, List<mainscript.radioCH> list, bool am) => EntityTelemetry.CaptureStations(state, list, am);
        internal static StateUsable DiagUsable(usablescript usable) => EntityTelemetry.CaptureUsable(usable);
    }
}
public partial class mainscript { public static int pickedUpLayer = 8; }
public partial class partslotscript { public string[] tipus; }
namespace TLDRevamp { internal static class TelemetryContent { internal static string ItemName(int id, string fallback = "") => fallback.Length > 0 ? fallback : "prefab" + id; } }
