using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal sealed class ClientSimulation
    {
        internal static ulong TestClaimSteamId = default;
        private readonly MpClient Client;
        private static readonly ConditionalWeakTable<MpClient, ClientSimulation> All = new ConditionalWeakTable<MpClient, ClientSimulation>();
        private ClientSimulation(MpClient client) { Client = client; }
        internal static bool Message(MpClient client, NetReader reader) => All.GetValue(client, c => new ClientSimulation(c)).OnTransport(reader);
        internal static void Tick(MpClient client)
        {
            if (!All.TryGetValue(client, out var simulation)) return;
            if (simulation._stalled.Count > 0 && Time.realtimeSinceStartup >= StallUntil) simulation.ReplayStalled();
            simulation.ReleaseLagged();
        }
        public static float StallUntil;
        public static long StalledMessages;
        private readonly Queue<byte[]> _stalled = new Queue<byte[]>();
        private readonly NetReader _replay = new NetReader();
        /// Test (`mp lag <ms>`): a lasting extra delay on every message from the host (a new, longer route), in order;
        /// 0 ends it (what is queued still comes when due).
        public static float LagS;
        public static long LaggedMessages;
        private readonly Queue<KeyValuePair<float, byte[]>> _lagged = new Queue<KeyValuePair<float, byte[]>>();
        internal bool OnTransport(NetReader r)
        {
            if (Time.realtimeSinceStartup < StallUntil)
            {
                var b = new byte[r.Remaining]; Array.Copy(r.Buf, r.Pos, b, 0, b.Length);
                _stalled.Enqueue(b); StalledMessages++;
                return true;
            }
            ReplayStalled();
            if (LagS > 0f || _lagged.Count > 0)
            {
                var b = new byte[r.Remaining]; Array.Copy(r.Buf, r.Pos, b, 0, b.Length);
                _lagged.Enqueue(new KeyValuePair<float, byte[]>(Time.realtimeSinceStartup + LagS, b)); LaggedMessages++;
                return true;
            }
            return false;
        }

        private void ReleaseLagged()
        {
            float now = Time.realtimeSinceStartup;
            while (_lagged.Count > 0 && _lagged.Peek().Key <= now)
            {
                var b = _lagged.Dequeue().Value;
                _replay.Load(b.Length); Array.Copy(b, 0, _replay.Buf, 0, b.Length);
                try { Client.DeliverObservedMessage(_replay); } catch (Exception e) { Plugin.Log.LogError("net message handler (lagged): " + e); }
            }
        }

        private void ReplayStalled()
        {
            while (_stalled.Count > 0)
            {
                var b = _stalled.Dequeue();
                _replay.Load(b.Length); Array.Copy(b, 0, _replay.Buf, 0, b.Length);
                try { Client.DeliverObservedMessage(_replay); } catch (Exception e) { Plugin.Log.LogError("net message handler (replayed): " + e); }
            }
        }

    }
}
