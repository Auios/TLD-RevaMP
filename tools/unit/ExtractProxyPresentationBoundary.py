"""Compile the current presentation owner with isolated fixtures."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
parts = ('CopyLod', 'Proxy', 'VisibleState')

def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (root / path).read_text(encoding='utf-8')


def member(source, name):
    pattern = r'^        (?:private|internal|public) static [^\n]*?\b' + name + r'\('
    start = re.search(pattern, source, re.M).start()
    opening = source.find('{', start)
    arrow = source.find('=>', start, opening)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

def visible_facade(original):
    original = original.replace('\r\n', '\n')
    result = '        private static bool ReadCompactMoving { get => EntityProxyPresentation.ReadCompactMoving; set => EntityProxyPresentation.ReadCompactMoving = value; }\n'
    for match in re.finditer(r'^        (private|internal) static (?!bool ReadCompactMoving)[^\n]*?\b(\w+)\(', original, re.M):
        name = match[2]
        if name in ('CargoBody', 'AttachedCarPart', 'LetGoInCar', 'FinalSnapshot', 'LooseRelease'): continue
        text = member(original, name)
        end = text.index('=>') if '=>' in text.split('{', 1)[0] else text.index('{')
        signature = text[:end].rstrip()
        args = []
        for arg in signature[signature.index('(') + 1:signature.rindex(')')].split(','):
            words = arg.strip().split('=')[0].strip().split()
            args.append(('ref ' if 'ref' in words else 'out ' if 'out' in words else '') + words[-1])
        result += signature + ' => EntityProxyPresentation.' + name + '(' + ', '.join(args) + ');\n'
    return result

if __name__ == '__main__':
    sources = {p: read(p).split('    internal static partial class EntityProxyPresentation\n    {\n')[1].split('\n    }\n')[0] for p in parts}
    target = Path(sys.argv[1]); target.parent.mkdir(parents=True, exist_ok=True)
    for part, s in sources.items():
        target.with_name(target.stem + part + '.cs').write_text('using System; using System.Collections.Generic; using UnityEngine; using UnityEngine.Rendering; using Ent = TLDRevamp.Net.Entities.Ent; using Object = UnityEngine.Object;\nnamespace TLDRevamp.Net { internal static partial class EntityProxyPresentation {\n' + s + '\n} }\n', encoding='utf-8')
    target.with_name(target.stem + 'Facade.cs').write_text('using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + visible_facade(sources['VisibleState']) + '\n} }\n', encoding='utf-8')
