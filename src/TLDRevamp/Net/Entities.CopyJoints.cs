using System.Collections.Generic;
using UnityEngine;

namespace TLDRevamp.Net
{
    /// A thing glued on with a body of its own (attachablescript.parentAtAttach off: the game's ball joint, position locked,
    /// from the thing to what it's on) holds what it's on where the thing is. On the machine that simulates the thing that
    /// is right: its body is dynamic and rides along. On a machine where the thing is another player's copy its body is
    /// kinematic (moved to the owner's pose), and a kinematic body on a locked joint is a wall: the car under it can only
    /// go where the copy was put last frame. The playtest of 2026-11-06: an Attachable07 glued on the client's roof
    /// (its own object, net 1355) held the host's copy of that car — "car tweaking", "heavy positional desync", the copy
    /// 5–16 m off the owner's resting car, the place repair not taking — and the same holds a car driven HERE that wears
    /// another player's glued thing.
    ///
    /// While the thing's copy is kinematic and what it's on isn't, its joint's position is free (the game re-points the
    /// joint every frame and drops a thing whose joint is gone, so the joint stays). The thing still sits on its spot: the
    /// game puts it there every frame (attachablescript.Update → SetOwnPosition), and the owner's poses say the same.
    /// Locked again as soon as the thing is simulated here or what it's on is a copy too. Trailers on a hitch have their
    /// own joint handling (Entities.Hitch) and are left alone. A/B: false = before (locked everywhere).
    internal static partial class EntityPhysics
    {
        internal static bool CopyJointsFree = true;
        internal static long CopyJointsFreed, CopyJointsLocked;
        private static readonly Dictionary<ConfigurableJoint, Entities.Ent> _freedJoints = new Dictionary<ConfigurableJoint, Entities.Ent>();
        internal static System.Collections.Generic.IReadOnlyDictionary<UnityEngine.ConfigurableJoint, TLDRevamp.Net.Entities.Ent> ObservedFreedJoints => _freedJoints;
        private static readonly List<Entities.Ent> _jointed = new List<Entities.Ent>();
        internal static System.Collections.Generic.IReadOnlyList<TLDRevamp.Net.Entities.Ent> ObservedJointed => _jointed;
        private static readonly List<ConfigurableJoint> _jointsDone = new List<ConfigurableJoint>();

        /// Every physics step: a copy's joint free while it would hold a body simulated here, locked otherwise.
        internal static void CopyJointTick()
        {
            foreach (var e in _jointed)
            {
                var at = e.Root != null ? e.Root.attachable : null;
                var j = at != null ? at.J as ConfigurableJoint : null;
                if (j == null || _freedJoints.ContainsKey(j)) continue;
                if (WantJointFree(e, j)) SetJointFree(j, e, true);
            }
            if (_freedJoints.Count == 0) return;
            _jointsDone.Clear();
            foreach (var kv in _freedJoints)
                if (kv.Key == null || !WantJointFree(kv.Value, kv.Key)) _jointsDone.Add(kv.Key);
            foreach (var j in _jointsDone)
            {
                if (j != null) SetJointFree(j, _freedJoints[j], false);
                _freedJoints.Remove(j);
            }
        }

        internal static bool WantJointFree(Entities.Ent e, ConfigurableJoint j)
        {
            if (!CopyJointsFree || !Entities.InSession || e.Root == null || !e.Proxy) return false;
            var at = e.Root.attachable;
            if (at == null || !at.attached || at.parentAtAttach || at.J != j) return false;
            var own = j.GetComponent<Rigidbody>(); var on = j.connectedBody;
            return own != null && own.isKinematic && on != null && !on.isKinematic;
        }

        internal static void SetJointFree(ConfigurableJoint j, Entities.Ent e, bool free)
        {
            var m = free ? ConfigurableJointMotion.Free : ConfigurableJointMotion.Locked;
            j.xMotion = m; j.yMotion = m; j.zMotion = m;
            if (free) { _freedJoints[j] = e; CopyJointsFreed++; }
            else CopyJointsLocked++;
            Plugin.Log.LogInfo($"copy joint {(free ? "free" : "locked")}: {e.Root?.name} net {e.NetId} (owner {e.OwnerId}) on {(j.connectedBody != null ? j.connectedBody.name : "-")}");
        }

        // Session cleanup.
        internal static void BeginJointRefresh() => _jointed.Clear();
        internal static void AddJointed(Entities.Ent entity) => _jointed.Add(entity);
        internal static void ResetFreedJoints()
        {
            foreach (var kv in _freedJoints) if (kv.Key != null) { try { SetJointFree(kv.Key, kv.Value, false); } catch { } }
            _freedJoints.Clear();
        }
    }

    public static partial class Entities
    {
        /// Every 0.5 s (FixedTick): which shared objects hang on a joint of their own.
        internal static void RefreshJointed()
        {
            EntityPhysics.BeginJointRefresh();
            foreach (var e in Placed())
            {
                var at = e.Root != null ? e.Root.attachable : null;
                if (at != null && at.attached && !at.parentAtAttach && at.J is ConfigurableJoint && !HitchTrailer(e)) EntityPhysics.AddJointed(e);
            }
        }
    }
}
