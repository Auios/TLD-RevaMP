using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace TLDRevamp.Net
{
    internal static partial class EntityLocks
    {
        /// The game's container checks (mountStuff.CanStore, Update) read the host's setting on a client. Shipped in the core
        /// plugin: the Dev split (730d713, 2026-11-13) had moved it to TLDRevamp.Dev with the diagnostics beside it, and player
        /// installs (no Dev plugin) went back to each machine's own setting — an item held in a trunk on one machine lay loose on
        /// the other ("copy taken out of a container here", 2026-11-20).
        [HarmonyPatch]
        internal static class PhysLockFromHost
        {
            private static readonly FieldInfo Field = GameLookups.Field(typeof(settingsscript.setting), nameof(settingsscript.setting.IPhysicsLock));
            private static readonly MethodInfo Replacement = AccessTools.Method(typeof(EntityLocks), nameof(EntityLocks.PhysicsLockSetting));
            public static int Patched;

            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return GameLookups.Method(typeof(mountStuff), nameof(mountStuff.CanStore));
                yield return GameLookups.Method(typeof(mountStuff), "Update");
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var ins in instructions)
                {
                    if (ins.LoadsField(Field)) { ins.opcode = OpCodes.Call; ins.operand = Replacement; Patched++; }   // stack: setting -> int
                    yield return ins;
                }
            }
        }
    }
}
