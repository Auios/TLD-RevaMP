using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// A car's ignition key turned by a player who doesn't own the car (a passenger, someone reaching in through the
    /// window). The game reads the key from whoever holds it, driver or not (carscript: iignition ||= ignitionUsable.
    /// Holding(); IgnitionStuff: a short hold toggles the ignition, a long one cranks the engine). On a copy that only
    /// changed the copy, and the owner's signals set it back the next frame: the key did nothing (playtest 2026-10-18,
    /// "ignition not turned on"). The key isn't one of the car's saved usables, so no edit carried it either. Now the
    /// holding goes to the owner (on change, again every KeyResendS while held), whose game is made to see the key held
    /// for as long: the toggle or the start happens there, the game's own way, and comes back with its signals.
    internal static partial class EntitySeats
    {
        private static readonly NetWriter W = new NetWriter();
        internal static float KeyResendS = 0.5f, KeyHoldTimeoutS = 1.5f;
        internal static long KeyHoldsSent, KeyHoldsApplied, KeyHoldsRefused;

        // ---- the copy's side: the local player holds a copy's key
        private static readonly Dictionary<carscript, float> _keySeen = new Dictionary<carscript, float>();
        private static carscript _keyCar; private static uint _keyNet; private static float _keySentAt;

        [HarmonyLib.HarmonyPatch(typeof(carscript), "IgnitionStuff")]
        private static class KeyOnCopy
        {
            [HarmonyLib.HarmonyPrefix]
            private static bool Prefix(carscript __instance)
            {
                if (!Entities.InSession || !Entities.IsProxy(__instance)) return true;
                if (__instance.ignitionUsable != null && __instance.ignitionUsable.Holding()) _keySeen[__instance] = Time.time;
                return false;
            }
        }

        internal static void KeyTick()
        {
            if (_keyCar == null && _keySeen.Count == 0 && _keyHolds.Count == 0) return;
            // copy side
            carscript held = null;
            foreach (var kv in _keySeen) if (kv.Key != null && Time.time - kv.Value < 0.1f) { held = kv.Key; break; }
            if (held == null) _keySeen.Clear();
            if (held != _keyCar)
            {
                // let go: how long it was held (from the first to the last frame the game saw the hand on it)
                if (_keyCar != null) SendKey(_keyNet, false, Mathf.Max(0f, _keyLastAt - _keyFirstAt));
                _keyCar = held; _keyNet = 0;
                uint net = 0;
                if (held != null) { var e = Entities.ProxySeatCar(held, true); if (e != null) net = e.NetId; }
                if (net != 0) { _keyNet = net; _keyFirstAt = Time.time; SendKey(net, true, 0f); }
                else _keyCar = null;
            }
            else if (held != null && Time.realtimeSinceStartup - _keySentAt >= KeyResendS) SendKey(_keyNet, true, 0f);
            if (held != null) _keyLastAt = Time.time;
            // owner side: the remote hand on the key, as the game's own hold
            if (_keyHolds.Count == 0) return;
            _keyDone.Clear();
            float now = Time.realtimeSinceStartup;
            foreach (var kv in _keyHolds)
            {
                var c = kv.Key; var h = kv.Value;
                if (c == null || c.ignitionUsable == null || Entities.IsProxy(c)) { _keyDone.Add(c); continue; }
                // still held past the game's toggle time: a start (cranking while held), not a switch
                if (!h.Active && now - h.PressAt > c.startingTime + KeyTapMarginS) { h.Active = true; h.Until = Mathf.Max(h.Until, now); }
                if (!h.Active) { if (now > h.PressAt + KeyHoldTimeoutS) _keyDone.Add(c); continue; }
                if (h.Frames >= 2 && now > h.Until) { _keyDone.Add(c); continue; }
                c.ignitionUsable.lastHoldTime = Time.time;
                h.Frames++;
            }
            foreach (var c in _keyDone) _keyHolds.Remove(c);
        }
        private static float _keyFirstAt, _keyLastAt;
        /// How long past the game's toggle time (carscript.startingTime) a press may take to be let go and still count as
        /// a switch: the release has a network trip of its own.
        internal static float KeyTapMarginS = 0.15f;

        private static void SendKey(uint net, bool held, float heldS)
        {
            if (net == 0) return;
            W.Reset(); W.U8(Entities.IgnitionKey); W.U32(net); W.U8((byte)(held ? 1 : 0)); W.U16((ushort)Mathf.Clamp(Mathf.RoundToInt(heldS * 1000f), 0, 65535));
            Entities.SendToServer(W, true);
            _keySentAt = Time.realtimeSinceStartup;
            KeyHoldsSent++;
        }

        /// Server: from a player at the car (seated in it, or within reach) to its owner.
        internal static void ServerKey(int from, NetReader r)
        {
            uint net = r.U32(); byte held = r.U8(); ushort ms = r.U16();
            if (r.Bad || !Entities.TryKeyOwner(net, from, out int owner)) { KeyHoldsRefused++; return; }
            if (from != 0 && !Entities.TakeKeyToken(from)) { KeyHoldsRefused++; return; }
            if (from != 0 && !Entities.KeyReachAllowed(from, net)) { KeyHoldsRefused++; Entities.RefuseKeyReach(from, net); return; }
            W.Reset(); W.U8(Entities.IgnitionKey); W.U32(net); W.U8(held); W.U16(ms);
            Entities.SendToPeer(owner, W, true);
        }

        // ---- the owner's side
        /// A press waits (not Active) until it is let go — then it is played back for as long as it was held, a tap
        /// stays a tap whatever the release's trip took — or until it has been held past the toggle time: then the key
        /// is held here for as long as it is held there (the engine cranks).
        private sealed class KeyHold { public float PressAt, Until; public bool Active; public int Frames; }
        private static readonly Dictionary<carscript, KeyHold> _keyHolds = new Dictionary<carscript, KeyHold>();
        private static readonly List<carscript> _keyDone = new List<carscript>();

        internal static void ApplyKey(NetReader r)
        {
            uint net = r.U32(); bool held = r.U8() != 0; float heldS = r.U16() / 1000f;
            if (r.Bad || !Entities.TryGetEntity(net, out var e) || e.Proxy || !Entities.ResolveEntity(e) || e.Root == null || e.Root.car == null) return;
            var c = e.Root.car;
            float now = Time.realtimeSinceStartup;
            if (!_keyHolds.TryGetValue(c, out var h)) { if (!held) return; _keyHolds[c] = h = new KeyHold { PressAt = now }; }
            if (held) h.Until = now + KeyHoldTimeoutS;   // pressed, or still held (every KeyResendS)
            else if (!h.Active)   // let go before we began: played back as long as it was held — a tap stays a tap
            { h.Active = true; h.Frames = 0; h.Until = now + (heldS < c.startingTime ? Mathf.Min(heldS, c.startingTime * 0.75f) : heldS); }
            else h.Until = Mathf.Min(h.Until, now);   // let go after a start: no longer than now
            KeyHoldsApplied++;
        }
    }
}
