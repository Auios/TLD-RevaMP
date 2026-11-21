using System;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        // These fixtures don't run distance hand-off. EntityInUseTest runs both real lookups.
        private static bool Mounted(SEnt se) => throw new InvalidOperationException("Unexpected mount lookup");
        private static bool LooseSplit(SEnt se) => throw new InvalidOperationException("Unexpected split lookup");
    }
}
