using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using BepInEx;
using TLDRevamp.Multiplayer;
using UnityEngine;

namespace ApiProbe
{
    /// A test mod for the multiplayer API's second step (tools/modapi2.py; never shipped): beacons with a label in their
    /// record, a needle in their state records (only players near get it), a far store range, and one session-wide
    /// setting. Driven through the debug bridge: `call ApiProbe.Probe.Spawn a 0 5`, `call ApiProbe.Probe.Status`.
    /// API 6: tallies, whose edits are replayed on copies (SharedType.ReplayEdits): `call ApiProbe.Probe.SpawnTally t 0 5 0`,
    /// `call ApiProbe.Probe.Bump t 1 10`, `call ApiProbe.Probe.Tally t`.
    [BepInPlugin("tldrevamp.apiprobe", "API Probe", Version)]
    [BepInDependency("tldrevamp.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "0.1.0";

        private void Awake()
        {
            Probe.Mod = MpApi.RegisterMod("apiprobe", "API Probe", Version);
            Probe.Type = MpApi.RegisterType(Probe.Mod, new BeaconType());
            Probe.TallyType = MpApi.RegisterType(Probe.Mod, new TallyType());
            Probe.RuleSetting = MpApi.RegisterSetting(Probe.Mod, "rule", () => Probe.Rule, v => { Probe.Rule = v; Probe.RuleApplied++; });
            Logger.LogInfo("API Probe registered (API " + MpApi.ApiVersion + ")");
        }
    }

    public sealed class Beacon : MonoBehaviour
    {
        public string Label = "";
        public float Needle;
        public int StatesRead;
    }

    public sealed class BeaconType : SharedType
    {
        public override string Name => "beacon";
        public override bool HasState => true;
        public override float StateHz => 10f;
        public override float StateRange => Probe.StateRangeM;
        public override float ViewRange => Probe.ViewRangeM;

        public override GameObject Create(RecordReader record, Vector3 position, Quaternion rotation)
        {
            string label = record.String();
            if (record.Bad) return null;
            return Probe.Make(label, position);
        }

        public override void Write(GameObject obj, RecordWriter record) => record.String(obj.GetComponent<Beacon>().Label);
        public override void Read(GameObject obj, RecordReader record) { var l = record.String(); if (!record.Bad) obj.GetComponent<Beacon>().Label = l; }
        public override void WriteState(GameObject obj, RecordWriter state) => state.Float(obj.GetComponent<Beacon>().Needle);

        public override void ReadState(GameObject obj, RecordReader state)
        {
            float v = state.Float();
            if (state.Bad) return;
            var b = obj.GetComponent<Beacon>();
            b.Needle = v; b.StatesRead++;
        }

        public override void OnPlaced(SharedObject obj) => Probe.Placed++;

        public override void OnStored(SharedObject obj)
        {
            Probe.Stored++;
            base.OnStored(obj);
        }
    }

    /// A count, the edits that made it, the session time of the last one, and `Pad` bytes of filler (a large record
    /// for the cost of replaying), whose edits copies replay. Edit 1: add n. Edit 2: add a random number (not
    /// deterministic on purpose: copies come to another record and must ask for the whole one).
    public sealed class Tally : MonoBehaviour
    {
        public string Label = "";
        public int Count, Pad;
        public string Log = "";
        public double LastT;
    }

    public sealed class TallyType : SharedType
    {
        public override string Name => "tally";
        public override int MaxRecordBytes => 64 * 1024;
        public override float ViewRange => Probe.ViewRangeM;
        public override bool ReplayEdits => Probe.Replay;

        public override GameObject Create(RecordReader record, Vector3 position, Quaternion rotation)
        {
            var go = Probe.MakeTally("", position);
            Read(go, record);
            return go;
        }

        public override void Write(GameObject obj, RecordWriter record)
        {
            var t = obj.GetComponent<Tally>();
            record.String(t.Label); record.Int(t.Count); record.String(t.Log); record.Double(t.LastT); record.VarUInt((uint)t.Pad);
            for (int i = 0; i < t.Pad; i++) record.Byte((byte)(i * 31 + t.Count));
        }

        public override void Read(GameObject obj, RecordReader record)
        {
            string label = record.String(); int count = record.Int(); string log = record.String(); double at = record.Double(); int pad = (int)record.VarUInt();
            if (record.Bad) return;
            var t = obj.GetComponent<Tally>();
            t.Label = label; t.Count = count; t.Log = log; t.LastT = at; t.Pad = pad;
            obj.name = "ApiProbeTally " + label;
        }

        public override bool OnEdit(SharedObject obj, RecordReader edit, int fromPlayer)
        {
            var t = obj.GameObject.GetComponent<Tally>();
            byte op = edit.Byte(); int n = edit.Int();
            if (edit.Bad) return false;
            if (op == 2) n = Random.Range(1, 1000);
            else if (op != 1) return false;
            t.Count += n;
            t.Log = (t.Log + fromPlayer + ":" + n + ",");
            if (t.Log.Length > 120) t.Log = t.Log.Substring(t.Log.Length - 120);
            t.LastT = MpApi.SessionTime;
            return true;
        }
    }

    public static class Probe
    {
        public static TallyType TallyType;
        /// Tallies replay their edits (off: the whole record after each, as before API 6).
        public static bool Replay = true;
        public static ModInfo Mod;
        public static BeaconType Type;
        public static SessionSetting RuleSetting;
        public static readonly float ViewRangeM = 800f, StateRangeM = 300f;
        public static string Rule = "easy";
        public static int Placed, Stored, RuleApplied;

        internal static GameObject Make(string label, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "ApiProbeBeacon " + label;
            go.transform.position = pos;
            go.AddComponent<Beacon>().Label = label;
            return go;
        }

        /// A beacon dx, dz metres from this player, shared and owned here.
        public static string Spawn(string label, float dx, float dz)
        {
            var p = mainscript.s.player.transform.position;
            var go = Make(label, p + new Vector3(dx, 1.5f, dz));
            go.AddComponent<visszarako>();   // moves with this machine's floating origin, as the session does for copies
            MpApi.Share(Type, go);
            return label;
        }

        private static Beacon Find(string label, out SharedObject so)
        {
            foreach (var o in MpApi.Objects)
                if (o.Type == Type && o.GameObject != null && o.GameObject.GetComponent<Beacon>().Label == label) { so = o; return o.GameObject.GetComponent<Beacon>(); }
            so = null; return null;
        }

        /// The owner moves the needle (copies near get it as a state record).
        public static string SetNeedle(string label, float v)
        {
            var b = Find(label, out var so);
            if (b == null) return "no " + label + " here";
            if (!so.IsMine) return label + " is not mine (owner " + so.Owner + ")";
            b.Needle = v;
            return "ok";
        }

        public static string SetRule(string v) { Rule = v; return Rule; }

        internal static GameObject MakeTally(string label, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "ApiProbeTally " + label;
            go.transform.position = pos;
            go.AddComponent<Tally>().Label = label;
            return go;
        }

        /// A tally dx, dz metres from this player with `pad` bytes of filler, shared and owned here.
        public static string SpawnTally(string label, float dx, float dz, int pad)
        {
            var go = MakeTally(label, mainscript.s.player.transform.position + new Vector3(dx, 1.5f, dz));
            go.GetComponent<Tally>().Pad = Mathf.Clamp(pad, 0, 60000);
            go.AddComponent<visszarako>();
            MpApi.Share(TallyType, go);
            return label;
        }

        private static Tally FindTally(string label, out SharedObject so)
        {
            foreach (var o in MpApi.Objects)
                if (o.Type == TallyType && o.GameObject != null && o.GameObject.GetComponent<Tally>().Label == label) { so = o; return o.GameObject.GetComponent<Tally>(); }
            so = null; return null;
        }

        /// `times` edits adding `n` from this machine, in one frame (on the owner they are applied at once); the time
        /// they took here, in milliseconds per edit.
        public static string Bump(string label, int n, int times)
        {
            var t = FindTally(label, out var so);
            if (t == null) return "no " + label + " here";
            var sw = Stopwatch.StartNew(); int sent = 0;
            for (int i = 0; i < times; i++) if (so.RequestEdit(w => { w.Byte(1); w.Int(n); })) sent++;
            sw.Stop();
            // not "{1:F4}}}": .NET reads "F4}" as the format
            return "{\"sent\":" + sent + ",\"msPerEdit\":" + (sw.Elapsed.TotalMilliseconds / Mathf.Max(1, times)).ToString("F4", CultureInfo.InvariantCulture) + "}";
        }

        /// `times` edits adding 1 to every tally this machine owns, all in one frame (over the host's message budget
        /// with a few tallies); how many it sent.
        public static string BumpAll(int times)
        {
            int sent = 0;
            foreach (var o in new List<SharedObject>(MpApi.Objects))
                if (o.Type == TallyType && o.IsMine && o.GameObject != null)
                    for (int i = 0; i < times; i++) if (o.RequestEdit(w => { w.Byte(1); w.Int(1); })) sent++;
            return "sent " + sent;
        }

        /// An edit that adds a random number: copies can't replay it (tests the whole-record fallback).
        public static string Noisy(string label)
        {
            var t = FindTally(label, out var so);
            if (t == null) return "no " + label + " here";
            return so.RequestEdit(w => { w.Byte(2); w.Int(0); }) ? "sent" : "refused";
        }

        public static string Claim(string label)
        {
            var t = FindTally(label, out var so);
            if (t == null) return "no " + label + " here";
            so.RequestOwnership();
            return "asked (owner " + so.Owner + ")";
        }

        public static string SetReplay(bool on) { Replay = on; return "replay " + on; }

        /// This machine's tally: its count, the last edit's time, the owner, and a hash of its record (the same
        /// on every machine once in step).
        public static string Tally(string label)
        {
            var t = FindTally(label, out var so);
            if (t == null) return "{\"here\":false}";
            var w = new RecordWriter(); TallyType.Write(t.gameObject, w);
            ulong h = 14695981039346656037UL; var buf = w.ToArray(); foreach (var x in buf) { h ^= x; h *= 1099511628211UL; }
            return string.Format(CultureInfo.InvariantCulture, "{{\"here\":true,\"count\":{0},\"lastT\":{1:R},\"owner\":{2},\"mine\":{3},\"bytes\":{4},\"hash\":\"{5:x16}\"}}",
                t.Count, t.LastT, so.Owner, so.IsMine ? "true" : "false", buf.Length, h);
        }

        public static string Status()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{\"beacons\":[");
            bool first = true;
            foreach (var o in MpApi.Objects)
            {
                if (o.Type != Type || o.GameObject == null) continue;
                var b = o.GameObject.GetComponent<Beacon>();
                if (!first) sb.Append(','); first = false;
                sb.Append(string.Format(ci, "{{\"label\":\"{0}\",\"id\":{1},\"owner\":{2},\"mine\":{3},\"needle\":{4:F2},\"statesRead\":{5}}}",
                    b.Label, o.Id, o.Owner, o.IsMine ? "true" : "false", b.Needle, b.StatesRead));
            }
            sb.Append(string.Format(ci, "],\"placed\":{0},\"stored\":{1},\"rule\":\"{2}\",\"ruleFromHost\":{3},\"ruleApplied\":{4}}}",
                Placed, Stored, Rule, RuleSetting != null && RuleSetting.FromHost ? "true" : "false", RuleApplied));
            return sb.ToString();
        }
    }
}
