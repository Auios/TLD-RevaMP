"""Compile the identity transactions themselves; the game and feature leaves are deterministic fakes."""
from pathlib import Path
import hashlib
import os
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASE = '3c78306'
baseline = '--baseline' in sys.argv


def read(part):
    path = 'src/TLDRevamp/Net/Entities' + ('.' + part if part else '') + '.cs'
    return file(path)


def file(path):
    return (subprocess.check_output(['git', 'show', BASE + ':' + path], cwd=ROOT).decode()
            if baseline else (ROOT / path).read_text(encoding='utf-8')).replace('\r\n', '\n')


def block(s, start):
    brace = s.index('{', start)
    arrow = s.find('=>', start, brace)
    if arrow >= 0:
        return s[start:s.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (s[end] == '{') - (s[end] == '}')
        end += 1
    return s[start:end]


def method(s, name):
    m = re.search(r'^        (?:public|private|internal) static [^\n=]*?\b' + name + r'\(', s, re.M)
    assert m, name
    return block(s, m.start())


def cases(s, method_name, names, prefix=''):
    full = method(s, method_name)
    result = []
    for name in names:
        start = full.index('                case ' + name + ':')
        result.append(block(full, start))
    return prefix + '\n            switch (type) {\n' + '\n'.join(result) + '\n            }'


parts = {
    '': ['ResetSessionLeftovers', 'Reset', 'CaptureRecord', 'ShareCar', 'ShareItem', 'StoredContents',
         'ToServer', 'IsStored', 'ShowStored', 'Bind', 'RebindMembers', 'Resolve', 'SetProxy', 'PartApart',
         'ApplyProxyBodies', 'FlushFarRecords', 'MoveBoltedRecords', 'DetachStored', 'MoveRecord',
         'ServerOnJoin', 'ServerOnLeave', 'ServerSendTo', 'ServerSendAll', 'Deliver', 'WriteAdd', 'WritePartDetached', 'AddReject',
         'ForgetProxyMap<T>', 'ForgetProxyMaps', 'RefreshProxyMaps'],
    'FarStore': ['KnownBy', 'RecordRootPos', 'ShareFarStore', 'AddFar', 'Offer', 'SendObject'],
    'Lifecycle': ['CameOff', 'FarOnSplit', 'DestroyMembers', 'OrphanPart', 'ServerRemoveItem', 'DestroyLocalCopy',
                  'ApplyRemoveItem', 'ShareSpawn', 'DeleteShared', 'RequestMemberGone', 'LostPartBack', 'HangBack', 'ForgetServerNet'],
    'Leases': ['ClientWorldReload', 'AttachedToItem'],
    'LockPose': ['RebindPoseLocks'],
    'EngineBinding': ['BindProxyEngine'],
    'PartOff': ['SplitPart'],
    'RelayInterest': ['InterestGotAll', 'InterestForget', 'InterestReset'],
    'Placed': ['Placed', 'StoredAway', 'LookupSet', 'PlacedOf', 'PlacedOfTransform', 'AllEnts', 'MarkersStale', 'PruneMarkers'],
    'Attach': ['KeepAttach', 'DropAttach', 'CascadeOwner'],
    'Hitch': ['HitchReset', 'RemoveHitches', 'CascadeHitches'],
    'Features': ['ForgetDeathCorpse', 'TrackRagdollLimb', 'ServerWornNow'],
    'PlayerSave': ['ServerInPocket'],
    'MemberGone': ['ApplyMemberGone'],
    'JoinLoading': ['ServerJoinLoading'],
}
output = ['using System; using System.Collections; using System.Collections.Generic; using System.Reflection; using UnityEngine; using Object = UnityEngine.Object; using Ent = TLDRevamp.Net.Entities.Ent;\nnamespace TLDRevamp.Net { public static partial class Entities {']
for part, names in parts.items():
    s = read(part)
    for name in names:
        output.append(method(s, name))
s = read('')
# The marker prune's own state (registrycheck.py, 2026-11-17): flagged by removals, read by Placed().
p = read('Placed')
output.append(re.search(r'^        private static int _markersStaleFrame = [^;]*;', p, re.M)[0])
output.append(re.search(r'^        public static long MarkersPruned;', p, re.M)[0])
output.append(block(s, s.index('        internal static bool IsStored(uint')))
output.append(block(s, s.index('        private sealed partial class SEnt')))
output.append('public static void ServerReceive(int from, byte type, NetReader r) {' + cases(s, 'ServerReceive', ['Share']) + '}')
prefix = '''if (EntityTeleportRefresh.DeferTpa(type, r)) return;
            EntityTeleportRefresh.TpaCopyMessage(type, r);'''
output.append('public static void ClientReceive(byte type, NetReader r) {' + cases(s, 'ClientReceive', ['Assigned', 'Add', 'PartDetached'], prefix) + '''
            if (type == RemoveItem) { uint net = r.U32(); if (!r.Bad) ApplyRemoveItem(net); }
        }''')
s = read('Lifecycle')
hook = block(s, s.index('        private static class ItemDestroyed'))
output.append(hook.replace('[HarmonyLib.HarmonyPostfix]', '').replace('private static void Postfix(', 'internal static void PostfixForTest('))
output.append('}')
# Keep the actual contact attach/detach implementations, rather than treating them as registration callbacks.
s = file('src/TLDRevamp/Net/Entities.Contact.cs')
output.append('internal static partial class EntityPhysics {')
output.extend(method(s, n) for n in ('AttachContact', 'DetachContact'))
output.append('} }')
for name, names in {
    'Mp': ['Stop', 'StopIfScenePending'],
    'PlayerSave': ['ClientRestoreIn', 'ClientReady', 'ClientSessionEnded', 'WornNote', 'ServerOnLeave', 'ServerStopping', 'ServerSendRestore', 'TakeHandedBack'],
}.items():
    s = file('src/TLDRevamp/Net/' + name + '.cs')
    output.append('namespace TLDRevamp.Net { static partial class ' + name + ' {')
    output.extend(method(s, n) for n in names)
    if name == 'PlayerSave':
        output.extend(block(s, s.index('        private sealed class ' + n)) for n in ('Item', 'Rec'))
    output.append('} }')
# The role indexes and release implementation are real, including removal from ProxyItems.
s = read('Ragdoll')
output.append('namespace TLDRevamp.Net { internal static partial class EntityRagdolls {')
output.extend(method(s, n) for n in ('RagdollRelease', 'ResetCopies'))
output.append('} }')
target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
effects = 'ProxyWheelOwner.Clear(); ProxyWheelIndex.Clear(); ProxyEngineOwner.Clear();'
source = '\n'.join(output) + '\n'
# This is the only production substitution permitted by this batch's source capture.
expanded = source.replace('ClearProxyBindings();', effects)
fingerprint = hashlib.sha256(expanded.encode()).hexdigest()
frozen = ROOT / 'tools/unit/EntityRegistrySourceRows.txt'
if os.environ.get('CAPTURE_ENTITY_REGISTRY_SOURCE') == '1':
    assert baseline, 'source capture must read the immutable HEAD'
    frozen.write_text(fingerprint + '\n', encoding='utf-8', newline='\r\n')
assert frozen.read_text().strip() == fingerprint, 'registry transaction source changed beyond the exact clear substitution'
if not baseline and (ROOT / 'src/TLDRevamp/Net/Entities.Registry.cs').exists():
    command = method(read('Registry'), 'ClearProxyBindings')
    assert re.sub(r'\s+', '', command.split('{', 1)[1].rsplit('}', 1)[0]) == re.sub(r'\s+', '', effects)
    source += 'namespace TLDRevamp.Net { public static partial class Entities {\n' + command + '\n} }\n'
target.write_text(source, encoding='utf-8', newline='\r\n')
