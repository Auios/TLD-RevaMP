using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static Type AIOwner => typeof(EntityAI);
    static object AICall(string name, params object[] args) => AIOwner.GetMethod(name, Static).Invoke(null, args);
    static object AIPatch(string name, string method, params object[] args) => AIOwner.GetNestedType(name, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);
    static void AIVector(string name)
    {
        // Captured from the unchanged production AI files at HEAD 5fa082f, before moving them.
        string expected = name switch {
            "state" => "322D010000001640E00000803F0000004000004040",
            "sound" => "332D010000000282010000A03F",
            "hit" => "302D0100000001000048410000803F0000004000004040",
            "fx" => "312D0100000001",
            "world" or "world replay" => "70000000000000F03F0000000000000040000000000000084007636163747573",
            "fx replay" => "312D010000820103",
            "damage" => "2802CCCC4C3E0000803F0000000000000000010000000000000000000000002D01000000",
            _ => throw new Exception(name)
        };
        Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == expected, name + " matches expected bytes");
        Check(Entities.Packets.Last().Reliable == (name != "state"), name + " reliability unchanged");
    }
    static IDictionary AITable(string name) => (IDictionary)Field(AIOwner, name);
    static IList AIList(string name) => (IList)Field(AIOwner, name);
    static byte[] AIHit(byte kind, float force, Vector3 dir, uint reps = 1)
    {
        var w = new NetWriter(); w.U8(Entities.BreakHit); w.U32(301); w.VarU32(0); w.U8(kind); w.F32(force); w.F32(dir.x); w.F32(dir.y); w.F32(dir.z); if (kind == 3) w.VarU32(reps); return w.Buf.Take(w.Len).ToArray();
    }
    static void AIChecks()
    {
        Entities.ByNet.Clear(); Entities.Packets.Clear(); Entities.IsHost = false;
        mainscript.s = new mainscript { player = new fpscontroller() };
        var it = new tosaveitemscript { name = "worker", idInSave = 902 };
        it.Go = new GameObject { Part = it, transform = it.transform };
        var a = new newAiScript { tosave = it, ParentComponent = it, rb = new Rigidbody(), head = new Transform(), anim = new TestAIAnim { targetForward = .5f, targetTurn = -.25f, onGround = true }, isChasing = true, isSeeing = true };
        a.TargetNull(); a.target.position = new Vector3(1, 2, 3); it.npc = a; it.ChildComponent = a;
        var br = new breakablescript { ParentComponent = it, name = "worker" };
        it.transform.Children.Add(br);
        var e = new Entities.Ent { NetId = 301, Epoch = 7, Root = it, RootId = 902, Items = { it } };
        Entities.ByNet[301] = e;
        RemotePlayers.Targets.Add((2, new Transform { position = new Vector3(1, 0, 0) }, new Transform { position = new Vector3(1, 2, 0) }, Vector3.zero, Vector3.up, 123));
        Time.unscaledTime = Time.realtimeSinceStartup = 10; AICall("AiTick"); AIVector("state"); var state = Entities.Packets.Last().Bytes;
        AIPatch("AiVoice", "Postfix", a, newAiScript.aiSoundType.attack, 130, 1.25f); AIVector("sound"); var sound = Entities.Packets.Last().Bytes;
        AICall("ForwardHit", br, (byte)1, 12.5f, new Vector3(1, 2, 3)); AIVector("hit"); var hit = Entities.Packets.Last().Bytes;
        AIPatch("BreakOut", "Postfix", br, false); AIVector("fx"); var fx = Entities.Packets.Last().Bytes;
        var world = new breakablescript { name = "cactus(Clone)" }; world.transform.position = new Vector3(1, 2, 3);
        AICall("WorldBreakOut", world); AIVector("world"); var worldBytes = Entities.Packets.Last().Bytes;
        AICall("ServerKeepWorldBreak", Reader(Entities.Packets.Last().Bytes)); AICall("ServerWorldBreaksTo", 4); AIVector("world replay");
        Entities.TestServer(301, 0);
        var server = (IDictionary)Field(typeof(Entities), "Server");
        var se = server[301u];
        se.GetType().GetField("Broken", Static | BindingFlags.Instance).SetValue(se, new System.Collections.Generic.List<int> { 0, 130 });
        AICall("SendBreaksTo", 4, 301u, new System.Collections.Generic.List<int> { 0, 130 }); AIVector("fx replay");
        a.sensU = new sens { selfCol = new Collider() }; Time.deltaTime = .02f;
        AICall("RemoteAttack", a); AIVector("damage");
        Entities.IsHost = true;
        Entities.HostReceive = (kind, reader) => { if (kind == Entities.AiSound) EntityAI.ServerWorldBreaksTo(7); };
        int nested = Entities.Packets.Count; AIPatch("AiVoice", "Postfix", a, newAiScript.aiSoundType.attack, 130, 1.25f);
        Check(Entities.Packets.Count == nested + 2 && Entities.Packets[nested].Bytes.SequenceEqual(sound) && Entities.Packets[nested + 1].Bytes.SequenceEqual(worldBytes), "host captures AI request before nested replay reuses feature writer");
        Entities.HostReceive = null; Entities.IsHost = false;
        a.target.position = new Vector3(1, 2, 3); a.isAttacking = false;
        AIStateChecks(e, a, state, sound);
        AIBreakChecks(e, a, br, hit, fx);
        AIWorldChecks(world, worldBytes);
        AITumbleChecks();
        AIBoundaryChecks();
        AICall("AiReset"); AICall("BreaksReset"); RemotePlayers.Targets.Clear(); Entities.ByNet.Clear(); Entities.Packets.Clear();
        UnityEngine.Object.Found = Array.Empty<object>(); Entities.ClearServer(); Entities.Reshares.Clear();
    }

    static void AIStateChecks(Entities.Ent e, newAiScript a, byte[] state, byte[] sound)
    {
        int sends = Entities.Packets.Count; AICall("AiTick"); Check(Entities.Packets.Count == sends, "AI cadence blocks same frame");
        Time.unscaledTime = 10.2f; AICall("AiTick"); Check(Entities.Packets.Count == sends, "unchanged AI waits for heartbeat");
        Time.unscaledTime = 11.1f; AICall("AiTick"); Check(Entities.Packets.Count == sends + 1 && Entities.Packets.Last().Bytes.SequenceEqual(state), "AI heartbeat repeats exact state");
        Entities.TestAudience = false; Time.unscaledTime = 11.3f; AICall("AiTick");
        Entities.TestAudience = true; Time.unscaledTime = 11.5f; AICall("AiTick"); Check(Entities.Packets.Count == sends + 2, "audience entry sends unchanged AI immediately");
        e.Epoch++; Time.unscaledTime = 11.7f; AICall("AiTick"); Check(Entities.Packets.Count == sends + 3, "AI epoch change sends immediately");
        a.died = true; Time.unscaledTime = 11.9f; AICall("AiTick");
        Check(Entities.Packets.Last().Bytes.Skip(6).SequenceEqual(new byte[] { 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }), "dead AI normalizes animation and look");
        sends = Entities.Packets.Count; Time.unscaledTime = 13; a.anim.targetForward = 1; long skipped = Entities.StatesSkippedDeadAnimation; AICall("AiTick");
        Check(Entities.Packets.Count == sends && Entities.StatesSkippedDeadAnimation == skipped + 1, "dead AI suppresses raw animation with accounting");
        a.died = false; a.anim.targetForward = .5f; e.Epoch = 7; e.Proxy = true;
        long applied = EntityAI.AiStatesApplied; AICall("ApplyAiState", Reader(state));
        Check(EntityAI.AiStatesApplied == applied + 1, "copy accepts owner's AI state");
        AICall("ShowCopy", a); Check(a.isChasing && a.isSeeing && a.anim.onGround && a.anim.targetForward == 64 / 127f && a.anim.targetTurn == -32 / 127f, "copy shows flags and signed animation inputs");
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, 1001f })
        {
            byte[] invalid = state.ToArray(); BitConverter.GetBytes(value).CopyTo(invalid, 9); AICall("ApplyAiState", Reader(invalid));
        }
        AICall("ApplyAiState", Reader(state.Take(9).ToArray())); Check(EntityAI.AiStatesApplied == applied + 1, "invalid or oversized look and truncated AI state rejected");
        e.Proxy = false; AICall("ApplyAiState", Reader(state)); Check(EntityAI.AiStatesApplied == applied + 1, "owner refuses copy animation"); e.Proxy = true;
        long sounds = EntityAI.AiSoundsApplied; sends = Entities.Packets.Count;
        newAiScript.DuringSound = () => AIPatch("AiVoice", "Postfix", a, newAiScript.aiSoundType.attack, 130, 1.25f);
        AICall("ApplyAiSound", Reader(sound)); Check(EntityAI.AiSoundsApplied == sounds + 1 && Entities.Packets.Count == sends && !(bool)Field(AIOwner, "_aiSoundReplay"), "sound replay cannot echo and clears its guard");
        newAiScript.DuringSound = () => throw new Exception("sound fixture");
        try { AICall("ApplyAiSound", Reader(sound)); Check(false, "sound throw propagates"); } catch (TargetInvocationException) { }
        Check(!(bool)Field(AIOwner, "_aiSoundReplay") && EntityAI.AiSoundsApplied == sounds + 1, "sound exception clears guard without counting applied"); newAiScript.DuringSound = null;
        AICall("ApplyAiSound", Reader(sound.Take(8).ToArray())); Check(EntityAI.AiSoundsApplied == sounds + 1, "truncated sound rejected");
        e.Proxy = false; AICall("ApplyAiSound", Reader(sound)); Check(EntityAI.AiSoundsApplied == sounds + 1, "owner does not replay own sound");

        Entities.TestServer(301, 5); Entities.TestAIServer(7, 6); int relays = Entities.AIRelays; long stale = EntityAI.AiStatesStale;
        Entities.TestServerAIState(4, Reader(state)); Check(EntityAI.AiStatesStale == stale + 1 && Entities.AIRelays == relays, "stale owner's AI cannot steer copies");
        Entities.TestServerAIState(5, Reader(state.Take(state.Length - 1).ToArray()));
        Entities.TestServerAIState(5, Reader(state.Concat(new byte[] { 0 }).ToArray())); Check(EntityAI.AiStatesStale == stale + 3 && Entities.TestAISnapshotCount == 0, "AI authority guard demands exact tail length");
        Entities.TestServerAIState(5, Reader(state)); Check(Entities.AIRelays == relays + 1 && Entities.TestAISnapshot(0).SequenceEqual(state), "valid authority caches snapshot before relay");
        var other = state.ToArray(); other[5] = 1; Entities.TestServerAIState(5, Reader(other));
        Check(Entities.TestAISnapshotCount == 2, "AI snapshots keep each item"); Entities.TestAIServer(8, 7); Entities.TestServerAIState(5, Reader(state));
        Check(Entities.TestAISnapshotCount == 1, "new authority epoch clears old AI snapshots");

        // Damage is accumulated per victim, flushed every tenth second, and carries the creature's identity.
        a.sensU = new sens { selfCol = new Collider() }; Time.unscaledTime = 20; Time.deltaTime = .02f; AICall("RemoteAttack", a); sends = Entities.Packets.Count;
        Time.unscaledTime = 20.02f; AICall("RemoteAttack", a); Time.unscaledTime = 20.04f; AICall("RemoteAttack", a);
        Check(Entities.Packets.Count == sends && AITable("_aiDmgAcc").Count == 1, "attack frames accumulate until flush");
        Time.unscaledTime = 20.11f; AICall("RemoteAttack", a);
        Check(Entities.Packets.Count == sends + 1 && Math.Abs(BitConverter.ToSingle(Entities.Packets.Last().Bytes, 2) - .6f) < 1e-6f && AITable("_aiDmgAcc").Count == 0, "AI flush sums damage and clears all victim buffers");
        a.sensU = null; AICall("RemoteAttack", a); Check(Entities.Packets.Count == sends + 1, "no sensor touch means no damage");
        PlayerCombat.RayDistance = -1; a.sensU = new sens { selfCol = new Collider() }; Time.unscaledTime = 21; AICall("RemoteAttack", a); Check(Entities.Packets.Count == sends + 1, "occluded remote player takes no damage"); PlayerCombat.RayDistance = 1;
        AIChaseChecks(a);
    }

    static void AIChaseChecks(newAiScript a)
    {
        a.isChasing = false; a.isSeeing = false; a.isAttacking = false; a.rb.isKinematic = false; a.head.position = Vector3.zero;
        Time.time = 10; float fov = a.fov; long seen = EntityAI.AiRemoteSeen;
        object[] prefix = { a, 0f }; AIPatch("AiUpdate", "Prefix", prefix);
        Check(EntityAI.AiRemoteSeen == seen + 1 && a.target.parent == RemotePlayers.Targets[0].head && a.targetIsSteamID && a.targetSteamID == 123 && a.fov == -1 && a.isChasing && a.chaseStart == 10, "AI chooses remote target with original chase fields");
        AIPatch("AiUpdate", "Finalizer", a, prefix[1], null); Check(a.fov == fov, "AI finalizer restores FOV");
        // Losing sight at arm's length keeps a nearer remote target while the local player is visible farther away.
        var local = mainscript.s.player; local.transform.position = new Vector3(20, 0, 0);
        Physics.Hits = new[] { new RaycastHit { distance = .5f, collider = new Collider { ParentComponent = local } } };
        PlayerCombat.RayDistance = -1; long kept = EntityAI.AiKeptOnRemote; prefix = new object[] { a, 0f };
        AIPatch("AiUpdate", "Prefix", prefix); Check(EntityAI.AiKeptOnRemote == kept + 1 && a.fov == -1 && AITable("AiChasing").Count == 1, "sticky remote target survives missed sight frame"); AIPatch("AiUpdate", "Finalizer", a, prefix[1], null);
        EntityAI.AiStickyRemote = false; prefix = new object[] { a, 0f }; AIPatch("AiUpdate", "Prefix", prefix); Check(AITable("AiChasing").Count == 0 && float.IsNaN((float)prefix[1]), "sticky switch removes remote target without overriding local sight"); EntityAI.AiStickyRemote = true;
        PlayerCombat.RayDistance = 1; Physics.Hits = Array.Empty<RaycastHit>(); local.transform.position = Vector3.zero;
        gamemodesettingsscript.s.S.peacefulEnemies = 2; seen = EntityAI.AiRemoteSeen; prefix = new object[] { a, 0f }; AIPatch("AiUpdate", "Prefix", prefix); Check(EntityAI.AiRemoteSeen == seen && float.IsNaN((float)prefix[1]), "peaceful mode skips remote AI hunting"); gamemodesettingsscript.s.S.peacefulEnemies = 0;
    }

    static void AIBreakChecks(Entities.Ent e, newAiScript a, breakablescript br, byte[] hit, byte[] fx)
    {
        Entities.ByNet[301] = e; e.Proxy = false; br.destroyed = false;
        long run = EntityAI.BreakHitsRun; AICall("ApplyBreakHit", Reader(hit)); Check(EntityAI.BreakHitsRun == run + 1 && br.ShootCalls == 1 && br.LastForce == 12.5f, "owner runs forwarded shot");
        foreach (float force in new[] { -1f, float.NaN, float.PositiveInfinity }) AICall("ApplyBreakHit", Reader(AIHit(2, force, Vector3.zero)));
        AICall("ApplyBreakHit", Reader(hit.Take(10).ToArray())); Check(EntityAI.BreakHitsRun == run + 1, "invalid forwarded force and truncated hit rejected");
        AICall("ApplyBreakHit", Reader(AIHit(3, 1, new Vector3(float.NaN, 0, 0), 900))); Check(br.ForceCalls == 600 && br.LastDir == new Vector3(1, 2, 3), "bite replay caps at 600 and invalid direction is ignored");
        e.Proxy = true; AICall("ApplyBreakHit", Reader(hit)); Check(EntityAI.BreakHitsRun == run + 2, "copy never runs forwarded hit");
        int sends = Entities.Packets.Count; long zero = EntityAI.ZeroHitsIgnored;
        Check(!(bool)AIPatch("CopyBreakForce", "Prefix", br, 0f) && EntityAI.ZeroHitsIgnored == zero + 1 && Entities.Packets.Count == sends, "zero force copy hit never wakes owner");
        long touch = EntityAI.CopyTouchesIgnored; Check(!(bool)AIPatch("CopyBreakBody", "Prefix", br, null) && EntityAI.CopyTouchesIgnored == touch + 1, "copy touch with no body rejected");
        var copyRb = new Rigidbody { ParentComponent = e.Root }; Check(!(bool)AIPatch("CopyBreakBody", "Prefix", br, copyRb) && EntityAI.CopyTouchesIgnored == touch + 2, "copy touching copy never forwards hit");
        var ownedRb = new Rigidbody { position = new Vector3(2, 0, 0) }; Check((bool)AIPatch("CopyBreakBody", "Prefix", br, ownedRb), "owned body touch still runs game's force calculation");
        AIPatch("CopyBreakBody", "Finalizer", null, br, ownedRb); Check((Vector3)Field(AIOwner, "_hitDir") == Vector3.zero, "body finalizer clears hit direction");
        long bump = EntityAI.BreakBumpsNoCollider; br.newAI = a;
        Check(!(bool)AIPatch("CopyBreakCollision", "Prefix", br, new Collision()) && EntityAI.BreakBumpsNoCollider == bump + 1, "missing creature collision collider guarded"); br.newAI = null;
        br.noShoot = true; Check(!(bool)AIPatch("CopyBreakShoot", "Prefix", br, 1f, Vector3.zero) && Entities.Packets.Count == sends, "noShoot copy blocks without request"); br.noShoot = false;
        Set(typeof(EntityExplosions), "_blastDepth", 1); AIPatch("CopyBreakForce", "Prefix", br, 1f); Check(Entities.Packets.Count == sends, "blast force replay doesn't duplicate forwarded hit"); Set(typeof(EntityExplosions), "_blastDepth", 0);
        var gib = new GameObject(); br.Gib = gib; long applied = EntityAI.BreakFxApplied;
        breakablescript.DuringBreak = b => { Check(b.Gib == null && (bool)Field(AIOwner, "_breakFxReplay"), "copy break suppresses gib under replay guard"); AIPatch("BreakOut", "Postfix", b, false); };
        AICall("ApplyBreakFx", Reader(fx)); Check(EntityAI.BreakFxApplied == applied + 1 && br.Gib == gib && Entities.Packets.Count == sends && !(bool)Field(AIOwner, "_breakFxReplay"), "copy FX applies once without echo and restores gib");
        AICall("ApplyBreakFx", Reader(fx)); Check(EntityAI.BreakFxApplied == applied + 1, "destroyed breakable doesn't replay twice");
        br.destroyed = false; breakablescript.DuringBreak = _ => throw new Exception("break fixture");
        try { AICall("BreakQuietly", br); Check(false, "break throw propagates"); } catch (TargetInvocationException) { }
        Check(br.Gib == gib && !(bool)Field(AIOwner, "_breakFxReplay"), "break exception restores replay guard and gib"); breakablescript.DuringBreak = null;

        AICall("BreaksReset"); var late = fx.ToArray(); late[^1] = 3; AICall("ApplyBreakFx", Reader(late));
        Check(!br.destroyed && AITable("_breaksHere").Count == 1, "late FX queues break until end of member removals");
        long lateApplied = EntityAI.BreaksAppliedLate; Time.realtimeSinceStartup = 30; AICall("BreakWaitTick");
        Check(br.destroyed && EntityAI.BreaksAppliedLate == lateApplied + 1, "late break retry applies retained index");
        br.destroyed = false; Time.realtimeSinceStartup = 32; AICall("BreakWaitTick"); Check(!br.destroyed, "same placement doesn't break again");
        // A new placing must receive the same break; one bad ragdoll can't end the whole session tick.
        AICall("KeepBreakHere", 301u, 0, null); long throws = EntityAI.BreaksThrewLate; breakablescript.DuringBreak = _ => throw new Exception("ragdoll fixture");
        for (int i = 0; i < EntityAI.BreakAgainTries + 1; i++) { Time.realtimeSinceStartup = 34 + i * 2; AICall("BreakWaitTick"); }
        Check(EntityAI.BreaksThrewLate == throws + EntityAI.BreakAgainTries, "late break exception retries exactly five times then gives up"); breakablescript.DuringBreak = null;
        Entities.ByNet.Remove(301); Time.realtimeSinceStartup = 50; AICall("BreakWaitTick"); Check(AITable("_breaksHere").Count == 0, "removed entity loses local break history"); Entities.ByNet[301] = e;
        Entities.TestServer(301, 0); for (int i = 0; i < EntityAI.MaxBreaksPerObject + 2; i++) AICall("ServerKeepBreak", Reader(fx));
        var server = (IDictionary)Field(typeof(Entities), "Server"); var se = server[301u]; var broken = (IList)se.GetType().GetField("Broken").GetValue(se);
        Check(broken.Count == EntityAI.MaxBreaksPerObject, "server break history capped per object");
        Entities.TestServer(301, 5); HitCheck.Allow = true; sends = Entities.Packets.Count;
        typeof(Entities).GetMethod("ServerBreakHit", Static).Invoke(null, new object[] { 5, Reader(hit) }); Check(Entities.Packets.Count == sends, "server refuses hit request from owner");
        HitCheck.Allow = false; typeof(Entities).GetMethod("ServerBreakHit", Static).Invoke(null, new object[] { 4, Reader(hit) }); Check(Entities.Packets.Count == sends, "server refuses out of reach break hit");
        HitCheck.Allow = true; typeof(Entities).GetMethod("ServerBreakHit", Static).Invoke(null, new object[] { 4, Reader(hit) }); Check(Entities.Packets.Count == sends + 1 && Entities.Packets.Last().Peer == 5 && Entities.Packets.Last().Bytes.SequenceEqual(hit), "kernel forwards accepted hit unchanged to authoritative owner");
        Check(!Entities.CreatureOf(4, 301, 0, out _, out _), "creature damage requires the server's current owner");
        Check(Entities.CreatureOf(5, 301, 0, out _, out bool known) && known, "kernel resolves accepted creature damage position");

        var body = new Rigidbody(); var returned = new Rigidbody(); var joint = new Component { ChildComponent = returned };
        var rag = new ragdollactivatescript { ParentComponent = e.Root, RB = body, RBs = new[] { body, null } }; e.Root.ragdollPos = new TestLimbSet { joints = new[] { joint, joint } };
        long healed = EntityAI.RagdollListsHealed; AIPatch("RagdollBodiesNow", "Prefix", rag);
        Check(EntityAI.RagdollListsHealed == healed + 1 && rag.RBs.SequenceEqual(new[] { body, returned }), "ragdoll healing replaces destroyed bodies and deduplicates returning limbs");
        AIPatch("RagdollBodiesNow", "Prefix", rag); Check(EntityAI.RagdollListsHealed == healed + 1, "healthy ragdoll list left alone");
        e.Proxy = false; br.destroyed = false;
    }

    static void AIWorldChecks(breakablescript world, byte[] bytes)
    {
        AICall("BreaksReset"); long applied = EntityAI.WorldBreaksApplied, late = EntityAI.WorldBreaksLate, scans = EntityAI.WorldBreakScans;
        EntityAI.WorldBreaksKept = true; world.destroyed = false; world.Gib = new GameObject(); world.reducedGib = new GameObject(); var gib = world.Gib;
        UnityEngine.Object.Found = new object[] { world }; AICall("ApplyWorldBreak", Reader(bytes));
        Check(!world.destroyed && AIList("_worldBreaksWaiting").Count == 1, "world break queues until grouped scan");
        Time.realtimeSinceStartup = 60; AICall("WorldBreakTick");
        Check(world.destroyed && world.Gib == gib && EntityAI.WorldBreaksApplied == applied + 1 && EntityAI.WorldBreaksLate == late + 1 && EntityAI.WorldBreakScans == scans + 1 && AIList("_worldBreaksWaiting").Count == 0, "near world break applies late in one scan with original gib restored");
        AICall("ApplyWorldBreak", Reader(bytes)); UnityEngine.Object.Found = Array.Empty<object>(); Time.realtimeSinceStartup = 62; AICall("WorldBreakTick");
        var waiting = AIList("_worldBreaksWaiting")[0]; var wait = waiting.GetType().GetField("Wait"); var next = waiting.GetType().GetField("NextTry");
        Check((float)wait.GetValue(waiting) == 4 && (float)next.GetValue(waiting) == 64, "missing world piece doubles retry delay");
        scans = EntityAI.WorldBreakScans; Time.realtimeSinceStartup = 63; AICall("WorldBreakTick"); Check(EntityAI.WorldBreakScans == scans, "world break backoff avoids scan before due");
        mainscript.s.player.transform.position = new Vector3(1000, 0, 0); Time.realtimeSinceStartup = 65; AICall("WorldBreakTick"); Check(EntityAI.WorldBreakScans == scans, "far world break waits for local streaming range"); mainscript.s.player.transform.position = Vector3.zero;
        world.destroyed = false; UnityEngine.Object.Found = new object[] { world }; AICall("WorldBreakTick"); Check(AIList("_worldBreaksWaiting").Count == 0 && world.destroyed, "world piece applies after returning to range");
        EntityAI.WorldBreaksKept = false; long missed = EntityAI.WorldBreaksMissed; AICall("ApplyWorldBreak", Reader(bytes)); Check(EntityAI.WorldBreaksMissed == missed + 1 && AIList("_worldBreaksWaiting").Count == 0, "old world mode counts immediate miss without retention"); EntityAI.WorldBreaksKept = true;
        AICall("ApplyWorldBreak", Reader(bytes.Take(10).ToArray())); Check(AIList("_worldBreaksWaiting").Count == 0, "truncated world break never queues");
        var wrong = bytes.ToArray(); BitConverter.GetBytes(double.NaN).CopyTo(wrong, 1); AICall("ApplyWorldBreak", Reader(wrong)); Check(AIList("_worldBreaksWaiting").Count == 0, "NaN world position rejected");
        AICall("ServerKeepWorldBreak", Reader(bytes)); int sends = Entities.Packets.Count;
        AICall("ServerWorldBreaksTo", 9); Check(Entities.Packets.Count == sends + 1 && Entities.Packets.Last().Peer == 9 && Entities.Packets.Last().Bytes.SequenceEqual(bytes), "late join receives original kept world bytes reliably");
        long shared = EntityAI.WorldGibsShared; Entities.Reshares.Clear(); var item = new tosaveitemscript(); var spawned = new GameObject { Part = item }; item.Children.Add(item); world.spawnedGib = spawned;
        AICall("WorldBreakOut", world); Check(EntityAI.WorldGibsShared == shared + 1 && Entities.Reshares.Contains(item), "world gib is queued for ordinary shared-object capture");
        long sent = EntityAI.WorldBreaksSent; world.ParentComponent = item; AICall("WorldBreakOut", world); Check(EntityAI.WorldBreaksSent == sent, "unshared item gib never names a generated world piece"); world.ParentComponent = null;
        AICall("BreaksReset"); for (int i = 0; i < EntityAI.MaxWorldBreaks + 2; i++) AICall("ServerKeepWorldBreak", Reader(bytes));
        Check(AIList("_worldBreaks").Count == EntityAI.MaxWorldBreaks, "server world break history retains original 4096 cap");
        EntityAI.WorldBreaksKept = false; AICall("BreaksReset"); AICall("ServerKeepWorldBreak", Reader(bytes)); Check(AIList("_worldBreaks").Count == 0, "world keep switch disables server history"); EntityAI.WorldBreaksKept = true;
    }

    static void AITumbleChecks()
    {
        Entities.ByNet.Clear(); var m = new thumbleweedMain { thumbleweedLimit = 2 };
        var own = new thumbleweedScript(); own.transform.position = new Vector3(2000, 0, 0); m.thumbleweeds.Add(own);
        var it = new tosaveitemscript(); var copy = new thumbleweedScript { ParentComponent = it }; it.thumbleweed = copy; copy.transform.position = new Vector3(300, 0, 0);
        Entities.ByNet[302] = new Entities.Ent { NetId = 302, Root = it, Proxy = true }; m.thumbleweeds.Add(copy);
        Check(EntityAI.TumbleweedsNear(m) == 2, "tumble limit counts all local weeds and deduplicates near copy at exact radius");
        long limited = EntityAI.TumbleLimited; object[] args = { m, false, null }; Check(!(bool)AIPatch("TumbleSpawn", "Prefix", args) && EntityAI.TumbleLimited == limited + 1, "shared near weeds enforce spawn limit");
        args = new object[] { m, true, null }; Check((bool)AIPatch("TumbleSpawn", "Prefix", args), "forced tumble spawn bypasses limit");
        copy.transform.position.x = 300.01f; Check(EntityAI.TumbleweedsNear(m) == 1, "remote weed outside radius never blocks local spawn");
        AIPatch("TumbleWind", "Prefix", copy); AIPatch("TumbleWind", "Prefix", own); Check(!copy.bwind && own.bwind, "wind acts only on locally simulated weed");
        var fresh = new tosaveitemscript { idInSave = 903 }; fresh.Go = new GameObject { Part = fresh }; m.nowSpawned = fresh;
        long shared = EntityAI.TumbleShared, spawn = Entities.SpawnShared; int sends = Entities.Packets.Count;
        Entities.TestTumbleWire = true;
        AIPatch("TumbleSpawn", "Postfix", m, null);
        Check(EntityAI.TumbleShared == shared + 1 && Entities.SpawnShared == spawn + 1 && Entities.Packets.Count == sends + 1, "new tumble delegates to real shared spawn and Share framing");
        Check(Convert.ToHexString(Entities.Packets.Last().Bytes) == "148703000003010203" && Entities.Packets.Last().Reliable, "tumble shared framing matches expected bytes with opaque 010203 record");
        Entities.TestTumbleWire = false; Entities.PendingShare.Clear();
        AIPatch("TumbleSpawn", "Postfix", m, fresh); Check(EntityAI.TumbleShared == shared + 1, "unchanged nowSpawned never shares again");
        var failed = new tosaveitemscript { Go = new GameObject() }; m.nowSpawned = failed; Entities.TestShareMiss = true; AIPatch("TumbleSpawn", "Postfix", m, fresh); Entities.TestShareMiss = false;
        Check(EntityAI.TumbleShared == shared + 1, "failed shared spawn never counts as shared tumble");
        Entities.InSession = false; args = new object[] { m, false, null }; Check((bool)AIPatch("TumbleSpawn", "Prefix", args), "solo tumble limit stays with game"); AIPatch("TumbleSpawn", "Postfix", m, null); Check(EntityAI.TumbleShared == shared + 1, "solo tumble spawn never shares"); Entities.InSession = true;
    }

    static void AIBoundaryChecks()
    {
        Check((bool)Field(typeof(Entities), "_aiLookupsReady") && GameLookups.FieldRefs.Contains("breakablescript.spawnedGib") && GameLookups.FieldRefs.Contains("newAiScript.chaseStart") && GameLookups.FieldRefs.Contains("newAiScript.currentChaseTime"), "kernel initialization warms the original AI lookups");
        Check(!ReferenceEquals(Field(AIOwner, "W"), Field(typeof(Entities), "W")) && AIOwner.GetField("WS", Static) == null, "AI owns send writer while authority and general forwarding keep kernel WS");
        string[] allowed = { "Ent", "InSession", "IsProxy", "RabbitBreakOnCopy", "EntOfRoot", "TryGetEntity", "BreakFx", "SendToServer", "ReshareSurvivors", "QueueReshare", "WorldBreak", "SendToPeer", "BreakHit", "BreakObjectOf", "ResolveEntity", "KeepBreakIndex", "ItemRef", "CollectCreatureAis", "WasteFixes", "CreatureAudience", "StatesSkippedDeadAnimation", "SkippedByInterest", "AiState", "IsFinite", "AiSound", "SpawnShared", "ShareSpawn", "KnownTumbleweeds" };
        foreach (string part in new[] { "Ai", "Tumbleweeds" })
        {
            string source = Regex.Replace(File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs"), @"//[^\r\n]*", "");
            foreach (Match m in Regex.Matches(source, @"Entities\.(\w+)")) Check(allowed.Contains(m.Groups[1].Value), "AI narrow kernel call " + m.Groups[1].Value);
        }
        foreach (string name in new[] { "RagdollBodiesNow", "CopyBreakShoot", "CopyBreakForce", "CopyBreakBody", "CopyBreakBlast", "CopyBreakCollision", "BreakOut", "AiUpdate", "AiVoice", "TumbleSpawn", "TumbleWind" })
            Check(AIOwner.GetNestedType(name, BindingFlags.NonPublic) != null && typeof(Entities).GetNestedType(name, BindingFlags.NonPublic) == null, "one AI Harmony owner for " + name);
        foreach (string name in new[] { "AiStatesSent", "AiStatesApplied", "AiStatesStale", "AiSoundsSent", "AiSoundsApplied", "AiRemoteSeen", "AiRemoteAttackFrames", "AiDamageSent", "AiKeptOnRemote", "BreakHitsSent", "BreakHitsRun", "BreakFxSent", "BreakFxApplied", "WorldBreaksSent", "WorldBreaksApplied", "WorldBreaksReplayed", "WorldBreaksLate", "WorldBreaksMissed", "WorldGibsShared", "WorldBreakScans", "CopyTouchesIgnored", "ZeroHitsIgnored", "RagdollListsHealed" })
        {
            Check(typeof(Entities).GetField(name, Static) == null, name + " has one feature backing field");
            Check(BridgeReflect.Get("TLDRevamp.Net.EntityAI." + name) == Field(AIOwner, name).ToString(), name + " real owner bridge path");
        }
        long sent = EntityAI.AiStatesSent, breaks = EntityAI.WorldBreaksSent; float scanAt = (float)Field(AIOwner, "_aiScanAt"), flushAt = (float)Field(AIOwner, "_aiDmgFlushAt");
        AICall("AiReset"); AICall("BreaksReset");
        Check(AITable("AiViews").Count == 0 && AITable("AiLastSent").Count == 0 && AITable("AiChasing").Count == 0 && AITable("_aiDmgAcc").Count == 0 && AIList("AiAll").Count == 0, "AI reset clears original maps and lists");
        Check(AIList("_worldBreaks").Count == 0 && AIList("_worldBreaksWaiting").Count == 0 && AITable("_breaksHere").Count == 0, "break reset clears both world queues and local histories");
        Check(EntityAI.AiStatesSent == sent && EntityAI.WorldBreaksSent == breaks && (float)Field(AIOwner, "_aiScanAt") == scanAt && (float)Field(AIOwner, "_aiDmgFlushAt") == flushAt, "reset preserves cumulative counters and original timer lifetimes");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.IndexOf("EntityAI.BreakWaitTick();") < kernel.IndexOf("EntityCarTuning.CarTuneTick();") && kernel.IndexOf("EntityCarTuning.CarTuneTick();") < kernel.IndexOf("EntityAI.WorldBreakTick();"), "retry tuning and world scan tick order preserved");
        Check(kernel.IndexOf("EntityAI.AiTick();") < kernel.IndexOf("EntityRabbits.RabbitTick();") && kernel.IndexOf("EntityAI.BreaksReset();") < kernel.IndexOf("EntityAI.AiReset();") && kernel.IndexOf("EntityAI.AiReset();") < kernel.IndexOf("EntityRabbits.RabbitReset();"), "AI tick and reset remain before rabbits");
    }
}
