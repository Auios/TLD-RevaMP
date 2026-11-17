"""Compile the current attachment owner against isolated game fixtures."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]


def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (root / path).read_text(encoding='utf-8').replace('\r\n', '\n')


def block(source, start):
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def method(source, name):
    match = re.search(r'^        (?:public|private|internal) static [^\n]*?\b' + name + r'\(', source, re.M)
    brace = source.find('{', match.start())
    arrow = source.find('=>', match.start())
    if arrow >= 0 and (brace < 0 or arrow < brace):
        return source[match.start():source.index(';', arrow) + 1]
    return block(source, match.start())


def feature(source):
    start = source.index('    internal static partial class EntityAttachments')
    return block(source, start)


def wire_kernel():
    body = method(read('Attach'), 'SendAttach')
    body = body.replace('new save_attachable(at, at.tosave.idInSave)', 'Record')
    body = body.replace('Fixes.SlotOwnerSave.Owner(at)', 'Parent').replace('SessionLog.Event(', 'WireEvent(')
    body = body.replace('WriteAttachPose(W, at)', 'TLDRevamp.Net.EntityAttachments.WriteAttachPose(W, at)')
    return 'namespace AttachmentFakes { internal static partial class WireKernel {\n' + body + '\n} }\n'


target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
if '--validation' in sys.argv:
    source = read('PartOff')
    source = source.replace('internal static partial class EntityAttachments', 'public static partial class Entities')
    source = source.replace('EntityAttachments.', '')
    source = source.replace('Entities.TellAttachmentRefused(', 'Mp.Server?.Tell(')
    target.write_text(source, encoding='utf-8')
else:
    classes = '\n'.join(feature(read(part)) for part in ('Attach', 'CarriedOff', 'PartOff'))
    target.write_text('using System.Collections.Generic; using UnityEngine; using TLDRevamp; using TLDRevamp.Net;\nnamespace TLDRevamp.Net {\n' + classes + '\n}\n' + wire_kernel(), encoding='utf-8')
