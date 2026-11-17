"""Compile the current repair helpers against isolated game fixtures."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
header = '    internal static partial class EntityRepair'


def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (root / path).read_text(encoding='utf-8').replace('\r\n', '\n')


def body(source, owner):
    return source.split(owner, 1)[1].split('\n    }', 1)[0].split('{', 1)[1]


output = []
for part in ('Repair', 'PlaceCheck'):
    source = read(part)
    moved = body(source, header)
    output.append('using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { ' + header + ' {\n' + moved + '\n} }')
source = read('PoiCensus')
moved = body(source, header)
# Isolate only game map/settings/database surfaces; the real writer, reader, comparisons and counters run.
for old, new in [('menuhandler', 'RepairFakes.Menu'), ('settingsscript', 'RepairFakes.Settings'),
                 ('itemdatabase', 'RepairFakes.Database'), ('mainscript', 'RepairFakes.Main')]:
    moved = re.sub(r'\b' + old + r'\b', new, moved)
output.append('using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { ' + header + ' {\n' + moved + '\n} }')

target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
for i, source in enumerate(output):
    target.with_name(target.stem + str(i) + '.cs').write_text(source, encoding='utf-8', newline='\n')
