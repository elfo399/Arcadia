# Dungeon framework

`CoreGenerator` retains graph topology, room placement, themes, prefab instantiation, connected doors, NavMesh, player spawn, minimap registration, and boss floor progression. It does not decorate authored rooms.

`Room` is the generated prefab context. Its stable runtime ID is the full deterministic composite of run seed, floor, anchor, placement role, and `RoomData.stableId`. It owns room/floor persistence, aggregate diagnostics, encounter ownership, and door-lock reasons. A rule owns its own encounter; Room does not treat every enemy in a prefab as one encounter.

Legacy authored `EnemySpawner` registrations are routed to the compatibility `CombatRoomRule` even when spawning happens after Room initialization. Existing Shop/Treasure/Curch/EvilCurch prefabs retain their legacy entry lock through the explicit compatibility flag; disable that flag for newly authored unlocked special rooms. Internal `InteractableDoor` components remain local unless their legacy whole-room mode is enabled.

## State and saving

Save version 8 separates run state (`runSeed`, modifiers, OncePerRun IDs) from the current-floor state (floor number and room/rule/minimap records). `DungeonRunStateController` is authoritative during a live run; `GameData` is only imported at run start and exported on save. Changing floor discards only floor records. Significant room, interaction, event, modifier, and minimap changes request the existing throttled player save.

Completed encounters remain completed. Interrupted combat, waves and challenges intentionally restart at their deterministic initial state: no enemy transforms, HP, projectiles, or timers are saved. v6 saves migrate through v7; v7 records are converted into v8 floor state while preserving modifiers and OncePerRun IDs.

## Legacy migration

Run **Arcadia > Dungeon > Generate and validate stable IDs** before authoring new dungeon content. It assigns missing `RoomData.stableId` values and unique prefab `RoomRule` IDs, while reporting duplicate RoomData IDs. It is editor-only and never mutates shared assets at runtime.

Old rooms with `EnemySpawner` automatically receive a runtime `CombatRoomRule`, including rooms that now also have rewards/events. Old `RoomData.rewards` remain active through `LegacyRoomRewardRule` unless a modern `RoomRewardRule` is present; never put both reward systems on a prefab deliberately.

## Authoring

- Normal combat: authored `EnemySpawner`s work unchanged; add `CombatRoomRule` only to explicitly configure combat.
- Waves: add `WaveRoomRule`, `DungeonWaveSpawnPoint`s, and ordered `SpawnTable` lists. Wave points use the same enemy construction as `EnemySpawner`.
- Challenges: add `ChallengeRoomRule`; it is voluntarily interacted with. Gauntlet, Timed Kill, and Perfect Combat are supported. Challenge failure resolves as failed, removes only challenge-owned enemies, and cannot grant success-gated rewards.
- Treasure/rewards: create a `LootPoolDefinition`, add `RoomRewardRule`, and add one `DungeonRewardOfferAnchor` with collider per physical pedestal. Offers are deterministic. The player selects a pedestal; no first-offer auto-claim exists. Configure `requiredRuleId` to gate a reward on a successful challenge/combat rule.
- Shrine: add `ShrineInteraction` to each authored choice object. Choose a thematic family, modifier, costs, and requirements. Modifiers use concrete outgoing/incoming damage multipliers and persist across floors.
- Secrets/internal areas: `DungeonSecretAccess` opens authored geometry only—never a graph cell/minimap icon. `InteractableDoor` is internal by default; configure a `DungeonRequirement` (including consumable inventory `ItemData`) and it will not unlock graph doors.
- Events: `DungeonNarrativeEvent` uses existing PlayerStats flags/Karma/Benedetto/Malefico and supports Repeatable, OncePerRun, and OncePerSave. Existing NPC/dialogue components remain the dialogue authority.

`DungeonFloorThemeTable` remains the theme source. Its optional `DungeonFloorDefinition` provides deterministic normal room min/max counts (`min == max` is fixed). Legacy CoreGenerator values remain the fallback.

Run modifiers also support maximum Health/Stamina/Mana, stamina regeneration, and flask healing multipliers. They are recalculated from base-derived stats, preserve current resource ratios, and never mutate permanent attributes.
