namespace TLDRevamp.Net
{
    internal static partial class EntitySeats
    {
        internal static float DriveClaimHoldS = 1.5f;
        internal static int SeatedPlayer = -1;
        internal static int DriverSeatTakenBy(uint net, int from) => net == 7 && from != SeatedPlayer ? SeatedPlayer : -1;
    }

    internal static partial class EntityPhysics
    {
        internal static long CrashAutoReturned;
    }

    public static partial class Entities
    {
        public partial class Root
        {
            public Mount attachable;
        }

        public class Mount
        {
            public bool attached;
            public bool toItem;
        }

        private static int NativeMountChecks;
        private static bool AttachedToItem(Root root)
        {
            NativeMountChecks++;
            return root.attachable.toItem;
        }
    }
}
