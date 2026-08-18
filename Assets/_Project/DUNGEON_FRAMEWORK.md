# Arcadia dungeon framework

`CoreGenerator` still owns deterministic graph topology, special-room placement, theme selection, prefab instantiation, door connection, NavMesh build, player spawn and minimap registration. It does not decorate rooms procedurally. Every physical room remains an authored prefab.

`Room` is the runtime context for a prefab. On generation it receives a stable ID derived from run seed, floor, grid anchor, placement role and `RoomData.stableId`. It owns connected doors, entry/completion state and dispatches lifecycle events to `RoomRule` components. A room is complete only after every blocking rule is complete. Legacy prefabs containing `EnemySpawner` but no rule get a runtime `CombatRoomRule` compatibility component.

## Authoring

Give every `RoomData` a stable ID once. Add one or more `RoomRule`s to the room root and give each a unique rule ID.

- Normal combat: add `CombatRoomRule` and authored `EnemySpawner`s.
- Treasure: create a `LootPoolDefinition`, add `RoomRewardRule`, assign the pool, and put a collider on its reward pedestal (or room) so it can be interacted with. Set its completion blocking setting only when a claim must gate the room.
- Waves: add `WaveRoomRule`, one or more `DungeonWaveSpawnPoint`s, and ordered wave `SpawnTable` lists.
- Challenges: add `ChallengeRoomRule`; it starts only on interaction. Select Gauntlet, Timed Kill, or Perfect Combat and configure its waves. Failed, unfinished challenges restart rather than restoring individual enemies.
- Shrine: add a `ShrineInteraction` to each authored choice object. Use a family label, requirement/cost, and a `RunModifierDefinition` (Blessing, Curse, or Pact). The generated controller is the only run-modifier authority.
- Secret/internal area: add `DungeonSecretAccess` to an authored lever/door and reference the object to open. It does not make a grid cell or minimap icon.
- NPC/narrative, risk/reward, sacrifice and stat checks: add `DungeonNarrativeEvent`, configure its occurrence policy, requirements, cost/sacrifice and karma/story consequences. Existing dialogue/NPC components remain the dialogue authority.

`DungeonFloorThemeTable` remains the theme source of truth. Its optional `DungeonFloorDefinition` sets deterministic normal-room ranges (`min == max` is exact) and exposes floor pool hooks.

## Save/load and determinism

Save version 7 adds JsonUtility-safe `SavedDungeonRunState`, keyed by room/rule IDs. Layout is regenerated from its original seed; visited/revealed/completed state, reward claims, interactions, modifier state and rule payloads are restored. Completed combat does not respawn. Incomplete combat, waves and challenges intentionally restart from their initial deterministic state. `System.Random` streams are derived through `DungeonDeterminism`; no generated content should use Unity global random.

Run-only modifiers and active dungeon state are cleared when `PlayerStats.TryCompleteRun` succeeds. Permanent consequences use existing `PlayerStats` story flags and Karma/Benedetto/Malefico persistence.
