using System;

namespace TLDRevamp
{
    internal enum DebugViewMode { Off, Focus, Problems, Everything }

    internal static class DebugView
    {
        private static DebugViewMode _mode;
        internal static int Revision;
        internal static uint PinnedNet = 0;
        internal static bool Ownership, Ghost, GhostAll, Links;
        internal static DebugViewMode Mode
        {
            get => _mode;
            set
            {
                _mode = (DebugViewMode)Math.Max(0, Math.Min(3, (int)value)); Revision++;
                Ownership = _mode >= DebugViewMode.Problems;
                Ghost = Links = _mode != DebugViewMode.Off; GhostAll = _mode == DebugViewMode.Everything;
            }
        }
        internal static bool Active => _mode != DebugViewMode.Off;
        internal static DebugViewMode Next(DebugViewMode mode) => (DebugViewMode)(((int)mode + 1) % 4);
        internal static void Enable(DebugViewMode mode) { if (!Active) Mode = mode; }
        internal static void EnableTool(DebugViewMode mode)
        {
            if (Active) return;
            Mode = mode; Ownership = Ghost = GhostAll = Links = false;
        }

    }

}
