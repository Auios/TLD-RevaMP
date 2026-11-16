using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    internal static class EntityMountFacts
    {
        /// A fitted part as the player's dismount sees it (fpscontroller.cs dismount branch): the game's conditions, its use
        /// ray cast at the part from five sides (the first hit and whether it counts) and its colliders with their layers.
        /// For `mp lookmount` and the bug report's parts.json (Entities.PartWatch).
        internal static void MountFacts(partslotscript ps, attachablescript part, pickupable P, fpscontroller pl, System.Text.StringBuilder b, int index)
        {
            bool sit = pl.CanPickupSit(P), canPick = P.CanPickup(), covered = part.cover_CantCraftIfCovered != null && part.cover_CantCraftIfCovered.hasPart();
            bool gameAllows = !ps.cantCraft && !ps.cantUnCraft && !ps.attachTarget && sit && canPick && !P.cantPickupWhileMount && !covered;
            uint partNet = Entities.MountRootNet(part.transform);
            b.Append(",\"part\":").Append(Json.Str(part.name)).Append(",\"net\":").Append(partNet).Append(",\"index\":").Append(index).Append(",\"canPickupSit\":").Append(sit ? "true" : "false")
             .Append(",\"canPickup\":").Append(canPick ? "true" : "false").Append(",\"cantPickup\":").Append(P.cantPickup ? "true" : "false")
             .Append(",\"cantPickupWhileMount\":").Append(P.cantPickupWhileMount ? "true" : "false").Append(",\"covered\":").Append(covered ? "true" : "false")
             .Append(",\"crafted\":").Append(P.crafted ? "true" : "false").Append(",\"attached\":").Append(part.attached ? "true" : "false")
             .Append(",\"gameAllows\":").Append(gameAllows ? "true" : "false");
            // the ray: from 1.3 m off the part's first live collider on five sides, the first hit and whether it counts
            Collider target = null; var cols = new List<string>();
            foreach (var c in part.GetComponentsInChildren<Collider>(true))
            {
                cols.Add(Json.Str(c.name + ":" + LayerMask.LayerToName(c.gameObject.layer) + (c.enabled && c.gameObject.activeInHierarchy ? "" : ":off") + (c.isTrigger ? ":trigger" : "")
                                  + ((pl.useLayer.value & (1 << c.gameObject.layer)) != 0 ? "" : ":notInUseLayer")));
                if (target == null && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger) target = c;
            }
            string hitFrom = "", firstHit = "";
            if (target != null)
                foreach (var d in new[] { Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    var ctr = target.bounds.center; var from = ctr + d * 1.3f;
                    if (!Physics.Raycast(from, (ctr - from).normalized, out var hit, pl.FrayRange, pl.useLayer)) continue;
                    var ls = hit.collider.GetComponent<layerScript>();
                    bool counts = hit.collider.GetComponent<partslotscript>() == ps || (ls != null && ls.p == P);
                    if (firstHit.Length == 0) firstHit = hit.collider.name + ":" + LayerMask.LayerToName(hit.collider.gameObject.layer) + (counts ? "" : " of " + hit.collider.transform.root.name);
                    if (counts) { hitFrom = d.ToString(); break; }
                }
            b.Append(",\"rayCounts\":").Append(Json.Str(hitFrom)).Append(",\"firstHit\":").Append(Json.Str(firstHit))
             .Append(",\"colliders\":[").Append(string.Join(",", cols)).Append("]");
        }
    }

    public static partial class Entities
    {
        internal static void MountFacts(partslotscript ps, attachablescript part, pickupable P, fpscontroller pl, System.Text.StringBuilder b, int index) => EntityMountFacts.MountFacts(ps, part, P, pl, b, index);
    }
}
