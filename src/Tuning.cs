using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;

namespace EnhancedCoilhead
{
    // A config value that can be overridden by the host's value while in a multiplayer lobby.
    internal abstract class SyncedSetting
    {
        public string Key;
        public bool Synced;
        public abstract string Serialize();
        public abstract void SetOverride(string s);
        public abstract void ClearOverride();
        public abstract bool HasOverride { get; }
    }

    internal sealed class Setting<T> : SyncedSetting
    {
        public readonly ConfigEntry<T> Entry;
        private T _override;
        private bool _has;

        public Setting(ConfigEntry<T> entry, bool synced)
        {
            Entry = entry;
            Synced = synced;
            Key = entry.Definition.Section + "." + entry.Definition.Key;
        }

        public T Value => _has ? _override : Entry.Value;
        public override bool HasOverride => _has;
        public override string Serialize() => Convert.ToString((object)Entry.Value, CultureInfo.InvariantCulture);
        public override void SetOverride(string s)
        {
            _override = (T)Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture);
            _has = true;
        }
        public override void ClearOverride() { _has = false; }
    }

    // All tunable values. Getters are called from transpiled game code, so changes apply live.
    internal static class Tuning
    {
        public static Setting<float> ChaseSpeedMultiplier;
        public static Setting<float> WanderSpeed;
        public static Setting<float> NoticeRange;
        public static Setting<float> LoseAggroDistance;
        public static Setting<float> LoseAggroTime;
        public static Setting<float> MoveTimeBeforeCooldown;
        public static Setting<float> CooldownDuration;
        public static Setting<int> Damage;

        // Audio (local to each player's machine, no sync needed)
        public static Setting<float> VolumeMultiplier;
        public static Setting<float> AudibleDistanceMultiplier;

        // Smart patrol
        public static Setting<bool> SmartPatrol;
        public static Setting<float> PatrolMemory;
        // Noise hearing
        public static Setting<bool> NoiseHearing;
        public static Setting<float> MinNoiseLoudness;
        public static Setting<float> MinVoiceLoudness;
        public static Setting<float> InvestigateTimeout;
        public static Setting<bool> LogNoises;
        // Smart targeting
        public static Setting<bool> SmartTargeting;
        public static Setting<float> WatchedPenalty;
        public static Setting<float> IsolationRange;
        public static Setting<float> IsolationFactor;
        public static Setting<float> TargetHoldTime;

        public static readonly List<SyncedSetting> All = new List<SyncedSetting>();

        private static Setting<T> Add<T>(ConfigFile c, string section, string key, T def, string desc, bool synced = true)
        {
            var s = new Setting<T>(c.Bind(section, key, def, desc), synced);
            All.Add(s);
            return s;
        }

        // "Section.Key=value;Section.Key=value" for every synced setting (host side)
        public static string Snapshot()
        {
            var sb = new StringBuilder();
            foreach (var s in All)
            {
                if (!s.Synced) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(s.Key).Append('=').Append(s.Serialize());
            }
            return sb.ToString();
        }

        // client side: apply the host's values
        public static int ApplySnapshot(string snapshot)
        {
            int applied = 0;
            foreach (var part in snapshot.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq), val = part.Substring(eq + 1);
                foreach (var s in All)
                {
                    if (!s.Synced || s.Key != key) continue;
                    try { s.SetOverride(val); applied++; }
                    catch (Exception e) { Plugin.Log.LogWarning($"[sync] bad value for {key}: {val} ({e.Message})"); }
                    break;
                }
            }
            return applied;
        }

        // Returns true if any override was active.
        public static bool ClearOverrides()
        {
            bool had = false;
            foreach (var s in All)
            {
                if (s.HasOverride) had = true;
                s.ClearOverride();
            }
            return had;
        }

        public static bool IsSynced(ConfigEntryBase entry)
        {
            string key = entry.Definition.Section + "." + entry.Definition.Key;
            foreach (var s in All)
                if (s.Key == key) return s.Synced;
            return false;
        }

        public static void Bind(ConfigFile c)
        {
            // Vanilla values in comments. Defaults are a bit scarier than vanilla.
            ChaseSpeedMultiplier   = Add(c, "Chase", "ChaseSpeedMultiplier", 1.15f, "Multiplier on chase speed (vanilla 1.0)");
            WanderSpeed            = Add(c, "Wander", "WanderSpeed", 6f, "Speed while searching, not chasing (vanilla 6)");
            NoticeRange            = Add(c, "Wander", "NoticeRange", 45f, "Distance at which it starts chasing a player with a clear path (vanilla 30)");
            LoseAggroDistance      = Add(c, "Chase", "LoseAggroDistance", 40f, "Distance beyond which it may lose a target (vanilla 40)");
            LoseAggroTime          = Add(c, "Chase", "LoseAggroTime", 9f, "Seconds out of sight and far away before giving up (vanilla 4.5)");
            MoveTimeBeforeCooldown = Add(c, "Cooldown", "MoveTimeBeforeCooldown", 9f, "Moving seconds before it needs to rest (vanilla 9)");
            CooldownDuration       = Add(c, "Cooldown", "CooldownDuration", 6f, "Rest seconds after a chase burst (vanilla effectively 20)");
            Damage                 = Add(c, "Combat", "Damage", 90, "Damage per hit (vanilla 90)");

            VolumeMultiplier          = Add(c, "Audio", "VolumeMultiplier", 1f, "Coil-Head volume, relative to the game's own level (Unity caps a source at 1.0, so values above the original level have no extra effect)", false);
            AudibleDistanceMultiplier = Add(c, "Audio", "AudibleDistanceMultiplier", 1f, "Scales how far away its sounds can be heard (1 = vanilla, <1 = only when close, >1 = carries further)", false);

            SmartPatrol        = Add(c, "Patrol", "SmartPatrol", true, "Sweep AI nodes systematically instead of vanilla random search");
            PatrolMemory       = Add(c, "Patrol", "PatrolMemory", 90f, "Seconds before an already-swept area is considered worth revisiting");

            NoiseHearing       = Add(c, "Noise", "NoiseHearing", true, "Investigate noises (footsteps, dropped items, noisemakers)");
            MinNoiseLoudness   = Add(c, "Noise", "MinNoiseLoudness", 0.3f, "Ignore noises quieter than this (turn on LogNoises to calibrate)");
            MinVoiceLoudness   = Add(c, "Noise", "MinVoiceLoudness", 0.75f, "Voice chat is ignored unless at least this loud (game range 0.6 normal talking .. 0.9 shouting)");
            InvestigateTimeout = Add(c, "Noise", "InvestigateTimeout", 20f, "Seconds before giving up on reaching a noise");
            LogNoises          = Add(c, "Noise", "LogNoises", false, "Log every noise heard (loudness, distance) for tuning", false);

            SmartTargeting     = Add(c, "Targeting", "SmartTargeting", true, "Choose victims by situation instead of just the closest player");
            WatchedPenalty     = Add(c, "Targeting", "WatchedPenalty", 1.5f, "Score multiplier (>1 = worse victim) for players who are looking at it");
            IsolationRange     = Add(c, "Targeting", "IsolationRange", 15f, "A player further than this from every teammate counts as isolated");
            IsolationFactor    = Add(c, "Targeting", "IsolationFactor", 0.6f, "Score multiplier (<1 = better victim) for isolated players");
            TargetHoldTime     = Add(c, "Targeting", "TargetHoldTime", 4f, "Minimum seconds to stay on a chosen victim (avoids flip-flopping)");
        }

        // Called from transpiled IL (must be public static, no args or matching signature)
        public static float Wander() => WanderSpeed.Value;
        public static float Notice() => NoticeRange.Value;
        public static float LoseDistance() => LoseAggroDistance.Value;
        public static float LoseTime() => LoseAggroTime.Value;
        public static float MoveTime() => MoveTimeBeforeCooldown.Value;
        public static float Cooldown() => CooldownDuration.Value;
        public static int HitDamage() => Damage.Value;
        public static float ChaseSpeed(float vanilla) => vanilla * ChaseSpeedMultiplier.Value;
    }
}
