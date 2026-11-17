"""Compile the current physics owner against isolated game fixtures."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
PARTS = ('Contact', 'CopyJoints', 'CrashAuthority', 'Footing', 'Physical', 'Riding')
def read(part):
    path = f'src/TLDRevamp/Net/Entities.{part}.cs'
    return (ROOT / path).read_text(encoding='utf-8').replace('\r\n', '\n')


def feature(part):
    s = read(part)
    marker = '    internal static partial class EntityPhysics\n    {\n'
    return s.split(marker, 1)[1].split('\n    }\n', 1)[0] if marker in s else ''


if __name__ == '__main__':
    target = Path(sys.argv[1]); target.parent.mkdir(parents=True, exist_ok=True)
    for part in PARTS:
        text = feature(part)
        # Separate transport, lookup and game dependencies from the owner algorithms.
        text = text.replace('Entities.', 'PhysicsFixture.').replace('PhysicsFixture.Ent', 'Entities.Ent')
        text = text.replace('Fixes.UnfreezeOnGround.Partners', 'PhysicsFixture.TowPartners')
        text = text.replace('EntityHands.EvalShown', 'PhysicsFixture.EvalShown').replace('EntityRagdolls.Following', 'PhysicsFixture.Following')
        text = text.replace('DevHooks.StandSeriesStep', 'PhysicsFixture.StandSeriesStep')
        text = text.replace('SessionLog.Event', 'PhysicsFixture.Event').replace('Mp.PingMs()', 'PhysicsFixture.PingMs()')
        target.with_name(target.stem + part + '.cs').write_text('using System; using System.Collections.Generic; using UnityEngine;\nnamespace TLDRevamp.Net { internal static partial class EntityPhysics {\n' + text + '\n} }\n', encoding='utf-8')
