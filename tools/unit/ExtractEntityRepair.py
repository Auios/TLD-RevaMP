"""Compile slice 18's actual helpers and freeze the retained transactions against 11cb11b."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
baseline = '--baseline' in sys.argv
header = '    internal static partial class EntityRepair'
kernel = '    public static partial class Entities'


def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (subprocess.check_output(['git', 'show', '11cb11b:' + path], cwd=root).decode('utf-8')
            if baseline else (root / path).read_text(encoding='utf-8')).replace('\r\n', '\n')


def method(source, name):
    match = re.search(r'^        (?:private|internal|public) static [^\n]*?\b' + name + r'\(', source, re.M)
    start = match.start()
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def body(source, owner):
    return source.split(owner, 1)[1].split('\n    }', 1)[0].split('{', 1)[1]


def normalized(source):
    return re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', source))


output = []
rows = []
for part, names in [('Repair', ('RepairState', 'RowMap')), ('PlaceCheck', ('GluedPlace',))]:
    source = read(part)
    if baseline:
        moved = '\n'.join(method(source, name).replace('private static', 'internal static') for name in names)
        moved = moved.replace('AttachFrame(at)', 'Entities.RepairAttachFrame(at)')
        retained = body(source, kernel)
        for name in names:
            retained = retained.replace(method(source, name), '')
    else:
        moved = body(source, header)
        retained = body(source, kernel)
    output.append('using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { ' + header + ' {\n' + moved + '\n} }')
    for label, code in [('feature', moved), ('kernel', retained)]:
        rows.append(part + '.' + label + '=' + hashlib.sha256(normalized(code).encode()).hexdigest().upper())

source = read('PoiCensus')
if baseline:
    moved = body(source, kernel)
    moved = re.sub(r'        public const byte PoiCensus = 84;[^\n]*\n', '', moved)
    moved = moved.replace('private static void PoiCensusTick()', 'internal static void PoiCensusTick()')
    moved = moved.replace('W.U8(PoiCensus)', 'W.U8(Entities.PoiCensus)')
    moved = moved.replace('ToServer(W, true)', 'Entities.SendToServer(W, true)')
    moved = moved.replace('!Finite(', '!Entities.IsFinite(').replace('Anomaly($', 'Entities.RepairAnomaly($')
    moved = '        private static readonly NetWriter W = new NetWriter();\n' + moved
else:
    moved = body(source, header)
rows.append('PoiCensus.feature=' + hashlib.sha256(normalized(moved).encode()).hexdigest().upper())
# Isolate only game map/settings/database surfaces; the real writer, reader, comparisons and counters run.
for old, new in [('menuhandler', 'RepairFakes.Menu'), ('settingsscript', 'RepairFakes.Settings'),
                 ('itemdatabase', 'RepairFakes.Database'), ('mainscript', 'RepairFakes.Main')]:
    moved = re.sub(r'\b' + old + r'\b', new, moved)
output.append('using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { ' + header + ' {\n' + moved + '\n} }')

target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
for i, source in enumerate(output):
    target.with_name(target.stem + str(i) + '.cs').write_text(source, encoding='utf-8', newline='\n')
actual = root / 'tools/unit/obj/EntityRepair/source-actual.txt'
actual.parent.mkdir(parents=True, exist_ok=True)
actual.write_text('\n'.join(rows) + '\n', encoding='utf-8', newline='\r\n')
if baseline and 'RepairAnomaly' not in (root / 'src/TLDRevamp/Net/Entities.Features.cs').read_text(encoding='utf-8'):
    target.with_name(target.stem + 'Baseline.cs').write_text('''using UnityEngine;
namespace TLDRevamp.Net { public static partial class Entities {
    public const byte PoiCensus = 84;
    internal static Transform RepairAttachFrame(attachablescript at) => AttachFrame(at);
    internal static void RepairAnomaly(string what) => Anomaly(what);
} }
''', encoding='utf-8')
else:
    stale = target.with_name(target.stem + 'Baseline.cs')
    if stale.exists():
        stale.unlink()
if '--capture-source' in sys.argv:
    if not baseline:
        raise SystemExit('Source capture requires --baseline')
    (root / 'tools/unit/EntityRepairSourceRows.txt').write_bytes(actual.read_bytes())
