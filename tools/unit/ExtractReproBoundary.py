"""Compile the retained repro entry points and narrow kernel calls in source tests."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
source = (root / 'src/TLDRevamp/Net/Entities.Features.cs').read_text(encoding='utf-8')
start = source.index('        internal static ReproRow ReproBinding(')
end = source.index('        public const byte RabbitState', start)
Path(sys.argv[1]).write_text('namespace TLDRevamp.Net { public static partial class Entities {\n'
    + next(line for line in source.splitlines() if 'internal static bool UsablePoiApplying =>' in line) + '\n'
    + source[start:end] + '\n} }', encoding='utf-8')
