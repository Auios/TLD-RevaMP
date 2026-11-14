using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal static partial class EntityRabbits
    {
        private static readonly List<KeyValuePair<int, Vector3d>> _rabAnchors = new List<KeyValuePair<int, Vector3d>>();
        internal static void ServerRabbitTick(float dt) => Entities.ChooseRabbitOwner(dt, ref _rabSrvAcc, _rabAnchors);
    }
}
