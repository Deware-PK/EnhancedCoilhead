using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace EnhancedCoilhead
{
    [HarmonyPatch(typeof(SpringManAI), nameof(SpringManAI.DoAIInterval))]
    internal static class Brain_Interval_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(SpringManAI __instance) => CoilBrain.Get(__instance).OnInterval(__instance);
    }

    [HarmonyPatch(typeof(SpringManAI), nameof(SpringManAI.Update))]
    internal static class Brain_Frame_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(SpringManAI __instance) => CoilBrain.Get(__instance).OnFrame(__instance);
    }

    // SpringManAI does not override DetectNoise, so we hook the base method and filter.
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.DetectNoise))]
    internal static class DetectNoise_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyAI __instance, Vector3 noisePosition, float noiseLoudness, int noiseID)
        {
            if (__instance is SpringManAI coil)
            {
                // Server-side AI handles it directly. A client forwards the noises that only exist on its
                // own machine (its voice, items it drops) so the host-side AI can hear them too.
                if (coil.IsServer)
                    CoilBrain.Get(coil).Hear(coil, noisePosition, noiseLoudness, noiseID, viaClient: false);
                else if (NetSync.ShouldRelay(noiseID, noiseLoudness))
                    NetSync.RelayNoise(coil, noisePosition, noiseLoudness, noiseID);
            }
        }
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.TargetClosestPlayer))]
    internal static class TargetClosest_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyAI __instance, bool __result)
        {
            if (__result && __instance is SpringManAI coil)
                CoilBrain.Get(coil).ReTarget(coil);
        }
    }
}
