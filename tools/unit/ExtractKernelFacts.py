"""Compile slice 30's helpers with isolated game surfaces, or capture their unchanged HEAD bodies."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
baseline = '--baseline' in sys.argv


def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (subprocess.check_output(['git', 'show', '71cfcae:' + path], cwd=root).decode('utf-8')
            if baseline else (root / path).read_text(encoding='utf-8')).replace('\r\n', '\n')


def method(source, name):
    m = re.search(r'^        (?:internal|private|public) static [^\n]*?\b' + name + r'\(', source, re.M)
    start = m.start()
    brace = source.find('{', start)
    arrow = source.find('=>', start)
    if arrow >= 0 and (brace < 0 or arrow < brace):
        return source[start:source.index(';', arrow) + 1]
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


owners = {'Lookup': 'EntityLookup', 'MountFacts': 'EntityMountFacts', 'Ground': 'EntityGround',
          'ModApi': 'EntityModApi', 'PlayerSave': 'EntityPlayerSave', 'Push': 'EntityNumbers'}
members = {'Lookup': ['TanksOf', 'NearestCar'], 'MountFacts': ['MountFacts'],
           'ModApi': ['NetIdOf', 'GameObjectOf', 'ShareGameObject'], 'PlayerSave': ['DropFromSave']}
aliases = '''using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameObject = KernelFactsFakes.GameObject;
using Transform = KernelFactsFakes.Transform;
using Collider = KernelFactsFakes.Collider;
using LayerMask = KernelFactsFakes.LayerMask;
using Physics = KernelFactsFakes.Physics;
using tosaveitemscript = KernelFactsFakes.Item;
using tankscript = KernelFactsFakes.Tank;
using partslotscript = KernelFactsFakes.Slot;
using attachablescript = KernelFactsFakes.Part;
using pickupable = KernelFactsFakes.Pickup;
using fpscontroller = KernelFactsFakes.Player;
using layerScript = KernelFactsFakes.Layer;
using savedatascript = KernelFactsFakes.Save;
using mainscript = KernelFactsFakes.Main;
using itemDataClass = KernelFactsFakes.Data;
using ItemSnapshot = KernelFactsFakes.Snapshot;
using GameLookups = KernelFactsFakes.Lookups;
'''
output = [aliases]
source_rows = []
for part, owner in owners.items():
    source = read(part)
    if baseline:
        if part == 'Push':
            body = '\n'.join(line for line in source.splitlines() if 'internal static bool Finite(' in line)
        elif part == 'Ground':
            body = 'public static string GroundLayers = "sand,sand2,sand3,road";'
        else:
            body = '\n'.join(method(source, name) for name in members[part])
        if part == 'Lookup':
            body = body.replace('TanksOf(Ent e)', 'TanksOf(List<tosaveitemscript> items)').replace('e.Items', 'items')
        if part == 'MountFacts':
            # Keep the original first-match walk in the fake kernel, rather than exposing its map to a moved owner.
            body = body.replace('uint partNet = 0; foreach (var x in ByNet.Values) if (x.Root != null && x.Root.transform == part.transform) { partNet = x.NetId; break; }',
                                'uint partNet = Entities.MountRootNet(part.transform);')
        if part == 'ModApi':
            body = body.replace('foreach (var e in ByNet.Values) if (e.Root == s) return e.NetId;', 'if (Entities.TryModRootNet(s, out uint net)) return net;')
            body = body.replace('ByNet.TryGetValue(net, out var e) && e.Root != null ? e.Root.gameObject : null', 'Entities.ModObject(net)')
            body = body.replace('!InSession', '!Entities.InSession').replace('top.car != null ? ShareCar(top) : ShareItem(top, null)', 'Entities.ModShareRoot(top)')
        output.append('namespace TLDRevamp.Net { internal static class ' + owner + ' {\n' + body + '\n} }')
    else:
        header = '    internal static class ' + owner
        start = source.index(header)
        end = source.index('\n    }', start) + len('\n    }')
        if part != 'Ground':
            output.append('namespace TLDRevamp.Net {\n' + source[start:end] + '\n}')
        body = source[start:end].split('{', 1)[1].rsplit('}', 1)[0]
    # Compare the actual owner bodies after only the listed boundary substitutions.
    code = re.sub(r'//[^\n]*', '', body)
    source_rows.append(part + '=' + hashlib.sha256(re.sub(r'\s+', '', code).encode()).hexdigest().upper())

for part in ('cs', 'ItemGroup', 'JoinLoading', 'Placed', 'Observation', 'DevObservation', 'StateCodec', 'RotCodec', 'DetachCodec', 'ShotFx'):
    path = 'src/TLDRevamp/Net/Entities' + ('' if part == 'cs' else '.' + part) + '.cs'
    text = (subprocess.check_output(['git', 'show', '71cfcae:' + path], cwd=root).decode('utf-8')
            if baseline else (root / path).read_text(encoding='utf-8')).replace('\r\n', '\n')
    if part == 'cs':
        # Batch 24 names this exact sequence without changing its position or effects.
        text = text.replace('ClearProxyBindings();', 'ProxyWheelOwner.Clear(); ProxyWheelIndex.Clear(); ProxyEngineOwner.Clear();')
    source_rows.append(part + '.file=' + hashlib.sha256(text.encode()).hexdigest().upper())
if '--capture-source' in sys.argv:
    (root / 'tools/unit/EntityKernelFactsSourceRows.txt').write_text('\n'.join(source_rows) + '\n', encoding='utf-8', newline='\r\n')
    sys.exit(0)
(root / 'tools/unit/obj/EntitySplitTest/KernelFactsActualRows.txt').write_text('\n'.join(source_rows) + '\n', encoding='utf-8', newline='\r\n')

# Exercise the original narrow identity facts too, against fixture-owned records.
source = read('ModApi') if baseline else read('Features')
if baseline:
    mod = method(source, 'NetIdOf')
    lookup = 'internal static bool TryModRootNet(tosaveitemscript item, out uint net) { foreach (var e in ByNet.Values) if (e.Root == item) { net = e.NetId; return true; } net = 0; return false; }'
    mount = 'internal static uint MountRootNet(Transform root) { uint partNet = 0; foreach (var x in ByNet.Values) if (x.Root != null && x.Root.transform == root) { partNet = x.NetId; break; } return partNet; }'
    obj = method(source, 'GameObjectOf').replace('GameObjectOf(', 'ModObject(')
    share = 'internal static string ModShareRoot(tosaveitemscript top) => top.car != null ? ShareCar(top) : ShareItem(top, null);'
    body = '\n'.join((lookup, mount, obj, share))
else:
    body = '\n'.join(method(source, name) for name in ('TryModRootNet', 'ModObject', 'ModShareRoot', 'MountRootNet'))
output.append('namespace KernelFactsFakes { internal static partial class Kernel {\n' + body + '\n} }')
target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
source = '\n'.join(output).replace('Entities.', 'KernelFactsFakes.Kernel.')
mapping = dict(re.findall(r'^using (\w+) = ([\w.]+);$', aliases, re.M))
source = re.sub(r'^using \w+ = [\w.]+;\n', '', source, flags=re.M)
source = re.sub(r'\b(' + '|'.join(mapping) + r')\b', lambda m: mapping[m[0]], source)
target.write_text(source, encoding='utf-8', newline='\n')
