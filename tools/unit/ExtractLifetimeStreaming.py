"""Freeze slice 27 against HEAD and compile its helpers with small game surfaces."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
parts = ('FallOff', 'FarPose', 'FarStore', 'Lifecycle', 'LinkedDelay', 'MemberGone', 'PlayerMade', 'Removal')
owner_header = '    internal static partial class EntityLifetimeStreaming\n    {\n'
kernel_header = '    public static partial class Entities\n    {\n'


def read(part, baseline=False):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (subprocess.check_output(['git', 'show', '150bfb1:' + path], cwd=root).decode('utf-8')
            if baseline else (root / path).read_text(encoding='utf-8')).replace('\r\n', '\n')


def block(source, start):
    brace = source.index('{', start)
    arrow = source.find('=>', start, brace)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def method(source, name):
    m = re.search(r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'\(', source, re.M)
    return block(source, m.start())


def body(source, header):
    return source.split(header, 1)[1].split('\n    }', 1)[0]


def adapt(part, text):
    if part == 'FallOff':
        text = text.replace('PartIsProxy(__instance)', 'Entities.LifetimePartIsProxy(__instance)').replace('SuppressedFallOff++', 'Entities.RecordLifetimeFallOff()')
    if part == 'LinkedDelay':
        text = text.replace('ProxyOf(r)', 'Entities.ProxyForLinkedDelay(r)')
    if part in ('PlayerMade', 'Lifecycle'):
        text = re.sub(r'(?<![.\w])InSession\b', 'Entities.LifetimeInSession', text)
    if part == 'PlayerMade':
        text = text.replace('long before = SpawnShared;', 'long before = Entities.LifetimeSpawnCount;')
        text = text.replace('ShareSpawn(it.gameObject)', 'Entities.ShareLifetimeSpawn(it.gameObject)').replace('if (SpawnShared > before)', 'if (Entities.LifetimeSpawnCount > before)')
    for name in ('UpdateFarRecord', 'LinkedDelayTick', 'SaveReady', 'RemoveRecord', 'FarResync', '_idField', 'WriteMemberGone'):
        text = re.sub(r'private static ([^\n]*?\b' + name + r'\()', r'internal static \1', text)
    return text


def partition(part):
    original = body(read(part, True), kernel_header)
    if part in ('FallOff', 'FarPose', 'LinkedDelay', 'PlayerMade'):
        return adapt(part, original), ''
    retained = original
    moved = []

    def take(text):
        nonlocal retained
        assert text in retained
        retained = retained.replace(text, '', 1)
        moved.append(text)

    if part == 'FarStore':
        retained = retained.replace('FarResyncs, FarResyncItems, FarShared', 'FarResyncs, FarShared')
        moved.append('        public static long FarResyncItems;')
        for name in ('PrefabName', '_idField', 'FarFromHere', 'FarResync'):
            take(method(original, name))
        start = original.index('        private static readonly HashSet<string> FarStateLists')
        take(original[start:original.index(';', start) + 1])
    elif part == 'Lifecycle':
        take('        private static readonly HashSet<tosaveitemscript> _playerDeleted = new HashSet<tosaveitemscript>();')
        start = original.index('        [HarmonyLib.HarmonyPatch(typeof(cheatedStuff)')
        take(block(original, start))
        for name in ('SaveReady', 'RemoveRecord'):
            take(method(original, name))
        retained = retained.replace('_playerDeleted.Remove(__instance)', 'EntityLifetimeStreaming.TakePlayerDeleted(__instance)')
        moved.append('        internal static bool TakePlayerDeleted(tosaveitemscript item) => _playerDeleted.Remove(item);')
        moved.append('        internal static void ClearPlayerDeleted() => _playerDeleted.Clear();')
    elif part == 'MemberGone':
        take(method(original, 'WriteMemberGone'))
        # The existing public protocol constant remains in the kernel facade.
        moved[-1] = moved[-1].replace('w.U8(MemberGone)', 'w.U8(Entities.MemberGone)')
    elif part == 'Removal':
        take(method(original, 'Unmount'))
    return adapt(part, '\n\n'.join(moved)), retained


def digest(text):
    text = re.sub(r'//[^\n]*', '', text)
    return hashlib.sha256(re.sub(r'\s+', '', text).encode('utf-8')).hexdigest()


def source_rows():
    for part in parts:
        moved, retained = partition(part)
        yield part + '-owner', digest(moved)
        if retained:
            yield part + '-kernel', digest(retained)


def write(path, text):
    path.write_bytes(text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))


if __name__ == '__main__':
    if '--capture-source' in sys.argv:
        write(root / 'tools/unit/EntityLifetimeStreamingSourceRows.txt', '\n'.join(n + '=' + h for n, h in source_rows()) + '\n')
        sys.exit(0)
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    baseline = '--baseline' in sys.argv
    expected = dict(line.split('=') for line in (root / 'tools/unit/EntityLifetimeStreamingSourceRows.txt').read_text().splitlines())
    for part in parts:
        moved, retained = partition(part) if baseline else (body(read(part), owner_header), body(read(part), kernel_header) if kernel_header in read(part) else '')
        assert digest(moved) == expected[part + '-owner'], part + ' moved behavior differs from HEAD'
        if retained:
            assert digest(retained) == expected[part + '-kernel'], part + ' retained transaction differs from HEAD'
        # The real Ent and method signatures are retained. Only unavailable game singletons are substituted.
        replacements = {'Entities': 'LifetimeStreamingFakes.Kernel', 'savedatascript': 'LifetimeStreamingFakes.Save',
                        'menuhandler': 'LifetimeStreamingFakes.Menu', 'itemPlaceRemoveScript': 'LifetimeStreamingFakes.Placer',
                        'ItemSnapshot': 'LifetimeStreamingFakes.Snapshot', 'GameLookups': 'LifetimeStreamingFakes.Lookups'}
        moved = re.sub(r'\b(' + '|'.join(replacements) + r')\b', lambda m: replacements[m[0]], moved)
        moved = moved.replace('Fixes.TrailerGrip', 'LifetimeStreamingFakes.TrailerGrip').replace('Quaternion.Angle(', 'LifetimeStreamingFakes.Rotation.Angle(')
        write(target.with_name(target.stem + part + '.cs'),
              'using System; using System.Collections; using System.Collections.Generic; using System.Reflection; using UnityEngine; using HarmonyLib;\n'
              + 'using Ent = TLDRevamp.Net.Entities.Ent;\nnamespace TLDRevamp.Net { internal static partial class EntityLifetimeStreaming {\n' + moved + '\n} }\n')
