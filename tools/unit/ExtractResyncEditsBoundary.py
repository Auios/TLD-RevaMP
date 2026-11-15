"""Compile the actual narrow owners and retained resync producer in EntitySplitTest."""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'

def method(source, name):
    match = re.search(r'^        (?:public|private|internal) static [^\n=]*?\b' + name + r'\(', source, re.M)
    start = match.start()
    brace = source.index('{', start)
    arrow = source.find('=>', start, brace)
    if arrow >= 0:
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

def sources(baseline=False):
    return {p: (subprocess.check_output(['git', 'show', 'f183a37:src/TLDRevamp/Net/Entities.' + p + '.cs'], cwd=root).decode()
                if baseline else (net / ('Entities.' + p + '.cs')).read_text(encoding='utf-8')).replace('\r\n', '\n')
            for p in ('EditBack', 'EditCheck', 'Edits', 'Resync')}

def owners(src):
    # Baseline adaptation changes only the narrow kernel seams, before production is moved.
    back = src['EditBack'].split('    public static partial class Entities\n    {\n', 1)[1].rsplit('    }', 1)[0]
    back = back.replace('SEnt se', 'uint net').replace('se.NetId', 'net').replace('EditHold +', 'Entities.EditBackHold +')
    a = back.index('                if (!Server.TryGetValue')
    b = back.index('                RefusedEditsAsked++;', a)
    back = back[:a] + '                if (!Entities.RequestEditBack((uint)(key >> 32), (uint)key)) continue;\n' + back[b:]
    back = back.replace('EditBackAfterHold(se, idx)', 'EditBackAfterHold(net, idx)').replace('private static void EditBack', 'internal static void EditBack')

    s = src['EditCheck']
    check = s[s.index('        /// An item'):s.index('        private static readonly Dictionary<uint,')]
    check += '\n' + '\n\n'.join(method(s, n) for n in ('TakeToken', 'Refuse', 'Sane', 'SaneColor', 'Ind', 'CheckPoiUsable', 'EditCheckJson'))
    check = check.replace('private static bool Sane', 'internal static bool Sane').replace('private static bool Ind', 'internal static bool Ind')
    check = check.replace('private static void Refuse', 'internal static void Refuse').replace('private static bool CheckPoiUsable', 'internal static bool CheckPoiUsable')
    check = check.replace('PoiKey k, PoiState st', 'Entities.PoiKey k, Entities.PoiState st')
    check = check.replace('if (n < 5) Anomaly(what + " (refused)"); else Anomalies++;', 'Entities.RecordEditRefusal(what, n < 5);')
    check = check.replace('Mp.Server == null || !Mp.Server.PeerPos(from, out var p)', '!Entities.TryEditPeerPosition(from, out var p)')
    check = re.sub(r'\bFinite\(', 'Entities.IsFinite(', check).replace('+ EditsOutOfReach +', '+ Entities.EditReachRefusals +').replace('+ Anomalies +', '+ Entities.EditAnomalyCount +')
    check += '''
        internal static bool TakeEditToken(int player) => TakeToken(_editBuckets, player, EditRatePerS, EditBurst);
        internal static void ForgetEditRate(int player) => _editBuckets.Remove(player);
        internal static void ForgetPoiRate(int player) => _poiBuckets.Remove(player);
        internal static void ResetRates() { _editBuckets.Clear(); _poiBuckets.Clear(); _refusalsLogged.Clear(); }
'''

    s = src['Edits']
    edits = '''        public static bool TankAddEdits = true;
        public static long TankAddsApplied;
        private static readonly Dictionary<uint, List<save_tankscript>> _tankTold = new Dictionary<uint, List<save_tankscript>>();
'''
    edits += '\n\n'.join(method(s, n) for n in ('TankChangesNow', 'ApplyTankAdds', 'KindBit'))
    edits = edits.replace('private static bool TankChangesNow', 'internal static bool TankChangesNow').replace('private static void ApplyTankAdds', 'internal static void ApplyTankAdds').replace('private static int KindBit', 'internal static int KindBit')
    edits = edits.replace('_applyingState = true;', 'Entities.SetFluidApplyingState(true);').replace('_applyingState = false;', 'Entities.SetFluidApplyingState(false);')
    edits = re.sub(r'\bKind(?:Usable|Tank|Part|Color|Food|Ammo|Plate|BusDoor|Blood|All)\b', lambda m: 'Entities.' + m[0], edits)
    edits += '''
        internal static void RememberTankTruth(tosaveitemscript it)
        {
            if (TankAddEdits && it != null && !_tankTold.ContainsKey(it.idInSave) && EntityCarTuning.TryCopyTankTruth(it.idInSave, out var truth)) _tankTold[it.idInSave] = truth;
        }
        internal static void ForgetTankEditTruth(uint id) => _tankTold.Remove(id);
'''

    s = src['Resync']
    resync = '        public static long ResyncForeignAttachDropped;\n'
    resync += '\n\n'.join(method(s, n) for n in ('CanonMap', 'StateRecord', 'StateHashes', 'DropUncanonAttach', 'StateOnly'))
    resync = resync.replace('private static ulong[] StateHashes', 'internal static ulong[] StateHashes').replace('private static void DropUncanonAttach', 'internal static void DropUncanonAttach')
    resync = re.sub(r'\bCanonBase\b', 'Entities.CanonBase', resync)
    return dict(EditBack=back, EditCheck=check, Edits=edits, Resync=resync)

if __name__ == '__main__':
    if '--back-only' in sys.argv:
        feature = (net / 'Entities.Features.cs').read_text(encoding='utf-8')
        Path(sys.argv[1]).write_text('namespace TLDRevamp.Net { public static partial class Entities {\n'
            + 'internal static float EditBackHold => EditHold;\n' + method(feature, 'RequestEditBack') + '\n} }', encoding='utf-8')
        sys.exit(0)
    baseline = '--baseline' in sys.argv
    src = sources(baseline)
    target = Path(sys.argv[1])
    target.parent.mkdir(parents=True, exist_ok=True)
    if baseline or '    internal static class EntityEditBack' not in src['EditBack']:
        for part, body in owners(src).items():
            target.with_name(target.stem + part + '.cs').write_text('using System; using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { internal static class Entity' + part + ' {\n' + body + '\n} }', encoding='utf-8')
    else:
        for part, source in src.items():
            target.with_name(target.stem + part + '.cs').write_text(source[:source.index('    public static partial class Entities')] + '}\n', encoding='utf-8')
    resync = src['Resync']
    body = method(resync, 'ResyncTick')
    server_edit = method(src['Edits'], 'ServerEdit')
    if baseline:
        server_edit = server_edit.replace('TakeToken(_editBuckets, from, EditRatePerS, EditBurst)', 'TakeEditToken(from)')
        for name in ('EditRefusedRate', 'EditRefusedKinds', 'EditRefusedValues'):
            server_edit = re.sub(r'\b' + name + r'\b', 'EntityEditCheck.' + name, server_edit)
    body += '\n' + server_edit
    facades = []
    for source in sources().values():
        retained = source[source.index('    public static partial class Entities'):]
        for line in retained.splitlines():
            if 'static ' not in line or not re.search(r'(?:=> Entity(?:EditBack|EditCheck|Edits|Resync)\.|get => Entity)', line):
                continue
            if re.search(r'\b(?:TakeEditToken|TankChangesNow|StateHashes)\(', line):
                continue  # These are supplied by the older seat/edit-flush source-including fixtures.
            facades.append(line)
    target.write_text('using System; using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net { public static partial class Entities {\n' + body + '\n' + '\n'.join(facades) + '\n} }', encoding='utf-8')
