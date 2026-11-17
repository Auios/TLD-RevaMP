"""Compile the actual moved sections, retaining the source-including kernel fixtures."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'

def feature(part):
    source = (net / ('Entities.' + part + '.cs')).read_text(encoding='utf-8')
    return source[:source.index('    public static partial class Entities')] + '}\n'

target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
# Each original file has its own using aliases.
for part in ('Teleport', 'LiveUsables', 'PoiMotion', 'PoiUsables'):
    target.with_name(target.stem + part + '.cs').write_text(feature(part), encoding='utf-8')

def between(part, start, end):
    source = (net / ('Entities.' + part + '.cs')).read_text(encoding='utf-8')
    return source[source.index(start):source.index(end, source.index(start))]

records = between('LiveUsables', '        internal struct UsableKey', '        private sealed class LocalUsable')
records += between('PoiMotion', '        internal struct PoiKey', '        private static readonly Dictionary<PoiKey, byte[]>')
target.write_text('using System; namespace TLDRevamp.Net { public static partial class Entities {\n' + records + '\n} }', encoding='utf-8')
