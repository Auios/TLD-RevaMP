using UnityEngine;
using Ent = TLDRevamp.Net.Entities.Ent;

namespace TLDRevamp.Net
{
    internal static partial class EntityRabbits
    {
        /// SetProxy: a rabbit changing hands. Made a copy: it stops biting here. Made ours: it carries on from the
        /// owner's last word - where it was heading, roar and bite over, its last speed as "last frame's" (aiscript kills a
        /// rabbit whose speed jumps: accelerationDeath - the copy's stale one against the handed-over speed killed it).
        internal static void RabbitRole(Ent e, bool proxy)
        {
            var ny = NyOf(e);
            if (ny == null) return;
            var a = ny.AI;
            if (ny.playerDeathTrigger != null) ny.playerDeathTrigger.SetActive(false);
            if (ny.attacking || ny.roaring) { ny.attacking = false; ny.roaring = false; if (a != null) a.goToTarget = true; }
            if (ny.move != null) ny.move.modifier = false;
            if (proxy) { RabViews.Remove(ny); RabSeen.Remove(ny); RestoreRabbitState(e); return; }
            if (a == null) return;
            if (a.move != null && a.move.RB != null) a.lastVelocity = a.move.RB.velocity;
            a.fleeing = false;   // the stopFleeing coroutine ran on the old owner: its next threat sets it again here
            if (RabViews.TryGetValue(ny, out var v) && v.Has) a.interestPos = mainscript.UnityPosFromGlobal(v.Interest);
            if (RabSeen.TryGetValue(ny, out bool seen) && seen) NyLastSee(ny) = Time.time;
            RabViews.Remove(ny);
        }

        internal static void SendRabbitHandover(Ent e, uint epoch)
        {
            var ny = NyOf(e);
            if (ny == null || ny.AI == null || !Entities.IsFinite(ny.AI.interestPos)) return;
            var target = mainscript.GlobalFromUnityPos(ny.AI.interestPos);
            var w = new NetWriter();
            RabbitStateCodec.Write(w, new RabbitStateCodec.State {
                Net = e.NetId, Epoch = epoch, Flags = RabFlags(ny),
                Run = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(ny.runValue, 0f, 0.5f) * 200f), 0, 100),
                Seen = Time.time - NyLastSee(ny) < ny.targetForgetTime, X = target.x, Y = target.y, Z = target.z });
            Entities.SendToServer(w, true); RabbitStatesSent++;
        }

        private static void RabbitHandoverTarget(Ent e, RabbitStateCodec.State s)
        {
            var ny = NyOf(e);
            if (ny == null || ny.AI == null) return;
            ny.AI.interestPos = mainscript.UnityPosFromGlobal(new Vector3d(s.X, s.Y, s.Z));
            if (s.Seen) NyLastSee(ny) = Time.time;
        }
    }
}
