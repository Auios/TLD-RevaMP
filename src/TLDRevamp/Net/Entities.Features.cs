using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public static void LateSample() => EntityDoors.LateSample();
        public static string CarDoors(uint net) => EntityDoors.CarDoors(net);
        public static string Food(uint net) => EntityDoors.Food(net);
        public static string Rabbit(uint net) => EntityDoors.Rabbit(net);
        internal static System.Collections.Generic.List<DoorRef> DoorsOf(Ent e) => EntityDoors.DoorsOf(e);
        internal static bool ApplyingDoorAttach { get => _applyingAttach; set => _applyingAttach = value; }
        internal static bool ApplyingDoorDetach => _applyingDetach;
        internal static void ApplyDoorAttachPose(Ent e, attachablescript at, Vector3 position, Quaternion rotation) => ApplyAttachPose(e, at, position, rotation);

        public const byte CarTune = 113;   // both: u32 net, CarTuning
        public const int MaxTuneBytes = 2048;
        public static float CarTuneEveryS { get => EntityCarTuning.CarTuneEveryS; set => EntityCarTuning.CarTuneEveryS = value; }
        public static float CarTuneResendS { get => EntityCarTuning.CarTuneResendS; set => EntityCarTuning.CarTuneResendS = value; }
        public static float CopyFluidCheckS { get => EntityCarTuning.CopyFluidCheckS; set => EntityCarTuning.CopyFluidCheckS = value; }
        public static float CopyFluidToleranceL { get => EntityCarTuning.CopyFluidToleranceL; set => EntityCarTuning.CopyFluidToleranceL = value; }
        public static long BloodCleanedOnCopy { get => EntityCarTuning.BloodCleanedOnCopy; set => EntityCarTuning.BloodCleanedOnCopy = value; }
        public static long BloodCleanSent { get => EntityCarTuning.BloodCleanSent; set => EntityCarTuning.BloodCleanSent = value; }
        public static long BloodCleanApplied { get => EntityCarTuning.BloodCleanApplied; set => EntityCarTuning.BloodCleanApplied = value; }
        public static long TunesSent { get => EntityCarTuning.TunesSent; set => EntityCarTuning.TunesSent = value; }
        public static long TunesResent { get => EntityCarTuning.TunesResent; set => EntityCarTuning.TunesResent = value; }
        public static long TunesApplied { get => EntityCarTuning.TunesApplied; set => EntityCarTuning.TunesApplied = value; }
        public static long TunesReapplied { get => EntityCarTuning.TunesReapplied; set => EntityCarTuning.TunesReapplied = value; }
        public static long TunesReplayed { get => EntityCarTuning.TunesReplayed; set => EntityCarTuning.TunesReplayed = value; }
        public static long TunesRefused { get => EntityCarTuning.TunesRefused; set => EntityCarTuning.TunesRefused = value; }
        public static long TunesKept { get => EntityCarTuning.TunesKept; set => EntityCarTuning.TunesKept = value; }
        public static string TuneLast { get => EntityCarTuning.TuneLast; set => EntityCarTuning.TuneLast = value; }
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, TuneHere> ObservedTuneHere => EntityCarTuning.ObservedTuneHere;
        internal static CarTuning CaptureTuning(carscript c) => EntityCarTuning.CaptureTuning(c);
        internal static int ApplyTuning(carscript c, CarTuning t) => EntityCarTuning.ApplyTuning(c, t);
        internal static int PrefabTuneKey(tosaveitemscript root) => EntityCarTuning.PrefabTuneKey(root);
        internal static bool IsTunableCar(Ent e) => EntityCarTuning.IsTunableCar(e);
        internal static void KnownTunings(System.Collections.Generic.Dictionary<uint, byte[]> into) => EntityCarTuning.KnownTunings(into);
        internal static void TunedRootIds(System.Collections.Generic.HashSet<uint> into) => EntityCarTuning.TunedRootIds(into);

        internal static System.Collections.Generic.IEnumerable<Ent> PlacedTuningCars()
        {
            foreach (var e in Placed()) if (!e.Proxy && EntityCarTuning.IsTunableCar(e)) yield return e;
        }
        internal static bool TuneOwnerIs(uint net, int from) => Server.TryGetValue(net, out var se) && se.OwnerId == from;
        internal static void KeepOwnedTune(uint net, byte[] bytes, int off, int len)
        {
            var se = Server[net];
            se.Tune = new byte[len]; System.Buffer.BlockCopy(bytes, off, se.Tune, 0, len);
        }
        internal static void MarkFeatureEdit(tosaveitemscript it, bool fromTank = false, string kind = "") => MarkEdit(it, fromTank, kind);
        internal static void ForgetTankEditTruth(uint id) => _tankTold.Remove(id);
        internal static bool HasPendingItemEdit(tosaveitemscript it) => EditDirty.ContainsKey(it);
        internal static bool TryTankFilledAt(tankscript tank, out float at) => _tankFilledAt.TryGetValue(tank, out at);
        internal static void SetFluidApplyingState(bool value) => _applyingState = value;
        internal static void CollectCopyFluidIds(System.Collections.Generic.HashSet<uint> into)
        {
            foreach (var e in ByNet.Values) if (e.Proxy && e.ItemIds != null) foreach (var id in e.ItemIds) into.Add(id);
        }
        internal static System.Collections.Generic.IEnumerable<Ent> CopyFluidEntities()
        {
            foreach (var e in ByNet.Values) if (e.Proxy && e.Root != null && e.Items != null) yield return e;
        }

        public const byte RadioSync = 54;
        public static float RadioResendS { get => EntityRadio.RadioResendS; set => EntityRadio.RadioResendS = value; }
        public static string RadioStatus() => EntityRadio.RadioStatus();

        public const byte WorldClock = 97;   // f64 st, f32 world seconds, f32 rate, u16 sleep epoch, bool snap
        public static float SleepClockS { get => EntityWorldClock.SleepClockS; set => EntityWorldClock.SleepClockS = value; }
        internal static void ApplyWorldTime(float wt, bool force) => EntityWorldClock.ApplyWorldTime(wt, force);

        public const byte StormSync = 56;
        public static float StormResendS { get => EntityWeather.StormResendS; set => EntityWeather.StormResendS = value; }
        public static long StormSyncsSent { get => EntityWeather.StormSyncsSent; set => EntityWeather.StormSyncsSent = value; }
        public static long StormSyncsApplied { get => EntityWeather.StormSyncsApplied; set => EntityWeather.StormSyncsApplied = value; }
        public static long StormsMade { get => EntityWeather.StormsMade; set => EntityWeather.StormsMade = value; }
        public static long StormsRemoved { get => EntityWeather.StormsRemoved; set => EntityWeather.StormsRemoved = value; }
        public static long StormSnaps { get => EntityWeather.StormSnaps; set => EntityWeather.StormSnaps = value; }
        internal static System.Collections.Generic.IReadOnlyDictionary<sandstormscript, uint> ObservedStormIds => EntityWeather.ObservedStormIds;
        internal static System.Collections.Generic.IReadOnlyDictionary<uint, sandstormscript> ObservedStormById => EntityWeather.ObservedStormById;

        public static string CarSignals(uint net) => EntityCarSignals.CarSignals(net);
        internal static int CarSignalKey(Ent e) => EntityCarSignals.CarSignalKey(e);

        internal static Ent EntOfRoot(Transform root)
        {
            var own = PlacedOfTransform(root);
            if (own != null) return own;
            foreach (var e in LookupSet()) if (e.Root != null && e.Root.transform.root == root) return e;
            return null;
        }

        internal static bool TankRef(tankscript t, out uint net, out int ti)
        {
            net = 0; ti = -1;
            foreach (var e in ByNet.Values)
            {
                if (e.Items == null || e.Root == null) continue;
                int i = TanksOf(e).IndexOf(t);
                if (i >= 0) { net = e.NetId; ti = i; return true; }
            }
            return false;
        }

        public const byte PlayerSound = 57, ShotFx = 45;
        public static long PlayerSoundsSent { get => EntityEffects.PlayerSoundsSent; set => EntityEffects.PlayerSoundsSent = value; }
        public static long PlayerSoundsPlayed { get => EntityEffects.PlayerSoundsPlayed; set => EntityEffects.PlayerSoundsPlayed = value; }
        public static long ModelStepsMuted { get => EntityEffects.ModelStepsMuted; set => EntityEffects.ModelStepsMuted = value; }
        public static long[] SoundsSentBy { get => EntityEffects.SoundsSentBy; set => EntityEffects.SoundsSentBy = value; }
        public static long[] SoundsPlayedBy { get => EntityEffects.SoundsPlayedBy; set => EntityEffects.SoundsPlayedBy = value; }
        internal static string ObservedLastSound => EntityEffects.ObservedLastSound;
        public static bool SprayFx { get => EntityEffects.SprayFx; set => EntityEffects.SprayFx = value; }
        internal static bool IsSpraying(tosaveitemscript it) => EntityEffects.IsSpraying(it);
        internal static sprayscript HeldSpray(fpscontroller pl) => EntityEffects.HeldSpray(pl);
        public static string LampState(uint net) => EntityEffects.LampState(net);
        public static string SprayFxState(uint net) => EntityEffects.SprayFxState(net);
        public static string ShotFxStats() => EntityEffects.ShotFxStats();
        private static readonly bool _effectsLookupsReady = EntityEffects.InitializeLookups();

        internal static bool IsFinite(Vector3d value) => Finite(value);
        internal static bool IsFinite(Quaternion value) => Finite(value);
        internal static bool ResolveEntity(Ent entity) => Resolve(entity);
        internal static bool ItemRef(tosaveitemscript it, out uint net, out int idx)
        {
            net = 0; idx = -1;
            if (it == null) return false;
            foreach (var e in ByNet.Values)
            {
                if (e.Items == null) continue;
                int i = e.Items.IndexOf(it);
                if (i >= 0) { net = e.NetId; idx = i; return true; }
            }
            return false;
        }

        public const byte ExplodeReq = 46, ExplosionFx = 47;
        public static string ExplosionStats() => EntityExplosions.ExplosionStats();

        internal static bool TryExplosionRequestOwner(int from, uint net, out int owner)
        {
            owner = 0;
            if (!Server.TryGetValue(net, out var se) || se.OwnerId == from) return false;
            if (!HitCheck.AllowOnObject(from, $"explosive or tank on object {net}", se.HasState ? se.Pos : se.Where, HitCheck.ObjShotM, HitCheck.ObjSizeM)) return false;
            owner = se.OwnerId;
            return true;
        }

        internal static bool TryGetEntity(uint net, out Ent entity) => ByNet.TryGetValue(net, out entity);
        internal static float HostClockRate => EntityWorldClock.HostClockRate;
        internal static bool IsFinite(Vector3 value) => Finite(value);
        internal static bool IsFinite(float value) => Finite(value);
        internal static bool IsFinite(double value) => Finite(value);
        internal static void SendToPeer(int playerId, NetWriter w, bool reliable) => ServerSendTo(playerId, w, reliable);
        internal static void SendToPeers(NetWriter w, bool reliable, int except, Vector3d? near = null, double range = 0) => ServerSendAll(w, reliable, except, near, range);
    }
}
