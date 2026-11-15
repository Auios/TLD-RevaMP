"""Compile the actual hitch dependency calls in the isolated transaction harness."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
source = (root / 'src/TLDRevamp/Net/Entities.Features.cs').read_text(encoding='utf-8')
start = source.index('        // Hitch client boundary.')
end = source.index('        // End hitch client boundary.', start)
if len(sys.argv) > 2 and sys.argv[2] == 'feature':
    source = (root / 'src/TLDRevamp/Net/Entities.Hitch.cs').read_text(encoding='utf-8')
    source = source[:source.index('    public static partial class Entities')] + '}\n'
else:
    source = 'using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + source[start:end] + '\n} }'
Path(sys.argv[1]).write_text(source, encoding='utf-8')
