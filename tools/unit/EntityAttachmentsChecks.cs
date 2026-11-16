using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;
using AW = AttachmentFakes.WireKernel;

static partial class EntitySplitTest
{
    static object AttachmentCall(string name, params object[] args) => Invoke(typeof(EntityAttachments), name, args);
    static void AttachmentHook(string phase, params object[] args) => Patch(typeof(EntityAttachments), "AttachHook", phase, args);
    static void AttachmentsChecks()
    {
        var rows = new List<string>(); var previousServer = Mp.Server;
        var owner = typeof(EntityAttachments);
        var cache = (IDictionary)Field(owner, "_carriedOff");
        var told = (IDictionary)Field(owner, "_partOffToldAt");
        Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.AttachmentNotices.Clear();
        Entities.InSession = true; Entities.IsHost = false; Entities.MyId = 2;
        Entities._applyingAttach = Entities._applyingState = Entities._applyingDetach = false;
        EntityAttachments.AttachRequests = Entities.CarriedPartsShared = true;
        EntityAttachments.AttachPosesSent = EntityAttachments.AttachPosesBad = EntityAttachments.AttachPosesApplied = 0;
        cache.Clear(); told.Clear();
        var item = new tosaveitemscript(); var at = new attachablescript { tosave = item, attached = true };
        item.attachable = at;
        var e = new Entities.Ent { NetId = 501, Epoch = 7, Root = item }; e.Items.Add(item); Entities.ByNet[e.NetId] = e;
        byte[] Pose()
        {
            var w = new NetWriter(); EntityAttachments.WriteAttachPose(w, at); return w.Buf.Take(w.Len).ToArray();
        }
        rows.Add("no-frame|" + Convert.ToHexString(Pose()));
        var frame = new Transform { position = new Vector3(10, 20, 30) };
        at.point = new Transform(); at.point.SetParent(frame, true); at.transform.position = new Vector3(11.25f, 17.5f, 33.75f);
        at.transform.rotation = Quaternion.identity;
        byte[] pose = Pose(); rows.Add("parent-frame|" + Convert.ToHexString(pose));
        at.slot = new partslotscript(); rows.Add("slot|" + Convert.ToHexString(Pose())); at.slot = null;
        AW.Record = new save_attachable(at, 0) { attachType = 1, indexType = 1, index = 5, poigenid = -1, poiid = new Vector3d(7, 8, 9), lpos = new Vector3d(4, 5, 6), gpos = new Vector3d(1, 2, 3) };
        AW.Parent = new tosaveitemscript(); var sender = new AW.Ent { NetId = 501, Epoch = 7, Root = item };
        AW.Group = new[] { sender, new AW.Ent { NetId = 600, Root = AW.Parent } }; AW.Packets.Clear();
        foreach (byte type in new byte[] { 41, 62 }) { AW.Send(sender, at, type); rows.Add("attach-wire-" + type + "|" + AW.Packets.Last()); }
        var detachWriter = new NetWriter(); EntityCodec.WriteDetachSync(detachWriter, 501, 7, new Vector3d(1, 2, 3), Quaternion.identity);
        rows.Add("detach-wire|" + Convert.ToHexString(detachWriter.Buf.AsSpan(0, detachWriter.Len)));
        for (int n = 0; n <= pose.Length; n++)
        {
            var r = new NetReader { Buf = pose, End = n };
            object[] args = { r, null, null, null, null };
            long before = EntityAttachments.AttachPosesBad;
            bool has = (bool)AttachmentCall("ReadAttachPose", args);
            rows.Add("read-" + n + "|" + has + "|" + r.Bad + "|" + r.Pos + "|" + (EntityAttachments.AttachPosesBad - before));
            if (n == pose.Length) Check((Vector3)args[1] == new Vector3(1.25f, -2.5f, 3.75f), "attach pose is relative to translated parent frame");
        }
        foreach (byte[] bad in new[] { new byte[] { 0 }, new byte[] { 3 }, new byte[] { 2, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 65 } })
        {
            var r = new NetReader { Buf = bad, End = bad.Length }; object[] args = { r, null, null, null, null };
            long before = EntityAttachments.AttachPosesBad;
            rows.Add("kind|" + Convert.ToHexString(bad) + "|" + AttachmentCall("ReadAttachPose", args) + "|" + r.Bad + "|" + (EntityAttachments.AttachPosesBad - before));
        }
        at.transform.position = new Vector3(5000, 0, 0);
        var large = Pose(); var largeReader = new NetReader { Buf = large, End = large.Length }; object[] largeArgs = { largeReader, null, null, null, null };
        long badBefore = EntityAttachments.AttachPosesBad;
        rows.Add("large|" + AttachmentCall("ReadAttachPose", largeArgs) + "|" + largeReader.Bad + "|" + (EntityAttachments.AttachPosesBad - badBefore));
        EntityAttachments.ApplyAttachPose(e, at, new Vector3(2, 3, 4), Quaternion.identity);
        Check(at.transform.position == new Vector3(12, 23, 34) && EntityAttachments.AttachPosesApplied == 1, "apply reconstructs world pose in parent frame");
        foreach (string guard in new[] { "ordinary", "attach", "state", "offline", "missing", "proxy", "proxy-inactive", "proxy-disabled" })
        {
            Entities.AttachmentTestTrace = ""; Entities.InSession = guard != "offline";
            Entities._applyingAttach = guard == "attach"; Entities._applyingState = guard == "state";
            at.tosave = guard == "missing" ? null : item; e.Proxy = guard.StartsWith("proxy");
            Entities.PlayerActing = guard != "proxy-inactive"; EntityAttachments.AttachRequests = guard != "proxy-disabled";
            var oldPose = e.Ip; AttachmentHook("Postfix", at);
            rows.Add("hook-" + guard + "|" + Entities.AttachmentTestTrace + "|" + !ReferenceEquals(oldPose, e.Ip));
        }
        Entities.InSession = Entities.PlayerActing = EntityAttachments.AttachRequests = true;
        Entities._applyingAttach = Entities._applyingState = false; at.tosave = item; e.Proxy = false;
        var carrierItem = new tosaveitemscript { partslotscripts = new List<partslotscript>() };
        var slot = new partslotscript { tosaveitem = carrierItem, Part = at }; carrierItem.partslotscripts.Add(slot); at.slot = slot;
        var carrier = new Entities.Ent { NetId = 600, Epoch = 9, Root = carrierItem, ParentNet = 700, PartIndex = 2 };
        carrier.Items.Add(carrierItem); Entities.ByNet.Clear(); Entities.ByNet[600] = carrier;
        item.transform.position = new Vector3(4, 5, 6); item.transform.rotation = new Quaternion(.1f, .2f, .3f, .9f);
        object[] far = { at, null }; Check((bool)AttachmentCall("CarriedFar", far) && ReferenceEquals(far[1], carrier), "far carrier is found by its own root");
        AttachmentCall("NoteCarrier", at); at.slot = null;
        long sentBefore = EntityAttachments.CarriedOffSent;
        Check((bool)AttachmentCall("SentCarriedOff", item), "detach consumes recorded carrier after slot is gone");
        var packet = Entities.Packets.Last();
        rows.Add("carried-wire|" + packet.Peer + "|" + packet.Reliable + "|" + Convert.ToHexString(packet.Bytes) + "|" + (EntityAttachments.CarriedOffSent - sentBefore));
        Check(ReferenceEquals(AttachmentCall("CarriedItem", 600u, 0, 2), item) && cache.Count == 0, "sender consumes saved detached item once");
        Check(ReferenceEquals(AttachmentCall("CarriedItem", 600u, 0, 3), item), "remote carrier slot resolves its current item");
        Check(AttachmentCall("CarriedItem", 600u, -1, 3) == null && AttachmentCall("CarriedItem", 999u, 0, 3) == null, "missing carrier and bad slot stay absent");
        Check(!(bool)AttachmentCall("SentCarriedOff", item), "carrier token consumed exactly once");
        for (int n = 0; n < 66; n++)
        {
            AttachmentCall("SendCarriedOff", item, carrier, n);
            if (n >= 63) rows.Add("cache-" + n + "|" + cache.Count);
        }
        at.slot = slot; AttachmentCall("NoteCarrier", at);
        long sent = EntityAttachments.CarriedOffSent; EntityAttachments.ResetCarriedOff();
        Check(cache.Count == 0 && EntityAttachments.CarriedOffSent == sent && Field(owner, "_detachCarrier") != null, "reset clears only carried cache and keeps token and counters");
        AttachmentCall("SentCarriedOff", (object)null);
        EntityAttachments.PartOffToldS = 30; Time.realtimeSinceStartup = 10; Mp.Server = new Mp.SeatServer();
        foreach (var step in new[] { (2, 501u, 10f), (2, 501u, 39.999f), (3, 501u, 39.999f), (2, 502u, 39.999f), (2, 501u, 40f) })
        {
            Time.realtimeSinceStartup = step.Item3;
            EntityAttachments.TellPartOffRefused(step.Item1, step.Item2);
            rows.Add("notice|" + step.Item1 + "|" + step.Item2 + "|" + step.Item3 + "|" + Entities.AttachmentNotices.Count + "|" + told.Count);
        }
        Check(Entities.AttachmentNotices.All(n => n.Notice == "A part that came off your car could not be shared with the other players, so for them it is still on."), "part refusal keeps its exact plain words");
        EntityAttachments.ResetCarriedOff(); Check(told.Count == 3, "carried reset keeps refusal notice history");
        const string path = "tools/unit/EntityAttachmentsRows.txt";
#if ATTACHMENTS_CAPTURE
        File.WriteAllLines(path, rows);
#else
        Check(File.ReadAllLines(path).SequenceEqual(rows), "attachment rows match pre-split 4851eaf");
#endif
        AttachmentsBoundaryChecks();
        Entities.ByNet.Clear(); Entities.Packets.Clear(); cache.Clear(); told.Clear(); Entities.AttachmentNotices.Clear();
        EntityAttachments.AttachPosesSent = EntityAttachments.AttachPosesBad = EntityAttachments.AttachPosesApplied = 0;
        EntityAttachments.AttachPoseOffLast = EntityAttachments.AttachPoseOffMax = EntityAttachments.AttachPoseAngLast = 0;
        EntityAttachments.CarriedOffSent = EntityAttachments.CarriedOffApplied = 0; Mp.Server = previousServer;
    }

    static void AttachmentsBoundaryChecks()
    {
        CoreMembers.Register(); var owner = typeof(EntityAttachments);
        Check(owner.IsAbstract && owner.IsSealed && !owner.IsPublic, "attachments have an internal static owner");
        foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly))
        {
            if (!(member is FieldInfo || member is PropertyInfo || member is MethodInfo) || typeof(Entities).GetMember(member.Name, Static).Length != 0) continue;
            foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                Check(Reflect.MemberPath(prefix + member.Name) == owner.FullName + "." + member.Name, "attachment alias " + prefix + member.Name);
        }
        Check(BridgeReflect.Set("Entities.AttachRequests", "False") == "false" && !EntityAttachments.AttachRequests, "attach request switch alias writes feature storage");
        EntityAttachments.AttachRequests = true;
        Check(!ReferenceEquals(Field(owner, "W"), Field(typeof(Entities), "W")), "carried messages own their writer");
        string all = "";
        foreach (string part in new[] { "Attach", "CarriedOff", "PartOff" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs");
            all += source.Substring(source.IndexOf("    internal static partial class EntityAttachments"));
        }
        string code = Regex.Replace(all, @"//[^\r\n]*", "");
        Check(!Regex.IsMatch(code, @"\b(ByNet|WS|SEnt|Server)\b") && !code.Contains("using static"), "attachments cannot read kernel tables server records or writers");
        string[] allowed = { "Ent", "InSession", "MyId", "PartOff", "AttachReq", "IsFinite", "AttachmentCarriesParts", "AttachmentApplying", "AttachmentApplyingDetach", "AttachmentPlayerActing", "AttachmentRoot", "AttachmentGroup", "TryAttachmentCarrier", "AttachmentUsablesChanged", "RefreshAttachmentBodies", "SendAttachment", "SendAttachmentMessage", "TellAttachmentRefused" };
        foreach (Match call in Regex.Matches(code, @"Entities\.(\w+)")) Check(allowed.Contains(call.Groups[1].Value), "narrow attachment dependency " + call.Value);
        foreach (string phase in new[] { "Prefix", "Postfix" })
            Check(Regex.IsMatch(all, @"\[HarmonyLib\.Harmony" + phase + @", HarmonyLib\.HarmonyPriority\(HarmonyLib\.Priority\.Normal \+ 1\)\]"), "attach " + phase + " precedes telemetry as before the move");
        Check(File.ReadAllText("src/TLDRevamp/Net/Entities.Hitch.cs").Contains("[HarmonyPriority(Priority.First)]"), "hitch joint still precedes attach prefix");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.Contains("_held.Clear(); _after.Clear(); DevHooks.CrashReset?.Invoke(); EntityAttachments.ResetCarriedOff();"), "carried reset retains its exact neighbors");
        AttachmentsSourceChecks();
    }

    static string AttachmentCanonical(string body)
    {
        body = body.Replace(", HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Normal + 1)", "")
            .Replace("Entities.AttachmentApplyingDetach", "_applyingDetach").Replace("Entities.AttachmentApplying", "_applyingAttach || _applyingState")
            .Replace("Entities.AttachmentRoot(", "OwnEntity(").Replace("Entities.AttachmentGroup(", "EditEntityOf(")
            .Replace("Entities.RefreshAttachmentBodies(", "ApplyProxyBodies(").Replace("Entities.SendAttachment(", "SendAttach(")
            .Replace("Entities.SendAttachmentMessage(", "ToServer(").Replace("Entities.IsFinite(", "Finite(")
            .Replace("Entities.AttachmentUsablesChanged(", "LiveUsablesChanged(").Replace("Entities.AttachmentPlayerActing", "PlayerActing")
            .Replace("Entities.AttachmentCarriesParts", "CarriedPartsShared")
            .Replace("!Entities.TryAttachmentCarrier(carrierNet, out var c)", "!ByNet.TryGetValue(carrierNet, out var c) || !Resolve(c)")
            .Replace("EntityCodec.", "").Replace("EntityAttachments.", "").Replace("Entities.", "");
        return Regex.Replace(body, @"\s+", "");
    }
    static void AttachmentsSourceChecks()
    {
        foreach (string row in File.ReadAllLines("tools/unit/EntityAttachmentsSourceRows.txt"))
        {
            string[] c = row.Split(' '); string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + c[0] + ".cs").Replace("\r\n", "\n");
            Match m = c[1] == "AttachHook" ? Regex.Match(source, @"        private static class AttachHook") : Regex.Match(source, @"^        (?:public|private|internal) static [^\n]*?\b" + c[1] + @"\(", RegexOptions.Multiline);
            Check(m.Success, c[1] + " is still present");
            int start = source.IndexOf('{', m.Index), arrow = source.IndexOf("=>", m.Index, StringComparison.Ordinal);
            string body;
            if (arrow >= 0 && (start < 0 || arrow < start)) body = source.Substring(arrow, source.IndexOf(';', arrow) + 1 - arrow);
            else
            {
                int depth = 1, end = start + 1; while (depth > 0) { depth += source[end] == '{' ? 1 : source[end] == '}' ? -1 : 0; end++; }
                body = source.Substring(c[1] == "AttachHook" ? m.Index : start, end - (c[1] == "AttachHook" ? m.Index : start));
            }
            if (c[1] == "RefusePartOff")
            {
                int notice = source.IndexOf("internal static void TellPartOffRefused", StringComparison.Ordinal);
                int a = source.IndexOf('{', notice), end = a + 1, depth = 1;
                while (depth > 0) { depth += source[end] == '{' ? 1 : source[end] == '}' ? -1 : 0; end++; }
                body = body.Replace("EntityAttachments.TellPartOffRefused(from, carNet);", source.Substring(a + 1, end - a - 2).Replace("Entities.TellAttachmentRefused(", "Mp.Server?.Tell("));
            }
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(AttachmentCanonical(body))));
            Check(hash == c[2], c[1] + " implementation matches 4851eaf after narrow substitutions");
        }
    }
}
