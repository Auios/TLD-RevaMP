"""Freeze slice 26 from 150bfb1 and compile its presentation owner with isolated fixtures."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'
parts = ('CopyLod', 'Proxy', 'VisibleState')

def read(part, baseline=False):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return subprocess.check_output(['git', 'show', '150bfb1:' + path], cwd=root).decode() if baseline else (root / path).read_text(encoding='utf-8')

def member(source, name, kind='method'):
    pattern = (r'^        (?:private|internal|public) static [^\n]*?\b' + name + r'\(' if kind == 'method'
               else r'^        private static class ' + name + r'\b')
    start = re.search(pattern, source, re.M).start()
    opening = source.find('{', start)
    arrow = source.find('=>', start, opening)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

retained = {
    'CopyLod': ('CopyCars', 'CopyCarCount'),
    'Proxy': ('IsProxy', 'ProxyVelocity', 'NearOtherPlayersCar', 'HandOver'),
}
# These indexes participate in the retained SetProxy/ApplyProxyBodies/reset transactions.
fields = ('ProxyItems', 'DamageGraceUntil', '_applyingDetach', 'ReplayBareDetach', '_handFor', '_handAt',
          'AutoDetachesIgnored', 'ProxyByOwnRoot', '_proxyByRoot', '_proxyByRootFrame', 'DetachReqsSent',
          'PartsReported', '_claimedFor', 'PartRejects', 'ProxyEngineOwner', 'ProxyWheelOwner', 'ProxyWheelIndex',
          '_playerActing', 'PlayerActing', 'HandOverS', 'HandedOver')

def baseline_parts():
    result = {}
    for part in parts:
        s = read(part, True).replace('\r\n', '\n')
        s = s.split('    public static partial class Entities\n    {\n', 1)[1].rsplit('    }', 1)[0]
        for name in retained.get(part, ()):
            s = s.replace(member(s, name), '')
        if part == 'Proxy':
            s = s.replace(member(s, 'PlayerActScope', 'class'), '')
            s = s.replace('        [HarmonyLib.HarmonyPatch(typeof(fpscontroller), "Update")]\n', '')
            for field in fields:
                s = re.sub(r'^        (?:internal|private|public) static [^\n]*\b' + field + r'\b[^\n]*\n', '', s, flags=re.M)
            s = s.replace('public static long SuppressedDamage, SuppressedFallOff;', 'public static long SuppressedDamage;')
            # SuppressedFallOff belongs to the parallel lifetime slice; no moved facade for it.
            replacements = {
                'IsProxy(': 'Entities.IsPresentationCopy(', 'OwnEntity(': 'Entities.PresentationEntity(',
                'DamageGraceUntil.Count > 0': 'Entities.PresentationHasDamageGrace',
                'DamageGraceUntil.TryGetValue(': 'Entities.TryPresentationDamageGrace(',
                'DamageGraceUntil.Remove(': 'Entities.RemovePresentationDamageGrace(',
                'NearOtherPlayersCar(': 'Entities.PresentationNeedsTorques(',
                'BindProxyEngine(': 'Entities.BindPresentationEngine(',
                'ProxyEngineOwner.Count == 0 || !ProxyEngineOwner.TryGetValue(': '!Entities.TryPresentationEngine(',
                'ProxyWheelOwner.Count == 0 || !ProxyWheelOwner.TryGetValue(': '!Entities.TryPresentationWheel(',
                'ProxyWheelIndex[__instance]': 'Entities.PresentationWheelIndex(__instance)',
            }
            for old, new in replacements.items(): s = s.replace(old, new)
        if part == 'CopyLod': s = s.replace('CopyCars()', 'Entities.PresentationCars()')
        if part == 'VisibleState':
            for name in ('Limbs', 'Limp'): s = re.sub(r'\b' + name + r'\(', 'Entities.Presentation' + name + '(', s)
            for name in ('ReadStateHead', 'WriteStateHead'): s = re.sub(r'\b' + name + r'\(', 'EntityCodec.' + name + '(', s)
        if part == 'VisibleState':
            s = re.sub(r'^        private static ', '        internal static ', s, flags=re.M)
        if part == 'Proxy':
            s = re.sub(r'^        private static (void (?:WriteWheels|ReadWheels|WriteEngine|ReadEngine)\()', r'        internal static \1', s, flags=re.M)
        result[part] = s
    return result

def digest(s): return hashlib.sha256(re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', s)).encode()).hexdigest()

def fixture(s):
    return s

def visible_facade(original=None):
    # Normal runs build the facade from the current owner (its moved methods keep their signatures), so they need no
    # git history (CI checks out one commit); capture/baseline runs still read the captured commit.
    original = (original if original is not None else read('VisibleState', True)).replace('\r\n', '\n')
    result = '        private static bool ReadCompactMoving { get => EntityProxyPresentation.ReadCompactMoving; set => EntityProxyPresentation.ReadCompactMoving = value; }\n'
    for match in re.finditer(r'^        (private|internal) static (?!bool ReadCompactMoving)[^\n]*?\b(\w+)\(', original, re.M):
        name = match[2]
        if name in ('CargoBody', 'AttachedCarPart', 'LetGoInCar', 'FinalSnapshot', 'LooseRelease'): continue
        text = member(original, name)
        end = text.index('=>') if '=>' in text.split('{', 1)[0] else text.index('{')
        signature = text[:end].rstrip()
        args = []
        for arg in signature[signature.index('(') + 1:signature.rindex(')')].split(','):
            words = arg.strip().split('=')[0].strip().split()
            args.append(('ref ' if 'ref' in words else 'out ' if 'out' in words else '') + words[-1])
        result += signature + ' => EntityProxyPresentation.' + name + '(' + ', '.join(args) + ');\n'
    return result

if __name__ == '__main__':
    if '--capture-source' in sys.argv:
        rows = [p + '=' + digest(s) for p, s in baseline_parts().items()]
        for p, names in retained.items():
            for name in names: rows.append(p + '.' + name + '=' + digest(member(read(p, True), name)))
        rows += [p + '=' + digest(read(p, True)) for p in ('EngineBinding',)]
        rows.append('PlayerActScope=' + digest(member(read('Proxy', True), 'PlayerActScope', 'class')))
        (root / 'tools/unit/EntityProxyPresentationSourceRows.txt').write_bytes(('\r\n'.join(rows) + '\r\n').encode())
        sys.exit()
    baseline = '--baseline' in sys.argv
    sources = baseline_parts() if baseline else {p: read(p).split('    internal static partial class EntityProxyPresentation\n    {\n')[1].split('\n    }\n')[0] for p in parts}
    if not baseline:
        rows = dict(l.split('=') for l in (root / 'tools/unit/EntityProxyPresentationSourceRows.txt').read_text().splitlines())
        for part, s in sources.items(): assert digest(s) == rows[part], part + ' differs from captured HEAD adapter'
        for part, names in retained.items():
            for name in names: assert digest(member(read(part), name)) == rows[part + '.' + name], name + ' retained behavior changed'
        assert digest(read('EngineBinding')) == rows['EngineBinding'], 'engine index transaction changed'
        assert digest(member(read('Proxy'), 'PlayerActScope', 'class')) == rows['PlayerActScope'], 'player acting scope changed'
    target = Path(sys.argv[1]); target.parent.mkdir(parents=True, exist_ok=True)
    for part, s in sources.items():
        target.with_name(target.stem + part + '.cs').write_text('using System; using System.Collections.Generic; using UnityEngine; using UnityEngine.Rendering; using Ent = TLDRevamp.Net.Entities.Ent; using Object = UnityEngine.Object;\nnamespace TLDRevamp.Net { internal static partial class EntityProxyPresentation {\n' + fixture(s) + '\n} }\n', encoding='utf-8')
    target.with_name(target.stem + 'Facade.cs').write_text('using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + visible_facade(None if baseline else sources['VisibleState']) + '\n} }\n', encoding='utf-8')
