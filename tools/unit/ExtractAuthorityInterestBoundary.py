"""Compile the relay and quota owner against isolated transport and record fixtures."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'

def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (root / path).read_text(encoding='utf-8').replace('\r\n', '\n')

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


def record_replay():
    return method((net / 'Entities.Features.cs').read_text(encoding='utf-8'), 'CatchUpInterestRecords')
if __name__ == '__main__':
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    sources = {p: read(p).split('    internal static partial class EntityAuthorityInterest\n    {\n', 1)[1].split('\n    }\n', 1)[0] for p in ('RelayInterest', 'ShareQuota')}
    for part, text in sources.items():
        replacements = {'Entities': 'AuthorityInterestFakes.Kernel', 'Mp': 'AuthorityInterestFakes.Transport',
                        'MpServer': 'AuthorityInterestFakes.Server', 'Protocol': 'AuthorityInterestFakes.Protocol'}
        text = re.sub(r'\b(' + '|'.join(replacements) + r')\b', lambda m: replacements[m[0]], text)
        target.with_name(target.stem + part + '.cs').write_text(
            'using System.Collections.Generic; using UnityEngine; using PoiKey = TLDRevamp.Net.Entities.PoiKey;\n'
            + 'namespace TLDRevamp.Net { internal static partial class EntityAuthorityInterest {\n' + text + '\n} }\n', encoding='utf-8')
    # Keep the server-record replay algorithm real even though the fixture has tiny records.
    replay = record_replay()
    replay = replay.replace('Server.TryGetValue', 'Records.TryGetValue').replace('se.Where', 'se.at').replace('se.ItemState.Count', 'se.bytes.Length')
    replay = replay.replace('WriteResyncAll(writer, se)', 'WriteInterestResync(writer, net)').replace('InterestPerTick', 'InterestCatchUpLimit')
    replay = replay.replace('_dueEnts', 'Due').replace('Mp.Server', 'Transport.Server').replace('EntityAuthorityInterest.ResyncCaughtUp', 'ResyncCaughtUp')
    replay = replay.replace('ResyncCaughtUp++', 'TLDRevamp.Net.EntityAuthorityInterest.ResyncCaughtUp++')
    writer = method(read('Resync'), 'WriteResyncAll').replace('private static void WriteResyncAll(NetWriter w, SEnt se)', 'internal static void EncodeRecord(NetWriter w, FullRecord se)').replace('SessionLog.St', 'Stamp')
    target.with_name(target.stem + 'Records.cs').write_text('using UnityEngine; using TLDRevamp.Net; namespace AuthorityInterestFakes { internal static partial class Kernel {\n' + replay + '\n' + writer + '\n} }', encoding='utf-8')
