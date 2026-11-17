"""Compile the creature damage, AI authority, and shared spawn writers in the split fixture."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]


def method(path, name):
    source = (root / path).read_text(encoding='utf-8')
    start = source.rfind('\n', 0, re.search(r'(?:private|internal|public) static \w+ ' + name + r'\(', source).start()) + 1
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


combat = 'src/TLDRevamp/Net/PlayerCombat.cs'
protocol = 'src/TLDRevamp/Net/Protocol.cs'
target = Path(sys.argv[1])
target.write_text('using System; using UnityEngine; namespace TLDRevamp.Net {\n'
                  + 'internal static partial class PlayerCombat {\n'
                  + 'private static readonly NetWriter W = new NetWriter(); public const byte PlayerDamage = 40; internal static long DamageSent;\n'
                  + method(combat, 'Send') + '\n'
                  + next(line for line in (root / combat).read_text(encoding='utf-8').splitlines() if 'void SendInstant(' in line) + '\n}\n'
                  + 'internal static partial class Protocol {\n' + method(protocol, 'WritePos') + '\n'
                  + next(line for line in (root / protocol).read_text(encoding='utf-8').splitlines() if 'int Cm(' in line) + '\n}\n}\n', encoding='utf-8')

# The authority decision stays in the dispatcher. Compile that exact case, including its reader offsets.
source = (root / 'src/TLDRevamp/Net/Entities.cs').read_text(encoding='utf-8')
case = source.split('                case AiState:', 1)[1].split('                case AiSound:', 1)[0]
with target.open('a', encoding='utf-8') as output:
    output.write('namespace TLDRevamp.Net { public static partial class Entities {\n'
                 + 'internal static void TestServerAIState(int from, NetReader r) { switch (AiState) { case AiState:'
                 + case + '} }\n} }\n')

# Tumbleweeds have no message of their own. Run their unchanged shared spawn and Share framing, too.
with target.open('a', encoding='utf-8') as output:
    output.write('namespace TLDRevamp.Net { using System.Collections.Generic; public static partial class Entities {\n'
                 + method('src/TLDRevamp/Net/Entities.Lifecycle.cs', 'ShareSpawn').replace('ShareSpawn(', 'TestTumbleShareSpawn(').replace('ShareItem(', 'TestTumbleShareItem(') + '\n'
                 + method('src/TLDRevamp/Net/Entities.cs', 'ShareItem').replace('ShareItem(', 'TestTumbleShareItem(') + '\n} }\n')
