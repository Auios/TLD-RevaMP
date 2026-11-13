using System.Linq;
using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using static TLDRevamp.Net.Voice;

namespace TLDRevamp.Net
{
    internal static class VoiceTools
    {

        public static string Tone(float seconds, int feed = -1, bool everyone = false) { ObservedToneUntil = Time.unscaledTime + seconds; ObservedToneNext = 0f; ObservedFeed = feed; ObservedToneEveryone = everyone; return Status(); }

        /// Bridge `mp voice probe`: does Steam's capture work on this machine? (records 1 s, reports what came back)
        public static string Probe()
        {
            if (!SteamTransport.SteamReady) return "{\"error\":\"steam not ready\"}";
            uint rate = SteamUser.GetVoiceOptimalSampleRate();
            SteamUser.StartVoiceRecording();
            var first = SteamUser.GetAvailableVoice(out uint a0);
            ObservedProbeUntil = Time.unscaledTime + 1f;
            return "{\"optimalRate\":" + rate + ",\"first\":" + Json.Str(first.ToString()) + ",\"avail\":" + a0 + "}";
        }
        internal static void StopTone() => _toneUntil = 0f;
        internal static void ProbeStatus(bool recording) { if (_probeUntil > 0f && Time.unscaledTime > _probeUntil && !recording) { try { SteamUser.StopVoiceRecording(); } catch { } _probeUntil = 0f; } }

        // ---- test: a 440 Hz tone sent as voice (PCM16, no Steam codec), and a capture probe
        internal static float _toneUntil, _toneNext;         internal static float ObservedToneNext { get => _toneNext; set => _toneNext = value; }
        internal static float ObservedToneUntil { get => _toneUntil; set => _toneUntil = value; }
private static double _tonePhase;
        internal static readonly byte[] _tone = new byte[Rate / 25 * 2];   // 40 ms
        internal static bool _toneEveryone;   // the bridge's `mp voice tone s all`: the tone as if the everyone key were held
        internal static bool ObservedToneEveryone { get => _toneEveryone; set => _toneEveryone = value; }
        internal static int _feed = -1;   // the bridge's `mp voice feed id s`: the tone straight into this machine's speaker for player id (no network)
        internal static int ObservedFeed { get => _feed; set => _feed = value; }

        internal static void ToneTick(float now)
        {
            if (now >= _toneUntil) return;
            if (_toneNext <= 0f || now - _toneNext > 0.2f) _toneNext = now;   // start (or after a stall): from now
            for (int k = 0; k < 4 && now >= _toneNext; k++) { _toneNext += 0.04f; ToneBlock(); }   // real time: 25 blocks/s at any frame rate
        }

        internal static void ToneBlock()
        {
            int n = _tone.Length / 2;
            for (int i = 0; i < n; i++)
            {
                short v = (short)(Math.Sin(_tonePhase) * 12000);
                _tonePhase += 2 * Math.PI * 440 / Rate;
                _tone[2 * i] = (byte)v; _tone[2 * i + 1] = (byte)(v >> 8);
            }
            EmitFrame(CodecPcm16, _tone, _feed, _toneEveryone);
        }
        internal static float _probeUntil;
        internal static float ObservedProbeUntil { get => _probeUntil; set => _probeUntil = value; }
    }
}
