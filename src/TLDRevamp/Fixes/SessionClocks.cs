namespace TLDRevamp.Fixes
{
    /// Recorder counters for the two host-authoritative clocks.
    public static class SessionClocks
    {
        public static long WorldSent, WorldReceived, WorldSlewed, WorldSnapped, WorldStale;
        public static long RadioProjected, RadioStale, RadioClamped, RadioLocalAdvancesBlocked;
    }
}
