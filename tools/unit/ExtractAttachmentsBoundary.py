"""Compile the attachment owner and freeze the pre-split implementations."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'


def read(part, baseline=False):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (subprocess.check_output(['git', 'show', '4851eaf:' + path], cwd=root).decode('utf-8')
            if baseline else (root / path).read_text(encoding='utf-8')).replace('\r\n', '\n')


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


def qualify(body):
    for old, new in (
        ('OwnEntity', 'Entities.AttachmentRoot'), ('EditEntityOf', 'Entities.AttachmentGroup'),
        ('ApplyProxyBodies', 'Entities.RefreshAttachmentBodies'), ('SendAttach', 'Entities.SendAttachment'),
        ('ToServer', 'Entities.SendAttachmentMessage'), ('Finite', 'Entities.IsFinite'),
        ('WriteRot', 'EntityCodec.WriteRot'), ('ReadRot', 'EntityCodec.ReadRot'),
        ('LiveUsablesChanged', 'Entities.AttachmentUsablesChanged')):
        body = re.sub(r'(?<![.\w])' + old + r'\(', new + '(', body)
    for name in ('Ent', 'InSession', 'MyId', 'PartOff', 'AttachReq', 'CarriedPartsShared'):
        body = re.sub(r'(?<![.\w])' + name + r'\b', 'Entities.' + name, body)
    body = body.replace('_applyingAttach || _applyingState', 'Entities.AttachmentApplying')
    body = body.replace('Entities.CarriedPartsShared', 'Entities.AttachmentCarriesParts')
    body = body.replace('PlayerActing', 'Entities.AttachmentPlayerActing')
    body = body.replace('_applyingDetach', 'Entities.AttachmentApplyingDetach')
    body = body.replace('!ByNet.TryGetValue(carrierNet, out var c) || !Resolve(c)', '!Entities.TryAttachmentCarrier(carrierNet, out var c)')
    return body


def baseline_feature():
    attach = read('Attach', True)
    hook = attach[attach.index('        [HarmonyLib.HarmonyPatch'):attach.index('        private static class AttachHook')] + block(attach, attach.index('        private static class AttachHook'))
    declarations = '\n'.join(line for line in attach.splitlines() if re.match(
        r'        public static (?:long AttachPosesSent|float AttachPoseOffLast|bool AttachRequests)', line))
    body = declarations + '\n' + hook + '\n' + '\n'.join(method(attach, name) for name in
        ('AttachFrame', 'WriteAttachPose', 'ReadAttachPose', 'ApplyAttachPose'))
    body = qualify(body).replace('private static void WriteAttachPose', 'internal static void WriteAttachPose')
    body = body.replace('private static bool ReadAttachPose', 'internal static bool ReadAttachPose')
    body = body.replace('private static void ApplyAttachPose', 'internal static void ApplyAttachPose')
    carried = read('CarriedOff', True).split('    public static partial class Entities\n    {\n')[1].rsplit('    }', 1)[0]
    body += '\n' + qualify(carried)
    for name in ('CarriedFar', 'CarriedItem', 'NoteCarrier', 'SentCarriedOff'):
        body = re.sub(r'private static ([^\n]*\b' + name + r'\()', r'internal static \1', body)
    body += '\n        internal static void ResetCarriedOff() => _carriedOff.Clear();\n'
    part = read('PartOff', True)
    told = method(part, 'RefusePartOff')
    told = told[told.index('            // once per player'):].rsplit('        }', 1)[0]
    told = told.replace('Mp.Server?.Tell(', 'Entities.TellAttachmentRefused(')
    body += '\n        public static float PartOffToldS = 30f;\n'
    body += '        private static readonly Dictionary<(int, uint), float> _partOffToldAt = new Dictionary<(int, uint), float>();\n'
    body += '        internal static void TellPartOffRefused(int from, uint carNet)\n        {\n' + told + '        }\n'
    return '    internal static partial class EntityAttachments\n    {\n        private static readonly NetWriter W = new NetWriter();\n' + body + '\n    }'


def canonical(body):
    body = body.replace(', HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Normal + 1)', '')
    body = body.replace('Entities.AttachmentApplyingDetach', '_applyingDetach')
    body = body.replace('Entities.AttachmentApplying', '_applyingAttach || _applyingState')
    for old, new in (
        ('Entities.AttachmentRoot(', 'OwnEntity('), ('Entities.AttachmentGroup(', 'EditEntityOf('),
        ('Entities.RefreshAttachmentBodies(', 'ApplyProxyBodies('), ('Entities.SendAttachment(', 'SendAttach('),
        ('Entities.SendAttachmentMessage(', 'ToServer('), ('Entities.IsFinite(', 'Finite('),
        ('Entities.AttachmentUsablesChanged(', 'LiveUsablesChanged('),
        ('Entities.AttachmentPlayerActing', 'PlayerActing'),
        ('Entities.AttachmentCarriesParts', 'CarriedPartsShared'),
        ('!Entities.TryAttachmentCarrier(carrierNet, out var c)', '!ByNet.TryGetValue(carrierNet, out var c) || !Resolve(c)')):
        body = body.replace(old, new)
    body = body.replace('EntityCodec.', '').replace('EntityAttachments.', '').replace('Entities.', '')
    return re.sub(r'\s+', '', body)


def wire_kernel(baseline):
    body = method(read('Attach', baseline), 'SendAttach')
    body = body.replace('new save_attachable(at, at.tosave.idInSave)', 'Record')
    body = body.replace('Fixes.SlotOwnerSave.Owner(at)', 'Parent').replace('SessionLog.Event(', 'WireEvent(')
    body = body.replace('WriteAttachPose(W, at)', 'TLDRevamp.Net.EntityAttachments.WriteAttachPose(W, at)')
    return 'namespace AttachmentFakes { internal static partial class WireKernel {\n' + body + '\n} }\n'


def hashes():
    rows = []
    for part, names in (
        ('Attach', ('AttachStored', 'DropFromLockRecords', 'RidesParent', 'RidesBolted', 'OwnEntity',
                    'SendAttach', 'WriteAttachPose', 'ReadAttachPose', 'ApplyAttachPose', 'AttachFrame',
                    'ServerAttachReq', 'ApplyAttachReq', 'ServerAttach', 'ApplyAttachSync', 'ClearSlotFor',
                    'CascadeOwner', 'KeepAttach', 'DropAttach', 'ReplayAttaches', 'KnownTo', 'SendKept', 'ServerDetachSync', 'AttachStats')),
        ('CarriedOff', ('CarriedFar', 'CarriedItem', 'SendCarriedOff', 'NoteCarrier', 'SentCarriedOff')),
        ('PartOff', ('ClaimEpochOk', 'PrefabSave', 'CheckPartOff', 'PartOffAttached', 'NearSplitPart', 'CarriedMember',
                     'RefusePartOff', 'ServerPartOff', 'ServerCarriedOff', 'SplitPart'))):
        source = read(part, True)
        for name in names:
            text = method(source, name)
            body = text[text.index('=>'):] if '=>' in text.split('{', 1)[0] else text[text.index('{'):]
            rows.append(part + ' ' + name + ' ' + hashlib.sha256(canonical(body).encode()).hexdigest().upper())
    hook = block(read('Attach', True), read('Attach', True).index('        private static class AttachHook'))
    rows.append('Attach AttachHook ' + hashlib.sha256(canonical(hook).encode()).hexdigest().upper())
    target = root / 'tools/unit/EntityAttachmentsSourceRows.txt'
    target.write_text('\n'.join(rows) + '\n', encoding='utf-8', newline='\r\n')


if '--capture-source' in sys.argv:
    hashes()
else:
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    baseline = '--baseline' in sys.argv
    if '--validation' in sys.argv:
        source = read('PartOff', baseline)
        if not baseline:
            source = source.replace('internal static partial class EntityAttachments', 'public static partial class Entities')
            source = source.replace('EntityAttachments.', '')
            source = source.replace('Entities.TellAttachmentRefused(', 'Mp.Server?.Tell(')
        target.write_text(source, encoding='utf-8')
    else:
        classes = baseline_feature() if baseline else '\n'.join(feature(read(part)) for part in ('Attach', 'CarriedOff', 'PartOff'))
        target.write_text('using System.Collections.Generic; using UnityEngine; using TLDRevamp; using TLDRevamp.Net;\nnamespace TLDRevamp.Net {\n' + classes + '\n}\n' + wire_kernel(baseline), encoding='utf-8')
