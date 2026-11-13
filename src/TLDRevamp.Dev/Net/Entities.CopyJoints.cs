using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static TLDRevamp.Net.Entities;

namespace TLDRevamp.Net
{
    internal static class EntityCopyJointsTools
    {

        /// Diagnostics (bridge `mp copyjoints`): every jointed shared object here and its joint's state.
        public static string CopyJointsJson()
        {
            RefreshJointed();
            var rows = new List<string>();
            foreach (var e in TLDRevamp.Net.Entities.ObservedJointed)
            {
                var j = e.Root.attachable.J as ConfigurableJoint;
                var own = j != null ? j.GetComponent<Rigidbody>() : null;
                rows.Add("{\"net\":" + e.NetId + ",\"name\":" + Json.Str(e.Root.name) + ",\"owner\":" + e.OwnerId + ",\"proxy\":" + (e.Proxy ? "true" : "false") +
                         ",\"on\":" + Json.Str(j != null && j.connectedBody != null ? j.connectedBody.name : "") +
                         ",\"onNet\":" + (j != null && j.connectedBody != null ? TelemetryNet(j.connectedBody) : 0) +
                         ",\"kinematic\":" + (own != null && own.isKinematic ? "true" : "false") +
                         ",\"onKinematic\":" + (j != null && j.connectedBody != null && j.connectedBody.isKinematic ? "true" : "false") +
                         ",\"free\":" + (j != null && j.xMotion == ConfigurableJointMotion.Free ? "true" : "false") + "}");
            }
            return "{\"enabled\":" + (CopyJointsFree ? "true" : "false") + ",\"freed\":" + CopyJointsFreed + ",\"locked\":" + CopyJointsLocked +
                   ",\"freeNow\":" + TLDRevamp.Net.Entities.ObservedFreedJoints.Count + ",\"jointed\":[" + string.Join(",", rows) + "]}";
        }
    }
}
