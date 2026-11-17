"""Compile the current presentation owner with isolated fixtures."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
parts = ('CopyLod', 'Proxy', 'VisibleState')

def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (root / path).read_text(encoding='utf-8')


if __name__ == '__main__':
    sources = {p: read(p).split('    internal static partial class EntityProxyPresentation\n    {\n')[1].split('\n    }\n')[0] for p in parts}
    target = Path(sys.argv[1]); target.parent.mkdir(parents=True, exist_ok=True)
    for part, s in sources.items():
        target.with_name(target.stem + part + '.cs').write_text('using System; using System.Collections.Generic; using UnityEngine; using UnityEngine.Rendering; using Ent = TLDRevamp.Net.Entities.Ent; using Object = UnityEngine.Object;\nnamespace TLDRevamp.Net { internal static partial class EntityProxyPresentation {\n' + s + '\n} }\n', encoding='utf-8')
