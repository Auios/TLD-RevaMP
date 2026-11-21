using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal enum OwnerChange : byte
        {
            Claim = 1, WornTakeBack, RabbitNearest, Handoff, AttachedToParent, Cascade,
            CrashClaim, CrashReturn, PlayerSilent, PlayerLeft, HitchTransfer
        }

        /// Whether this change ends a crash lease (the car no longer goes back to CrashReturnTo). Each path says it
        /// explicitly; owner-1.md lists where paths differ (player left keeps it, player silent clears it).
        private enum CrashReturnRule : byte { Keep, Clear }

        private struct OwnerHistoryEntry
        {
            internal float At;
            internal int From, To;
            internal uint Epoch;
            internal OwnerChange Why;
        }

        private sealed partial class SEnt
        {
            internal OwnerHistoryEntry[] OwnerHistory;
            internal int OwnerHistoryNext, OwnerHistoryCount;
            internal float OwnerHistoryLoggedAt;
            internal bool OwnerHistoryLogged;
        }

        private static int _ownerHistoryEvents;

        /// The host's only way to change who owns an object: the decision log, owner, epoch, the crash-lease rule, the
        /// history entry and the Owner message to every player. Callers keep their own extras (claim fields, counters,
        /// the cascade). The two callbacks are for the few extras whose place matters: afterLog runs before the owner
        /// changes (the hitch freeze goes out before the grant), beforeSend after it, with the new epoch, before the
        /// grant is sent (the host's own copy gets the grant at once).
        private static void ServerSetOwner(SEnt se, int to, OwnerChange why, CrashReturnRule crash, NetWriter writer,
            string decision, Action<SEnt, int> afterLog = null, Action<SEnt, int> beforeSend = null)
        {
            OwnerLog(se, to, decision);
            afterLog?.Invoke(se, to);
            int from = se.OwnerId;
            se.OwnerId = to;
            se.Epoch++;
            if (crash == CrashReturnRule.Clear) se.CrashReturnTo = -1;
            beforeSend?.Invoke(se, to);
            // Record before sending: local delivery can change another owner synchronously.
            if (se.OwnerHistory == null) se.OwnerHistory = new OwnerHistoryEntry[8];
            se.OwnerHistory[se.OwnerHistoryNext] = new OwnerHistoryEntry
            {
                At = Time.realtimeSinceStartup, From = from, To = to, Epoch = se.Epoch, Why = why
            };
            se.OwnerHistoryNext = (se.OwnerHistoryNext + 1) % 8;
            if (se.OwnerHistoryCount < 8) se.OwnerHistoryCount++;
            writer.Reset(); writer.U8(Owner); writer.U32(se.NetId); writer.VarU32((uint)to); writer.U32(se.Epoch);
            ServerSendAll(writer, true, -1);
        }

        private static string OwnerHistoryResult(SEnt se, float now)
        {
            var b = new StringBuilder();
            for (int i = 0; i < se.OwnerHistoryCount; i++)
            {
                var row = se.OwnerHistory[(se.OwnerHistoryNext - se.OwnerHistoryCount + i + 8) % 8];
                if (i != 0) b.Append(';');
                b.Append((row.At - now).ToString("F3", CultureInfo.InvariantCulture)).Append('-')
                 .Append(row.From.ToString(CultureInfo.InvariantCulture)).Append('>')
                 .Append(row.To.ToString(CultureInfo.InvariantCulture)).Append('@')
                 .Append(row.Epoch.ToString(CultureInfo.InvariantCulture)).Append(':').Append(row.Why);
            }
            return b.ToString();
        }

        /// Reason names as event results: fixed words, allowed by TelemetryPrivacy (free text would arrive as "[removed]").
        internal static readonly string[] OwnerChangeNames =
        {
            "", "Claim", "WornTakeBack", "RabbitNearest", "Handoff", "AttachedToParent", "Cascade",
            "CrashClaim", "CrashReturn", "PlayerSilent", "PlayerLeft", "HitchTransfer"
        };

        /// Into the session's event ring (marks and bug reports carry it): one "now" row (current owner and epoch, how
        /// many changes follow), then one row per change, oldest first, in numeric fields only - from, to, the new
        /// epoch, the reason (count = OwnerChange, result = its name) and how long ago (value, seconds, negative).
        private static void LogOwnerHistory(SEnt se)
        {
            float now = Time.realtimeSinceStartup;
            if (_ownerHistoryEvents >= 200 || se.OwnerHistoryLogged && now - se.OwnerHistoryLoggedAt < 10f) return;
            se.OwnerHistoryLogged = true; se.OwnerHistoryLoggedAt = now; _ownerHistoryEvents++;
            SessionLog.Event("owner-history", se.NetId, to: se.OwnerId, epoch: se.Epoch, count: se.OwnerHistoryCount, result: "now");
            for (int i = 0; i < se.OwnerHistoryCount; i++)
            {
                var row = se.OwnerHistory[(se.OwnerHistoryNext - se.OwnerHistoryCount + i + 8) % 8];
                SessionLog.Event("owner-history", se.NetId, from: row.From, to: row.To, epoch: row.Epoch, count: (int)row.Why,
                    result: OwnerChangeNames[(int)row.Why], value: row.At - now);
            }
        }

        internal static string OwnerHistoryJson(uint net)
        {
            if (!Server.TryGetValue(net, out var se)) return "{\"net\":" + net + ",\"error\":\"no such host object\"}";
            return "{\"net\":" + net + ",\"owner\":" + se.OwnerId + ",\"epoch\":" + se.Epoch +
                   ",\"count\":" + se.OwnerHistoryCount + ",\"history\":" + Json.Str(OwnerHistoryResult(se, Time.realtimeSinceStartup)) + "}";
        }
    }
}
