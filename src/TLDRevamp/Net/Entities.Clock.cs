using UnityEngine;

namespace TLDRevamp.Net
{
    internal static class EntityWorldClock
    {
        private static readonly NetWriter W = new NetWriter();
        internal static float HostClockRate => _hostRate;
        private static bool _wasSleeping;
        private static double _hostRateAt = -1, _hostScaledAt;
        private static float _hostRate = 1f, _hostScale = -1f;
        private static double _worldSentAt = -100;
        private static double _worldStamp = -1, _worldPosition, _worldRate, _worldFollow, _worldFollowAt;
        private static double _worldWakeStamp = -1;
        private static bool _worldFollowing, _worldSnap;
        private static bool _worldHasEpoch, _worldAwaitingWake;
        private static ushort _worldEpoch, _worldSeenEpoch;
        private static napszakvaltakozas _worldInstance;
        private static float _sleepProgress = -1f;   // a sleeping player's cloud curve, for the world clock (Fixes.CloudClock)
        private static double _sleepProgressAt = -100;

        /// Sleep/load discontinuities shift the same quantity as vanilla sleep; periodic anchors use the follower.
        public static long WorldTimeJumps;
        private static float _wokeAt = -100f;
        public static long WorldTimeStaleIgnored;
        public static float SleepClockS = 0.25f;
        public static long SleepClocksSent;
        private static float _sleepClockAcc;
        internal static void ApplyWorldTime(float wt, bool force)
        {
            var sw = napszakvaltakozas.s;
            if (sw == null || (mainscript.s != null && mainscript.s.sleeping)) return;   // our own sleep races the clock; it syncs when it ends
            // just woke: a clock sent before our jump reached the server (the periodic one, in flight) would undo the
            // sleep here for up to 10 s — the server's next clock has our jump in it
            if (!force && Time.realtimeSinceStartup - _wokeAt < 3f) { WorldTimeStaleIgnored++; return; }
            float len = sw.dt + sw.nt, diff = wt - WorldTimeNow();
            if (len > 0f) { if (diff > len * 0.5f) diff -= len; else if (diff < -len * 0.5f) diff += len; }
            if (!force && Mathf.Abs(diff) < 1f) return;   // within a second: leave it (no sky jitter)
            sw.tekeres += diff;
            _worldFollowing = false;
            WorldTimeJumps++;
        }

        /// Small stamped anchors keep local frame clamps and pauses from accumulating into sky drift.
        internal static void ServerTimeTick(float dt)
        {
            if (!Entities.IsHost || napszakvaltakozas.s == null) return;
            if (ModObjects.ClockTime - _worldSentAt < 2) return;
            if (mainscript.s != null && mainscript.s.sleeping) return;   // the host's sleep: its jump goes out when it ends
            _worldSentAt = ModObjects.ClockTime;
            WriteWorldClock(false); Entities.SendToPeers(W, false, 0); Fixes.SessionClocks.WorldSent++;
        }

        internal static void SleepTick(float dt)
        {
            // E3: a finished sleep races the world clock on the sleeper's machine only — broadcast the jump
            bool sleepingNow = mainscript.s != null && mainscript.s.sleeping;
            if (_wasSleeping && !sleepingNow)
            {
                _wokeAt = Time.realtimeSinceStartup;
                _worldWakeStamp = ModObjects.ClockTime; _worldStamp = -1; _worldFollowing = false;
                _worldAwaitingWake = !Entities.IsHost;
                float wt = WorldTimeNow();
                if (wt >= 0f) { W.Reset(); W.U8(Entities.SleepSync); W.F32(wt); W.F64(ModObjects.ClockTime); W.F32(-1f); Entities.SendToServer(W, true); }
            }
            // while asleep, the clock goes out as it runs (the game turns it ~8 hours in a few seconds): the others' sky
            // followed only once the sleep was over, and the host's periodic clock pauses while it sleeps (playtest
            // 2026-10-18, "daytime sync takes longer for client")
            if (sleepingNow)
            {
                _sleepClockAcc += dt;
                if (_sleepClockAcc >= SleepClockS)
                {
                    _sleepClockAcc = 0f;
                    float wt = WorldTimeNow();
                    // the same wire form as the wake-up one above: SleepSync carries the session stamp since protocol 34
                    if (wt >= 0f && Entities.IsFinite(wt)) { W.Reset(); W.U8(Entities.SleepSync); W.F32(wt); W.F64(ModObjects.ClockTime); W.F32(Fixes.CloudClock.MySleepProgress()); Entities.SendToServer(W, true); SleepClocksSent++; }
                }
            }
            else _sleepClockAcc = SleepClockS;   // the first one at once
            _wasSleeping = sleepingNow;
        }

        internal static void ServerWorldClockTo(int playerId)
        {
            if (napszakvaltakozas.s != null)
            {
                WriteWorldClock(true); Fixes.SessionClocks.WorldSent++;
                Entities.SendToPeer(playerId, W, true);
            }
        }

        /// Server: a sleep ended — everyone's clock jumps to the sleeper's world time. While a player sleeps their live
        /// clock comes here too, with where their sleep's cloud curve stands (sleepProgress, -1 when it ends; NaN from a
        /// sender without it): the world clock carries it on (Fixes.CloudClock).
        internal static void ServerSleepSync(int from, float worldTime, double stamp, float sleepProgress = float.NaN)
        {
            if (!float.IsNaN(sleepProgress)) { _sleepProgress = sleepProgress; _sleepProgressAt = ModObjects.ClockTime; Fixes.CloudClock.SleepProgress(sleepProgress); }
            if (from != 0) ApplyWorldTime((float)ClockMath.Project(worldTime, stamp, ModObjects.ClockTime, _hostRate), true);
            _worldEpoch++;
            WriteWorldClock(true); Entities.SendToPeers(W, true, 0); Fixes.SessionClocks.WorldSent++;
        }

        /// Sample at the send frame, rather than exporting the last physics step's stale t.
        private static float WorldTimeNow()
        {
            var sw = napszakvaltakozas.s;
            return sw == null ? -1f : (float)ClockMath.Wrap(Time.timeSinceLevelLoadAsDouble + sw.tekeres - sw.startTime, sw.dt + sw.nt);
        }

        /// Hitches can clamp scaled time even at timeScale 1; measure that rate against session time.
        internal static void HostClockTick()
        {
            if (!Entities.IsHost) return;
            double now = ModObjects.ClockTime, scaled = Time.timeAsDouble;
            if (_hostRateAt < 0 || _hostScale != Time.timeScale)
            {
                _hostRateAt = now; _hostScaledAt = scaled; _hostScale = Time.timeScale; _hostRate = _hostScale;
                _worldSentAt = -100; EntityRadio.InvalidateBroadcast();
            }
            else if (now - _hostRateAt >= 2)
            {
                _hostRate = Mathf.Max(0f, (float)((scaled - _hostScaledAt) / (now - _hostRateAt)));
                _hostRateAt = now; _hostScaledAt = scaled;
            }
        }

        private static void WriteWorldClock(bool snap)
        {
            W.Reset(); W.U8(Entities.WorldClock); W.F64(ModObjects.ClockTime); W.F32(WorldTimeNow());
            bool frozen = settingsscript.s != null && settingsscript.s.S.BDevMode && kaposztaleves.s != null && !kaposztaleves.s.settings.daynight;
            W.F32(frozen ? 0f : _hostRate); W.U16(_worldEpoch); W.Bool(snap);
            // the clouds (Fixes.CloudClock): the host's running world time, days and sleeps included, and
            // a sleeping player's cloud curve (-1: nobody asleep; a sleeper gone quiet for 5 s counts as awake)
            W.F64(Fixes.CloudClock.WorldTotal);
            W.F32(ModObjects.ClockTime - _sleepProgressAt < 5 ? _sleepProgress : -1f);
        }

        internal static void ReceiveWorldClock(NetReader r)
        {
            double stamp = r.F64(); float position = r.F32(), rate = r.F32(); ushort epoch = r.U16(); bool snap = r.Bool();
            double total = r.Remaining >= 8 ? r.F64() : double.NaN; float sleepProgress = r.Remaining >= 4 ? r.F32() : float.NaN;
            if (r.Bad || Entities.IsHost || !Entities.IsFinite(position) || !Entities.IsFinite(rate) || rate < 0f || double.IsNaN(stamp) || double.IsInfinity(stamp)) return;
            if (!ClockMath.AcceptWorldSample(stamp, _worldStamp, _worldWakeStamp, snap, _worldAwaitingWake, epoch, _worldSeenEpoch)) { Fixes.SessionClocks.WorldStale++; return; }
            _worldStamp = stamp; _worldPosition = position; _worldRate = rate;
            _worldSnap |= snap || !_worldHasEpoch || epoch != _worldSeenEpoch;
            _worldSeenEpoch = epoch; _worldHasEpoch = true; _worldAwaitingWake = false;
            Fixes.SessionClocks.WorldReceived++;
            if (Entities.IsFinite(total)) Fixes.CloudClock.HostTotal(total, position, stamp, rate);
            if (!float.IsNaN(sleepProgress)) Fixes.CloudClock.SleepProgress(sleepProgress);
        }

        /// Replace only the sky's local time advance; physics and gameplay keep their own time scale.
        [HarmonyLib.HarmonyPatch(typeof(napszakvaltakozas), "FixedUpdate")]
        private static class WorldFollowsHost
        {
            [HarmonyLib.HarmonyPrefix]
            private static void Prefix(napszakvaltakozas __instance)
            {
                if (!Entities.InSession || Entities.IsHost || _worldStamp < 0 || mainscript.s == null) { _worldFollowing = false; return; }
                if (mainscript.s.sleeping || _wasSleeping) { _worldFollowing = false; return; }
                double now = ModObjects.ClockTime, length = __instance.dt + __instance.nt;
                double target = ClockMath.Wrap(ClockMath.Project(_worldPosition, _worldStamp, now, _worldRate), length);
                bool force = _worldSnap || !_worldFollowing || _worldInstance != __instance;
                double next = ClockMath.Follow(_worldFollow, now - _worldFollowAt, target, _worldRate, length, force, out bool snapped);
                double natural = Time.timeSinceLevelLoadAsDouble + __instance.tekeres - __instance.startTime;
                __instance.tekeres += (float)(next - natural);
                _worldFollow = next; _worldFollowAt = now; _worldInstance = __instance; _worldFollowing = true; _worldSnap = false;
                if (snapped) Fixes.SessionClocks.WorldSnapped++; else Fixes.SessionClocks.WorldSlewed++;
            }
        }

        internal static void ClockReset()
        {
            _hostRateAt = -1; _hostScale = -1; _hostRate = 1f;
            _worldStamp = _worldWakeStamp = -1; _worldFollowing = _worldSnap = false; _worldInstance = null;
            _worldHasEpoch = _worldAwaitingWake = false; _worldEpoch = _worldSeenEpoch = 0;
            _wasSleeping = false; _wokeAt = -100f;
            _sleepProgress = -1f; _sleepProgressAt = -100;
            _worldSentAt = -100;
            EntityRadio.ResetSession();
        }
    }
}
