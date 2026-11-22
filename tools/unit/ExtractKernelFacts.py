"""Compile the current kernel helpers with isolated game surfaces."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]


def read(part):
    path = 'src/TLDRevamp/Net/Entities.' + part + '.cs'
    return (root / path).read_text(encoding='utf-8').replace('\r\n', '\n')


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
for part, owner in owners.items():
    source = read(part)
    header = '    internal static class ' + owner
    start = source.index(header)
    end = source.index('\n    }', start) + len('\n    }')
    if part != 'Ground':
        output.append('namespace TLDRevamp.Net {\n' + source[start:end] + '\n}')
# Exercise the original narrow identity facts too, against fixture-owned records.
source = read('Features')
body = '\n'.join(method(source, name) for name in ('TryModRootNet', 'ModObject', 'ModShareRoot', 'ModIsCopy', 'MountRootNet'))
output.append('namespace KernelFactsFakes { internal static partial class Kernel {\n' + body + '\n} }')
target = Path(sys.argv[1])
target.parent.mkdir(parents=True, exist_ok=True)
source = '\n'.join(output).replace('Entities.', 'KernelFactsFakes.Kernel.').replace('internal static class EntityModApi', 'internal static partial class EntityModApi')
mapping = dict(re.findall(r'^using (\w+) = ([\w.]+);$', aliases, re.M))
source = re.sub(r'^using \w+ = [\w.]+;\n', '', source, flags=re.M)
source = re.sub(r'\b(' + '|'.join(mapping) + r')\b', lambda m: mapping[m[0]], source)
target.write_text(source, encoding='utf-8', newline='\n')
