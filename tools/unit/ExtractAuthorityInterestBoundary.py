"""Compile the relay and quota owner against isolated transport and record fixtures."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'
parts = ('Bandwidth', 'Handoff', 'RelayInterest', 'ShareQuota', 'StateInterest')

def read(part, baseline=False):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (subprocess.check_output(['git', 'show', '4851eaf:' + path], cwd=root).decode('utf-8')
            if baseline else (root / path).read_text(encoding='utf-8')).replace('\r\n', '\n')

def method(source, name):
    match = re.search(r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'(?:<[^>]+>)?\(', source, re.M)
    start = match.start()
    brace = source.index('{', start)
    arrow = source.find('=>', start, brace)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

def body(source):
    return source.split('    public static partial class Entities\n    {\n', 1)[1].rsplit('    }', 1)[0]

def owners():
    relay = body(read('RelayInterest', True))
    for name in ('PoiAllStale', 'InterestForget', 'InterestReset'):
        relay = relay.replace(method(relay, name), '')
    relay = relay.replace(method(relay, 'InterestGotAll'), '')
    relay = relay.replace('        /// A joiner: every usable changed this session is due — it goes out as they come near (ServerCatchUpTick).\n', '')
    relay = relay.replace('        /// SendObject gave the player every record of it: nothing of it is owed any more.\n', '')
    relay = re.sub(r'^        private static readonly Dictionary<int, HashSet<uint>> _staleEnts[^\n]*\n', '', relay, flags=re.M)
    relay = re.sub(r'^        private static readonly List<uint> _dueEnts[^\n]*\n', '', relay, flags=re.M)
    relay = relay.replace('_spots', '_relaySpots')
    relay = relay.replace('RelayResync(SEnt se, int from, NetWriter msg)', 'RelayResync(uint net, Vector3d where, int from, NetWriter msg)')
    relay = relay.replace('se.Where.x, se.Where.z', 'where.x, where.z').replace('se.NetId', 'net')
    relay = relay.replace('                var stale = StaleOf(_staleEnts, s.Id);\n', '')
    relay = relay.replace('stale.Add(net)', 'Entities.MarkInterestStale(s.Id, net)').replace('stale.Remove(net)', 'Entities.TakeInterestStale(s.Id, net)')
    relay = relay.replace('WriteResyncAll(WFull, se)', 'Entities.WriteInterestResync(WFull, net)')
    relay = relay.replace('!Server.TryGetValue(net, out var se) || Near(s, where.x, where.z)',
                          '!Entities.TryInterestPosition(net, out var at) || Near(s, at.x, at.z)')
    relay = relay.replace('if (!Server.TryGetValue(net, out var se) || se.ItemState.Count == 0) continue;\n                        Entities.WriteInterestResync(WFull, net);',
                          'if (!Entities.TryWriteInterestResync(WFull, net)) continue;')
    relay = relay.replace('!ServerPoiUsables.ContainsKey(k)', '!Entities.HasInterestPoi(k)')
    relay = relay.replace('ServerPoiUsables.TryGetValue(k, out var b)', 'Entities.TryInterestPoi(k, out var b)')
    start = relay.index('                if (_staleEnts.TryGetValue(s.Id, out var ents)')
    end = relay.index('                if (_stalePoi.TryGetValue', start)
    relay = relay[:start] + '                Entities.CatchUpInterestRecords(s.Id, s.Pos, WFull);\n' + relay[end:]
    relay = relay.replace('int ents = 0, pois = 0;\n            foreach (var s in _staleEnts.Values) ents += s.Count;', 'int ents = Entities.CountInterestStale(), pois = 0;')
    relay += '''
        internal static void MarkPoiStale(int player, PoiKey key) => StaleOf(_stalePoi, player).Add(key);
        internal static void ForgetRelay(int player) { _stalePoi.Remove(player); }
        internal static void ResetRelay() { _stalePoi.Clear(); }
        internal static void PreparePoiStale(int player) => StaleOf(_stalePoi, player);
'''
    for old, new in (('ServerSendAll', 'SendToPeers'), ('Deliver', 'DeliverInterestMessage'), ('SkippedByInterest', 'RecordInterestSkip')):
        relay = re.sub(r'\b' + old + r'\(', 'Entities.' + new + '(', relay)
    relay = re.sub(r'\bInterestPerTick\b', 'Entities.InterestCatchUpLimit', relay)
    relay = relay.replace('private static void RelayResync', 'internal static void RelayResync').replace('private static void RelayPoi', 'internal static void RelayPoi')
    relay = relay.replace('private static void ServerCatchUpTick', 'internal static void ServerCatchUpTick').replace('private static void InterestGotAll', 'internal static void InterestGotAll')
    quota = body(read('ShareQuota', True))
    # dev 83ce52c (2026-11-17) removed the per-player ownership cap (ShareMaxLive) after this batch was captured;
    # the baseline gets exactly that removal and nothing else.
    quota = quota.replace('ShareBurst = 600, ShareMaxLive = 4000, ShareMaxRecordBytes', 'ShareBurst = 600, ShareMaxRecordBytes')
    quota = quota.replace('public float DropsSince; public int Live; public float LiveAt = -10f; }', 'public float DropsSince; }')
    quota = re.sub(r' {12}else\n {12}\{\n {16}if \(now - b\.LiveAt > 1f\)\n.*?\n {12}\}\n', '', quota, count=1, flags=re.S)
    quota = quota.replace('{ b.Tokens -= 1f; b.Live++; return true; }', '{ b.Tokens -= 1f; return true; }')
    assert 'Live' not in quota, 'cap removal substitution did not apply'
    quota = quota.replace('private static bool ShareAllowed', 'internal static bool ShareAllowed').replace('private static void ShareQuotaTick', 'internal static void ShareQuotaTick')
    return {'RelayInterest': relay, 'ShareQuota': quota}

def source_rows():
    # Whole retained files have no adapter normalization. The original relay/quota algorithms
    # are frozen separately after adapting only the narrow record/transport operations above.
    rows = []
    for part in ('Bandwidth', 'Handoff', 'StateInterest'):
        rows.append((part, hashlib.sha256(read(part, True).encode()).hexdigest()))
    for part, text in owners().items():
        rows.append((part + '-owner', digest(text)))
    rows.append(('record-replay', replay_digest(record_replay(True))))
    rows.append(('record-writer', digest(method(read('Resync', True), 'WriteResyncAll'))))
    return rows

def digest(text):
    return hashlib.sha256(re.sub(r'\s+', '', text).encode()).hexdigest()

def record_replay(baseline):
    if not baseline:
        return method((net / 'Entities.Features.cs').read_text(encoding='utf-8'), 'CatchUpInterestRecords')
    original = method(read('RelayInterest', True), 'ServerCatchUpTick')
    start = original.index('                if (_staleEnts.TryGetValue(s.Id, out var ents)')
    end = original.index('                if (_stalePoi.TryGetValue', start)
    text = 'internal static void CatchUpInterestRecords(int player, Vector3d viewer, NetWriter writer) {\n' + original[start:end] + '\n}'
    return text.replace('s.Id', 'player').replace('Near(s, se.Where.x, se.Where.z)', 'NearInterestRecord(viewer, se.Where)').replace('WFull', 'writer')

def replay_digest(text):
    return digest(re.sub(r'//[^\n]*', '', text).replace('EntityAuthorityInterest.ResyncCaughtUp', 'ResyncCaughtUp'))

if __name__ == '__main__':
    if '--capture-source' in sys.argv:
        (root / 'tools/unit/EntityAuthorityInterestSourceRows.txt').write_bytes(
            ('\r\n'.join(n + '=' + h for n, h in source_rows()) + '\r\n').encode())
        sys.exit(0)
    baseline = '--baseline' in sys.argv
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    sources = owners() if baseline else {p: read(p).split('    internal static partial class EntityAuthorityInterest\n    {\n', 1)[1].split('\n    }\n', 1)[0] for p in ('RelayInterest', 'ShareQuota')}
    if not baseline:
        expected = dict(line.split('=') for line in (root / 'tools/unit/EntityAuthorityInterestSourceRows.txt').read_text().splitlines())
        for part in ('Bandwidth', 'Handoff', 'StateInterest'):
            assert hashlib.sha256(read(part).encode()).hexdigest() == expected[part], part + ' retained behavior changed'
        for part, text in sources.items():
            assert digest(text) == expected[part + '-owner'], part + ' owner differs from the captured HEAD adapter'
        assert replay_digest(record_replay(False)) == expected['record-replay'], 'entity record catch-up differs from HEAD'
        assert digest(method(read('Resync'), 'WriteResyncAll')) == expected['record-writer'], 'record bytes differ from HEAD'
    for part, text in sources.items():
        replacements = {'Entities': 'AuthorityInterestFakes.Kernel', 'Mp': 'AuthorityInterestFakes.Transport',
                        'MpServer': 'AuthorityInterestFakes.Server', 'Protocol': 'AuthorityInterestFakes.Protocol'}
        text = re.sub(r'\b(' + '|'.join(replacements) + r')\b', lambda m: replacements[m[0]], text)
        target.with_name(target.stem + part + '.cs').write_text(
            'using System.Collections.Generic; using UnityEngine; using PoiKey = TLDRevamp.Net.Entities.PoiKey;\n'
            + 'namespace TLDRevamp.Net { internal static partial class EntityAuthorityInterest {\n' + text + '\n} }\n', encoding='utf-8')
    # Keep the server-record replay algorithm real even though the fixture has tiny records.
    replay = record_replay(baseline)
    replay = replay.replace('Server.TryGetValue', 'Records.TryGetValue').replace('se.Where', 'se.at').replace('se.ItemState.Count', 'se.bytes.Length')
    replay = replay.replace('WriteResyncAll(writer, se)', 'WriteInterestResync(writer, net)').replace('InterestPerTick', 'InterestCatchUpLimit')
    replay = replay.replace('_dueEnts', 'Due').replace('Mp.Server', 'Transport.Server').replace('EntityAuthorityInterest.ResyncCaughtUp', 'ResyncCaughtUp')
    replay = replay.replace('ResyncCaughtUp++', 'TLDRevamp.Net.EntityAuthorityInterest.ResyncCaughtUp++')
    writer = method(read('Resync', baseline), 'WriteResyncAll').replace('private static void WriteResyncAll(NetWriter w, SEnt se)', 'internal static void EncodeRecord(NetWriter w, FullRecord se)').replace('SessionLog.St', 'Stamp')
    target.with_name(target.stem + 'Records.cs').write_text('using UnityEngine; using TLDRevamp.Net; namespace AuthorityInterestFakes { internal static partial class Kernel {\n' + replay + '\n' + writer + '\n} }', encoding='utf-8')
