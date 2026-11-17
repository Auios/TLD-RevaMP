using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityEditCheckTools
    {
        internal static readonly NetWriter W = new NetWriter();

        // ---------------------------------------------------------------- test hooks (bridge: mp forgeedit / forgepoi)
        /// `mp forgeedit <net> <what> [n]`: n edits of our copy of someone else's object, made up the way a cheating
        /// player could: all (KindAll), tank (99,999 L), nan (a usable's rotation NaN), colourstate (a colour edit that
        /// repairs part 0), ok (the copy's own usable state: passes), rate (ok, n at once).
        public static string TestForgeEdit(uint net, string what, int n)
        {
            if (!ObservedEntities.TryGetValue(net, out var e) || !e.Proxy || !Resolve(e)) return "{\"error\":\"no copy of someone else's object with that net here\"}";
            int idx = -1;
            for (int i = 0; i < e.Items.Count && idx < 0; i++)
            {
                var it = e.Items[i];
                if (it == null) continue;
                if (what == "tank" ? it.tanks != null && it.tanks.Count > 0 : what == "colourstate" ? it.partconditions != null && it.partconditions.Count > 0 : it.usables != null && it.usables.Count > 0) idx = i;
            }
            if (idx < 0) return "{\"error\":\"no item of it has what that needs\"}";
            var map = EntityResync.CanonMap(e.Items);
            var d = EntityResync.StateOnly(ItemSnapshot.Capture(e.Items[idx]));
            d.car = new List<save_carscript>();
            int kinds = KindUsable;
            switch (what)
            {
                case "all": kinds = KindAll; break;
                case "tank": kinds = KindTank; if (d.tanks.Count > 0) { d.tanks[0].amount = new[] { 99999f }; d.tanks[0].type = new[] { 0 }; } break;
                case "nan": if (d.usable.Count > 0) d.usable[0].xRot = float.NaN; break;
                case "colourstate": kinds = KindColor; if (d.partconditions.Count > 0) d.partconditions[0].state = d.partconditions[0].state == 0 ? 1 : 0; break;
                case "ok": case "rate": break;
                default: return "{\"error\":\"all | tank | nan | colourstate | ok | rate\"}";
            }
            ItemSnapshot.Remap(d, map);
            byte[] rec = RecordCodec.Encode(d);
            for (int k = 0; k < n; k++)
            {
                W.Reset(); W.U8(Edit); W.U32(e.NetId); W.F64(SessionLog.St); W.VarU32((uint)idx); W.VarU32((uint)kinds); W.VarU32((uint)rec.Length); W.Bytes(rec, 0, rec.Length);
                ToServer(W, true);
            }
            return "{\"sent\":" + n + ",\"item\":" + idx + ",\"kinds\":" + kinds + ",\"bytes\":" + rec.Length + "}";
        }

        /// `mp forgepoi gen x y z u slide [n]`: a building usable change sent as is (the building needn't be here).
        public static string TestForgePoi(string[] a)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var k = new PoiKey { Gen = int.Parse(a[1]), X = double.Parse(a[2], ci), Y = double.Parse(a[3], ci), Z = double.Parse(a[4], ci), U = int.Parse(a[5]) };
            var u = EntityUsables.PoiUsableOf(k);
            var st = u != null ? new PoiState { XRot = u.xRot, YRot = u.yRot, Slide = u.slideValue, Turn = u.currentTurnState, Tuned = u.tunedFloat } : new PoiState();
            st.Slide = float.Parse(a[6], ci);
            int n = a.Length > 7 ? int.Parse(a[7]) : 1;
            for (int i = 0; i < n; i++) { W.Reset(); W.U8(PoiUsable); EntityUsables.WritePoi(W, k, st); var key = new UsableKey { Building = true, Poi = k }; ushort seq = NextUsableSequence(key); W.U16(seq); W.F64(SessionLog.St); ToServer(W, true); }
            return "{\"sent\":" + n + ",\"spawnedHere\":" + (u != null ? "true" : "false") + "}";
        }
    }
}
