using System;
using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal sealed class LegacyBot : MpClient.IClientController
    {
        internal MpClient Client;
        private readonly BotMotion _motion;
        private readonly NetWriter _w = new NetWriter();
        public LegacyBot(BotMotion motion) { _motion = motion; }
        public string Name => "bot";
        public bool Silent => false;
        public Vector3d Position { get; private set; }
        public float Yaw { get; private set; }
        public uint SeatNet => _carNet != 0 && LegacyBots.BotDriverSeat >= 0 ? _carNet : 0;
        public int SeatIndex => _carNet != 0 && LegacyBots.BotDriverSeat >= 0 ? LegacyBots.BotDriverSeat : 0;
        public bool Wants(byte type) => type == Entities.DetachReq || type == Entities.Assigned;
        public void Tick(int id) { Position = _motion.Step(Time.unscaledDeltaTime, out float yaw); Yaw = yaw; }
        public void SentState()
        {
            BotCarTick(Position); BotItemTick();
            if (LegacyBots.BotPoses) { _w.Reset(); _w.U8(Protocol.Pose); if (PlayerLook.WriteLocalPose(_w)) Client.T.Send(Client.T.Connections[0], _w, SteamTransport.SendUnreliable); }
        }
        public void Receive(byte type, NetReader r)
        {
            switch (type)
            {
                case Entities.DetachReq:
                {
                    // test: a bot owns its car — a removal request is answered as the owner's game does (the part off, PartOff)
                    uint net = r.U32(); int idx = (int)r.VarU32();
                    if (r.Bad || net != _carNet || _carNet == 0) return;
                    _w.Reset(); _w.U8(Entities.PartOff); _w.U32(_carNet); _w.U32(_carEpoch); _w.VarU32((uint)idx);
                    var gp = _carPrev; _w.F64(gp.x); _w.F64(gp.y + 1.0); _w.F64(gp.z); _w.F32(0); _w.F32(0); _w.F32(0); _w.F32(1);
                    Client.T.Send(Client.T.Connections[0], _w, SteamTransport.SendReliable);
                    break;
                }
                case Entities.Assigned:
                {
                    uint local = r.U32(), net = r.U32(), ep = r.U32();
                    if (local == 0xB1700000u + (uint)Client.MyId) { _itemNet = net; _itemEpoch = ep; }   // our item's network id
                    else { _carNet = net; _carEpoch = ep; }                                         // our car's
                    break;
                }
            }
        }
        // load-test bots that drive (LegacyBots.BotCars): the shared car and its state stream
        public byte[] ItemRecord; private bool _itemShared; private uint _itemNet, _itemEpoch; private ushort _itemSeq;
        private readonly List<KeyValuePair<double, Vector3d>> _itemHistP = new List<KeyValuePair<double, Vector3d>>();
        private readonly List<Quaternion> _itemHistQ = new List<Quaternion>();
        public byte[] CarRecord; private bool _carShared; private uint _carNet, _carEpoch; private ushort _carSeq; private Vector3d _carPrev; private double _carPrevT; private bool _carHasPrev;
        private Vector3d _gA, _gB; private bool _gSeg;   // ground under the bot's path: a sample behind, one 3 m ahead
        /// A driving bot: share the car once, then its state with every player tick (the same message a real owner sends).
        private Vector3d OnGround(Vector3d g, Vector3d travel)
        {
            var dir = new Vector3d(travel.x, 0, travel.z);
            double len = Math.Sqrt(dir.x * dir.x + dir.z * dir.z);
            if (len < 1e-4) return _gSeg ? new Vector3d(g.x, _gA.y, g.z) : g;
            dir = new Vector3d(dir.x / len, 0, dir.z / len);
            double t = 0;
            if (_gSeg)
            {
                double ax = _gB.x - _gA.x, az = _gB.z - _gA.z;
                t = ((g.x - _gA.x) * ax + (g.z - _gA.z) * az) / Math.Max(1e-6, ax * ax + az * az);
            }
            if (!_gSeg || t >= 1.0 || t < -0.5)
            {
                if (!_gSeg || t < -0.5 || t > 2.0) { if (!GroundAt(g, out _gA)) return g; }   // start / lost the segment
                else _gA = _gB;
                if (!GroundAt(new Vector3d(g.x + dir.x * 3.0, 0, g.z + dir.z * 3.0), out _gB)) { _gB = _gA; _gB.x += dir.x * 3.0; _gB.z += dir.z * 3.0; }
                _gSeg = true;
                double ax = _gB.x - _gA.x, az = _gB.z - _gA.z;
                t = ((g.x - _gA.x) * ax + (g.z - _gA.z) * az) / Math.Max(1e-6, ax * ax + az * az);
            }
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return new Vector3d(g.x, _gA.y + (_gB.y - _gA.y) * t, g.z);
        }

        private static bool GroundAt(Vector3d g, out Vector3d onGround)
        {
            var u = mainscript.UnityPosFromGlobal(g);
            var hit = GroundPlacement.GroundHit(u.x, u.z);
            onGround = hit != null ? new Vector3d(g.x, mainscript.GlobalFromUnityPos(hit.Value.point).y, g.z) : g;
            return hit != null;
        }

        /// The item bot: share the item once, then states keeping it at LegacyBots.BotItemOffset in LegacyBots.BotItemCar as the car was
        /// LegacyBots.BotItemLag seconds ago.
        private void BotItemTick()
        {
            if (ItemRecord == null) return;
            if (!_itemShared)
            {
                _itemShared = true;
                _w.Reset(); _w.U8(Entities.Share); _w.U32(0xB1700000u + (uint)Client.MyId); _w.VarU32((uint)ItemRecord.Length); _w.Bytes(ItemRecord, 0, ItemRecord.Length);
                Client.T.Send(Client.T.Connections[0], _w, SteamTransport.SendReliable);
                return;
            }
            var car = LegacyBots.BotItemCar;
            if (_itemNet == 0 || car == null) return;
            double now = Time.unscaledTimeAsDouble;
            _itemHistP.Add(new KeyValuePair<double, Vector3d>(now, mainscript.GlobalFromUnityPos(car.transform.TransformPoint(LegacyBots.BotItemOffset))));
            _itemHistQ.Add(car.transform.rotation);
            int k = 0;
            while (k < _itemHistP.Count - 1 && _itemHistP[k + 1].Key <= now - LegacyBots.BotItemLag) k++;
            if (k > 0) { _itemHistP.RemoveRange(0, k); _itemHistQ.RemoveRange(0, k); }
            var g = _itemHistP[0].Value; var q = _itemHistQ[0];
            _w.Reset();
            EntityCodec.WriteStateHead(_w, _itemNet, _itemEpoch, ++_itemSeq, now - LegacyBots.BotItemLag, g, q, Vector3.zero, false, false, LegacyBots.BotItemHeld);
            _w.U8(0);   // no wheels
            _w.U8(0);   // engine: none
            _w.U8(0);   // no car signals
            _w.U8(0);   // no limbs
            Client.T.Send(Client.T.Connections[0], _w, SteamTransport.SendUnreliable);
        }

        internal bool SendItemAttach(uint carNet, int parentIdx, int index, Vector3d lpos, Vector3d gpos)
        {
            if (ItemRecord == null || _itemNet == 0) return false;
            _w.Reset(); _w.U8(Entities.AttachSync); _w.U32(_itemNet); _w.U8(1); _w.U8(2); _w.VarU32((uint)(index + 1));
            _w.U32(carNet); _w.VarU32((uint)parentIdx); _w.VarU32(0);
            _w.F64(0); _w.F64(0); _w.F64(0);
            _w.F64(lpos.x); _w.F64(lpos.y); _w.F64(lpos.z);
            _w.F64(gpos.x); _w.F64(gpos.y); _w.F64(gpos.z);
            _w.U8(0);   // no pose: the item stays where the bot left it
            Client.T.Send(Client.T.Connections[0], _w, SteamTransport.SendReliable);
            ItemRecord = null;   // bolted on: no more states from the bot (the car's owner simulates it now)
            return true;
        }

        private void BotCarTick(Vector3d g)
        {
            if (CarRecord == null) return;
            if (!_carShared)
            {
                _carShared = true;
                _w.Reset(); _w.U8(Entities.Share); _w.U32(0xB0700000u + (uint)Client.MyId); _w.VarU32((uint)CarRecord.Length); _w.Bytes(CarRecord, 0, CarRecord.Length);
                Client.T.Send(Client.T.Connections[0], _w, SteamTransport.SendReliable);
                return;
            }
            if (_carNet == 0) return;
            // on the ground, as a player's car is: a circle at the host player's height ran through hills — near the
            // host's own cars such a copy turns physical (Entities.Physical) and was fought out of the terrain until a
            // 16.9 s frame hung the host (loadtest --radius 30, v0.57.78). Height between two ground samples on the path
            // (behind / 3 m ahead): continuous, one ray per 3 m of travel.
            if (_carHasPrev) g = OnGround(g, g - _carPrev);
            else if (GroundAt(g, out var g0)) g = g0;
            // the real time since the last state: sends fall on frame boundaries (40/60 ms apart at a 20 ms host frame),
            // a fixed 1/20 s gave the copies velocities up to ±20 % off their positions
            double tNow = Time.unscaledTimeAsDouble;
            float dt = _carHasPrev ? Mathf.Max(1e-3f, (float)(tNow - _carPrevT)) : 1f / Protocol.StateHz;
            Vector3 v = _carHasPrev ? (Vector3)(g - _carPrev) / dt : Vector3.zero;
            _carPrev = g; _carPrevT = tNow; _carHasPrev = true;
            var rot = v.sqrMagnitude > 0.01f ? Quaternion.LookRotation(v) : Quaternion.identity;   // pitched with the slope
            _w.Reset();
            EntityCodec.WriteStateHead(_w, _carNet, _carEpoch, ++_carSeq, tNow /* the frame start: the bot's position is integrated to it */, new Vector3d(g.x, g.y + 0.6, g.z), rot, v, true);
            _w.U8((byte)(4 | (LegacyBots.BotTorques ? 0x80 : 0)));                   // 4 wheels (+ torques), the owner's layout
            short rpm = (short)Mathf.Clamp(v.magnitude / 0.33f * 60f / (2f * Mathf.PI), 0f, 32767f);
            for (int i = 0; i < 4; i++) { _w.U8(0); _w.U8(0); _w.U16((ushort)rpm); }
            if (LegacyBots.BotTorques) for (int i = 0; i < 4; i++) { _w.U8(0); _w.U8(8); }
            _w.U8(1 | 2); _w.U16(2200);                                      // engine running at 2200 rpm
            _w.U8(0);                                                        // no car signals (EntityCarSignals.CarSignals)
            _w.U8(0);                                                        // no limbs (Entities.Ragdoll)
            Client.T.Send(Client.T.Connections[0], _w, SteamTransport.SendUnreliable);
        }

    }
}
