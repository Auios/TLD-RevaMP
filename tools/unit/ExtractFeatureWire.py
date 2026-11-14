"""Compile the shared door and edit sends with the real moved hooks in EntitySplitTest."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[2]


def method(part, name):
    path = f'src/TLDRevamp/Net/Entities.{part}.cs'
    source = (subprocess.check_output(['git', 'show', f'08b120c:{path}'], cwd=root).decode()
              if '--baseline' in sys.argv else (root / path).read_text(encoding='utf-8'))
    source = source.replace('\r\n', '\n')
    start = source.rfind('\n', 0, source.index(' static void ' + name + '(')) + 1
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    body = source[start:end]
    if '--baseline' in sys.argv:
        for hook in ('DoorAttachFrame', 'AttachFramePath', 'KeepDoorAttach'):
            body = body.replace(hook + '(', 'EntityDoors.' + hook + '(')
        body = body.replace('CopyTanksAreNow(', 'EntityCarTuning.CopyTanksAreNow(')
    return body


target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text('using UnityEngine;\nusing System.Collections.Generic;\nnamespace TLDRevamp.Net { public static partial class Entities {\n'
                  + method('Attach', 'WriteAttachPose') + '\n'
                  + method('LiveUsables', 'WriteLiveUsable') + '\n'
                  + method('Edits', 'EditTick') + '\n} }\n', encoding='utf-8', newline='\n')
