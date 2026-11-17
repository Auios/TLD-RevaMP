"""Compile the actual narrow owners and retained resync producer in EntitySplitTest."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'

def method(source, name):
    match = re.search(r'^        (?:public|private|internal) static [^\n=]*?\b' + name + r'\(', source, re.M)
    start = match.start()
    brace = source.find('{', start)
    arrow = source.find('=>', start)
    if brace >= 0 and arrow > brace: arrow = -1
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

def sources():
    return {p: (net / ('Entities.' + p + '.cs')).read_text(encoding='utf-8').replace('\r\n', '\n')
            for p in ('EditBack', 'EditCheck', 'Edits', 'Resync')}


if __name__ == '__main__':
    if '--back-only' in sys.argv:
        feature = (net / 'Entities.Features.cs').read_text(encoding='utf-8')
        Path(sys.argv[1]).write_text('namespace TLDRevamp.Net { public static partial class Entities {\n'
            + 'internal static float EditBackHold => EditHold;\n' + method(feature, 'RequestEditBack') + '\n} }', encoding='utf-8')
        sys.exit(0)
    src = sources()
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    for part, source in src.items():
        target.with_name(target.stem + part + '.cs').write_text(source[:source.index('    public static partial class Entities')] + '}\n', encoding='utf-8')
    resync = src['Resync']
    body = method(resync, 'ResyncTick')
    server_edit = method(src['Edits'], 'ServerEdit').replace('EntityEditCheck.TakeEditToken(from)', 'TakeKeyTokenForFixture(from)')
    body += '\n' + server_edit
    retained = src['EditBack'].split('    public static partial class Entities')[1]
    body += '\n' + '\n'.join(method(retained, name) for name in ('EditBackLater', 'EditBackAfterHold'))
    target.write_text('using System; using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + body + '\n} }', encoding='utf-8')
