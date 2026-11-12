using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Tpa
    {
        private static readonly HashSet<uint> _arrivalObjects = new HashSet<uint>();
        internal static void ObjectApplied(uint net)
        {
            if ((_arrivalPending || _objectsAt > 0) && _arrivalObjects.Add(net))
                DevHooks.TpaObject?.Invoke(net, false, Entities.MyId, Time.realtimeSinceStartupAsDouble);
        }
    }
}
