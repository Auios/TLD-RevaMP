"""Compile the physics owner and freeze the 150bfb1 extraction boundary."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
PARTS = ('Contact', 'CopyJoints', 'CrashAuthority', 'Footing', 'Physical', 'Riding')
METHODS = {
    'Contact': ('AttachContact', 'DetachContact', 'ApplyContactImpulse'),
    'CopyJoints': ('CopyJointTick', 'WantJointFree', 'SetJointFree'),
    'CrashAuthority': ('WaitingApart', 'AfterHeld', 'AfterTick', 'TryTakeCrash', 'CrashHoldTick', 'CopyWhereOwnerHasIt', 'Refuse', 'EndHeld', 'CrashAuthorityStatus'),
    'Footing': ('StandInterpTick', 'StandInterpOff', 'StandsOnAtPhysics'),
    'Physical': ('LocalAboard', 'HealCopyBody', 'SetPartColliders', 'LimbsByBody', 'MoveLimbs', 'StopBodyMotion', 'EnterPhysical', 'ExitPhysical', 'DropPhysical', 'Servo', 'Ghost', 'Apart', 'FreeSeconds'),
    'Riding': (),
}
CLASSES = {'Contact': ('ContactRelay',), 'CrashAuthority': ('HeldCrash', 'AfterCrash'), 'Physical': ('CopyWheelTorques', 'CopyContact')}
CALLS = {'PlacedOfTransform': 'PhysicsRoot', 'NetOf': 'PhysicsNetOf', 'HitchTrailer': 'PhysicsTrailer',
         'WheelsOf': 'PhysicsWheels', 'CameOff': 'PhysicsPartCameOff', 'Limbs': 'PhysicsLimbs',
         'TelemetryProxySnap': 'PhysicsSnap', 'IsProxy': 'PhysicsIsProxy', 'SetProxy': 'SetCrashProxy',
         'ToServer': 'SendPhysicsRequest', 'Resolve': 'ResolvePhysics'}

def read(part, baseline=False):
    path = f'src/TLDRevamp/Net/Entities.{part}.cs'
    return (subprocess.check_output(['git', 'show', '150bfb1:' + path], cwd=ROOT).decode('utf-8')
            if baseline else (ROOT / path).read_text(encoding='utf-8')).replace('\r\n', '\n')

def members(source, name, kind='method'):
    pattern = (r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'\('
               if kind == 'method' else r'^        (?:public|private|internal) (?:sealed |static )?class ' + name + r'\b')
    result = []
    for m in re.finditer(pattern, source, re.M):
        start = m.start()
        brace = source.find('{', m.end())
        arrow = source.find('=>', m.end(), brace if brace >= 0 else len(source))
        if arrow >= 0:
            end = source.index(';', arrow) + 1
        else:
            depth, end = 1, brace + 1
            while depth:
                depth += (source[end] == '{') - (source[end] == '}')
                end += 1
        # Annotations belong to their class.
        if kind == 'class':
            line = source.rfind('\n', 0, start - 1) + 1
            if source[line:start].lstrip().startswith('[Harmony'):
                start = line
        result.append(source[start:end])
    return result

def fields(part, source):
    lines = []
    for line in source.splitlines():
        if not line.startswith('        ' ) or line.startswith('         '):
            continue
        if part == 'Contact' and re.match(r'        (?:public static (?:long|float)|internal const float) ', line): lines.append(line)
        if part == 'CopyJoints' and (' static ' in line and not re.search(r'\b(?:void|bool) \w+\(', line)): lines.append(line)
        if part == 'CrashAuthority' and (line.startswith('        public static ') and not ('(' in line.split('//')[0] or '=>' in line.split('//')[0]) or re.match(r'        private static readonly List<(?:HeldCrash|AfterCrash)>', line)): lines.append(line)
        if part == 'Footing' and re.match(r'        (?:public static (?:long StandInterpFrames|bool StandCopyAtPhysics)|private static bool _standInterp)', line): lines.append(line)
        if part == 'Physical' and (re.match(r'        public static (?:bool|float|long|int|string) ', line) and not re.search(r'\b\w+\(', line.split('//')[0]) or 'private static readonly Dictionary<carscript, Ent> PhysicalCars' in line): lines.append(line)
    return lines

def qualify(text):
    text = re.sub(r'(?<![.\w])Ent\b', 'Entities.Ent', text)
    for old, new in CALLS.items():
        text = re.sub(r'(?<![.\w])' + old + r'\(', 'Entities.' + new + '(', text)
    for fact in ('InSession', 'ContactImpulse', 'CrashClaim', 'CrashRelease'):
        text = re.sub(r'(?<![.\w])' + fact + r'\b', 'Entities.' + fact, text)
    text = text.replace('CargoContact(NetId, c); RabbitContact(NetId, c); ItemContact(NetId, c);', 'Entities.PhysicsContacts(NetId, c);')
    text = text.replace('ByNet.TryGetValue(net, out var e)', 'Entities.TryPhysicsContact(net, out var e)')
    text = text.replace('_telemetryFooting', 'Entities.PhysicsFootingNet')
    text = text.replace('ByNet.TryGetValue(Entities.PhysicsFootingNet, out var e)', 'Entities.TryPhysicsStand(Entities.PhysicsFootingNet, out var e)')
    # Facades need these methods; helpers that have no compiled callers keep their visibility.
    for name in sum((list(v) for v in METHODS.values()), []):
        text = re.sub(r'private static ([^\n]*?\b' + name + r'\()', r'internal static \1', text)
    text = text.replace('internal static void AfterHeld', 'private static void AfterHeld')
    return text

def baseline_feature(part):
    s = read(part, True)
    selected = fields(part, s)
    for name in CLASSES.get(part, ()): selected += members(s, name, 'class')
    for name in METHODS[part]: selected += members(s, name)
    if part == 'Contact': selected.insert(0, '        private static readonly NetWriter W = new NetWriter();')
    return qualify('\n\n'.join(selected))

def feature(part):
    s = read(part)
    marker = '    internal static partial class EntityPhysics\n    {\n'
    return s.split(marker, 1)[1].split('\n    }\n', 1)[0] if marker in s else ''

def digest(s):
    return hashlib.sha256(re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', s)).encode()).hexdigest()

def cleanup(part):
    kernel = subprocess.check_output(['git', 'show', '150bfb1:src/TLDRevamp/Net/Entities.cs'], cwd=ROOT).decode('utf-8').replace('\r\n', '\n')
    if part == 'CopyJoints':
        original = re.search(r'            foreach \(var kv in _freedJoints\)[^\n]*\n            _freedJoints.Clear\(\);', kernel)[0]
        return ('        internal static void BeginJointRefresh() => _jointed.Clear();\n'
                '        internal static void AddJointed(Entities.Ent entity) => _jointed.Add(entity);\n'
                '        internal static void ResetFreedJoints()\n        {\n' + original + '\n        }')
    if part == 'CrashAuthority':
        original = re.search(r'_held.Clear\(\); _after.Clear\(\);', kernel)[0]
        return '        internal static void ResetCrashHolds() { ' + original + ' }'
    if part == 'Physical':
        original = re.search(r'PhysicalCars.Clear\(\);', kernel)[0]
        return '        internal static void ResetPhysicalCars() => ' + original
    if part == 'Footing':
        original = re.search(r'            if \(_standInterp\) StandInterpOff[^\n]*', kernel)[0]
        return '        internal static void ResetStandInterpolation()\n        {\n' + original + '\n        }'
    return ''

def source_rows():
    rows = []
    for part in PARTS:
        s = read(part, True)
        rows.append((part + '-feature', digest(baseline_feature(part))))
        if cleanup(part): rows.append((part + '-cleanup', digest(cleanup(part))))
        for m in re.finditer(r'^        (?:public|private|internal) static [^\n]*?\b(\w+)\(', s, re.M):
            name = m[1]
            if name in METHODS[part]: continue
            for i, body in enumerate(members(s, name)): rows.append((part + '.' + name + str(i), digest(body)))
    for name in ('Tick', 'ServerOnLeave', 'ServerReceive', 'ClientReceive'):
        kernel = subprocess.check_output(['git', 'show', '150bfb1:src/TLDRevamp/Net/Entities.cs'], cwd=ROOT).decode('utf-8').replace('\r\n', '\n')
        rows.append(('Kernel.' + name, digest(members(kernel, name)[0])))
    rows.append(('Physical.rebase-class', digest(members(read('Physical', True), 'SeatCopiesAfterRebase', 'class')[0])))
    return rows

if __name__ == '__main__':
    if '--capture-source' in sys.argv:
        (ROOT / 'tools/unit/EntityPhysicsSourceRows.txt').write_bytes(('\r\n'.join(n + '=' + h for n, h in source_rows()) + '\r\n').encode())
        sys.exit(0)
    target = Path(sys.argv[1]); target.parent.mkdir(parents=True, exist_ok=True)
    baseline = '--baseline' in sys.argv
    expected = dict(line.split('=') for line in (ROOT / 'tools/unit/EntityPhysicsSourceRows.txt').read_text().splitlines())
    if not baseline:
        kernel = (ROOT / 'src/TLDRevamp/Net/Entities.cs').read_text(encoding='utf-8')
        for name in ('Tick', 'ServerOnLeave', 'ServerReceive', 'ClientReceive'):
            assert digest(members(kernel, name)[0]) == expected['Kernel.' + name], name + ' kernel behavior changed'
        assert digest(members(read('Physical'), 'SeatCopiesAfterRebase', 'class')[0]) == expected['Physical.rebase-class'], 'retained rebase patch changed'
    for part in PARTS:
        text = baseline_feature(part) + ('\n        // Session cleanup.\n' + cleanup(part) if cleanup(part) else '') if baseline else feature(part)
        if not baseline:
            # Cleanup is compared with the original statements in the kernel reset.
            original = text.split('        // Session cleanup.', 1)[0]
            assert digest(original) == expected[part + '-feature'], part + ' feature differs from HEAD adapter'
            if cleanup(part):
                assert digest(text.split('        // Session cleanup.', 1)[1]) == expected[part + '-cleanup'], part + ' cleanup differs from HEAD statements'
            for key, value in expected.items():
                if key.startswith(part + '.') and key != 'Physical.rebase-class':
                    name = key.split('.')[1][:-1]; index = int(key[-1])
                    actual = members(read(part), name)[index]
                    if key == 'CopyJoints.RefreshJointed0':
                        actual = actual.replace('EntityPhysics.BeginJointRefresh()', '_jointed.Clear()').replace('EntityPhysics.AddJointed(e)', '_jointed.Add(e)')
                    assert digest(actual) == value, key + ' retained method changed'
        # Separate transport, lookup and game dependencies from the owner algorithms.
        text = text.replace('Entities.', 'PhysicsFixture.').replace('PhysicsFixture.Ent', 'Entities.Ent')
        text = text.replace('Fixes.UnfreezeOnGround.Partners', 'PhysicsFixture.TowPartners')
        text = text.replace('EntityHands.EvalShown', 'PhysicsFixture.EvalShown').replace('EntityRagdolls.Following', 'PhysicsFixture.Following')
        text = text.replace('DevHooks.StandSeriesStep', 'PhysicsFixture.StandSeriesStep')
        text = text.replace('SessionLog.Event', 'PhysicsFixture.Event').replace('Mp.PingMs()', 'PhysicsFixture.PingMs()')
        target.with_name(target.stem + part + '.cs').write_text('using System; using System.Collections.Generic; using UnityEngine;\nnamespace TLDRevamp.Net { internal static partial class EntityPhysics {\n' + text + '\n} }\n', encoding='utf-8')
