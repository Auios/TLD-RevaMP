"""Compile the mark focus boundary and retained census bodies in EntitySplitTest."""
from pathlib import Path
import sys
import re

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'
marks = (net / 'TelemetryMarks.cs').read_text(encoding='utf-8')
anomaly = (net / 'Entities.Anomaly.cs').read_text(encoding='utf-8')
start = anomaly.index('        public const byte CensusAsk')
end = anomaly.index('        internal static double NearestPlayerM', start)
parts = [anomaly[start:end]]

def method(source, name):
    start = re.search(r'^        (?:internal|private|public) static [^\n(]*?\b' + name + r'\(', source, re.M).start()
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

parts += [method(anomaly, n) for n in ['ServerCensusAsk', 'ApplyCensusAsk', 'ServerCensusReply']]
focus = '    internal static partial class EntityTelemetry {\n' + '\n'.join(method(marks, n) for n in ['CaptureMarkFocus', 'FocusTarget', 'MarkNets']) + '\n    }\n'
Path(sys.argv[1]).write_text('using System; using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net {\n'
    + focus + '\npublic static partial class Entities {\n' + '\n'.join(parts) + '\n} }', encoding='utf-8')
