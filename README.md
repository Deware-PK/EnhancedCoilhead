# EnhancedCoilhead

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **Lethal Company** that makes the Coil-Head smarter and scarier. Every behaviour is configurable live through [LethalConfig](https://thunderstore.io/c/lethal-company/p/AinaVT/LethalConfig/), and the host's settings are synced to all players.

> Unofficial fan mod. Lethal Company is made by Zeekerss. This repository contains none of the game's code or assets.

## Features

- **Systematic patrol**: instead of wandering at random, it sweeps the map's AI nodes (nearest unvisited first), remembers what it has checked, and starts a new round once the map is covered. After losing a chase it first checks where it last saw a player.
- **Hears noises**: dropped items, noisemakers, tools and other sounds pull it toward the source. Louder sounds win over quieter ones.
- **Hears voice chat**: shouting draws it in (normal talking does not, by default). Clients' voice is relayed to the host.
- **Picks its victim**: favours players who are not looking at it and players far from their teammates, instead of always taking the closest. It holds a target for a few seconds to avoid flip-flopping.
- **Tunable stats**: chase speed, wander speed, notice range, how long it chases, how long it rests, and damage.
- **Audio controls**: local volume and audible distance (per player, not synced).

## Requirements

- BepInEx 5 (BepInExPack)
- [LethalNetworkAPI](https://thunderstore.io/c/lethal-company/p/xilophor/LethalNetworkAPI/)
- [LethalConfig](https://thunderstore.io/c/lethal-company/p/AinaVT/LethalConfig/) (optional; without it, edit the config file instead)

**All players should install the mod.** A client's local settings are overridden by the host's while in a lobby.

## Configuration

Values are in the in-game LethalConfig menu and in `BepInEx/config/com.phakin.enhancedcoilhead.cfg`. Vanilla values are shown for reference.

| Section | Setting | Default | Vanilla | Synced |
|---|---|---|---|---|
| Chase | `ChaseSpeedMultiplier` | 1.15 | 1.0 | yes |
| Chase | `LoseAggroDistance` | 40 | 40 | yes |
| Chase | `LoseAggroTime` | 9 | 4.5 | yes |
| Wander | `WanderSpeed` | 6 | 6 | yes |
| Wander | `NoticeRange` | 45 | 30 | yes |
| Cooldown | `MoveTimeBeforeCooldown` | 9 | 9 | yes |
| Cooldown | `CooldownDuration` | 6 | 20 | yes |
| Combat | `Damage` | 90 | 90 | yes |
| Patrol | `SmartPatrol` | true | n/a | yes |
| Patrol | `PatrolMemory` | 90 | n/a | yes |
| Noise | `NoiseHearing` | true | n/a | yes |
| Noise | `MinNoiseLoudness` | 0.3 | n/a | yes |
| Noise | `MinVoiceLoudness` | 0.75 | n/a | yes |
| Noise | `InvestigateTimeout` | 20 | n/a | yes |
| Noise | `LogNoises` | false | n/a | no |
| Targeting | `SmartTargeting` | true | n/a | yes |
| Targeting | `WatchedPenalty` | 1.5 | n/a | yes |
| Targeting | `IsolationRange` | 15 | n/a | yes |
| Targeting | `IsolationFactor` | 0.6 | n/a | yes |
| Targeting | `TargetHoldTime` | 4 | n/a | yes |
| Audio | `VolumeMultiplier` | 1 | 1 | no |
| Audio | `AudibleDistanceMultiplier` | 1 | 1 | no |

Noise loudness reference (from the game): dropped item 0.5, flashlight 0.4 to 0.65, shovel/knife/noisemakers about 0.8, boombox 0.9, voice chat 0.6 (talking) to 0.9 (shouting). Turn on `LogNoises` to see what the Coil-Head hears and tune the thresholds.

## How it works

The mod patches the game with [HarmonyX](https://github.com/BepInEx/HarmonyX).

- **Stats** (`src/CoilheadPatches.cs`, `src/Tuning.cs`): IL transpilers replace hard-coded numbers in `SpringManAI` (speeds, ranges, timers, damage) with live config reads. Each transpiler logs whether it matched, so a game update that moves things shows up as a warning in the log instead of a crash. The real cooldown length is the `20f` set inside `SetCoilheadOnCooldownClientRpc`, which overrides the value in `Update`.
- **Behaviour** (`src/CoilBrain.cs`, `src/AiPatches.cs`): a per-Coil-Head "brain" is driven by postfixes on `DoAIInterval` and `Update`. It takes over the wander phase by marking the game's own search routine as "in progress" so the vanilla random search does not restart, then steers with `SetDestinationToPosition`. Noise comes in through `EnemyAI.DetectNoise`, and victim choice by re-scoring after `TargetClosestPlayer`.
- **Multiplayer** (`src/NetSync.cs`): uses LethalNetworkAPI. The host sends a config snapshot to clients on join and whenever a value changes. Two noises are produced only on one client's machine, so clients forward them to the host's AI: voice chat (ID 75, speaker only) and dropped items (ID 941, item owner only). With `LogNoises` on, the host log shows `via=client` for relayed noises and `via=local` for ones it heard itself.
- **LethalConfig** (`src/LethalConfigSetup.cs`): kept in its own class and compiled only if LethalConfig is present, so the mod still loads without it.

### Project layout

```
EnhancedCoilhead/
  EnhancedCoilhead.csproj
  src/
    Plugin.cs              entry point, BepInEx wiring
    Tuning.cs              all settings + host/client override logic
    CoilheadPatches.cs     IL transpilers (stats)
    CoilBrain.cs           patrol, noise, targeting, audio
    AiPatches.cs           Harmony hooks into the game
    NetSync.cs             config sync + voice relay
    LethalConfigSetup.cs   in-game config menu
  thunderstore/            package files (manifest, README, icon, LICENSE)
```

## Building

You need the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer) and a Lethal Company install.

1. Open `EnhancedCoilhead.csproj` and set:
   - `GameDir`: your Lethal Company folder.
   - `ProfileDir`: your Thunderstore Mod Manager profile (the one with LethalNetworkAPI installed; LethalConfig too if you want the in-game menu).
2. Build:

   ```
   dotnet build -c Release
   ```

   If `ProfileDir\BepInEx\plugins` exists, the DLL is copied into it automatically.

The build references the game's DLLs from `GameDir` and does not copy them anywhere. Do not commit game files or decompiled code.

### Packaging for Thunderstore

1. Copy `bin/Release/netstandard2.1/EnhancedCoilhead.dll` into `thunderstore/`.
2. Zip the files inside `thunderstore/` (the files must be at the root of the zip, not in a folder).
3. Bump the version in both `thunderstore/manifest.json` and `PluginInfo.VERSION` in `src/Plugin.cs`, and add a line to `thunderstore/CHANGELOG.md`.

## Known limits

- Multiplayer has had limited testing; please include both host and client `LogOutput.log` in bug reports.
- Clients relay voice chat and dropped-item noises to the host. Other noises the game only raises on the owner's machine (if any) are not heard from clients; footsteps, noisemakers, flashlights and boomboxes already reach the host.
- Other mods that rewrite the same Coil-Head code may conflict; the transpilers log a warning if their expected code is missing.

## Contributing

Issues and pull requests are welcome. When reporting a bug, include your `LogOutput.log` and the list of mods in your profile.

## License

[MIT](LICENSE)
