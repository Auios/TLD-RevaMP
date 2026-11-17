using System;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Mp;

namespace TLDRevamp.Net
{
    internal static class LegacyBots
    {
        internal static readonly List<MpClient> Bots = new List<MpClient>();
        private static MpClient Create(SteamTransport transport, BotMotion motion, byte[] item = null, byte[] car = null, ulong host = 0)
        {
            var bot = new LegacyBot(motion) { ItemRecord = item, CarRecord = car };
            var client = new MpClient(transport, bot) { HostSteamId = host };
            bot.Client = client; client.ControllerStateSent = bot.SentState;
            return client;
        }
        internal static void Tick() { for (int i = 0; i < Bots.Count; i++) Bots[i].Tick(); }
        internal static void Reset() { foreach (var bot in Bots) bot.Dispose(); Bots.Clear(); }
        internal static string BotStatus()
        {
            if (Bots.Count > 0)
            {
                int ready = 0; long inB = 0, outB = 0; int pingSum = 0, pingN = 0, pingMax = 0;
                foreach (var b in Bots)
                {
                    if (b.MyId > 0) ready++;
                    inB += b.T.BytesIn; outB += b.T.BytesOut;
                    if (b.T.Connections.Count > 0) { int pg = b.T.Status(b.T.Connections[0]).Ping; if (pg >= 0) { pingSum += pg; pingN++; if (pg > pingMax) pingMax = pg; } }
                }
                return "\"bots\":{\"count\":" + Bots.Count + ",\"ready\":" + ready + ",\"bytesIn\":" + inB + ",\"bytesOut\":" + outB +
                          ",\"pingAvg\":" + (pingN > 0 ? pingSum / pingN : -1) + ",\"pingMax\":" + pingMax + ",\"lag\":" + Json.Str(DevHooks.LagInfo?.Invoke() ?? "off") + "}";
            }
            return null;
        }
        /// radius < 0: spread the bots over distance tiers (|radius|·{0.01, 0.1, 1}: e.g. −200000 → 2 km, 20 km, 200 km).
        /// Load test: bots that drive — each shares a copy of BotCarRecord (bridge `mp botcar`: the car nearest the player)
        /// and sends that car's state at the players' rate, wheels and engine included (torques too when BotTorques: as
        /// a car within 40 m of another player's). To the host and every client they cost what a driving player costs.
        public static byte[] BotCarRecord;
        public static bool BotCars = default, BotTorques = true;
        /// Test: bots send the host player's own pose (limbs, hum, pee) as theirs — what the host does shows on every bot's model.
        public static bool BotPoses = default;
        /// Test (Entities.Cargo): a bot that owns a loose item and keeps it at Offset in the car nearest the host's
        /// player, Lag seconds late (a remote owner's view of it), Held = in its hands.
        public static byte[] BotItemRecord; public static tosaveitemscript BotItemCar; public static Vector3 BotItemOffset;
        public static float BotItemLag = 0.2f; public static bool BotItemHeld;
        /// Test: every item bot bolts its item onto `slot` of `carNet` (the AttachSync a remote player's attach key sends).
        public static string BotAttach(uint carNet, string slot)
        {
            if (DevHooks.SlotRef == null || !DevHooks.SlotRef(carNet, slot, out int parentIdx, out int index, out var lpos, out var gpos)) return "{\"error\":\"no such slot\"}";
            int n = 0;
            foreach (var b in Bots) if (b.Player is LegacyBot && ((LegacyBot)b.Player).SendItemAttach(carNet, parentIdx, index, lpos, gpos)) n++;
            return "{\"sent\":" + n + ",\"parentIdx\":" + parentIdx + ",\"index\":" + index + "}";
        }

        public static string AddItemBot(string[] a)
        {
            if (Server == null) return "{\"error\":\"host first\"}";
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"no player\"}";
            tosaveitemscript item = null; float best = 15f;
            if (a.Length > 5) item = Entities.RootOfNet(uint.Parse(a[5], ic));   // a given shared item
            if (item == null) foreach (var it in savedatascript.s.items.Values)
            {
                if (it == null || it.car != null || it.transform.parent != null || it.P == null || (it.attachable != null && it.attachable.attached)) continue;
                float d = (it.transform.position - pl.transform.position).magnitude;
                if (d < best) { best = d; item = it; }
            }
            if (item == null) return "{\"error\":\"no loose item within 15 m\"}";
            BotItemCar = EntityLookup.NearestCar();
            if (BotItemCar == null) return "{\"error\":\"no car near\"}";
            BotItemRecord = Entities.CaptureRecord(item);
            BotItemOffset = new Vector3(float.Parse(a[0], ic), float.Parse(a[1], ic), float.Parse(a[2], ic));
            if (a.Length > 3) BotItemLag = float.Parse(a[3], ic);
            if (a.Length > 4) BotItemHeld = a[4] == "1" || a[4] == "True" || a[4] == "true";
            var home = LocalGlobal();
            Bots.Add(Create(SteamTransport.ConnectIP("127.0.0.1:" + Server.Port), new BotMotion(home, 4f, 0f, 0f), item: BotItemRecord));
            return "{\"item\":" + Json.Str(item.name) + ",\"car\":" + Json.Str(BotItemCar.name) + ",\"bytes\":" + BotItemRecord.Length + "}";
        }
        public static int BotDriverSeat;
        public static string CaptureBotCar()
        {
            var root = EntityLookup.NearestCar();
            if (root == null) return "{\"error\":\"no car near\"}";
            BotCarRecord = Entities.CaptureRecord(root);
            var seats = root.GetComponentsInChildren<seatscript>(true);
            BotDriverSeat = System.Array.FindIndex(seats, x => x.driverSeat0);
            return "{\"car\":" + Json.Str(root.name) + ",\"bytes\":" + BotCarRecord.Length + ",\"driverSeat\":" + BotDriverSeat + "}";
        }

        public static string AddBots(int n, float radius, float speed)
        {
            if (Server == null) return "{\"error\":\"host first\"}";
            if (BotCars && BotCarRecord == null) return "{\"error\":\"mp botcar first\"}";
            var home = LocalGlobal();
            for (int i = 0; i < n; i++)
            {
                float a = (Bots.Count + i) * Mathf.PI * 2f / Mathf.Max(1, Bots.Count + n);
                float r = radius >= 0 ? radius : -radius * (i % 3 == 0 ? 0.01f : i % 3 == 1 ? 0.1f : 1f);
                Bots.Add(Create(SteamTransport.ConnectIP("127.0.0.1:" + Server.Port), new BotMotion(home, r, a, speed / Mathf.Max(1f, r)), car: BotCars ? BotCarRecord : null));
            }
            return Status();
        }

        /// Bots on ANOTHER machine (§9a: the host's own loopback bots cost it nothing on the network): this game connects
        /// n bot players to a host over Steam (relay included), each circling its own spot. spreadKm > 0: the spots are
        /// spread around this player up to that far (players far apart). Runs in a joiner's game or one in no session.
        public static string AddRemoteBots(int n, ulong hostSteamId, float radius, float speed, float spreadKm)
        {
            if (Server != null) return "{\"error\":\"not on the host: mp bots\"}";
            var home = LocalGlobal();
            var rnd = new System.Random(Bots.Count * 7919 + n);
            for (int i = 0; i < n; i++)
            {
                var c = home;
                if (spreadKm > 0f)
                {
                    double ang = rnd.NextDouble() * Math.PI * 2, d = Math.Sqrt(rnd.NextDouble()) * spreadKm * 1000.0;
                    c = new Vector3d(home.x + Math.Cos(ang) * d, home.y, home.z + Math.Sin(ang) * d);
                }
                float a = (Bots.Count + i) * Mathf.PI * 2f / Mathf.Max(1, Bots.Count + n);
                Bots.Add(Create(SteamTransport.ConnectP2P(hostSteamId, P2PPort), new BotMotion(c, radius, a, speed / Mathf.Max(1f, radius)), host: hostSteamId));
            }
            return Status();
        }

        /// Bots leave (joins and leaves during a long session): the last n.
        public static string DropBots(int n)
        {
            for (int i = 0; i < n && Bots.Count > 0; i++) { var b = Bots[Bots.Count - 1]; Bots.RemoveAt(Bots.Count - 1); b.Dispose(); }
            return Status();
        }

    }
    /// A bot's movement: a circle around a centre (global coordinates), like MpLab's fake players.
    public sealed class BotMotion
    {
        private readonly Vector3d _center;
        private readonly float _radius, _omega;
        private float _angle;
        public BotMotion(Vector3d center, float radius, float angle, float omega) { _center = center; _radius = radius; _angle = angle; _omega = omega; }
        public Vector3d Step(float dt, out float yaw)
        {
            _angle += _omega * dt;
            yaw = -_angle * Mathf.Rad2Deg;
            return new Vector3d(_center.x + Math.Cos(_angle) * _radius, _center.y, _center.z + Math.Sin(_angle) * _radius);
        }
    }

}
