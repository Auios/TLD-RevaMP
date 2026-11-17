"""Compile the current cargo owner against game fixtures."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
PARTS = ('BodyPush', 'Cargo', 'ItemPush', 'Push', 'PushGrip')

def read(part):
    path = f'src/TLDRevamp/Net/Entities.{part}.cs'
    return (ROOT / path).read_text(encoding='utf-8').replace('\r\n', '\n')

def member(source, name):
    pattern = r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'\('
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


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8', newline='\r\n')

def main():
    target = Path(sys.argv[1])
    features = []
    for part in PARTS:
        s = read(part).split('    public static partial class Entities')[0]
        if part == 'Push':
            s = s.split('    /// Numbers received by physics, clocks and health, independent of session identity.')[0]
        s = s.rstrip() + '\n}\n'
        features.append(s)
    # Separate files retain their original using scopes.
    for i, s in enumerate(features):
        write(target.with_name(target.stem + str(i) + '.cs'), s)
    release = member(read('Cargo'), 'ClaimLetGoInCar').replace('ClaimLetGoInCar(', 'CargoTestLetGo(')
    visible = read('VisibleState')
    helpers = [release, member(visible, 'LetGoInCar'), member(read('Push'), 'ServerPush').replace('ServerPush(', 'CargoTestServerPush(')]
    write(target.with_name(target.stem + 'Kernel.cs'), 'using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + '\n'.join(helpers) + '\n} }\n')
if __name__ == '__main__':
    main()
