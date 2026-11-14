namespace TLDRevamp
{
    /// Historical member paths stay usable as core features gain their own owners.
    internal static class CoreMembers
    {
        internal static void Register()
        {
            Reflect.RegisterMovedMembers(typeof(Net.Entities), typeof(Net.EntityRadio), typeof(Net.EntityWorldClock), typeof(Net.EntityWeather), typeof(Net.EntityCodec), typeof(Net.EntityCarSignals), typeof(Net.EntityEffects), typeof(Net.EntityExplosions), typeof(Net.EntityCarTuning), typeof(Net.EntityDoors), typeof(Net.EntitySeats), typeof(Net.EntityHands), typeof(Net.EntityRagdolls));
        }
    }
}
