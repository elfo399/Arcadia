# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Root: `C:/Users/Alfon/Desktop/Unity/Arcadia` (verified `Assets`, package manifest and project version).
- Last analyzed: 2026-09-07, commit `abca6f608343247614726b83c9eec2ec51a0a717`.
- Scope: onboarding for furnishing authored dungeon room prefabs with existing theme assets. This document records the baseline before that task's changes.
- Existing guidance: `handoff.md` contains architectural history; its workspace, commit and several old `Assets/Scripts` paths are stale. No repository or ancestor `AGENTS.md` was found.

## Confirmed Environment

- Unity **2022.3.62f3**, revision `96770f904ca7` (`ProjectSettings/ProjectVersion.txt`).
- Universal Render Pipeline **14.0.12**; Graphics Settings reference `Assets/_Project/Settings/Rendering/Ultra_PipelineAsset.asset`.
- Input System **1.14.2**; `activeInputHandler: 1`, with first-party `UnityEngine.InputSystem` use in `PlayerController.cs`.
- Intended release platforms: **unknown** from this focused inspection.

## Important Packages And Frameworks

| Area | Finding | Confidence / evidence |
| --- | --- | --- |
| Rendering and authoring | URP/Shader Graph 14.0.12, ProBuilder 5.2.4; legacy Post Processing package also installed | Confirmed, `Packages/manifest.json` |
| Navigation | AI Navigation 1.1.7; generator builds a `NavMeshSurface` after room generation | Confirmed, manifest and `Scripts/Dungeon/CoreGenerator.cs` |
| Camera and UI | Cinemachine 2.10.5, TMP 3.0.9, UGUI 1.0.0 | Confirmed, manifest |
| Tests | Unity Test Framework 1.1.33 is an indirect dependency | Confirmed, `Packages/packages-lock.json` |
| Networking | No multiplayer framework identified in manifest or lockfile | Unknown runtime networking beyond this package inspection |
| Package sources | Inspected dependencies are Unity registry/builtin; no Git/local/MCP dependency identified | Confirmed, manifest and lockfile |

## Directory Structure

Paths below are relative to `Assets/_Project/` unless otherwise indicated.

| Path | Purpose | Confidence |
| --- | --- | --- |
| `Scripts/` | First-party gameplay, player, combat, dungeon, rooms, UI, dialogue, save/system code | Confirmed |
| `Scripts/Editor/` | Asset tools, migrations, room validation, prefab scatter and ProBuilder floor tooling | Confirmed |
| `Data/Database/Floor/Floors/` | Authored room prefabs and theme/room-set data, grouped by floor and theme | Confirmed |
| `Prefabs/Floor/Floors/` | Theme scenery and imported asset collections | Confirmed |
| `Data/ScriptableObjects/Rooms/Rooms/` | Shared room definitions grouped by structural role and size | Confirmed |
| `Scenes/` | `GameScene.unity`, `HubSceneV1.unity` | Confirmed |
| `Art/`, `Input/`, `Resources/`, `Settings/`, `Weather/` | Supporting assets, input actions, runtime resources and configuration | Confirmed directories; individual systems not fully audited |
| `Assets/_Prototype/Imported3D/` | Additional imported model libraries | Confirmed |

Theme folders include Forest, DarkForest, Castel, RunedCastel, Catacomb, Necropolis and Final. Preserve their existing spelling and asset identities.

## Assembly Boundaries

No `.asmdef` or `.asmref` was found under tracked/project source paths. First-party scripts therefore use the standard `Assembly-CSharp` runtime and `Assembly-CSharp-Editor` editor split (**confirmed** folder conventions and generated project files). Editor-only tools reside under `Scripts/Editor`; no first-party test assembly was identified.

## Scenes And Startup Flow

- **Confirmed:** only `Assets/_Project/Scenes/GameScene.unity` is enabled in `ProjectSettings/EditorBuildSettings.asset`; it is the configured build startup scene.
- **Confirmed:** `CoreGenerator` selects theme/floor definitions, instantiates weighted room prefabs and builds navigation; default grid offsets are 50 units on X/Z.
- **Confirmed:** `SceneLoader` loads a configured scene from a player trigger; its default is `GameScene`. `CoreGenerator.hubSceneName` defaults to `HubScene`.
- **Unknown:** current serialized hub transition overrides. The existing scene is named `HubSceneV1`, and no hub scene is enabled in Build Settings; do not assume the default hub transition succeeds in a player build.

## Architecture

| Pattern | Finding | Confidence / evidence |
| --- | --- | --- |
| Data-driven generation | `DungeonThemeDefinition` points to `DungeonRoomSet`; weighted pools provide prefab variants by room role and grid size | Confirmed, `Scripts/Dungeon/` |
| Room composition | `Room` owns lifecycle, graph door sockets and portal placement; attached `RoomRule` components own gameplay conditions | Confirmed, `Scripts/Rooms/Room.cs`, `CombatRoomRule.cs` |
| Persistent identity | `RoomData.stableId`, rule IDs and saved room state are part of deterministic generation/persistence | Confirmed, `RoomData.cs`, `Room.cs` |
| Manager-oriented runtime | MonoBehaviours and static manager access such as `CoreGenerator.Instance` and `PlayerStats.instance` | Confirmed in inspected code |
| UI boundaries | Keep large UI behavior out of `InventoryUI`; dedicated menu/section managers own orchestration | Existing rule from `handoff.md`; UI implementation not re-audited |

## Coding Conventions

Inspected first-party classes mostly use the global namespace, PascalCase types/methods, camelCase fields, public inspector fields and `[SerializeField] private` fields. Brace style is commonly Allman, but some room code is densely formatted. Comments mix Italian and English. Preserve local style; no general async or test convention was established.

## Testing And Validation

- No first-party EditMode/PlayMode tests or CI test instructions identified; Test Framework availability does not imply test coverage.
- Existing `Arcadia/Validation/Validate Room Type Rules` menu action checks expected rule components under the room prefab root (`Scripts/Editor/RoomTypeValidation.cs`).
- For furnishing, validate prefab load/save, missing references/scripts, geometry placement, door/interaction access and representative renders. Navigation is built at runtime, so final traversal still benefits from Play Mode inspection.
- No tests, builds, scene opening or asset imports were run as part of this onboarding.

## Available Unity Tooling

- Unity MCP capabilities: **unavailable** in the active tool catalog; no MCP package/configuration identified. Repository inspection remains available.
- Unity Editor connection, console, Play Mode, profiler and live object inspection: **unverified** by this onboarding. The parent task separately handles local Editor batch inspection and validation.
- Existing authoring menus include `Tools/Arcadia/Prefab Scatter Tool` and room validation. Do not invoke migration/maintenance tools merely to place scenery.

## Important Constraints And Unknowns

- User scope is furnishing with existing assets. Preserve completed reference rooms and existing gameplay configuration, door sockets, floor surfaces, triggers, enemies, portals and room definitions.
- `RoomData.OnValidate` can generate missing stable IDs; `DungeonRoomSet.OnValidate` migrates serialized pools. Avoid saving unrelated ScriptableObjects during decoration work.
- Preserve `.meta` GUIDs and nested prefab references. Keep routes to doors and interactables clear; evaluate render bounds and collider behavior for placed scenery.
- Asset collections include imported/vendor content. Place instances in authored room prefabs; do not rewrite source meshes, materials or vendor libraries for decoration.
- `Library/`, `Temp/`, `Logs/`, `obj/` and generated solutions are cache/output, not authoritative architecture evidence.
- Current furnishing completeness and visual quality require the parent task's prefab/render inspection. Build readiness and complete runtime behavior are not established by this document.

## Source Files Inspected

`handoff.md`; `Packages/manifest.json`; `Packages/packages-lock.json`; `ProjectSettings/ProjectVersion.txt`; `EditorBuildSettings.asset`; `GraphicsSettings.asset`; `ProjectSettings.asset`; `Assets/_Project/Settings/Rendering/Ultra_PipelineAsset.asset` and its meta; `Assets/_Project/Scripts/Dungeon/{CoreGenerator,DungeonRoomSet,DungeonThemeDefinition}.cs`; `Scripts/Rooms/{Room,RoomData,CombatRoomRule}.cs`; `Scripts/Scene/SceneLoader.cs`; `Scripts/Player/PlayerController.cs`; `Scripts/Editor/{PrefabScatterToolWindow,RoomTypeValidation}.cs`. Directory/file searches supplied the assembly, scene and theme inventories.

<!-- unity-onboarding:generated:end -->
