"""Compile the shared door and edit sends with the real moved hooks in EntitySplitTest."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]


def method(part, name):
    path = 'src/TLDRevamp/Net/Entities' + ('.' + part if part else '') + '.cs'
    source = (root / path).read_text(encoding='utf-8')
    source = source.replace('\r\n', '\n')
    start = source.rfind('\n', 0, source.index(' static void ' + name + '(')) + 1
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text('using UnityEngine;\nusing System.Collections.Generic;\nnamespace TLDRevamp.Net { public static partial class Entities {\n'
                  + method('Edits', 'EditTick').replace('EntityEdits.TankChangesNow(', 'TankChangesForFixture(') + '\n} }\n', encoding='utf-8', newline='\n')

# Driver claims remain in the kernel. Exercise their real producer and arbitration bodies too.
def without_wheel_freeze(code):
    # the frozen-car safety net at the wheel (FreeOwnedCarAtWheel, 2026-11-21) needs the game's freeze holds: this fixture
    # exercises the claims, so an owned car at the wheel is left alone here as before the net
    code = code.replace('{ _frozenAtWheel = null; return; }', 'return;')
    code = code.replace('if (!e.Proxy) { FreeOwnedCarAtWheel(e); return; }', 'if (!e.Proxy) return;')
    code = code.replace('                    _frozenAtWheel = null;\n', '').replace('            _frozenAtWheel = null;\n', '')
    assert '_frozenAtWheel' not in code and 'FreeOwnedCarAtWheel' not in code, 'DriveClaimTick changed: update ExtractFeatureWire.py'
    return code


pickup = (root / 'src/TLDRevamp/Net/Entities.Pickup.cs').read_text(encoding='utf-8')
with target.open('a', encoding='utf-8', newline='\n') as output:
    output.write('namespace TLDRevamp.Net { public static partial class Entities {\n'
                 + '\n'.join(line for line in pickup.splitlines()
                             if 'static long OwnerReconfirmed' in line or 'ReconfirmEveryS =' in line or ' _reconfirmedAt = ' in line) + '\n'
                 + '\n'.join(without_wheel_freeze(method(part, name)) for part, name in (
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

# These scenarios isolate cargo acceptance and token availability; their owner algorithms have separate row tests.
features = (root / 'src/TLDRevamp/Net/Entities.Features.cs').read_text(encoding='utf-8')
features = features.replace('EntityCargoInteraction.ClaimCargo(e, reason: reason)', 'ClaimCargoForFixture(e, reason: reason)')
features = features.replace('EntityCargoInteraction.ClaimCargo(e, released: true, reason: ClaimReason.CargoRelease)', 'ClaimCargoForFixture(e, released: true, reason: ClaimReason.CargoRelease)')
features = features.replace('EntityEditCheck.TakeEditToken(from)', 'TakeKeyTokenForFixture(from)')
target.with_name(target.stem + 'Features.cs').write_text(features, encoding='utf-8')
