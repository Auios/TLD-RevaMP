"""Compile the current lifetime and streaming helpers with small game surfaces."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
parts = ('FallOff', 'FarPose', 'FarStore', 'Lifecycle', 'LinkedDelay', 'MemberGone', 'PlayerMade', 'Removal')
owner_header = '    internal static partial class EntityLifetimeStreaming\n    {\n'


def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (root / path).read_text(encoding='utf-8').replace('\r\n', '\n')


def body(source, header):
    return source.split(header, 1)[1].split('\n    }', 1)[0]


def write(path, text):
    path.write_bytes(text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))


if __name__ == '__main__':
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    for part in parts:
        moved = body(read(part), owner_header)
        # The real Ent and method signatures are retained. Only unavailable game singletons are substituted.
        replacements = {'Entities': 'LifetimeStreamingFakes.Kernel', 'savedatascript': 'LifetimeStreamingFakes.Save',
                        'menuhandler': 'LifetimeStreamingFakes.Menu', 'itemPlaceRemoveScript': 'LifetimeStreamingFakes.Placer',
                        'ItemSnapshot': 'LifetimeStreamingFakes.Snapshot', 'GameLookups': 'LifetimeStreamingFakes.Lookups'}
        moved = re.sub(r'\b(' + '|'.join(replacements) + r')\b', lambda m: replacements[m[0]], moved)
        moved = moved.replace('Fixes.TrailerGrip', 'LifetimeStreamingFakes.TrailerGrip').replace('Quaternion.Angle(', 'LifetimeStreamingFakes.Rotation.Angle(')
        write(target.with_name(target.stem + part + '.cs'),
              'using System; using System.Collections; using System.Collections.Generic; using System.Reflection; using UnityEngine; using HarmonyLib;\n'
              + 'using Ent = TLDRevamp.Net.Entities.Ent;\nnamespace TLDRevamp.Net { internal static partial class EntityLifetimeStreaming {\n' + moved + '\n} }\n')
