"""Compile diagnostic projections and capture their original bodies before moving them."""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
baseline = '--baseline' in sys.argv

def read(path):
    return subprocess.check_output(['git', 'show', 'HEAD:' + path], cwd=root).decode('utf-8') if baseline else (root / path).read_text(encoding='utf-8')

def method(source, name):
    found = re.search(r'^        (?:internal|private|public) static [^\n(]*?\b' + name + r'\(', source, re.M)
    start = found.start()
    brace, arrow = source.find('{', start), source.find('=>', start)
    if arrow >= 0 and (brace < 0 or arrow < brace): return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

output = [read("src/TLDRevamp/Net/Entities.PartWatch.cs").replace("EntityMountFacts.MountFacts(", "DiagMountFactsFixture.MountFacts(")]
for path, names, owner in [
    ('Entities.Telemetry.cs', ['TelemetryPose', 'TelemetryRootCategory', 'TelemetryTrace'], 'EntityTelemetry'),
    ('Entities.Anomaly.cs', ['CensusRows', 'CensusHash'], 'EntityDiag'),
    ('TelemetryMarks.cs', ['CaptureUsable', 'CaptureStations', 'CaptureSlot'], 'EntityTelemetry')]:
    source = read('src/TLDRevamp/Net/' + path)
    if not baseline:
        # The kernel slot wrapper retains the identity lookups before the detached projection.
        if path == 'TelemetryMarks.cs':
            output.append('namespace TLDRevamp.Net { public static partial class Entities {\n' + method(source.split('    public static partial class Entities', 1)[1], 'CaptureSlot') + '\n} }')
        source = source.split('    public static partial class Entities', 1)[0]
    code = '\n'.join(method(source, name) for name in names)
    actual = 'Entities' if baseline else owner
    if not baseline: code = re.sub(r'(?<![.\w])Ent\b', 'Entities.Ent', code)
    output.append('namespace TLDRevamp.Net { ' + ('public' if baseline else 'internal') + ' static partial class ' + actual + ' {\n' + code + '\n} }')

# Use the actual detached snapshot schema, budget, Add methods and JSON writer, not a fixture serializer.
source = read('src/TLDRevamp/TelemetryData.cs')
start = source.index('    internal struct StateEntity')
end = source.index('\n    }', source.index('    internal sealed class TelemetryState')) + len('\n    }')
output.append('namespace TLDRevamp {\n' + source[start:end] + '\n}')
# Pose rows are detached values too.
source = read('src/TLDRevamp/TelemetryHistory.cs')
start = source.index('    internal struct MotionRow')
end = source.index('\n    }', start) + len('\n    }')
output.append('namespace TLDRevamp {\n' + source[start:end] + '\n}')

# New source captures pin retained transactions as well as branches outside the fixed fixtures.
# Capture reads immutable HEAD; ordinary clean builds need only the checked-in rows and current source.
import hashlib

def canonical(code):
    code = re.sub(r'(?m)^\s*///?[^\n]*', '', code)
    code = code.replace('Entities.PartReach', 'PartReach').replace('Entities.Ent', 'Ent')
    for old, new in [('Entities.PartWatchGroup(', 'EditEntityOf('), ('Entities.NetOfRoot(', 'NetOfRoot('),
                     ('Entities.StatusHeld(', 'IsHeld('), ('Entities.StatusStored(', 'IsStored('),
                     ('Entities.StoredPos(', 'StoredPos('), ('Entities.V3(', 'V3(')]:
        code = code.replace(old, new)
    # callers name the moved members' real owner (no forwarders on Entities); the pre-move bodies called them unqualified
    code = re.sub(r'\b(?:EntityDiag|EntityTelemetry)\.(PartWatchNearbyM|PartWatchFlags|PartWatchLast|CollidersExpected|Reach|'
                  r'NearParts|Shows|PartsDiagnostics|CensusRows|CensusHash|TelemetryPose|TelemetryRootCategory|TelemetryTrace|'
                  r'CaptureUsable|CaptureStations)\b', r'\1', code)
    code = re.sub(r'\b(?:internal|private|public) static ', 'static ', code)
    return '\n'.join(line.strip() for line in code.splitlines() if line.strip())

sources = []
def pin(name, code): sources.append(name + '=' + hashlib.sha256(canonical(code).encode()).hexdigest())
for part, names in [
    ('Anomaly', ['AnomalyJson', 'AnomalyLine', 'Anomaly', 'NearestPlayerM', 'AnomalyStoreOut', 'AnomalyPlaced', 'AnomalyTick', 'ServerCensusAsk', 'ApplyCensusAsk', 'ServerCensusReply', 'ApplyCensusReply']),
    ('Telemetry', ['ResetTelemetry', 'TelemetryTouch', 'TelemetryNet', 'TelemetryFocusTick', 'TelemetryRender', 'TelemetryUsable', 'TelemetryReceived', 'TelemetryPhysicsTick', 'TelemetryPhysics', 'TelemetryProxySnap', 'TelemetrySentTeleport', 'TelemetryCategory', 'TelemetryPrefabCategory', 'TelemetryPacketCategory', 'TelemetryOwned', 'TelemetryAtRest', 'TelemetryCensus'])]:
    source = read('src/TLDRevamp/Net/Entities.' + part + '.cs')
    for name in names: pin(part + '.' + name, method(source, name))
source = read('src/TLDRevamp/Net/TelemetryMarks.cs')
for name in ['CaptureTelemetry', 'TelemetryRecord', 'CaptureTelemetryState', 'CaptureCarried', 'CaptureCarry', 'CaptureUsables', 'TelemetryParent', 'CaptureStoredSlots']:
    pin('Marks.' + name, method(source, name))
for part, names in [('Entities.Anomaly.cs', ['CensusRows', 'CensusHash']),
                    ('Entities.Telemetry.cs', ['TelemetryPose', 'TelemetryRootCategory', 'TelemetryTrace']),
                    ('TelemetryMarks.cs', ['CaptureUsable', 'CaptureStations', 'CaptureSlot']),
                    ('Entities.PartWatch.cs', ['CollidersExpected', 'Reach', 'NearParts', 'Shows', 'PartsDiagnostics'])]:
    source = read('src/TLDRevamp/Net/' + part)
    for name in names:
        code = method(source, name)
        if name == 'NearParts' and not baseline:
            code = code.replace('Entities.VisitEnts(e =>', 'foreach (var e in ByNet.Values)').replace('groupR * groupR) return;', 'groupR * groupR) continue;')
            code = code.rsplit('});', 1)[0] + '}' + code.rsplit('});', 1)[1]
        if name == 'CaptureSlot':
            if not baseline:
                code = code.replace(', tosaveitemscript it, Entities.Ent group)', ')')
                code = code.replace('{\n', '{\n            var it = at != null ? at.tosave : null;\n', 1)
            code = code.replace('            var group = it != null ? OwnEntity(it) ?? SharedGroupOf(it) : null;\n', '')
        pin(part + '.' + name, code)
source = read('src/TLDRevamp/Net/Entities.Status.cs')
status = method(source, 'Status')
if baseline:
    start = status.index('                var g = e.Root')
    end = status.index('\n            }\n            return', start)
    row = status[start:end].replace('rows.Add(', 'return ', 1)
    row = row[:-2] + ';'
    status = status[:start] + '                rows.Add(EntityDiag.StatusRow(e));' + status[end:]
else:
    row = method(source, 'StatusRow').split('{', 1)[1].rsplit('}', 1)[0]
pin('Status.row', row)
pin('Status.selection-and-summary', status)
pin('Status.velocity', method(source, 'Vel'))
if '--capture-source' in sys.argv:
    if not baseline: raise SystemExit('source capture requires --baseline')
    (root / 'tools/unit/EntityDiagSourceRows.txt').write_text('\n'.join(sources) + '\n', encoding='utf-8')
output.append('internal static class EntityDiagSources { internal static readonly string[] Rows = new[] {\n' + ',\n'.join('"' + row + '"' for row in sources) + '\n}; }')
target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text('using System; using System.Collections.Generic; using System.IO; using UnityEngine;\n' + '\n'.join(output), encoding='utf-8', newline='\n')
