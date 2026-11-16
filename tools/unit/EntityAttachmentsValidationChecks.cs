using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static void AttachmentValidationChecks()
        {
            var rows = new List<string>();
            void Run(string name, Action<SEnt> setup, int from = 2, int idx = 1, uint epoch = 3, Vector3d? pos = null, Quaternion? rot = null, int length = -1, bool extra = false)
            {
                var car = Car(); PartRejects = ""; PartOffRejected = 0; PartOffAlreadyOff = 0; SessionLog.Reason = "";
                setup(car);
                var r = Off(idx, epoch, pos, rot);
                if (length >= 0) r.End = length;
                if (extra) { r.Buf[r.End++] = 0; r.Buf[r.End++] = 7; }
                ServerPartOff(from, r);
                rows.Add(name + "|" + SessionLog.Reason + "|" + PartRejects + "|" + PartOffRejected + "|" + PartOffAlreadyOff + "|" + Broadcasts + "|" + _nextNet + "|" + string.Join(",", car.SplitOff.OrderBy(n => n)) + "|" + Mp.Server.NoticeTarget + "|" + Mp.Server.Notice + "|" + Plugin.Log.Warning);
            }
            Run("accepted", _ => { });
            Run("wrong-owner-before-epoch-pose", _ => { }, 9, epoch: 8, pos: new Vector3d(double.NaN, 0, 0));
            Run("epoch-before-pose", _ => { }, epoch: 8, pos: new Vector3d(double.NaN, 0, 0));
            Run("finite-before-reach", _ => { }, pos: new Vector3d(double.NaN, 0, 0));
            Run("reach-before-rotation-index", _ => { }, idx: 99, pos: new Vector3d(9999, 0, 0), rot: new Quaternion(0, 0, 0, 0));
            Run("rotation-before-index", _ => { }, idx: 99, rot: new Quaternion(0, 0, 0, 0));
            Run("index", _ => { }, idx: 99);
            Run("already-off-before-owner", c => c.SplitOff.Add(1), from: 9, epoch: 8);
            Run("gone", c => c.Gone = new HashSet<int> { 1 });
            Run("not-attached", c => c.Record.attachable.Clear());
            Run("bad-slot", c => c.Record.attachable[0].index = -1);
            Run("bad-parent", c => c.Record.attachable[0].parentid = 777);
            Run("prefab-slot", c => c.Record.attachable[0].index = 9);
            Run("glue", c => c.Record.attachable[0].indexType = 1);
            Run("prev-owner", c => { c.OwnerId = 8; c.PrevOwnerId = 2; c.PrevOwnerAt = 10; c.Epoch = 4; });
            Run("claim-epoch", c => { c.ClaimedBy = 2; c.ClaimedFromEpoch = 2; c.ClaimedAt = 10; }, epoch: 2);
            Run("missing-car", c => Server.Clear());
            for (int n = 0; n < Off().End; n++) Run("truncated-" + n, _ => { }, length: n);
            Run("carried-tail-malformed", _ => { }, extra: true);
            var first = Car(); PartRejects = "";
            RefusePartOff(2, 1, 1, 3, "fixture"); rows.Add("notice-first|" + _partOffToldAt.Count + "|" + Mp.Server.Notice);
            Mp.Server.Notice = null; Time.realtimeSinceStartup += PartOffToldS - .001f;
            RefusePartOff(2, 1, 2, 3, "fixture"); rows.Add("notice-before|" + _partOffToldAt.Count + "|" + Mp.Server.Notice);
            Time.realtimeSinceStartup = 10 + PartOffToldS;
            RefusePartOff(2, 1, 3, 3, "fixture"); rows.Add("notice-at|" + _partOffToldAt.Count + "|" + Mp.Server.Notice);
            const string path = "tools/unit/EntityAttachmentsValidationRows.txt";
#if ATTACHMENTS_CAPTURE
            File.WriteAllLines(path, rows);
#else
            ValidationTest.Check(File.ReadAllLines(path).SequenceEqual(rows), "attachment validation rows match pre-split 4851eaf");
#endif
        }
    }
}
