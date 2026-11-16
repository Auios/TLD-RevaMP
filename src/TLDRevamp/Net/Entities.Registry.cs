namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static void ClearProxyBindings()
        {
            ProxyWheelOwner.Clear(); ProxyWheelIndex.Clear(); ProxyEngineOwner.Clear();
        }
    }
}
