#if HITCH_RUNTIME
using System;
using System.Collections.Generic;
using System.IO;
using TLDRevamp.Net;
static class EntityHitchChecks
{
    static readonly List<string> Rows = new List<string>();
    internal static void Row(string row) => Rows.Add(row);
    internal static void Capture(string why) => Rows.Add(Entities.HitchRow(why));
    internal static void Finish()
    {
        var expected = File.ReadAllLines("tools/unit/EntityHitchRows.txt");
        if (Rows.Count != expected.Length) throw new Exception("hitch row count changed");
        for (int i = 0; i < expected.Length; i++)
            if (Rows[i] != expected[i]) throw new Exception("hitch HEAD row changed at " + i + ": " + Rows[i]);
    }
}
#else
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TLDRevamp;

using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static void HitchChecks()
    {
        CoreMembers.Register();
        var owner = typeof(EntityHitches);
        Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, "hitches has an internal static owner");
        foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
        {
            bool retained = typeof(Entities).GetMember(member.Name, Static).Length != 0;
            foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "hitch alias " + prefix + member.Name);
        }
        BridgeReflect.Set("TLDRevamp.Net.Entities.ProxyGripsMoved", "17");
        Check(EntityHitches.ProxyGripsMoved == 17 && BridgeReflect.Get("Entities.ProxyGripsMoved") == "17", "hitch counter aliases share the real field");
        EntityHitches.ProxyGripsMoved = 0;

        var op = new EntityHitches.HitchOp { Trailer = 20, Car = 10, TrailerEpoch = 0x10203040, CarEpoch = 0x50607080,
            Slot = -2, From = 130, Connect = true, ReleaseBrake = false };
        var w = new NetWriter(); w.U8(Entities.HitchRequest); EntityHitches.WriteHitch(w, op);
        Check(Convert.ToHexString(w.Buf.AsSpan(0, w.Len)) == "63140000000A00000040302010807060500384020100", "hitch request legacy bytes including signed slot and sender");
        var read = EntityHitches.ReadHitch(Reader(w.Buf.Take(w.Len).ToArray()));
        Check(read.Trailer == op.Trailer && read.Car == op.Car && read.TrailerEpoch == op.TrailerEpoch && read.CarEpoch == op.CarEpoch &&
            read.Slot == op.Slot && read.From == op.From && read.Connect && !read.ReleaseBrake, "hitch codec reads all fields in order");
        EntityHitches.ApplyHitchCommand(new NetReader());
        Check(!EntityHitches.HasCommand(20), "bad command never enters queue");
        w = new NetWriter(); w.U8(Entities.HitchCommand); EntityHitches.WriteHitch(w, op);
        EntityHitches.ApplyHitchCommand(Reader(w.Buf.Take(w.Len).ToArray()));
        Check(EntityHitches.HasCommand(20), "command queue belongs to feature");
        EntityHitches.RemoveCommand(20); Check(!EntityHitches.HasCommand(20), "one command removal leaves no pending command");
        EntityHitches.ApplyHitchCommand(Reader(w.Buf.Take(w.Len).ToArray()));
        BridgeReflect.Call("Entities.ClearCommands", Array.Empty<string>());
        Check(!EntityHitches.HasCommand(20), "command reset alias clears the actual queue");

        var e = new Entities.Ent { Root = new tosaveitemscript { attachable = new attachablescript() } };
        Entities.ByNet[20] = e; Entities.PlayerActing = true;
        Set(owner, "_hitchApplying", true);
        Check(!Entities.ReproDetachAllowed && Entities.ReproAttachEntity(20) == null, "repro guards observe feature applying state through kernel");
        Set(owner, "_hitchApplying", false);
        Check(Entities.ReproDetachAllowed && Entities.ReproAttachEntity(20) == e, "repro guards reopen after hitch apply");
        Entities.ByNet.Clear();
        HitchGuardChecks();
        HitchSourceChecks();
    }

    static void HitchGuardChecks()
    {
        var hook = typeof(EntityHitches).GetNestedType("HitchSlotHook", BindingFlags.NonPublic).GetMethod("Prefix", Static);
        var at = new attachablescript { utanfuto = new HitchTrailerBody() };
        bool AttachAllowed() => (bool)hook.Invoke(null, new object[] { at, null });
        int sent = Entities.Packets.Count;
        Entities.InSession = false; Check(AttachAllowed(), "hitch hook leaves solo attach to game"); Entities.InSession = true;
        Set(typeof(EntityHitches), "_hitchApplying", true); Check(AttachAllowed(), "hitch apply does not request another attach"); Set(typeof(EntityHitches), "_hitchApplying", false);
        Entities.HitchApplyingAttach = true; Check(AttachAllowed(), "ordinary attach apply does not request a hitch"); Entities.HitchApplyingAttach = false;
        Entities.PlayerActing = false; Check(AttachAllowed(), "automatic attach remains a game action"); Entities.PlayerActing = true;
        Check(!AttachAllowed() && Entities.Packets.Count == sent, "player attach to unshared target is refused before send");

        var root = new tosaveitemscript(); var body = new Rigidbody { velocity = new Vector3(1, 2, 3) };
        var other = new tosaveitemscript(); var splitBody = new Rigidbody();
        root.ChildComponent = body; root.Children.Add(body); other.Children.Add(splitBody);
        var e = new Entities.Ent { Root = root, Split = new System.Collections.Generic.HashSet<int> { 1 } };
        e.Items.Add(root); e.Items.Add(other); e.Items.Add(root);
        Check(EntityHitches.HitchBodies(e).SequenceEqual(new[] { body }), "hitch body walk skips split members and deduplicates bodies");
        EntityHitches.HitchFreezeBodies(e);
        Check(body.isKinematic && !splitBody.isKinematic && e.HitchBodies.Count == 1 && e.LastVel == new Vector3(1, 2, 3), "freeze saves velocity and changes only participating bodies");
        e.HitchWaiting = true; EntityHitches.HitchThaw(e); Check(body.isKinematic, "waiting trailer stays frozen");
        e.HitchWaiting = false; e.Proxy = true; EntityHitches.HitchThaw(e); Check(body.isKinematic, "proxy trailer stays frozen");
        e.Proxy = false; e.SentAtRest = true; EntityHitches.HitchThaw(e);
        Check(!body.isKinematic && body.velocity == e.LastVel && e.HitchBodies.Count == 0 && !e.SentAtRest, "thaw restores motion and rest state");

        at = root.attachable = new attachablescript(); root.P = new pickupable();
        var grip = new attachgripscript { GripPos = new Transform { position = new Vector3(4, 5, 6) } };
        root.ChildComponent = grip; at.ChildComponent = body;
        long moved = EntityHitches.ProxyGripsMoved;
        EntityHitches.ProxyGripToTrailer(e);
        Check(at.transform.position == grip.GripPos.position && body.position == grip.GripPos.position && EntityHitches.ProxyGripsMoved == moved + 1, "loose proxy drawbar moves both transform and rigidbody once");
        EntityHitches.ProxyGripToTrailer(e);
        Check(EntityHitches.ProxyGripsMoved == moved + 1, "settled drawbar stays quiet inside distance threshold");
        EntityHitches.ProxyGripsMoved = 0;
    }

    static string HitchCanonical(string body) => body
        .Replace("!Entities.TryHitchFreezeTarget(net, epoch, out var tr)", "!ByNet.TryGetValue(net, out var tr) || tr.Epoch != epoch || tr.OwnerId != MyId")
        .Replace("!Entities.TryHitchFrozenTarget(net, epoch, out var tr)", "!ByNet.TryGetValue(net, out var tr) || tr.Proxy || tr.Epoch != epoch")
        .Replace("Entities.IsHitchTrailer(", "HitchTrailer(")
        .Replace("Entities.HitchTowEntity(", "HitchCar(")
        .Replace("Entities.TryHitchEntity(", "ByNet.TryGetValue(")
        .Replace("Entities.HitchRootEntity(", "OwnEntity(")
        .Replace("Entities.HitchGroupEntity(", "EditEntityOf(")
        .Replace("Entities.HitchMayJoin(", "HitchCanJoin(")
        .Replace("Entities.ResolveHitch(", "Resolve(")
        .Replace("Entities.FreezeHitchProxy(", "ApplyProxyBodies(")
        .Replace("Entities.UnmountHitch(", "Unmount(")
        .Replace("Entities.SyncHitchDetach(", "SendDetachSync(")
        .Replace("Entities.SendHitchRequest(", "ToServer(")
        .Replace("Entities.HitchDamageGrace(car, tr.HitchUntil);", "DamageGraceUntil[car.Root.car] = tr.HitchUntil;")
        .Replace("Entities.HitchCommandTimeoutS", "HitchTimeoutS")
        .Replace("Entities.HitchCommandExpires", "HitchTransferTimeout")
        .Replace("Entities.HitchLocalId", "MyId")
        .Replace("Entities.HitchInSession", "InSession")
        .Replace("Entities.HitchPlayerActing", "PlayerActing")
        .Replace("Entities.HitchApplyingAttach", "_applyingAttach")
        .Replace("Entities.HitchApplyingDetach", "_applyingDetach")
        .Replace("EntityHitches.RemoveCommand(net);", "_hitchCommands.Remove(net);")
        .Replace("Entities.IsFinite(", "Finite(")
        .Replace("EntityCodec.", "").Replace("EntityHitches.", "").Replace("Entities.", "");

    static void HitchSourceChecks()
    {
        string source = File.ReadAllText("src/TLDRevamp/Net/Entities.Hitch.cs").Replace("\r\n", "\n");
        string feature = source.Split("    public static partial class Entities")[0];
        Check(!Regex.IsMatch(feature, @"using\s+static\s+.*Entities|Entities\.(?:W|WS|ByNet|Server|ObservedEntities|AllEnts|TryGetEntity|Placed)\b"), "hitches cannot access kernel maps, enumerators or writers");
        foreach (string name in new[] { "HitchCraftHook", "HitchSlotHook", "HitchJointHook" })
            Check(feature.Contains("class " + name), name + " moved with feature");
        string hook = feature.Substring(feature.IndexOf("class HitchJointHook", StringComparison.Ordinal));
        Check(Regex.IsMatch(hook, @"\[HarmonyPriority\(Priority.First\)\]\s+private static bool Prefix"), "hitch joint still runs before AttachHook and TelemetryJointCreated prefixes");
        foreach (string row in File.ReadAllLines("tools/unit/EntityHitchSourceRows.txt"))
        {
            var c = row.Split(' ');
            var matches = Regex.Matches(source, @"^        (?:private|internal|public) static [^\n]*\b" + c[0] + @"\(", RegexOptions.Multiline);
            var match = matches[int.Parse(c[1])];
            int brace = source.IndexOf('{', match.Index), arrow = source.IndexOf("=>", match.Index, StringComparison.Ordinal);
            string body;
            if (arrow >= 0 && (brace < 0 || arrow < brace)) body = source.Substring(arrow, source.IndexOf(';', arrow) + 1 - arrow);
            else
            {
                int depth = 1, end = brace + 1;
                while (depth > 0) { depth += source[end] == '{' ? 1 : source[end] == '}' ? -1 : 0; end++; }
                body = source.Substring(brace, end - brace);
            }
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Regex.Replace(HitchCanonical(body), @"\s+", ""))));
            Check(hash == c[2], c[0] + " hitch implementation matches f183a37 after boundary substitutions");
        }
        string kernel = source.Substring(feature.Length);
        foreach (string name in new[] { "ServerHitchRequest", "BeginHitchTransfer", "ServerHitchFrozen", "CompleteHitchTransfer", "HitchBeforeOwner", "CascadeHitches", "HitchReset", "HitchTick", "ServerHitchTick", "ReleaseHitch", "RemoveHitches", "HitchInitialTick", "HitchPhysicsTelemetry", "HitchFallGuard" })
            Check(kernel.Contains(name), name + " retains map walks or authority in kernel");
        string pipeline = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(pipeline.Contains("_hitchOps.Clear(); _hitchTransfers.Clear(); EntityHitches.ClearCommands(); _hitchDeparted.Clear();"), "reset keeps hitch transaction clear order");
        string observation = File.ReadAllText("src/TLDRevamp/Net/Entities.Observation.cs");
        Check(observation.Contains("_hitchTransfers.ContainsKey(net) || EntityHitches.HasCommand(net)"), "pending observation keeps transfer before command query");
    }
}
#endif
