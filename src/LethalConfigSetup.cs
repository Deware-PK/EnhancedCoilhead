#if HAS_LETHALCONFIG
using System.Runtime.CompilerServices;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;

namespace EnhancedCoilhead
{
    // Kept in its own class so the mod still loads when LethalConfig is missing.
    internal static class LethalConfigSetup
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register()
        {
            F(Tuning.ChaseSpeedMultiplier, 0.5f, 3f);
            F(Tuning.WanderSpeed, 2f, 12f);
            F(Tuning.NoticeRange, 10f, 100f);
            F(Tuning.LoseAggroDistance, 10f, 100f);
            F(Tuning.LoseAggroTime, 1f, 30f);
            F(Tuning.MoveTimeBeforeCooldown, 3f, 30f);
            F(Tuning.CooldownDuration, 1f, 30f);
            F(Tuning.VolumeMultiplier, 0f, 1f);
            F(Tuning.AudibleDistanceMultiplier, 0.25f, 3f);
            B(Tuning.SmartPatrol);
            F(Tuning.PatrolMemory, 10f, 300f);
            B(Tuning.NoiseHearing);
            F(Tuning.MinNoiseLoudness, 0f, 2f);
            F(Tuning.MinVoiceLoudness, 0f, 1f);
            F(Tuning.InvestigateTimeout, 5f, 60f);
            B(Tuning.LogNoises);
            B(Tuning.SmartTargeting);
            F(Tuning.WatchedPenalty, 1f, 3f);
            F(Tuning.IsolationRange, 5f, 50f);
            F(Tuning.IsolationFactor, 0.2f, 1f);
            F(Tuning.TargetHoldTime, 0f, 15f);
            LethalConfigManager.AddConfigItem(new IntSliderConfigItem(
                Tuning.Damage.Entry, new IntSliderOptions { Min = 10, Max = 200, RequiresRestart = false }));
        }

        private static void B(Setting<bool> e)
        {
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(
                e.Entry, new BoolCheckBoxOptions { RequiresRestart = false }));
        }

        private static void F(Setting<float> e, float min, float max)
        {
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(
                e.Entry, new FloatSliderOptions { Min = min, Max = max, RequiresRestart = false }));
        }
    }
}
#endif
