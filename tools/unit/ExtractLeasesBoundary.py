"""Compile the lease owner with isolated game fixtures; freeze HEAD before the move."""
from pathlib import Path
import re
import subprocess
import sys
import hashlib

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'

def read(name, baseline=False):
    path = 'src/TLDRevamp/Net/Entities.' + name + '.cs'
    return (subprocess.check_output(['git', 'show', 'a976944:' + path], cwd=root).decode('utf-8')
            if baseline else (root / path).read_text(encoding='utf-8')).replace('\r\n', '\n')

def method(s, name):
    m = re.search(r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'\(', s, re.M)
    start = m.start()
    a = s.index('{', start)
    arrow = s.find('=>', start, a)
    if arrow >= 0:
        return s[start:s.index(';', arrow) + 1]
    depth, end = 1, a + 1
    while depth:
        depth += (s[end] == '{') - (s[end] == '}')
        end += 1
    return s[start:end]

def patch_class(s, name):
    start = s.index('        private static class ' + name)
    a = s.index('{', start)
    depth, end = 1, a + 1
    while depth:
        depth += (s[end] == '{') - (s[end] == '}')
        end += 1
    return s[start:end]

def baseline_owner():
    s = read('Leases', True)
    body = s.split('    public static partial class Entities\n    {\n')[1].rsplit('    }', 1)[0]
    body = body[:body.index('        // ------------------------------------------------------------------ picking up')]
    for name in ('ResetCounters', 'ShareRoots', 'AttachedToItem', 'HostStarted', 'ClientWorldReload'):
        body = body.replace(method(s, name), '')
    a = body.index('            var shared = new HashSet<tosaveitemscript>();')
    b = body.index('            if (list.Count > 0)', a)
    body = body[:a] + '            var list = Entities.DisableLeaseBuiltIns(pobj);\n' + body[b:]
    body = body.replace('BuiltInShared += ShareRoots(', 'BuiltInShared += Entities.ShareLeaseRoots(')
    body = body.replace('LeaseItemsShared += ShareRoots(', 'LeaseItemsShared += Entities.ShareLeaseRoots(')
    body += '\n        private static readonly HashSet<string> _leasesDone = new HashSet<string>();\n'
    body += read('LeaseValidation', True).split('    public static partial class Entities\n    {\n')[1].rsplit('    }', 1)[0]
    body = re.sub(r'\bToServer\(', 'Entities.SendLeaseRequest(', body)
    body = re.sub(r'\bServerSendTo\(', 'Entities.SendToPeer(', body)
    body = re.sub(r'\bServerSendAll\(', 'Entities.SendToPeers(', body)
    body = re.sub(r'\bAnomaly\(', 'Entities.LeaseAnomaly(', body)
    body = re.sub(r'\bFinite\(', 'Entities.IsFinite(', body)
    body = re.sub(r'\b(InSession|IsHost)\b', r'Entities.\1', body)
    # Keep the original player check in the frozen run. Production later delegates this exact body.
    body = '        private static readonly NetWriter W = new NetWriter();\n' + body
    return 'using System; using System.Collections.Generic; using System.Globalization; using UnityEngine;\nnamespace TLDRevamp.Net { internal static class EntityWorldLeases {\n' + body + '\n} }\n'

def main():
    if '--capture-source' in sys.argv:
        s = baseline_owner()
        names = ['Key', 'OnLeaseTaken', 'OnLeaseFreed', 'FindPoiObject', 'FindPoi', 'LeaseTable', 'EnsureWorld',
                 'SaveLeases', 'LoadLeases', 'OnLeaseGrant', 'TrySpawnLeased', 'CaptureTick', 'LeaseTick',
                 'LeaseReachM', 'RefuseLease', 'LeaseRefusalsJson', 'ServerLeaseRequest', 'ServerLeaseDoneRequest',
                 'LeasePosition', 'CheckLease', 'ServerLease', 'ServerLeaseDone', 'FreeLease', 'ServerLeaseExpiry', 'ServerLeaseLeave']
        rows = [name + ' ' + digest(method(s, name)) for name in names]
        original = read('Leases', True)
        for name in ['ShareRoots', 'AttachedToItem', 'GrabRefusedTick', 'HandCopyTick', 'Ridden']:
            rows.append('kernel.' + name + ' ' + digest(method(original, name)))
        for name in ['PickupClaim', 'CarCopyNoLift']:
            rows.append('patch.' + name + ' ' + digest(patch_class(original, name)))
        for name in ['LeaseGate', 'WorldReset', 'WorldFromReadSave', 'SaveLeasesWithWorld', 'RotateLeasesWithAutosaves']:
            rows.append('moved.' + name + ' ' + digest(patch_class(s, name)))
        taken = method(original, 'OnLeaseTaken')
        rows.append('disable ' + digest(taken[taken.index('            var shared ='):taken.index('            if (list.Count > 0)')]))
        rows.append('position ' + digest(method(read('LeaseValidation', True), 'LeasePlayerPosition')))
        (root / 'tools/unit/EntityLeasesSourceRows.txt').write_bytes(('\r\n'.join(rows) + '\r\n').encode('utf-8'))
        return
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    if '--validation' in sys.argv:
        s = read('LeaseValidation').replace('internal static partial class EntityWorldLeases', 'public static partial class Entities')
        for before, after in [('Entities.IsFinite(', 'Finite('), ('Entities.LeaseAnomaly(', 'Anomaly('),
                              ('Entities.SendToPeer(', 'ServerSendTo('), ('Entities.SendToPeers(', 'ServerSendAll('),
                              ('Entities.TryLeasePlayerPosition(from, out pos)', 'LeasePlayerPositionOriginal(from, out pos)')]:
            s = s.replace(before, after)
        if 'LeasePlayerPositionOriginal' in s:
            old = method(read('LeaseValidation', True), 'LeasePlayerPosition').replace('LeasePlayerPosition(', 'LeasePlayerPositionOriginal(')
            s = s.rsplit('    }', 1)[0] + old + '\n    }\n}\n'
        s = re.sub(r'Entities\.(LeaseGrant|LeaseTaken|LeaseFreed)', r'\1', s)
        target.write_text(s, encoding='utf-8')
        return
    if '--baseline' in sys.argv or 'class EntityWorldLeases' not in read('Leases'):
        s = baseline_owner()
    else:
        s = 'using System.Globalization;\n' + read('Leases').split('    public static partial class Entities')[0] + '}\n'
        s += 'namespace TLDRevamp.Net' + read('LeaseValidation').split('namespace TLDRevamp.Net', 1)[1]
        for row in (root / 'tools/unit/EntityLeasesSourceRows.txt').read_text().splitlines():
            name, expected = row.split()
            if name.startswith('kernel.'):
                actual = method(read('Leases'), name.split('.')[1])
            elif name.startswith('patch.'):
                actual = patch_class(read('Leases'), name.split('.')[1])
            elif name.startswith('moved.'):
                actual = patch_class(s, name.split('.')[1])
            elif name == 'disable':
                actual = method(read('Leases'), 'DisableBuiltIns')
                actual = actual[actual.index('            var shared ='):actual.index('            return list;')].replace('EntityWorldLeases.BuiltInDisabled', 'BuiltInDisabled')
            elif name == 'position':
                actual = method(read('Features'), 'TryLeasePlayerPosition').replace('TryLeasePlayerPosition(', 'LeasePlayerPosition(')
            else:
                actual = method(s, name)
            if digest(actual) != expected:
                raise AssertionError('lease HEAD body changed: ' + name)
    # Game and transport adapters are per-file aliases, never copied lease algorithms.
    aliases = ['Entities=LeaseFakes.Kernel', 'Mp=LeaseFakes.Transport', 'mainscript=LeaseFakes.Main', 'menuhandler=LeaseFakes.Menu',
               'poiScript=LeaseFakes.PoiObject', 'poiGenScript=LeaseFakes.Generator',
               'newSaveScreenScript=LeaseFakes.SaveScreen', 'DataFromMenuScript=LeaseFakes.MenuData',
               'pathscript=LeaseFakes.Paths', 'savedatascript=LeaseFakes.Saved', 'itemdatabase=LeaseFakes.Database',
               'PlayerSave=LeaseFakes.Players']
    # Qualify fixture dependencies only; global game stubs already define several of these names.
    replacements = dict(a.split('=') for a in aliases)
    s = re.sub(r'\b(' + '|'.join(replacements) + r')\b', lambda m: replacements[m[0]], s)
    target.write_text(s, encoding='utf-8')

def digest(s):
    s = re.sub(r'\b(private|public|internal)\b', '', s).replace('Entities.', '')
    return hashlib.sha256(re.sub(r'\s+', '', s).encode('utf-8')).hexdigest()

if __name__ == '__main__':
    main()
