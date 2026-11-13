using UnityEngine;

namespace TLDRevamp.VR.Native
{
    internal readonly struct VrHandInput
    {
        internal readonly bool Active, Trigger, Grip, Primary;
        internal readonly Vector3 Position;
        internal readonly Quaternion Rotation;
        internal VrHandInput(bool active, Vector3 position, Quaternion rotation, bool trigger, bool grip, bool primary)
        { Active = active; Position = position; Rotation = rotation; Trigger = trigger; Grip = grip; Primary = primary; }
    }
}
