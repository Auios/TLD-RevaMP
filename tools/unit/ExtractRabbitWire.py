"""Capture unchanged HEAD rabbit send bodies before assigning their new owner."""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
target = Path(sys.argv[1])
if '--baseline' not in sys.argv:
    target.write_text('// HEAD byte capture is opt-in with RabbitWireBaseline=--baseline.\n', encoding='utf-8')
    raise SystemExit(0)

def method(file, name):
    source = subprocess.check_output(['git', 'show', '73f02e3:src/TLDRevamp/Net/' + file], cwd=root).decode()
    start = source.rfind('\n', 0, re.search(r'(?:private|internal|public) static \w+ ' + name + r'\(', source).start()) + 1
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

parts = [method('Entities.Rabbits.cs', name) for name in ['RabFlags', 'SendRabbitFx', 'RabbitRemoteBite', 'RabbitDamageFlush', 'RabbitBreakOnCopy', 'RabbitBreakFlush']]
parts += [method('Entities.RabbitRole.cs', 'SendRabbitHandover')]
target.write_text('using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities { internal static class RabbitBaseline {\n'
    + '''public const byte RabbitState = 90, RabbitFx = 91;
    static bool RabbitSync = true, _rabTrigger;
    static long RabbitStatesSent, RabbitFxSent, RabbitDamageSent, RabbitBreakHitsSent, RabbitBreakHitsSkipped;
    static float _rabDmgFlushAt, _rabBreakFlushAt;
    static ref float NyLastSee(nyulscript n) => ref n.lastSeeTarget;
    static ref bool NyEvil(nyulscript n) => ref n.alreadyEvil;
    static readonly Dictionary<nyulscript, bool> RabGreen = new Dictionary<nyulscript, bool>();
    static Ent RabEnt(nyulscript n) => ByNet[301];
    static readonly Dictionary<int, float> _rabDmg = new Dictionary<int, float>();
    static readonly Dictionary<int, Vector3> _rabDmgDir = new Dictionary<int, Vector3>(), _rabDmgAt = new Dictionary<int, Vector3>();
    static readonly Dictionary<int, uint> _rabDmgNet = new Dictionary<int, uint>();
    sealed class RabHit { public float Sum; public int N; }
    static readonly Dictionary<breakablescript, RabHit> _rabHits = new Dictionary<breakablescript, RabHit>();
    ''' + '\n'.join(parts) + '\n} } }', encoding='utf-8')
