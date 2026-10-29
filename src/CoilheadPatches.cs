using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace EnhancedCoilhead
{
    internal static class IL
    {
        // Replace every float literal == target with a call to Tuning.<getter>(). Returns how many were replaced.
        public static int ReplaceFloat(List<CodeInstruction> code, float target, string getter)
        {
            var m = AccessTools.Method(typeof(Tuning), getter);
            int n = 0;
            foreach (var ins in code)
            {
                if (ins.opcode == OpCodes.Ldc_R4 && ins.operand is float f && Math.Abs(f - target) < 1e-4f)
                {
                    ins.opcode = OpCodes.Call; // keeps labels / exception blocks
                    ins.operand = m;
                    n++;
                }
            }
            return n;
        }

        public static void Report(string where, string what, int found, int expected)
        {
            if (found != expected)
                Plugin.Log.LogWarning($"[{where}] {what}: replaced {found}, expected {expected} (game updated?)");
            else
                Plugin.Log.LogInfo($"[{where}] {what}: ok");
        }
    }

    [HarmonyPatch(typeof(SpringManAI), nameof(SpringManAI.DoAIInterval))]
    internal static class DoAIInterval_Patch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            IL.Report("DoAIInterval", "wander speed 6f",   IL.ReplaceFloat(code, 6f,   nameof(Tuning.Wander)),       1);
            IL.Report("DoAIInterval", "notice range 30f",  IL.ReplaceFloat(code, 30f,  nameof(Tuning.Notice)),       1);
            IL.Report("DoAIInterval", "lose distance 40f", IL.ReplaceFloat(code, 40f,  nameof(Tuning.LoseDistance)), 1);
            IL.Report("DoAIInterval", "lose time 4.5f",    IL.ReplaceFloat(code, 4.5f, nameof(Tuning.LoseTime)),     1);
            return code;
        }
    }

    [HarmonyPatch(typeof(SpringManAI), nameof(SpringManAI.Update))]
    internal static class Update_Patch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            IL.Report("Update", "move time 9f",  IL.ReplaceFloat(code, 9f,  nameof(Tuning.MoveTime)),  1);
            IL.Report("Update", "cooldown 11f (pre-RPC)",  IL.ReplaceFloat(code, 11f, nameof(Tuning.Cooldown)),  1);

            // After every load of currentChaseSpeed, multiply by our config value.
            var field = AccessTools.Field(typeof(SpringManAI), "currentChaseSpeed");
            var scale = AccessTools.Method(typeof(Tuning), nameof(Tuning.ChaseSpeed));
            int n = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode == OpCodes.Ldfld && ReferenceEquals(code[i].operand, field))
                {
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, scale));
                    i++;
                    n++;
                }
            }
            IL.Report("Update", "currentChaseSpeed loads", n, 2);
            return code;
        }
    }

    // The real cooldown length: the ClientRpc overwrites onCooldownPhase with 20f on every machine (host included).
    [HarmonyPatch(typeof(SpringManAI), nameof(SpringManAI.SetCoilheadOnCooldownClientRpc))]
    internal static class CooldownRpc_Patch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            IL.Report("SetCoilheadOnCooldownClientRpc", "cooldown 20f", IL.ReplaceFloat(code, 20f, nameof(Tuning.Cooldown)), 1);
            return code;
        }
    }

    [HarmonyPatch(typeof(SpringManAI), nameof(SpringManAI.OnCollideWithPlayer))]
    internal static class OnCollide_Patch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var m = AccessTools.Method(typeof(Tuning), nameof(Tuning.HitDamage));
            int n = 0;
            foreach (var ins in code)
            {
                if ((ins.opcode == OpCodes.Ldc_I4_S || ins.opcode == OpCodes.Ldc_I4) && Convert.ToInt32(ins.operand) == 90)
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = m;
                    n++;
                }
            }
            IL.Report("OnCollideWithPlayer", "damage 90", n, 1);
            return code;
        }
    }
}
