using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    internal static partial class EntityLifetimeStreaming
    {
        internal static void UpdateFarRecord(Ent e, Vector3d pos, Quaternion rot)
        {
            if ((e.FarPos - pos).sqrMagnitude == 0 && Quaternion.Angle(e.FarRot, rot) == 0) return;
            e.FarPos = pos; e.FarRot = rot; e.FarDirty = true;   // written to the far store at most once a second
        }
    }
}
