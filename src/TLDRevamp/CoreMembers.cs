namespace TLDRevamp
{
    /// Historical member paths stay usable as core features gain their own owners.
    internal static class CoreMembers
    {
        internal static void Register()
        {
            Reflect.RegisterMovedMembers(typeof(Net.Entities), typeof(Net.EntityRadio), typeof(Net.EntityWorldClock), typeof(Net.EntityWeather), typeof(Net.EntityCodec), typeof(Net.EntityCarSignals), typeof(Net.EntityEffects), typeof(Net.EntityExplosions), typeof(Net.EntityCarTuning), typeof(Net.EntityDoors), typeof(Net.EntitySeats), typeof(Net.EntityHands), typeof(Net.EntityRagdolls), typeof(Net.EntityAI), typeof(Net.EntityRabbits), typeof(Net.EntityReproHooks), typeof(Net.EntityDiag), typeof(Net.EntityTelemetry), typeof(Net.EntityTeleportRefresh), typeof(Net.EntityUsables), typeof(Net.EntityEditBack), typeof(Net.EntityEditCheck), typeof(Net.EntityEdits), typeof(Net.EntityResync), typeof(Net.EntityHitches), typeof(Net.EntityLocks), typeof(Net.EntityWorldLeases), typeof(Net.EntityCargoInteraction), typeof(Net.EntityAttachments), typeof(Net.EntityAuthorityInterest), typeof(Net.EntityPhysics), typeof(Net.EntityProxyPresentation), typeof(Net.EntityLifetimeStreaming));
        }
    }
}
