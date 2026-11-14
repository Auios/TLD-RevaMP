using System;
using System.Linq;
using System.Reflection;
using TLDRevamp.Net;
using UnityEngine;

static partial class EntitySplitTest
{
    // Fixed bytes captured from e4778b3 before extracting the production codecs.
    static readonly string[] RotationBytes = { "03000000000000", "00CCDB684898B7", "01CCDB98B76848", "02CCDB684898B7", "037EA5825A7EA5" };
    const string DetachBytes = "2A78563412EFCDAB89000000000000F43F00000000000004C00000000000C27240CDCCCC3DCDCC4CBE9A99993ECDCCCCBE";
    static readonly string[] StateBytes = {
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F29",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F3981010000A03F000020C00000704001CCDB98B76848",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F7981010000A03F000020C00000704001CCDB98B76848800103",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F6981028001030000A03F000020C00000704001CCDB98B76848",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F6900",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7FAD7C0030F8D00702CCDB684898B7",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F6D00",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F2F",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7FAD7C0030F8D00702CCDB684898B7",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F3981010000C0FF000000400000404001CCDB98B76848",
        "17AC02808001CDAB40E20100D2040000D6F6FFFFFFFFFF7F00CCDB684898B77C0016FFFF7F6981028001030000C0FF000000400000404001CCDB98B76848",
    };

    static void CodecVectors()
    {
        var w = new NetWriter();
        var rotations = new[] { Quaternion.identity, new Quaternion(-.8f, .2f, -.4f, .4f), new Quaternion(.2f, -.8f, .4f, -.4f), new Quaternion(.2f, -.4f, -.8f, .4f), new Quaternion(.5f, -.5f, .5f, -.5f) };
        for (int i = 0; i < rotations.Length; i++)
        {
            foreach (var owner in new[] { typeof(Entities), typeof(EntityCodec) })
            {
                w.Reset(); owner.GetMethod("WriteRot", Static).Invoke(null, new object[] { w, rotations[i] });
                Check(Convert.ToHexString(w.Buf.Take(w.Len).ToArray()) == RotationBytes[i], owner.Name + " rotation fixed bytes " + i);
                var r = RawReader(RotationBytes[i]);
                var q = (Quaternion)owner.GetMethod("ReadRot", Static).Invoke(null, new object[] { r });
                Check(!r.Bad && r.Pos == r.End && Math.Abs(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w - 1) < .00001, "rotation read normalized and complete " + i);
                w.Reset(); EntityCodec.WriteRot(w, q);
                Check(Convert.ToHexString(w.Buf.Take(w.Len).ToArray()) == (i == 4 ? "007EA5825A7FA5" : RotationBytes[i]), "rotation byte round trip " + i);
            }
        }
        w.Reset(); Entities.WriteDetachSync(w, 0x12345678, 0x89abcdef, new Vector3d(1.25, -2.5, 300.125), new Quaternion(.1f, -.2f, .3f, -.4f));
        Check(Convert.ToHexString(w.Buf.Take(w.Len).ToArray()) == DetachBytes, "detach facade fixed bytes");
        var detach = Reader(Convert.FromHexString(DetachBytes));
        Check(Entities.ReadDetachSync(detach, out var dn, out var dp, out var dq) && dn == 0x12345678 && dp.x == 1.25 && dp.y == -2.5 && dp.z == 300.125 && dq.x == .1f && dq.y == -.2f && dq.z == .3f && dq.w == -.4f && detach.Pos == detach.End, "detach facade reads fixed vector");
        w.Reset(); EntityCodec.WriteDetachSync(w, dn, 0x89abcdef, dp, dq);
        Check(Convert.ToHexString(w.Buf.Take(w.Len).ToArray()) == DetachBytes, "detach byte round trip through owner");
        detach = Reader(Convert.FromHexString(DetachBytes));
        Check(EntityCodec.ReadDetachSync(detach, out dn, out dp, out dq) && detach.Pos == detach.End, "detach owner reads fixed vector");
        for (int n = 1; n < Convert.FromHexString(DetachBytes).Length; n++)
            Check(!EntityCodec.ReadDetachSync(Reader(Convert.FromHexString(DetachBytes).Take(n).ToArray()), out _, out _, out _), "truncated detach rejected " + n);
        for (int mode = 0; mode < 11; mode++)
        {
            w.Reset();
            Entities.WriteStateHead(w, 300, 16384, 0xabcd, 123.4565, new Vector3d(1.2345, -2.3455, 3000000), rotations[1], new Vector3(1.235f, -2.345f, 400), true,
                stored: mode == 7, held: mode >= 5 && mode <= 8, spraying: true,
                carriedBy: mode == 1 || mode == 2 || mode == 9 ? 129u : 0,
                localPos: mode == 9 || mode == 10 ? new Vector3(float.NaN, 2, 3) : new Vector3(1.25f, -2.5f, 3.75f), localRot: rotations[2],
                final: true, locked: mode == 2 || mode == 3 || mode == 4 || mode == 6 || mode == 10,
                lockItem: 128, lockMount: 3, lockContainer: mode == 3 || mode == 10 ? 257u : 0,
                handPose: mode >= 5 && mode <= 8, handPos: new Vector3(.1235f, -3, 1.9995f), handRot: rotations[3]);
            Check(Convert.ToHexString(w.Buf.Take(w.Len).ToArray()) == StateBytes[mode], "state facade fixed bytes " + mode);
            var r = Reader(Convert.FromHexString(StateBytes[mode]));
            Entities.ReadStateHead(r, out uint net, out uint epoch, out double stamp, out var g, out var q, out var v, out bool driven, out bool stored, out bool held, out bool spraying);
            Check(!r.Bad && r.Pos == r.End && net == 300 && epoch == 16384 && stamp == 123.456 && g.x == 1.234 && g.y == -2.346 && g.z == 2147483.647 && v.x == 1.24f && v.y == -2.34f && v.z == 327.67f && driven && spraying && stored == (mode == 7) && held == (mode >= 5 && mode <= 8), "state read fixed values " + mode);
            Check(EntityCodec.ReadFinalState && EntityCodec.ReadLockedState == (mode == 2 || mode == 3 || mode == 4 || mode == 6 || mode == 10) && EntityCodec.ReadHandPose == (mode == 5 || mode == 8), "state flags reset at read " + mode);
            Check(EntityCodec.ReadCarriedBy == (mode == 1 || mode == 2 ? 129u : 0) && EntityCodec.ReadLockContainer == (mode == 3 ? 257u : 0) && EntityCodec.ReadLockItem == (mode == 2 || mode == 3 || mode == 10 ? 128 : -1) && EntityCodec.ReadLockMount == (mode == 2 || mode == 3 || mode == 10 ? 3 : -1), "state frame and lock scratch " + mode);
            Check(EntityCodec.ReadHandPose ? EntityCodec.ReadHandPos.x == .124f && EntityCodec.ReadHandPos.y == -2 && EntityCodec.ReadHandPos.z == 2 : EntityCodec.ReadHandPos.sqrMagnitude == 0 && EntityCodec.ReadHandRot.w == 1, "hand scratch payload or reset " + mode);
            if (mode < 9)
            {
                w.Reset(); EntityCodec.WriteStateHead(w, net, epoch, 0xabcd, stamp, g, q, v, driven, stored, held, spraying,
                    EntityCodec.ReadCarriedBy, EntityCodec.ReadLocalPos, EntityCodec.ReadLocalRot, EntityCodec.ReadFinalState,
                    EntityCodec.ReadLockedState, EntityCodec.ReadLockItem, EntityCodec.ReadLockMount, EntityCodec.ReadLockContainer,
                    EntityCodec.ReadHandPose, EntityCodec.ReadHandPos, EntityCodec.ReadHandRot);
                Check(Convert.ToHexString(w.Buf.Take(w.Len).ToArray()) == StateBytes[mode], "state owner byte round trip " + mode);
            }
            r = Reader(Convert.FromHexString(StateBytes[mode]));
            EntityCodec.ReadStateHead(r, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
            Check(!r.Bad && r.Pos == r.End, "state owner reads fixed vector " + mode);
            for (int n = 1; n < Convert.FromHexString(StateBytes[mode]).Length; n++)
            {
                r = Reader(Convert.FromHexString(StateBytes[mode]).Take(n).ToArray());
                EntityCodec.ReadStateHead(r, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
                Check(r.Bad, "truncated state detected " + mode + "/" + n);
            }
        }
        var empty = Reader(Convert.FromHexString(StateBytes[0]));
        EntityCodec.ReadStateHead(empty, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
        Check(EntityCodec.ReadCarriedBy == 0 && EntityCodec.ReadLockContainer == 0 && EntityCodec.ReadLocalPos.sqrMagnitude == 0 && EntityCodec.ReadLocalRot.w == 1 && EntityCodec.ReadLockItem == -1 && EntityCodec.ReadLockMount == -1 && !EntityCodec.ReadHandPose, "ordinary state clears preceding extensions");
        EntityCodec.ReadHandPose = true;
        w.Reset(); EntityCodec.WriteRot(w, Quaternion.identity); EntityCodec.ReadRot(RawReader(RotationBytes[0]));
        EntityCodec.ReadDetachSync(Reader(Convert.FromHexString(DetachBytes)), out _, out _, out _);
        Check(EntityCodec.ReadHandPose, "rotation and detach leave state scratch lifetime unchanged");
    }

    static NetReader RawReader(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex); return new NetReader { Buf = bytes, Pos = 0, End = bytes.Length };
    }
}
