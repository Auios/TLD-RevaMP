using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TLDRevamp;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    static Type RagOwner => typeof(EntityRagdolls);
    static object RagCall(string name, params object[] args) => RagOwner.GetMethod(name, Static).Invoke(null, args);
    static void RagPatch(string name, string method, params object[] args) => RagOwner.GetNestedType(name, BindingFlags.NonPublic).GetMethod(method, Static).Invoke(null, args);
    static Entities.Ent RagBody()
    {
        var root = new tosaveitemscript { name = "PlayerRagdol", idInSave = 901 };
        root.Go = new GameObject { Part = root, transform = root.transform }; root.transform.position = new Vector3(1, 2, 3);
        root.ragdollPos = new TestLimbSet { childs = new[] { new Transform { name = "hip", position = new Vector3(1.25f, 2.5f, 2.25f) }, new Transform { name = "head", position = new Vector3(1, 3, 3) } } };
        return new Entities.Ent { NetId = 301, Epoch = 7, RootId = 901, Root = root };
    }
    static byte[] RagWrite(Entities.Ent e, bool key)
    {
        var w = new NetWriter(); RagCall("WriteRagdoll", w, e, key); return w.Buf.Take(w.Len).ToArray();
    }
    static void RagVector(string name, byte[] bytes)
    {
        // Captured by running the unchanged production files at HEAD 230cde4 before extraction.
        string expected = name switch {
            "key" => "020101EC4D000000000000F03F0000000000000040000000000000084003000000000000FA00F40112FD030000000000000000E803000003000000000000",
            "delta" => "020201027D00E803000003000000000000",
            "frame" => "02000000803E0000003F000040BF030000000000000000003E0000803F0000000003000000000000",
            "none" => "00",
            "death remove" or "death dormant remove" => "232D010000",
            _ => throw new Exception(name)
        };
        Check(Convert.ToHexString(bytes) == expected, name + " matches frozen 230cde4 bytes");
    }
    static NetReader RagReader(byte[] bytes) => new NetReader { Buf = bytes, End = bytes.Length };
    static void RagRead(byte[] bytes, Entities.Ent e) => EntityRagdolls.ReadRagdoll(RagReader(bytes), e, new Vector3d(1, 2, 3), Quaternion.identity);
    static byte[] RagState(byte[] block)
    {
        var w = new NetWriter(); EntityCodec.WriteStateHead(w, 301, 7, 42, 10, new Vector3d(1, 2, 3), Quaternion.identity, Vector3.zero, false);
        w.U8(0); w.U8(0); w.U8(0); w.Bytes(block, 0, block.Length); return w.Buf.Take(w.Len).ToArray();
    }
    static void RagdollsChecks()
    {
        var e = RagBody(); Time.realtimeSinceStartup = 10;
        byte[] key = RagWrite(e, true); RagVector("key", key);
        Check(e.Rag.Staged && e.Rag.KeyN == -1, "writing a Key stages it without committing");
        RagCall("RagdollSent", e, false);
        Check(!e.Rag.Staged && e.Rag.KeyN == -1, "unsent Key never becomes the delta baseline");
        RagVector("key", RagWrite(e, true)); RagCall("RagdollSent", e, true);
        Check(e.Rag.KeyId == 1 && e.Rag.KeyEpoch == 7 && e.Rag.KeyN == 2 && !e.Rag.Staged, "sent Key commits id epoch and arrays");
        e.Root.ragdollPos.childs[1].position.x += .125f;
        Set(RagOwner, "RagdollDeltaMaxShare", 1f);
        byte[] delta = RagWrite(e, false); RagVector("delta", delta);
        Check(EntityRagdolls.RagdollMoved(e) == false, "written limb pose is the movement baseline");
        e.Root.ragdollPos.childs[1].position.x += .005f;
        Check(!EntityRagdolls.RagdollMoved(e), "sub-centimetre jitter skips");
        e.Root.ragdollPos.childs[1].position.x -= .005f;
        Time.realtimeSinceStartup = 12;
        Check(RagWrite(e, false)[1] == RagdollWire.Delta, "exact key interval still permits delta");
        Time.realtimeSinceStartup = 12.01f;
        Check(RagWrite(e, false)[1] == RagdollWire.Key, "expired key interval forces key");
        Time.realtimeSinceStartup = 10;
        e.Epoch++;
        Check(RagWrite(e, false)[1] == RagdollWire.Key, "epoch change forces key"); e.Epoch--;
        EntityRagdolls.RagdollDeltaMaxShare = 0;
        Check(RagWrite(e, false)[1] == RagdollWire.Key, "delta share threshold forces key");
        Set(RagOwner, "RagdollDelta", false); RagVector("frame", RagWrite(e, false));
        Set(RagOwner, "RagdollDelta", true); Set(RagOwner, "RagdollDeltaMaxShare", .5f);
        e.Root.ragdollPos = null; RagVector("none", RagWrite(e, false));
        RagdollReadChecks(key, delta);
        RagdollDeathChecks();
        Entities.ByNet.Clear(); Entities.Packets.Clear(); mainscript.s = new mainscript { player = new fpscontroller() };
        e = RagBody(); mainscript.s.player.spawnedRagdol = e.Root.gameObject; mainscript.s.player.died = true;
        RagPatch("ShareRealDeathRagdoll", "Postfix", mainscript.s.player);
        Set(RagOwner, "DeathCorpseStays", false);
        RagPatch("ReleaseCorpseOnRevive", "Postfix", mainscript.s.player);
        RagVector("death remove", Entities.Packets.Last().Bytes);
        Check(Entities.Packets.Last().Reliable && e.Root.gameObject.Destroyed && !Entities.HasEntity(301), "ordinary deletion precedes local corpse destruction");
        e = RagBody(); mainscript.s.player.spawnedRagdol = e.Root.gameObject;
        RagPatch("ShareRealDeathRagdoll", "Postfix", mainscript.s.player); Entities.TestDeleteDormant = true;
        RagPatch("ReleaseCorpseOnRevive", "Postfix", mainscript.s.player);
        RagVector("death dormant remove", Entities.Packets.Last().Bytes);
        Check(Entities.Packets.Last().Reliable && e.Root.gameObject.Destroyed && !Entities.HasEntity(301), "dormant fallback sends reliably then forgets the entity and destroys the corpse");
        e = RagBody(); mainscript.s.player.spawnedRagdol = e.Root.gameObject;
        RagPatch("ShareRealDeathRagdoll", "Postfix", mainscript.s.player);
        Entities.TestDeleteThrow = true; Entities.IsHost = true;
        long releases = EntityRagdolls.DeathCorpsesReleased;
        Entities.HostReceive = (kind, r) => {
            Check(kind == Entities.RemoveItem && r.U32() == 301 && Entities.HasEntity(301) && !e.Root.gameObject.Destroyed && EntityRagdolls.DeathCorpsesReleased == releases, "fallback sends before forgetting destroying and counting");
            RagPatch("ReleaseCorpseOnRevive", "Postfix", mainscript.s.player);
            var reused = (NetWriter)Field(RagOwner, "W"); reused.Reset(); reused.U8(99);
        };
        RagPatch("ReleaseCorpseOnRevive", "Postfix", mainscript.s.player);
        RagVector("death dormant remove", Entities.Packets.Last().Bytes);
        Check(EntityRagdolls.DeathCorpsesReleased == releases + 1 && !Entities.HasEntity(301) && e.Root.gameObject.Destroyed, "failed delete falls back safely through host reentry and writer reuse");
        Entities.HostReceive = null; Entities.IsHost = false; Entities.TestDeleteThrow = false;
        Entities.TestDeleteDormant = false; Set(RagOwner, "DeathCorpseStays", true);
        RagdollOutfitChecks();
        CoreMembers.Register();
        foreach (string field in new[] { "RagdollDelta", "RagdollKeyEveryS", "RagdollDeltaMaxShare" })
        {
            string value = field == "RagdollDelta" ? "False" : "0.75";
            BridgeReflect.Set("TLDRevamp.Net.Entities." + field, value);
            Check(BridgeReflect.Get("TLDRevamp.Net.Entities." + field) == BridgeReflect.Get("TLDRevamp.Net.EntityRagdolls." + field), field + " full script path reaches sole backing value");
        }
        EntityRagdolls.RagdollDelta = true; EntityRagdolls.RagdollKeyEveryS = 2; EntityRagdolls.RagdollDeltaMaxShare = .5f;
        Check(!ReferenceEquals(Field(RagOwner, "W"), Field(typeof(Entities), "W")), "death fallback owns its writer");
        foreach (string field in new[] { "RagdollCopies", "RagdollLimbOwner", "_ragBlock", "_deathCorpse", "DeathItemSaveIds", "RagdollKeysSent" })
            Check(typeof(Entities).GetField(field, Static) == null, field + " has one feature backing field");
        Check(typeof(Entities.Rag).DeclaringType == typeof(Entities) && RagOwner.GetNestedType("Rag") == null, "Rag retains its CLR identity");
        foreach (string part in new[] { "Ragdoll", "Death", "Corpses" })
        {
            string source = File.ReadAllText("src/TLDRevamp/Net/Entities." + part + ".cs").Split("    public static partial class Entities")[0];
            source = Regex.Replace(source, @"//[^\r\n]*", "");
            string[] allowed = { "Ent", "Rag", "InSession", "RagdollFrame", "LimbsByBody", "IsFinite", "TrackRagdollLimb", "RagdollRabbitBodyGone", "PlayerCorpses", "NetIdOfSaveId", "TryDeathCorpse", "DeleteShared", "HasEntity", "RemoveItem", "SendToServer", "ForgetDeathCorpse", "SpawnShared", "ShareSpawn", "NetIdOf" };
            Check(Regex.Matches(source, @"Entities\.(\w+)").Cast<Match>().All(m => allowed.Contains(m.Groups[1].Value)), part + " uses only its named narrow kernel boundary");
        }
        foreach (string patch in new[] { "ShareRealDeathRagdoll", "ReleaseCorpseOnRevive", "OutfitIntoRecord", "OutfitRecordChecked", "CorpseLoadedWhole" })
            Check(RagOwner.GetNestedType(patch, BindingFlags.NonPublic).GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length == 1 && typeof(Entities).GetNestedType(patch, BindingFlags.NonPublic) == null, patch + " has one attributed feature owner");
        string kernel = File.ReadAllText("src/TLDRevamp/Net/Entities.cs");
        Check(kernel.Contains("int ragAt = WS.Len; ragKey = EntityRagdolls.WriteRagdoll(WS, e, first || final);") && kernel.Contains("if (WS.Buf[ragAt] > 0) EntityRagdolls.RagdollStatesSent++;"), "state uses existing shared writer and counts nonempty blocks at same slot");
        Check(kernel.IndexOf("EntityRagdolls.DeathReset();") < kernel.IndexOf("ProxyItems.Clear();") && kernel.Contains("CargoGhostNets.Clear(); EntityRagdolls.ResetCopies(); EntityEffects.ResetSpraying();"), "death and copy reset order remains explicit");
        EntityRagdolls.DeathReset(); EntityRagdolls.ResetCopies(); Entities.ByNet.Clear(); Entities.Packets.Clear();
    }

    static void RagdollReadChecks(byte[] key, byte[] delta)
    {
        EntityRagdolls.ResetCopies();
        var copy = RagBody(); copy.Proxy = true;
        long missing = EntityRagdolls.RagdollDeltasNoKey;
        RagRead(delta, copy);
        Check(EntityRagdolls.RagdollDeltasNoKey == missing + 1 && !copy.Rag.Has, "delta without key cannot apply");
        RagRead(key, copy); RagRead(delta, copy);
        Check(copy.Rag.Has && copy.Rag.CHas && copy.Rag.CId == 1 && EntityRagdolls.Following(copy) && Math.Abs(copy.Rag.TPos[1].x - .125f) < .001f, "copy combines cumulative delta with key");
        foreach (int cut in Enumerable.Range(1, key.Length - 1))
        {
            var fresh = RagBody(); fresh.Proxy = true; RagRead(key.Take(cut).ToArray(), fresh);
            Check(fresh.Rag == null || !fresh.Rag.Has, "truncated key never applies at " + cut);
        }
        var oldTarget = copy.Rag.TPos;
        RagRead(delta.Take(delta.Length - 1).ToArray(), copy);
        Check(ReferenceEquals(oldTarget, copy.Rag.TPos) && EntityRagdolls.Following(copy), "bad block leaves current pose and copy following intact");
        copy.Epoch++;
        RagRead(delta, copy);
        Check(ReferenceEquals(oldTarget, copy.Rag.TPos) && EntityRagdolls.RagdollDeltasNoKey == missing + 2, "delta from previous epoch is refused"); copy.Epoch--;
        var wrong = RagBody(); wrong.Proxy = true; wrong.Root.ragdollPos.childs[0].name = "wrong";
        long hashes = EntityRagdolls.RagdollHashMismatch; RagRead(key, wrong);
        Check(!wrong.Rag.Has && wrong.Rag.CHas && EntityRagdolls.RagdollHashMismatch == hashes + 1, "hierarchy hash mismatch keeps key without moving copy");
        var unplaced = RagBody(); unplaced.Root = null; RagRead(key, unplaced);
        Check(unplaced.Rag.CHas && !unplaced.Rag.Has, "unplaced owner still retains key");
        var own = RagBody(); RagRead(key, own);
        Check(own.Rag.CHas && !own.Rag.Has, "nonproxy retains key without applying");
        var badCount = RagBody(); badCount.Proxy = true; badCount.Root.ragdollPos.childs = new[] { new Transform() }; RagRead(key, badCount);
        Check(!badCount.Rag.Has, "limb count mismatch refuses display");
        foreach (float x in new[] { 51f, float.NaN, float.PositiveInfinity })
        {
            var w = new NetWriter(); RagdollWire.WriteFrame(w, new[] { new Vector3(x, 0, 0), Vector3.zero }, new[] { Quaternion.identity, Quaternion.identity });
            var fresh = RagBody(); fresh.Proxy = true; RagRead(w.Buf.Take(w.Len).ToArray(), fresh);
            Check(fresh.Rag == null || !fresh.Rag.Has, "invalid or distant limb refused " + x);
        }
        var state = RagState(key); var deltaState = RagState(delta);
        Check(StateTail.TryRead(state, out var tail) && tail.Mode == StateTail.Mode.Key && tail.Net == 301 && tail.Epoch == 7 && state.Length - tail.Offset == key.Length, "real state tail locates and sizes moved key bytes");
        Check(StateTail.ReplayKey(deltaState, state, 7, out var replay) && replay.SequenceEqual(state), "interest replay supplies byte-identical key before delta");
        var late = RagBody(); long lateCount = EntityRagdolls.RagdollLateKeys;
        EntityRagdolls.KeepLateKey(RagReader(state), 0, late, 8);
        Check(late.Rag == null, "wrong epoch late key ignored");
        EntityRagdolls.KeepLateKey(RagReader(state), 0, late, 7);
        EntityRagdolls.KeepLateKey(RagReader(state), 0, late, 7);
        Check(late.Rag.CHas && !late.Rag.Has && EntityRagdolls.RagdollLateKeys == lateCount + 1, "late key retained once without applying a stale state");
        EntityRagdolls.RagdollDelta = false; var disabled = RagBody(); EntityRagdolls.KeepLateKey(RagReader(state), 0, disabled, 7);
        Check(disabled.Rag == null, "disabled delta ignores late keys"); EntityRagdolls.RagdollDelta = true;
        var wrapped = RagBody(); wrapped.Rag = new Entities.Rag { CHas = true, CEpoch = 7, CId = 255 };
        EntityRagdolls.KeepLateKey(RagReader(state), 0, wrapped, 7);
        Check(wrapped.Rag.CId == 1, "late key comparison accepts byte id wrap");
        foreach (var limb in copy.Root.ragdollPos.childs) { limb.ChildComponent = new Rigidbody(); limb.Go = new GameObject { Part = limb.ChildComponent }; }
        EntityRagdolls.RagdollTick(1);
        var first = copy.Root.ragdollPos.childs[0];
        Check(EntityRagdolls.TryLimbOwner(first.gameObject, out var owner) && owner == copy && Entities.ProxyItems.Contains(first.gameObject), "copy follow registers limb owner and proxy pickup index");
        Check(EntityRagdolls.RagdollOf(new pickupable { Go = first.gameObject }) == copy, "grabbing detached limb resolves the same entity");
        long released = EntityRagdolls.RagdollReleasedOnNone; RagRead(new byte[] { 0 }, copy);
        Check(!copy.Rag.Has && !EntityRagdolls.Following(copy) && !EntityRagdolls.TryLimbOwner(first.gameObject, out _) && !Entities.ProxyItems.Contains(first.gameObject) && EntityRagdolls.RagdollReleasedOnNone == released + 1, "none releases copy and both limb indexes");
        var mod = RagBody(); TLDRevamp.Multiplayer.MpApi.Root = mod.Root.gameObject;
        TLDRevamp.Multiplayer.MpApi.Limbs = new[] { new Transform { name = "mod" } };
        Check(EntityRagdolls.Limbs(mod) == TLDRevamp.Multiplayer.MpApi.Limbs && EntityRagdolls.Limp(mod), "mod registration precedes game limbs and null callback is limp");
        TLDRevamp.Multiplayer.MpApi.Limp = () => throw new Exception("fixture"); long errors = EntityRagdolls.ModLimpErrors;
        Check(!EntityRagdolls.Limp(mod) && EntityRagdolls.ModLimpErrors == errors + 1, "mod limp exception counted and refused");
        mod.PartIndex = 0; Check(EntityRagdolls.Limbs(mod) == null, "part entity never uses root limb registration");
        TLDRevamp.Multiplayer.MpApi.Root = null; TLDRevamp.Multiplayer.MpApi.Limbs = null; TLDRevamp.Multiplayer.MpApi.Limp = null;
        var far = RagBody(); far.Root.ragdollPos.childs[0].position.x = 100;
        Check(RagWrite(far, true)[1] == RagdollWire.Frame && !far.Rag.Staged, "millimetre overflow falls back to frame without committing key");
        var vehicle = RagBody(); vehicle.Root.car = new carscript(); Check(RagWrite(vehicle, true)[1] == RagdollWire.Frame, "vehicle limbs retain frame form");
    }

    static void RagdollDeathChecks()
    {
        Entities.ByNet.Clear(); Entities.Packets.Clear(); EntityRagdolls.DeathReset();
        var pl = new fpscontroller { died = true }; mainscript.s = new mainscript { player = pl };
        var e = RagBody(); pl.spawnedRagdol = e.Root.gameObject;
        long shared = EntityRagdolls.DeathCorpsesShared, missed = EntityRagdolls.DeathCorpseShareMissed;
        Entities.InSession = false; RagPatch("ShareRealDeathRagdoll", "Postfix", pl); Entities.InSession = true;
        EntityRagdolls.DeathRagdollShare = false; RagPatch("ShareRealDeathRagdoll", "Postfix", pl); EntityRagdolls.DeathRagdollShare = true;
        RagPatch("ShareRealDeathRagdoll", "Postfix", new fpscontroller { died = true, spawnedRagdol = pl.spawnedRagdol });
        Check(EntityRagdolls.DeathCorpsesShared == shared && Entities.ByNet.Count == 0, "session disabled and other-player death guards");
        pl.spawnedRagdol = null; RagPatch("ShareRealDeathRagdoll", "Postfix", pl);
        Check(EntityRagdolls.DeathCorpseShareMissed == missed + 1, "missing actual corpse counted");
        pl.spawnedRagdol = e.Root.gameObject; Entities.TestShareMiss = true;
        RagPatch("ShareRealDeathRagdoll", "Postfix", pl); Entities.TestShareMiss = false;
        Check(EntityRagdolls.DeathCorpseShareMissed == missed + 2 && EntityRagdolls.DeathCorpseNetId == 0, "refused unknown share counted without capturing a corpse");
        pl.spawnedRagdol = e.Root.gameObject; Entities.TestSharePendingNet = true;
        RagPatch("ShareRealDeathRagdoll", "Postfix", pl);
        Check(EntityRagdolls.DeathCorpsesShared == shared + 1 && EntityRagdolls.DeathCorpseSaveId == 901 && EntityRagdolls.DeathCorpseNetId == 0, "death captures exact save id while share net is pending");
        Entities.TestSharePendingNet = false;
        Check(Entities.NetIdOfSaveId(901) == 301 && EntityRagdolls.DeathInfo().Contains("\"corpseNetId\":0"), "corpse net remains the captured value after asynchronous assignment");
        long dup = EntityRagdolls.DeathCorpseDuplicates; RagPatch("ShareRealDeathRagdoll", "Postfix", pl);
        Check(EntityRagdolls.DeathCorpseDuplicates == dup + 1 && EntityRagdolls.DeathCorpsesShared == shared + 1 && EntityRagdolls.DeathCorpseNetId == 301, "duplicate death reuses corpse and refreshes captured net");
        EntityRagdolls.DeathItemSaveIds.Add(901); EntityRagdolls.DeathItemNetIds.Add(0);
        EntityRagdolls.DeathItemSaveIds.Add(999); EntityRagdolls.DeathItemNetIds.Add(456);
        Check(EntityRagdolls.DeathInfo().Contains("\"itemIds\":[[901,301],[999,456]]"), "gear ids resolve pending share then fall back to captured net");
        Check(Entities.NetIdOfSaveId(0) == 0 && Entities.NetIdOfSaveId(999) == 0, "save lookup guards zero and absent ids");
        long kept = EntityRagdolls.DeathCorpsesKept; EntityRagdolls.DeathCorpseStays = true;
        RagPatch("ReleaseCorpseOnRevive", "Postfix", pl); RagPatch("ReleaseCorpseOnRevive", "Postfix", pl);
        Check(EntityRagdolls.DeathCorpsesKept == kept + 1 && Entities.HasEntity(301) && !e.Root.gameObject.Destroyed, "respawn keeps corpse exactly once");
        EntityRagdolls.DeathReset();
        Check(EntityRagdolls.DeathCorpseSaveId == 0 && EntityRagdolls.DeathCorpseNetId == 0 && EntityRagdolls.DeathItemSaveIds.Count == 0 && EntityRagdolls.DeathItemNetIds.Count == 0 && EntityRagdolls.DeathCorpsesShared == shared + 1, "reset clears session ids and gear but preserves cumulative counters");
        var child = new tosaveitemscript(); child.transform.parent = e.Root.transform;
        Entities.ByNet.Clear(); Entities.ByNet[0] = new Entities.Ent { NetId = 0, Root = child };
        Check(Entities.TryDeathCorpse(e.Root.gameObject, out uint zero) && zero == 0, "corpse membership preserves first child match independently of net id");
        Entities.ByNet.Clear(); var open = RagBody(); pl.spawnedRagdol = open.Root.gameObject;
        RagPatch("ShareRealDeathRagdoll", "Postfix", pl); EntityRagdolls.DeathCorpseStays = false;
        long released = EntityRagdolls.DeathCorpsesReleased; EntityRagdolls.DeathReset();
        Check(EntityRagdolls.DeathCorpsesReleased == released + 1 && open.Root.gameObject.Destroyed && EntityRagdolls.DeathCorpseNetId == 0, "reset releases an open corpse under the old policy");
        EntityRagdolls.DeathCorpseStays = true;
        open = RagBody(); pl.spawnedRagdol = open.Root.gameObject;
        RagPatch("ShareRealDeathRagdoll", "Postfix", pl); kept = EntityRagdolls.DeathCorpsesKept;
        EntityRagdolls.DeathReset();
        Check(EntityRagdolls.DeathCorpsesKept == kept + 1 && !open.Root.gameObject.Destroyed && Entities.HasEntity(301), "reset keeps an open corpse under the default policy");
        Entities.ByNet.Clear();
    }

    static void RagdollOutfitChecks()
    {
        var model = new playermodeloutfitscript { selectedCharacter = 0, characters = new[] { new playermodeloutfitscript.Character { ch = new playermodeloutfitscript.Model { segments = new[] { new playermodeloutfitscript.Segment { BEnabled = true, Ccolor = new Color(.25f, .5f, .75f, 1) } } } } } };
        var record = new save_outfit(); long saved = EntityRagdolls.OutfitsSaved;
        RagPatch("OutfitIntoRecord", "Postfix", record, model);
        Check(EntityRagdolls.OutfitsSaved == saved + 1 && record.selectedCharacter == 0 && record.segments.Length == 1 && record.segments[0].enabled && record.segments[0].b == .75f, "outfit save copies selected character and segment flags/colors");
        EntityRagdolls.CorpseOutfitSaved = false; RagPatch("OutfitIntoRecord", "Postfix", record, model); EntityRagdolls.CorpseOutfitSaved = true;
        model.selectedCharacter = -1; RagPatch("OutfitIntoRecord", "Postfix", record, model); model.selectedCharacter = 0;
        Check(EntityRagdolls.OutfitsSaved == saved + 1, "disabled and invalid outfit saves are ignored");
        var load = RagOwner.GetNestedType("OutfitRecordChecked", BindingFlags.NonPublic).GetMethod("Prefix", Static);
        Check((bool)load.Invoke(null, new object[] { record, model }), "valid outfit load allowed");
        long refused = EntityRagdolls.OutfitLoadsRefused; record.selectedCharacter = 1;
        Check(!(bool)load.Invoke(null, new object[] { record, model }) && EntityRagdolls.OutfitLoadsRefused == refused + 1, "foreign character index refused and counted");
        Check(!(bool)load.Invoke(null, new object[] { record, null }), "null outfit target refused");
        record.selectedCharacter = -1;
        Check(!(bool)load.Invoke(null, new object[] { record, model }), "negative character refused");
        record.selectedCharacter = 0; var character = model.characters[0]; model.characters[0] = null;
        Check(!(bool)load.Invoke(null, new object[] { record, model }), "missing character refused"); model.characters[0] = character;
        mainscript.s = new mainscript { player = new fpscontroller { outfit = model } };
        RagPatch("CorpseLoadedWhole", "Postfix", model); Check(model.Resets == 0, "local player keeps first person cut");
        mainscript.s.player.outfit = null; RagPatch("CorpseLoadedWhole", "Postfix", model);
        Check(model.Resets == 1, "loaded corpse has cut removed");
        mainscript.s.player.spawnedRagdol = model.gameObject; model.transform.parent = mainscript.s.player.transform;
        RagPatch("CorpseLoadedWhole", "Postfix", model);
        Check(model.Resets == 1, "ready corpse under local player retains its cut");
        EntityRagdolls.CorpseWhole = false; RagPatch("CorpseLoadedWhole", "Postfix", model); EntityRagdolls.CorpseWhole = true;
        Check(model.Resets == 1, "whole corpse switch retains old display behavior");
    }
}
