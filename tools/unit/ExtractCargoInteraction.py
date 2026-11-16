"""Compile the cargo owner against game fixtures; capture 4851eaf before moving."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
NET = ROOT / 'src/TLDRevamp/Net'
PARTS = ('BodyPush', 'Cargo', 'ItemPush', 'Pickup', 'Push', 'PushGrip')
METHODS = {
    'BodyPush': ('BodyNearTick', 'BodyContact', 'Pushable'),
    'Cargo': ('LooseItem', 'CargoContact', 'ClaimCargo', 'TraceClaim', 'ClaimTraceTick', 'PlaceAtNewest',
              'ContentsCandidate', 'InCar', 'SweepMeetsCar', 'OverCar', 'SurfaceUnder', 'OverlapsCar'),
    'ItemPush': ('ItemContact', 'RideVelocity', 'MovingItemBody'),
    'Push': ('PushTick', 'ApplyPush'),
    'PushGrip': ('PushGripTick', 'GripStatus', 'GripPrefabs'),
}
FIELDS = {
    'Cargo': ('LoosePartClaims', 'CargoClaimCooldownS', 'LetGoClaims', 'LetGoClaimS', 'LetGoClaimTries',
              'ClaimTrace', 'ClaimTraceMax', '_trace', 'SeamlessClaims', 'ClaimsPlacedAtNewest', 'ContentsMarginM',
              '_sweepHits', 'ContentsOverCar', '_overHits', 'SurfaceSkipsItems', '_ghostHits', '_ghostCols'),
    'ItemPush': ('RideRelative',),
}
CALLS = {
    'ProxyOf': 'CargoCopyOf', 'CargoBody': 'IsCargoBody', 'RidesParent': 'CargoRidesParent',
    'Limbs': 'CargoLimbs', 'Limp': 'CargoLimp', 'SetProxy': 'SetCargoProxy',
    'WriteClaim': 'WriteCargoClaim', 'ToServer': 'SendCargoRequest', 'OverlappedCar': 'CargoOverlappedCar',
    'CarLocalBounds': 'CargoCarBounds', 'LockedAway': 'CargoLockedAway',
    'IsProxy': 'IsPushCopy', 'EditEntityOf': 'PushEntityOf', 'PlacedOfTransform': 'GripEntityOf',
    'Finite': 'IsFinite', 'NyOf': 'CargoRabbit', 'CarEnt': 'CargoCarEntity', 'IsHeld': 'CargoItemHeld',
}
FACTS = {'InSession': 'CargoInSession', 'MyId': 'CargoLocalId', 'CargoClaims': 'CargoClaimsEnabled'}

def read(part, baseline=False):
    path = f'src/TLDRevamp/Net/Entities.{part}.cs'
    return (subprocess.check_output(['git', 'show', '4851eaf:' + path], cwd=ROOT).decode('utf-8')
            if baseline else (ROOT / path).read_text(encoding='utf-8')).replace('\r\n', '\n')

def member(source, name, kind='method'):
    pattern = (r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'\('
               if kind == 'method' else r'^        private (?:sealed |static )?class ' + name + r'\b')
    m = re.search(pattern, source, re.M)
    if not m:
        raise ValueError(name)
    start = m.start()
    brace = source.find('{', start)
    arrow = source.find('=>', start, brace if brace >= 0 else len(source))
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

def qualify(body):
    body = re.sub(r'\bEnt\b', 'Entities.Ent', body)
    body = re.sub(r'\bClaimReason\b', 'Entities.ClaimReason', body)
    for old, new in CALLS.items():
        body = re.sub(r'(?<![.\w])' + old + r'\(', 'Entities.' + new + '(', body)
    for old, new in FACTS.items():
        body = re.sub(r'(?<![.\w])' + old + r'\b', 'Entities.' + new, body)
    body = body.replace('ClaimsSent++;', 'Entities.CargoClaimSent();')
    body = body.replace('CargoClaimedOnContact++;', 'Entities.CargoContactClaimed();')
    body = body.replace('!ByNet.TryGetValue(carNet, out var car)', '!Entities.TryCargoContactCar(carNet, out var car)')
    body = body.replace('!ByNet.TryGetValue(net, out var mine)', '!Entities.TryItemContactSource(net, out var mine)')
    body = body.replace('!ByNet.TryGetValue(net, out var e)', '!Entities.TryPushTarget(net, out var e)')
    return body

def baseline_feature(part):
    s = read(part, True)
    if part in ('BodyPush', 'PushGrip'):
        s = s[:s.index('    public static partial class Entities')] + s[s.index('    public static partial class Entities'):].replace('public static partial class Entities', 'internal static partial class EntityCargoInteraction', 1)
        if part == 'BodyPush':
            s = s[:s.index('    /// On the local player')].rstrip() + '\n}\n'
    else:
        selected = []
        for name in FIELDS.get(part, ()):
            selected.append(next(line for line in s.splitlines() if re.search(r'\b' + name + r'\b', line) and ' static ' in line))
        if part == 'Cargo':
            selected += ['        public static long CargoClaimed;', '        private static int _claimTraces;']
        if part == 'ItemPush':
            # Item scanning remains in the kernel and keeps its settings and scratch buffer.
            pass
        if part == 'Push':
            selected += ['        private static readonly NetWriter W = new NetWriter();',
                         next(line for line in s.splitlines() if 'public static long PushesSent' in line),
                         member(s, 'PushAcc', 'class'),
                         next(line for line in s.splitlines() if 'Dictionary<pushablescript, PushAcc> PushOut' in line),
                         '        private static float _pushSendAcc;',
                         '        [HarmonyLib.HarmonyPatch(typeof(pushablescript), nameof(pushablescript.Push))]\n' + member(s, 'PushToOwner', 'class')]
        selected += [member(s, name) for name in METHODS[part]]
        s = 'using System.Collections.Generic; using UnityEngine;\nnamespace TLDRevamp.Net {\n    internal static partial class EntityCargoInteraction\n    {\n' + '\n'.join(selected) + '\n    }\n}\n'
    s = qualify(s)
    # The generic transform above must not qualify the retained class name in the declaration.
    for name in METHODS[part]:
        s = re.sub(r'\b(private|public|internal) static ([^\n]+?) ' + name + r'\(', r'internal static \2 ' + name + '(', s)
    if part == 'ItemPush':
        s = s.replace('ItemClaims', 'Entities.CargoItemClaims').replace('ItemMinSpeed', 'Entities.CargoItemMinSpeed')
        s = s.replace('ItemClaimedOnContact++;', 'Entities.ItemContactClaimed();')
    if part == 'Push':
        s = s.replace('W.U8(PushIn)', 'W.U8(Entities.PushMessage)')
    return s

def legacy_body(body):
    for old, new in CALLS.items():
        body = body.replace('Entities.' + new + '(', old + '(')
    for old, new in FACTS.items():
        body = body.replace('Entities.' + new, old)
    body = body.replace('Entities.Ent', 'Ent').replace('Entities.ClaimReason', 'ClaimReason')
    body = body.replace('Entities.CargoClaimSent();', 'ClaimsSent++;').replace('Entities.CargoContactClaimed();', 'CargoClaimedOnContact++;')
    body = body.replace('Entities.ItemContactClaimed();', 'ItemClaimedOnContact++;')
    body = body.replace('Entities.CargoItemClaims', 'ItemClaims').replace('Entities.CargoItemMinSpeed', 'ItemMinSpeed')
    body = body.replace('Entities.PushMessage', 'PushIn')
    for name, key in (('TryCargoContactCar', 'carNet'), ('TryItemContactSource', 'net'), ('TryPushTarget', 'net')):
        body = body.replace('Entities.' + name + '(' + key + ',', 'ByNet.TryGetValue(' + key + ',')
    return body

def normal(body):
    return re.sub(r'\s+', '', re.sub(r'\b(?:public|private|internal)\b', '', legacy_body(body)))

def source_rows(baseline):
    rows = []
    for part in PARTS:
        s = read(part, baseline)
        # Freeze every method in all six files, including the authority and map loops left on the kernel.
        for m in re.finditer(r'^        (?:public|private|internal) static [^\n]*?\b(\w+)\(', s, re.M):
            if '=' in m[0]:
                continue
            name = m.group(1)
            if part == 'Push' and name == 'Finite' and '=> EntityNumbers.Finite(' in member(s[m.start():], name):
                # Slice 30 compares the original bodies on EntityNumbers, not the compatibility forwarders too.
                continue
            if not baseline and name in METHODS.get(part, ()):
                # The feature body occurs before its forwarding facade.
                if any(row.startswith(part + '.' + name + '=') for row in rows):
                    continue
            rows.append(part + '.' + name + '=' + hashlib.sha256(normal(member(s[m.start():], name)).encode()).hexdigest().upper())
        if part == 'Push':
            patch = member(s, 'PushToOwner', 'class')
            if not baseline:
                patch = patch.replace('[HarmonyLib.HarmonyPrefix, HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Normal + 1)]', '[HarmonyLib.HarmonyPrefix]')
            rows.append(part + '.PushToOwner=' + hashlib.sha256(normal(patch).encode()).hexdigest().upper())
        if part == 'Pickup':
            rows.append('Pickup.file=' + hashlib.sha256(s.encode()).hexdigest().upper())
    return rows

def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8', newline='\r\n')

def main():
    if '--capture-source' in sys.argv:
        write(ROOT / 'tools/unit/EntityCargoInteractionSourceRows.txt', '\n'.join(source_rows(True)) + '\n')
        return
    target = Path(sys.argv[1])
    baseline = '--baseline' in sys.argv
    features = []
    for part in METHODS:
        s = baseline_feature(part) if baseline else read(part).split('    public static partial class Entities')[0]
        if not baseline and part == 'Push':
            s = s.split('    /// Numbers received by physics, clocks and health, independent of session identity.')[0]
        if not baseline:
            s = s.rstrip() + '\n}\n'
        features.append(s)
    # Separate files retain their original using scopes.
    for i, s in enumerate(features):
        write(target.with_name(target.stem + str(i) + '.cs'), s)
    release = member(read('Cargo', baseline), 'ClaimLetGoInCar').replace('ClaimLetGoInCar(', 'CargoTestLetGo(')
    for name in ('LooseItem', 'OverlapsCar', 'ClaimCargo'):
        release = release.replace(name + '(', 'EntityCargoInteraction.' + name + '(')
    visible = read('VisibleState', baseline)
    helpers = [release, member(visible, 'LetGoInCar'), member(read('Push', baseline), 'ServerPush').replace('ServerPush(', 'CargoTestServerPush(')]
    write(target.with_name(target.stem + 'Kernel.cs'), 'using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + '\n'.join(helpers) + '\n} }\n')
    if not baseline:
        expected = (ROOT / 'tools/unit/EntityCargoInteractionSourceRows.txt').read_text().splitlines()
        actual = source_rows(False)
        # File reordering is deliberate; each named body must still match its frozen HEAD body.
        if sorted(expected) != sorted(actual):
            raise AssertionError('\n'.join(sorted(set(expected) ^ set(actual))))

if __name__ == '__main__':
    main()
