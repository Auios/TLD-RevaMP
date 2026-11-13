using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using static TLDRevamp.Net.PlayerCombat;

namespace TLDRevamp.Net
{
    internal static class PlayerCombatTools
    {
        private static readonly NetWriter W = new NetWriter();

        /// Test (bridge `mp forgehit <id> <kind> <gx> <gy> <gz> <dx> <dy> <dz> [<creature net> <index>]`): a hit message
        /// as a modified client could send it — any origin, any direction, any creature, 1 hp.
        public static string Forge(int target, byte kind, Vector3d origin, Vector3 dir, uint creatureNet = 0, int creatureIdx = 0)
        {
            TLDRevamp.Net.PlayerCombat.ObservedW.Reset(); TLDRevamp.Net.PlayerCombat.ObservedW.U8(PlayerDamage); TLDRevamp.Net.PlayerCombat.ObservedW.VarU32((uint)target); TLDRevamp.Net.PlayerCombat.ObservedW.F32(1f); TLDRevamp.Net.PlayerCombat.ObservedW.F32(dir.x); TLDRevamp.Net.PlayerCombat.ObservedW.F32(dir.y); TLDRevamp.Net.PlayerCombat.ObservedW.F32(dir.z);
            TLDRevamp.Net.PlayerCombat.ObservedW.U8(kind); Protocol.WritePos(TLDRevamp.Net.PlayerCombat.ObservedW, origin);
            if (kind == HitCheck.KCreature) { TLDRevamp.Net.PlayerCombat.ObservedW.U32(creatureNet); TLDRevamp.Net.PlayerCombat.ObservedW.VarU32((uint)creatureIdx); }
            Entities.SendToServer(TLDRevamp.Net.PlayerCombat.ObservedW, true);
            return "{\"sent\":true}";
        }

        /// Test (bridge `mp shootat <id>`): the nearest gun to the local player aims its muzzle at remote player `id`
        /// and fires through the game's own weaponscript.Shot (so the transpiled raycast is what's tested).
        public static string ShootAt(int id, float aboveGround = -1f)
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"not in game\"}";
            weaponscript gun = null; float best = float.MaxValue;
            foreach (var w in Object.FindObjectsOfType<weaponscript>())
            {
                if (w == null || w.S == null || w.ammo == null) continue;
                float d = (w.transform.position - pl.transform.position).sqrMagnitude;
                if (d < best) { best = d; gun = w; }
            }
            if (gun == null) return "{\"error\":\"no gun nearby\"}";
            (int id, Vector3 feet, Vector3 head)? target = null;
            foreach (var b in RemotePlayers.BodyCapsules()) if (b.id == id) target = b;
            if (target == null) return "{\"error\":\"no body for player " + id + "\"}";
            // the gun in the shooter's hands, as a player fires it: one lying where it was spawned fires from there, and
            // the host rightly refuses a shot that started 13-25 m from the shooter (hitcheck run 2, C and E)
            var cam = pl.Cam != null ? pl.Cam.transform : pl.transform;
            if ((gun.S.position - cam.position).magnitude > 1.5f)
            {
                var root = gun.transform.root;
                var rb = root.GetComponent<Rigidbody>();
                root.position += cam.position + cam.forward * 0.6f - gun.S.position;
                if (rb != null) { rb.position = root.position; rb.velocity = Vector3.zero; }
            }
            var chest = Vector3.Lerp(target.Value.feet, target.Value.head, 0.6f);
            if (aboveGround >= 0f)   // a height over the ground under them (not from the body segment under test): knees, hips…
                foreach (var t in RemotePlayers.AiTargets())
                {
                    if (t.id != id) continue;
                    var root = t.body.position;
                    if (Physics.Raycast(root + Vector3.up * 0.5f, Vector3.down, out var gh, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        chest = new Vector3(root.x, gh.point.y + aboveGround, root.z);
                }
            var rot = gun.S.rotation;
            bool inf = gun.infinite;
            long t0 = ShotsTested, h0 = ShotHits;
            bool could = gun.CanShoot();
            try { gun.infinite = true; gun.S.rotation = Quaternion.LookRotation(chest - gun.S.position); gun.Shot(0); }
            finally { gun.S.rotation = rot; gun.infinite = inf; }
            return "{\"gun\":" + Json.Str(gun.name) + ",\"canShoot\":" + (could ? "true" : "false") + ",\"rayTested\":" + (ShotsTested - t0)
                   + ",\"hits\":" + (ShotHits - h0) + ",\"dist\":" + (chest - gun.S.position).magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "}";
        }

        /// Test tooling: the nearest gun fires once at a GLOBAL point (a car part, a tank) through the game's own Shot.
        public static string ShootPoint(double gx, double gy, double gz, string from = "")
        {
            var pl = mainscript.s != null ? mainscript.s.player : null;
            if (pl == null) return "{\"error\":\"not in game\"}";
            weaponscript gun = null; float best = float.MaxValue;
            foreach (var w in Object.FindObjectsOfType<weaponscript>())
            {
                if (w == null || w.S == null || w.ammo == null) continue;
                float d = (w.transform.position - pl.transform.position).sqrMagnitude;
                if (d < best) { best = d; gun = w; }
            }
            // every weapon in the scene (diagnosing a shot that meets nothing): name, distance, ammo, can it shoot
            var cands = new List<string>();
            foreach (var w in Object.FindObjectsOfType<weaponscript>())
                if (w != null) cands.Add("[" + Json.Str(w.name) + "," + (w.transform.position - pl.transform.position).magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                                         + "," + (w.S != null ? "true" : "false") + "," + (w.ammo != null ? "true" : "false") + "," + (w.CanShoot() ? "true" : "false") + "]");
            if (gun == null) return "{\"error\":\"no gun nearby\",\"weapons\":[" + string.Join(",", cands) + "]}";
            bool canShoot = gun.CanShoot();
            var at = mainscript.UnityPosFromGlobal(new Vector3d(gx, gy, gz));
            var rot = gun.S.rotation; var spos = gun.S.position;
            // `eye`: fired from the player's eye as if aimed (a gun lying on the floor next to a wall can't hit anything)
            // `near`: from 1.2 m short of the target on the line from the eye (test placement next to walls doesn't matter)
            bool fromEye = from == "eye" || from == "near";
            if (fromEye)
            {
                var cam = pl.Cam != null ? pl.Cam.transform : pl.transform;
                var dn = (at - cam.position).normalized;
                // never inside the shooter's own body (a creature pressed against them: 1.2 m short is behind the eye)
                float along = from == "near" ? Mathf.Max(0.3f, (at - cam.position).magnitude - 1.2f) : 0.3f;
                gun.S.position = cam.position + dn * along;
            }
            bool inf = gun.infinite;
            string first = "";
            // in the right hand the game fires from the player's head along the view (weaponscript.Shot: P.inRightHand →
            // player.Th): aim the view at the target like a player would — the muzzle doesn't matter then
            bool inHand = gun.P != null && gun.P.inRightHand && pl.Th != null;
            var thRot = inHand ? pl.Th.rotation : Quaternion.identity;
            var dir = at - (inHand ? pl.Th.position : gun.S.position);
            if (inHand) pl.Th.rotation = Quaternion.LookRotation(dir);
            if (Physics.Raycast(inHand ? pl.Th.position : gun.S.position, dir, out var h, dir.magnitude + 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                first = (h.collider.transform.parent != null ? h.collider.transform.parent.name + "/" : "") + h.collider.name;
            var hitNames = new List<string>();
            LastN = -1;   // stays -1 if the game's Shot returned before its ray
            try
            {
                gun.infinite = true; gun.S.rotation = Quaternion.LookRotation(dir); gun.Shot(0);
                // what the shot's own ray met (sorted by Shot), nearest first
                var hits = AccessTools.FieldRefAccess<weaponscript, RaycastHit[]>("hit")(gun);
                for (int i = 0; i < LastN && i < hits.Length && i < 6; i++)
                    if (hits[i].collider != null) hitNames.Add(hits[i].collider.transform.root.name + "/" + hits[i].collider.name + "@" + hits[i].distance.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            }
            finally { gun.S.rotation = rot; gun.infinite = inf; if (fromEye) gun.S.position = spos; if (inHand) pl.Th.rotation = thRot; }
            // everything on the shot's line, any layer, triggers too (what the gun's mask leaves out)
            var all = new List<string>();
            if (LastN >= 0)
                foreach (var hh in Physics.RaycastAll(LastOrigin, LastDir, 60f, ~0, QueryTriggerInteraction.Collide))
                    all.Add(Json.Str(hh.collider.transform.root.name + "/" + hh.collider.name + " L" + hh.collider.gameObject.layer + (hh.collider.isTrigger ? " trig" : "")
                                     + ((gun.LM.value & (1 << hh.collider.gameObject.layer)) != 0 ? " inMask" : "") + "@" + hh.distance.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
            var og = mainscript.GlobalFromUnityPos(LastOrigin);
            string ray = "{\"from\":[" + og.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + og.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + og.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                         + "],\"dir\":[" + LastDir.normalized.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + LastDir.normalized.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + LastDir.normalized.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                         + "],\"mask\":" + gun.LM.value + ",\"all\":[" + string.Join(",", all) + "]}";
            return "{\"shotHits\":[" + string.Join(",", hitNames.ConvertAll(Json.Str)) + "],\"gun\":" + Json.Str(gun.name) + ",\"hithole\":" + (gun.hithole ? "true" : "false") + ",\"explodeStuffs\":" + (gun.explodeStuffs ? "true" : "false") + ",\"inHand\":" + (inHand ? "true" : "false") + ",\"canShoot\":" + (canShoot ? "true" : "false") + ",\"rayN\":" + LastN + ",\"ray\":" + ray + ",\"weapons\":[" + string.Join(",", cands) + "],\"dist\":" + dir.magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",\"firstHit\":" + Json.Str(first) + "}";
        }
    }
}
