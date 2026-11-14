"""Compile the shared door and edit sends with the real moved hooks in EntitySplitTest."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[2]


def method(part, name, seats=False):
    path = 'src/TLDRevamp/Net/Entities' + ('.' + part if part else '') + '.cs'
    baseline = '--seats-baseline' in sys.argv if seats else '--baseline' in sys.argv
    revision = 'aa5fc86' if seats else '08b120c'
    source = (subprocess.check_output(['git', 'show', f'{revision}:{path}'], cwd=root).decode()
              if baseline else (root / path).read_text(encoding='utf-8'))
    source = source.replace('\r\n', '\n')
    start = source.rfind('\n', 0, source.index(' static void ' + name + '(')) + 1
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    body = source[start:end]
    if seats and baseline:
        body = body.replace('DriverSeatRefused++;', 'EntitySeats.DriverSeatRefused++;')
        body = body.replace('DriverSeatConflictTick(', 'EntitySeats.DriverSeatConflictTick(')
    if not seats and baseline:
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

# Driver claims remain in the kernel. Exercise their real producer and arbitration bodies too.
with target.open('a', encoding='utf-8', newline='\n') as output:
    output.write('namespace TLDRevamp.Net { public static partial class Entities {\n'
                 + '\n'.join(method(part, name, True) for part, name in (
                     ('Pickup', 'WriteClaim'), ('Pickup', 'ServerClaim'), ('', 'RefuseClaim'),
                     ('', 'DriveClaimTick'), ('', 'CheckDriverSeat'))) + '\n} }\n')

# Compile the held send cadence and release extension directly from the scheduler too.
source = (root / 'src/TLDRevamp/Net/Entities.cs').read_text(encoding='utf-8')
interval = source.split('float sendEvery = ', 1)[1].split(';', 1)[0]
release = next(line.strip() for line in source.splitlines() if 'if (released && e.SentHeld) e.FastSendUntil =' in line)
with target.open('a', encoding='utf-8', newline='\n') as output:
    output.write('namespace TLDRevamp.Net { public static partial class Entities {\n'
                 + 'internal static float TestHeldInterval(Ent e, double now) => ' + interval + ';\n'
                 + 'internal static void TestHeldRelease(Ent e, double now, bool released) { ' + release + ' }\n'
                 + '} }\n')
