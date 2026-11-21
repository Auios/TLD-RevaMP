"""Compile the eleven real host paths with deterministic game and transport seams."""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'
baseline = '--baseline' in sys.argv

def read(part):
    path = net / ('Entities' + ('.' + part if part else '') + '.cs')
    if baseline:
        return subprocess.check_output(['git', 'show', '165fb43:' + path.relative_to(root).as_posix()], cwd=root).decode('utf-8')
    return path.read_text(encoding='utf-8')

def block(s, start):
    brace = s.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (s[end] == '{') - (s[end] == '}')
        end += 1
    return s[start:end]

def method(s, name):
    m = re.search(r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'\(', s, re.M)
    assert m, name
    return block(s, m.start())

paths = {'Pickup': ['ServerClaim'], 'Features': ['WornTakesBack', 'ChooseRabbitOwner'],
         'Handoff': ['ServerHandoffTick'], 'Attach': ['ServerAttach', 'CascadeOwner'],
         'CrashAuthority': ['ServerCrashClaim', 'ReturnCrash'],
         '': ['ServerTakeSilent', 'ServerOnLeave', 'RefuseClaim', 'ServerSendAll', 'ServerSendTo'], 'Hitch': ['CompleteHitchTransfer']}
output = []
for part, token in (('Attach', 'public const byte AttachSync'), ('Features', 'public const byte HitchRequest')):
    output.append(next(line for line in read(part).splitlines() if token in line))
for part, names in paths.items():
    output.extend(method(read(part), name) for name in names)
output.append(block(read(''), read('').index('        private sealed partial class SEnt')))
pickup = read('Pickup')
output.append(block(pickup, pickup.index('        internal enum ClaimReason')))
output.extend(line for line in pickup.splitlines() if any(t in line for t in ('static long OwnerReconfirmed', 'ReconfirmEveryS =', ' _reconfirmedAt = ', 'static string ClaimName')))
owner = net / 'Entities.OwnerChange.cs'
if owner.exists():
    output.append(owner.read_text(encoding='utf-8').split('public static partial class Entities', 1)[1].strip()[1:].rsplit('}', 2)[0])
target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
source = 'using System; using System.Collections.Generic; using System.Globalization; using System.Text; using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + '\n'.join(output) + '\n} }'
source = re.sub(r'\bmainscript\b', 'OwnerChangeMain', source)
json = (root / 'src/TLDRevamp/TelemetryData.cs').read_text(encoding='utf-8')
source += '\nnamespace TLDRevamp {\n' + block(json, json.index('    internal static class Json')) + '\n}\n'
target.write_text(source, encoding='utf-8', newline='\r\n')
