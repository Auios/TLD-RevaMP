using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static void UsablesChecks()
    {
        UsablesSourceChecks();
        CoreMembers.Register();
        foreach (var owner in new[] { typeof(EntityUsables), typeof(EntityTeleportRefresh) })
        {
            Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed, owner.Name + " internal static owner");
            foreach (var member in owner.GetMembers(Static | BindingFlags.DeclaredOnly).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo))
            {
                bool retained = typeof(Entities).GetMember(member.Name, Static).Length > 0;
                foreach (string prefix in new[] { "Entities.", "TLDRevamp.Net.Entities." })
                    Check(Reflect.MemberPath(prefix + member.Name) == (retained ? prefix + member.Name : owner.FullName + "." + member.Name), "usable/refresh alias " + prefix + member.Name);
            }
        }
        foreach (string part in new[] { "Teleport", "LiveUsables", "PoiMotion", "PoiUsables" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Split("    public static partial class Entities")[0];
            Check(!Regex.IsMatch(source, @"using\s+static\s+.*Entities|Entities\.(?:W|WS|ByNet|Server)\b"), part + " moved section uses narrow qualified kernel calls");
            Check(!Regex.IsMatch(source, @"Entities\.(?:ObservedEntities|AllEnts|TryGetEntity)\b"), part + " has no entity table view");
        }
        Check(typeof(Entities).GetField("TpaInbox", Static) == null && typeof(Entities).GetField("TpaRefetched", Static) == null, "refresh queue and dedup have one owner");
        Check(typeof(Entities).GetField("_usableHierarchy", Static) == null && typeof(Entities).GetField("_applyingPoi", Static) == null, "usable hierarchy and POI guard have one owner");
        BridgeReflect.Set("TLDRevamp.Net.Entities.PoiUsablesOnSpawn", "0");
        Check(EntityUsables.PoiUsablesOnSpawn == 0 && BridgeReflect.Get("Entities.PoiUsablesOnSpawn") == "0", "POI counter forwarding and aliases share storage");

        var item = new tosaveitemscript(); var a = new usablescript(); var b = new usablescript();
        item.usables.Add(a); var first = EntityUsables.ItemUsables(item);
        Check(ReferenceEquals(first, EntityUsables.ItemUsables(item)), "unchanged registered order reuses array");
        item.usables[0] = b;
        Check(EntityUsables.ItemUsables(item).Single() == b && !ReferenceEquals(first, EntityUsables.ItemUsables(item)), "same length replacement invalidates cache");
        item.usables.Add(a); Check(EntityUsables.ItemUsables(item).SequenceEqual(new[] { b, a }), "linked controls retain registered order");
        EntityUsables.InvalidateHierarchy(item); var replaced = EntityUsables.ItemUsables(item);
        Check(!ReferenceEquals(replaced, first), "topology invalidation rebuilds hierarchy");
        EntityUsables.ResetHierarchy(); Check(!ReferenceEquals(replaced, EntityUsables.ItemUsables(item)), "session/reload replaces hierarchy");

        var rows = File.ReadAllLines("tools/unit/EntityUsablesRows.txt"); var w = new NetWriter();
        var k = new Entities.UsableKey { Net = 401, Epoch = 3, Item = 2, Usable = 7 };
        var v = new Entities.UsableValue { X = 1, Y = 65535, Slide = 32768, Turn = -17, Tuned = 513 };
        EntityUsables.WriteLiveUsable(w, k, 65535, 123.5, v);
        Check(Convert.ToHexString(w.Buf.AsSpan(0, w.Len)) == rows[0], "item bytes match pre-move HEAD capture");
        EntityUsables.ReadLiveUsable(Reader(Convert.FromHexString(rows[0])), out var read, out var seq, out var stamp, out var value);
        Check(read.Equals(k) && seq == 65535 && stamp == 123.5 && value.Equals(v), "item codec retains sequence, address and value");
        k = new Entities.UsableKey { Building = true, Poi = new Entities.PoiKey { Gen = 2, X = -8000000.125, Y = 50.5, Z = -6000000.25, U = 130 } };
        var state = new Entities.PoiState { XRot = -45.25f, YRot = 17.5f, Slide = .375f, Turn = -3, Tuned = .625f };
        EntityUsables.WriteLiveUsable(w, k, 0, 124.5, v, state);
        Check(Convert.ToHexString(w.Buf.AsSpan(0, w.Len)) == rows[1], "POI motion bytes match pre-move HEAD capture");
        EntityUsables.ReadLiveUsable(Reader(Convert.FromHexString(rows[1])), out read, out seq, out stamp, out value, out var poi);
        Check(read.Equals(k) && seq == 0 && stamp == 124.5 && EntityUsables.SameUsable(poi, state), "POI codec preserves far double address and raw values");
        for (int n = 1; n < Convert.FromHexString(rows[1]).Length; n++)
        {
            var r = Reader(Convert.FromHexString(rows[1]).Take(n).ToArray());
            EntityUsables.ReadLiveUsable(r, out _, out _, out _, out _, out _);
            Check(r.Bad, "POI codec detects truncated byte " + n);
        }
        w.Reset(); EntityUsables.WritePoi(w, k.Poi, new Entities.PoiState { XRot = float.NaN });
        Check(!EntityUsables.ReadPoi(new NetReader { Buf = w.Buf, End = w.Len }, out _, out _), "nonfinite POI values rejected");

        Entities.KnownPoiUsables.Clear(); Entities.KnownPoiUsables[k.Poi] = state; Entities.InSession = true;
        var p = new poiScript { poigenid = k.Poi.Gen, posID = new Vector3d(k.Poi.X, k.Poi.Y, k.Poi.Z) };
        for (int i = 0; i <= k.Poi.U; i++) p.usables.Add(null);
        p.usables[k.Poi.U] = a; a.setidPoi = true; bool guarded = false;
        a.DuringRefresh = () => { guarded = EntityUsables.ApplyingPoi && !Entities.ReproUsableAllowed; };
        var apply = typeof(EntityUsables).GetMethod("ApplyKnown", Static);
        apply.Invoke(null, new object[] { p });
        Check(guarded && !EntityUsables.ApplyingPoi && a.xRot == state.XRot && a.tunedFloat == state.Tuned && EntityUsables.PoiUsablesOnSpawn == 1, "spawn replay applies under guard, then releases it");
        a.DuringRefresh = () => throw new Exception("refresh");
        try { apply.Invoke(null, new object[] { p }); } catch (TargetInvocationException) { }
        Check(!EntityUsables.ApplyingPoi && EntityUsables.PoiUsablesOnSpawn == 1, "failed spawn replay unwinds guard without counting apply"); a.DuringRefresh = null;
        a.xRot = 0;
        typeof(EntityUsables).GetNestedType("PoiSnapshotLoaded", BindingFlags.NonPublic).GetMethod("Postfix", Static).Invoke(null, new object[] { p });
        Check(a.xRot == state.XRot && EntityUsables.PoiUsablesOnSpawn == 2, "session snapshot wins after the game's saved POI load");
        var map = new menuhandler.Map(); var gen = new menuhandler.Gen(); var chunk = new menuhandler.Chunk();
        menuhandler.s = new menuhandler { currentMainMap = map };
        map.poiGens.Add(null); map.poiGens.Add(null); map.poiGens.Add(gen);
        gen.chunks[default] = chunk; chunk.pois[p.posID] = new menuhandler.Poi { pobj = p };
        Check(EntityUsables.PoiUsableOf(k.Poi) == a, "game lookup uses generator, chunk, POI and registered usable index");
        var badKey = k.Poi; badKey.Gen = -1; Check(EntityUsables.PoiUsableOf(badKey) == null, "negative generator rejected");
        badKey = k.Poi; badKey.U = -1; Check(EntityUsables.PoiUsableOf(badKey) == null, "negative usable rejected");
        gen.chunks.Clear(); Check(EntityUsables.PoiUsableOf(k.Poi) == null, "streamed-out building lookup returns null"); menuhandler.s = null;
        var changed = typeof(EntityUsables).GetNestedType("PoiUsableChanged", BindingFlags.NonPublic).GetMethod("Postfix", Static);
        int marked = Entities.MarkedPoi;
        changed.Invoke(null, new object[] { a, false }); a.setidPoi = false; changed.Invoke(null, new object[] { a, true });
        a.setidPoi = true; Entities.InSession = false; changed.Invoke(null, new object[] { a, true }); Entities.InSession = true;
        Set(typeof(EntityUsables), "_applyingPoi", true); changed.Invoke(null, new object[] { a, true }); Set(typeof(EntityUsables), "_applyingPoi", false);
        Check(Entities.MarkedPoi == marked, "POI hook sync/session/address/apply guards");
        changed.Invoke(null, new object[] { a, true }); Check(Entities.MarkedPoi == marked + 1, "POI hook reaches kernel through narrow command");

        var server = Mp.Server; Mp.Server = null; EntityTeleportRefresh.ResetTpaClient(); Entities.TpaRows.Clear();
        Tpa.WaitingWorld = true;
        for (int i = 0; i < 201; i++) EntityTeleportRefresh.DeferTpa(22, new NetReader { Buf = BitConverter.GetBytes(i), End = 4 });
        EntityTeleportRefresh.DrainTpaInbox(); Check(Entities.TpaRows.Count == 0, "world wait preserves queued messages");
        Tpa.WaitingWorld = false; EntityTeleportRefresh.DrainTpaInbox();
        Check(Entities.TpaRows.Count == 200 && !EntityTeleportRefresh.TpaInboxEmpty, "replay bounds work at 200");
        EntityTeleportRefresh.DeferTpa(23, new NetReader { Buf = new byte[] { 99 }, End = 1 });
        EntityTeleportRefresh.DrainTpaInbox();
        Check(Entities.TpaRows[^2] == "22:C8000000" && Entities.TpaRows[^1] == "23:63" && EntityTeleportRefresh.TpaInboxEmpty, "new messages join replay tail between frames");
        Set(typeof(EntityTeleportRefresh), "_tpaReplaying", true);
        Check(!EntityTeleportRefresh.DeferTpa(22, new NetReader()), "replay cannot defer itself"); Set(typeof(EntityTeleportRefresh), "_tpaReplaying", false);
        Tpa.WaitingWorld = true; EntityTeleportRefresh.DeferTpa(22, new NetReader { Buf = new byte[] { 1 }, End = 1 });
        BridgeReflect.Call("Entities.ResetTpaClient", Array.Empty<string>());
        Check(EntityTeleportRefresh.TpaInboxEmpty && BridgeReflect.Get("TLDRevamp.Net.Entities.TpaInbox.Count") == "0", "reset alias clears real queue");
        EntityTeleportRefresh.DeferTpa(22, new NetReader { Buf = new byte[] { 1 }, End = 1 });
        Entities.TpaReplayThrows = true; Tpa.WaitingWorld = false;
        try { EntityTeleportRefresh.DrainTpaInbox(); } catch (Exception) { }
        Check(!(bool)Field(typeof(EntityTeleportRefresh), "_tpaReplaying"), "failed replay unwinds guard"); Entities.TpaReplayThrows = false;
        EntityTeleportRefresh.ResetTpaClient(); Tpa.RefreshingObjects = true; Entities.IsHost = false;
        var request = new NetWriter(); request.VarU32(123456); int sent = Entities.Packets.Count;
        for (int i = 0; i < 2; i++) EntityTeleportRefresh.TpaCopyMessage(Entities.State, new NetReader { Buf = request.Buf, End = request.Len });
        Check(Entities.Packets.Count == sent + 1 && Entities.Packets[^1].Reliable && Entities.Packets[^1].Bytes.SequenceEqual(new byte[] { 58, 64, 226, 1, 0 }), "missing copy requests one reliable refetch per refresh");
        BridgeReflect.Call("TLDRevamp.Net.Entities.BeginTpaRefresh", Array.Empty<string>());
        EntityTeleportRefresh.TpaCopyMessage(Entities.CompactState, new NetReader { Buf = request.Buf, End = request.Len });
        Check(Entities.Packets.Count == sent + 2, "begin refresh alias clears dedup for a repeat loss"); Tpa.RefreshingObjects = false;
        Tpa.WaitingWorld = false; Mp.Server = server; Entities.KnownPoiUsables.Clear();
    }

    static void UsablesSourceChecks()
    {
        foreach (string row in File.ReadAllLines("tools/unit/EntityBatch12SourceRows.txt"))
        {
            var c = row.Split(' ');
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + c[0] + ".cs").Replace("\r\n", "\n");
            var match = Regex.Match(source, @"^        (?:private|internal|public) static [^\n]*\b" + c[1] + @"\(", RegexOptions.Multiline);
            Check(match.Success, c[0] + "." + c[1] + " implementation exists");
            int brace = source.IndexOf('{', match.Index), arrow = source.IndexOf("=>", match.Index, StringComparison.Ordinal), end;
            string body;
            if (arrow >= 0 && (brace < 0 || arrow < brace)) body = source.Substring(arrow, source.IndexOf(';', arrow) + 1 - arrow);
            else
            {
                int depth = 1; end = brace + 1;
                while (depth > 0) { depth += source[end] == '{' ? 1 : source[end] == '}' ? -1 : 0; end++; }
                body = source.Substring(brace, end - brace);
            }
            body = body.Replace("Entities.RequestTpaCopy(net);", "W.Reset(); W.U8(Refetch); W.U32(net); WriteLostPart(W, net); ToServer(W, true);")
                .Replace("Entities.HasTpaCopy(net)", "ByNet.ContainsKey(net)").Replace("Entities.ReplayTpaMessage(", "ClientReceive(")
                .Replace("!Entities.HasKnownPoiUsables", "KnownPoiUsables.Count == 0").Replace("Entities.TryKnownPoiUsable(", "KnownPoiUsables.TryGetValue(")
                .Replace("EntityUsables.InvalidateHierarchy(it);", "_usableHierarchy.Remove(it);")
                .Replace("EntityUsables.ResetHierarchy();", "_usableHierarchy = new ConditionalWeakTable<tosaveitemscript, UsableHierarchy>();")
                .Replace("EntityUsables.ApplyingPoi", "_applyingPoi").Replace("Entities.MarkPoiUsable(", "MarkLiveUsable(")
                .Replace("Entities.IsFinite(", "Finite(").Replace("EntityUsables.", "").Replace("EntityEditCheck.", "").Replace("Entities.", "");
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Regex.Replace(body, @"\s+", ""))));
            Check(hash == c[2], c[0] + "." + c[1] + " matches eaa5f6c after boundary substitutions");
        }
        string poi = File.ReadAllText("src/TLDRevamp/Net/Entities.PoiUsables.cs").Split("    public static partial class Entities")[0];
        foreach (string patch in new[] { "PoiUsableChanged", "PoiSpawned", "PoiSnapshotLoaded" })
            Check(Regex.Matches(poi, "class " + patch + @"\b").Count == 1, patch + " has one attributed owner");
        Check(poi.Contains("[HarmonyPatch(typeof(usablescript), nameof(usablescript.SyncMulti), new[] { typeof(bool) })]") &&
            poi.Contains("[HarmonyPatch(typeof(poiScript), nameof(poiScript.FStart))]") &&
            poi.Contains("[HarmonyPatch(typeof(save_poi), nameof(save_poi.Load), new[] { typeof(poiScript) })]"), "moved POI hooks retain exact overload targets and default priorities");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.IndexOf("if (EntityTeleportRefresh.DeferTpa(type, r)) return;") < kernel.IndexOf("EntityTeleportRefresh.TpaCopyMessage(type, r);"), "defer still precedes refetch/dispatch");
        string features = File.ReadAllText("src/TLDRevamp/Net/Entities.Features.cs");
        Check(features.Contains("ReproUsableAllowed => PlayerActing && !_applyingLive && !_applyingState && !UsablePoiApplying;") && features.Contains("UsablePoiApplying => EntityUsables.ApplyingPoi;"), "repro reads both apply guards through narrow facade");
    }
}
