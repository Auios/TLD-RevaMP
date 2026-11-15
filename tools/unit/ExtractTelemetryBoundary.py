"""Compile the mark focus boundary and retained census bodies in EntitySplitTest."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
net = root / 'src/TLDRevamp/Net'
marks = (net / 'TelemetryMarks.cs').read_text(encoding='utf-8')
start = marks.index('    public static partial class Entities')
end = marks.index('        private const int TelemetryRecordLimit', start)
# After extraction this section belongs to the feature, before the retained map capture.
if '    internal static class EntityTelemetry' in marks:
    start = marks.index('    internal static class EntityTelemetry')
    end = marks.index('    public static partial class Entities', start)
    focus = marks[start:end]
else:
    focus = marks[start:end] + '    }\n'

anomaly = (net / 'Entities.Anomaly.cs').read_text(encoding='utf-8')
start = anomaly.index('        public const byte CensusAsk')
end = anomaly.index('        internal static double NearestPlayerM', start)
parts = [anomaly[start:end]]

def method(source, name):
    start = source.rfind('\n', 0, source.index(' ' + name + '(')) + 1
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

parts += [method(anomaly, n) for n in ['CensusHash', 'ServerCensusAsk', 'ApplyCensusAsk', 'ServerCensusReply']]
Path(sys.argv[1]).write_text('using System; using System.Collections.Generic; using UnityEngine; namespace TLDRevamp.Net {\n'
    + focus + '\npublic static partial class Entities {\n' + '\n'.join(parts) + '\n} }', encoding='utf-8')
