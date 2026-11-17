using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal static Transform[] PresentationLimbs(Ent entity) => EntityRagdolls.Limbs(entity);
        internal static bool PresentationLimp(Ent entity) => EntityRagdolls.Limp(entity);
    }
}
