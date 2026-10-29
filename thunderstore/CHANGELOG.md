# Changelog

## 0.2.0
Multiplayer fixes, tested with a host and a client (2 players over LAN).

- **Fixed: host settings never reached clients.** Sync relied on a per-frame update that does not run in Lethal Company, so clients silently used their own values. Sync is now event-driven: clients request the host's settings when they join, the host pushes changes as soon as a synced value is edited, and clients drop the host's values when they leave the lobby.
- **Fixed: items dropped by clients were never heard.** The game only raises that noise on the client who dropped the item; clients now forward it to the host, like voice chat.
- **Fixed: targeting could bounce between players during a chase.** The game hands the Coil-Head to the chased player's machine, and each machine forgot the target lock. A new owner now holds the current victim for `TargetHoldTime` before reconsidering.
- **Fixed: after a chase it went to where the player was, not where it last saw them.** It now remembers the victim's position only while it can actually see them.
- **Fixed: a place to investigate could expire while it was resting** (for example when `CooldownDuration` is longer than `InvestigateTimeout`).
- With `LogNoises` on, noise lines show `via=client` or `via=local`, and target and patrol decisions are logged too.
- The host log warns when a client runs a different version of the mod.

## 0.1.0 (first release)
- Initial release: systematic patrol, noise and voice hearing, smarter victim selection, tunable stats, audio controls, host-synced config.
