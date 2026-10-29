# EnhancedCoilhead

Makes the Coil-Head smarter and scarier, with every behaviour adjustable in-game through LethalConfig.

**All players should install this mod.** The host's settings are sent to everyone when they join (and whenever the host changes a value).

## What it changes

- **Systematic patrol.** Instead of wandering at random, it sweeps the map's AI nodes, remembers what it has already checked, and starts a new round once the map is covered. After losing a chase it first checks where it last saw you.
- **Hears noises.** Dropped items, noisemakers, tools and, above all, **voice chat** draw it toward the sound. Louder noises win over quieter ones. Voice only counts when it is loud (`MinVoiceLoudness`, default 0.75), so shouting works but normal talking does not.
- **Chooses its victim.** Instead of always taking the closest player it favours players who are not looking at it and players far from their teammates, and it sticks with a target for a few seconds so it doesn't flip-flop.
- **Tunable stats.** Chase speed, wander speed, notice range, how long it chases, how long it rests, and damage.
- **Audio.** Local volume and audible-distance controls (per player, not synced).

## Settings

Everything is in the in-game LethalConfig menu and can be changed live. The host's values apply to all players while in a lobby. `LogNoises` prints each noise it hears to the BepInEx log, which helps with tuning `MinNoiseLoudness`.

## Known limits

- **Multiplayer is lightly tested.** Everything was tested solo as host. Config sync to clients and relaying clients' voice to the host are new; please report problems with your `LogOutput.log` from both host and client.
- Clients relay voice chat and dropped-item noises to the host. Footsteps, noisemakers, flashlights and boomboxes already reach the host on their own.
- This is an early release; behaviour is balanced for a harder game, so expect it to be scary. Lower `ChaseSpeedMultiplier` and raise `CooldownDuration` if it is too much.

## Credits

Built on BepInEx, HarmonyX, LethalConfig by AinaVT and LethalNetworkAPI by Xilophor. Lethal Company is by Zeekerss; this mod is unofficial.

## License

MIT. See the LICENSE file in this package.
