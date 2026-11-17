"""Compile the current lease owner with isolated game fixtures."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]

def read(name):
    path = 'src/TLDRevamp/Net/Entities.' + name + '.cs'
    return (root / path).read_text(encoding='utf-8').replace('\r\n', '\n')

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


def main():
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    if '--validation' in sys.argv:
        s = read('LeaseValidation').replace('internal static partial class EntityWorldLeases', 'public static partial class Entities')
        for before, after in [('Entities.IsFinite(', 'Finite('), ('Entities.LeaseAnomaly(', 'Anomaly('),
                              ('Entities.SendToPeer(', 'ServerSendTo('), ('Entities.SendToPeers(', 'ServerSendAll('),
                              ('Entities.TryLeasePlayerPosition(from, out pos)', 'LeasePlayerPositionOriginal(from, out pos)')]:
            s = s.replace(before, after)
        if 'LeasePlayerPositionOriginal' in s:
            # Compile the kernel player-position helper under the validation fixture name.
            old = method(read('Features'), 'TryLeasePlayerPosition').replace('TryLeasePlayerPosition(', 'LeasePlayerPositionOriginal(')
            s = s.rsplit('    }', 1)[0] + old + '\n    }\n}\n'
        s = re.sub(r'Entities\.(LeaseGrant|LeaseTaken|LeaseFreed)', r'\1', s)
        target.write_text(s, encoding='utf-8')
        return
    s = 'using System.Globalization;\n' + read('Leases').split('    public static partial class Entities')[0] + '}\n'
    s += 'namespace TLDRevamp.Net' + read('LeaseValidation').split('namespace TLDRevamp.Net', 1)[1]
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


if __name__ == '__main__':
    main()
