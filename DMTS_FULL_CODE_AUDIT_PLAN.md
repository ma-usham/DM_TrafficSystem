# DM Traffic System Full Code Audit And Refactor Plan

## Scope

- Reviewed all `60` C# scripts under `Assets/DM_TrafficSystem`.
- Also reviewed the `2` tutorial scripts under `Assets/TutorialInfo` so the audit is explicit about what is and is not part of the shipping traffic system.
- Total reviewed code under `Assets/DM_TrafficSystem`: `9036` lines.
- Biggest files by size:
  - `Editor/DMTS_TOOL/Static/RoadBuilder.cs` (`675`)
  - `Editor/DMTS_TOOL/ConnectRoad/ConnectRoadToolState.cs` (`615`)
  - `Editor/DMTS_TOOL/Static/WaypointConnectionBuilder.cs` (`585`)
  - `Editor/DMTS_TOOL/ConnectRoad/ConnectRoadSceneTool.cs` (`435`)
  - `Editor/DMTS_TOOL/CreateRoad/RoadSceneTool.cs` (`408`)

## Reviewed Script Inventory

### Editor/CustomEditors

- `Editor/CustomEditors/AILaneEditor.cs`
- `Editor/CustomEditors/AIVehicleEditor.cs`
- `Editor/CustomEditors/PriorityIntersectionEditor.cs`
- `Editor/CustomEditors/RoadEditor.cs`
- `Editor/CustomEditors/TrafficLightIntersectionEditor.cs`

### Editor/DMTS_TOOL

- `Editor/DMTS_TOOL/ConnectRoad/ConnectRoadConnectionsPanel.cs`
- `Editor/DMTS_TOOL/ConnectRoad/ConnectRoadSceneTool.cs`
- `Editor/DMTS_TOOL/ConnectRoad/ConnectRoadToolState.cs`
- `Editor/DMTS_TOOL/CreateRoad/LaneSettingsPanel.cs`
- `Editor/DMTS_TOOL/CreateRoad/RoadSceneTool.cs`
- `Editor/DMTS_TOOL/CreateRoad/RoadSettingsPanel.cs`
- `Editor/DMTS_TOOL/DMTS_Window.cs`
- `Editor/DMTS_TOOL/Pages/ConnectRoadPage.cs`
- `Editor/DMTS_TOOL/Pages/CreateRoadPage.cs`
- `Editor/DMTS_TOOL/Pages/IntersectionSetupPage.cs`
- `Editor/DMTS_TOOL/Pages/IPage.cs`
- `Editor/DMTS_TOOL/Pages/MainPage.cs`
- `Editor/DMTS_TOOL/Pages/PriorityIntersectionPage.cs`
- `Editor/DMTS_TOOL/Pages/RoadSetupPage.cs`
- `Editor/DMTS_TOOL/Pages/TrafficLightIntersectionPage.cs`
- `Editor/DMTS_TOOL/Pages/TrafficManagerPage.cs`
- `Editor/DMTS_TOOL/Pages/ViewRoadsPage.cs`
- `Editor/DMTS_TOOL/Static/DMTSPrefs.cs`
- `Editor/DMTS_TOOL/Static/GlobalSceneGizmoState.cs`
- `Editor/DMTS_TOOL/Static/RoadBuilder.cs`
- `Editor/DMTS_TOOL/Static/RoadSceneGizmoDrawer.cs`
- `Editor/DMTS_TOOL/Static/RoadSceneVisibilityUtility.cs`
- `Editor/DMTS_TOOL/Static/SplineMathUtils.cs`
- `Editor/DMTS_TOOL/Static/WaypointConnectionBuilder.cs`

### RunTime/Authoring

- `RunTime/Authoring/AILane.cs`
- `RunTime/Authoring/AIWaypointConnection.cs`
- `RunTime/Authoring/Road.cs`

### RunTime/Components

- `RunTime/Components/AIVehicle.cs`
- `RunTime/Components/AIWaypoint.cs`
- `RunTime/Components/PriorityIntersection.cs`
- `RunTime/Components/TrafficLightIntersection.cs`
- `RunTime/Components/TrafficLightViolationDetector.cs`

### RunTime/Core

- `RunTime/Core/DMTS_API.cs`
- `RunTime/Core/TrafficManager.cs`
- `RunTime/Core/TrafficManager.Jobs.cs`
- `RunTime/Core/TrafficManager.Spawner.cs`
- `RunTime/Core/TrafficSpatialGrid.cs`
- `RunTime/Core/TrafficWaypointUpdater.cs`
- `RunTime/Core/VehiclePool.cs`

### RunTime/Enums

- `RunTime/Enums/DrivingDirection.cs`
- `RunTime/Enums/SplineMoveMode.cs`
- `RunTime/Enums/TrafficLightState.cs`
- `RunTime/Enums/VehicleType.cs`

### RunTime/Jobs

- `RunTime/Jobs/BehaviorDecisionJob.cs`
- `RunTime/Jobs/BuildWheelRaycastCommandsJob.cs`
- `RunTime/Jobs/SensorAggregationJob.cs`
- `RunTime/Jobs/TrafficSimulationJob.cs`
- `RunTime/Jobs/VehicleSensorJob.cs`

### RunTime/Navigation

- `RunTime/Navigation/TrafficPathfinder.cs`

### RunTime/ScriptableObjects

- `RunTime/ScriptableObjects/VehicleCollection.cs`

### RunTime/SimplePlayerController.cs

- `RunTime/SimplePlayerController.cs`

### RunTime/Structs

- `RunTime/Structs/DriverBehavior.cs`
- `RunTime/Structs/LiveDebugData.cs`
- `RunTime/Structs/VehicleState.cs`
- `RunTime/Structs/WaypointSettings.cs`

### TutorialInfo

- `Assets/TutorialInfo/Scripts/Readme.cs`
- `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`

## Architecture Snapshot

### Runtime

- `TrafficManager` owns buffers, jobs, pooling, spawning, and API-facing waypoint caches.
- `TrafficWaypointUpdater` is the main-thread graph bridge between `AIWaypoint` objects and the NativeArray waypoint buffer.
- `TrafficSimulationJob`, `VehicleSensorJob`, `SensorAggregationJob`, and `BuildWheelRaycastCommandsJob` form the runtime simulation loop.
- `AIVehicle` holds prefab-side authoring data, wheel visuals, sensors, and debug state.
- `TrafficSpatialGrid` and `TrafficPathfinder` provide lookup and routing support.

### Editor

- `DMTS_Window` is the editor shell.
- `RoadSceneTool`, `ConnectRoadSceneTool`, and the page classes drive the authoring workflows.
- `RoadBuilder` generates lanes and waypoints from splines.
- `WaypointConnectionBuilder` generates cross-road transition splines and their intermediate waypoints.
- `RoadSceneGizmoDrawer` and `RoadSceneVisibilityUtility` handle nearly all passive scene drawing and hierarchy organization.

## Scalability Verdict

This system is reasonably structured for small-to-medium traffic scenes, but it is not yet architected for very high-density traffic.

- Good fit today:
  - Small to moderate maps.
  - Tens of active vehicles.
  - Lower-hundreds only after careful tuning and platform-specific testing.
- Weak fit today:
  - Dense city traffic with hundreds of fully simulated vehicles at once.
  - Frequent runtime pathfinding requests.
  - Large editor scenes with many roads, connections, and always-on gizmos.

Important note:

- This verdict is based on source analysis, not on benchmark scenes. I did not run runtime stress profiling in this pass.

## Highest-Priority Findings

### 1. The simulation still blocks on the main thread every fixed step

Evidence:

- `RunTime/Core/TrafficManager.Jobs.cs:81-305`
- `RunTime/Jobs/BuildWheelRaycastCommandsJob.cs:10-39`
- `RunTime/Core/TrafficManager.Spawner.cs:178-185`

Why it matters:

- The job system is used, but the frame still waits on `_finalJobHandle.Complete()` in the same `FixedUpdate`.
- Every active vehicle also carries multiple physics queries per step:
  - traffic boxcast
  - player boxcast
  - left side boxcast
  - right side boxcast
  - up to `4` wheel raycasts
- That means the architecture is parallelized, but not deeply pipelined. Scale will flatten quickly as active vehicle count rises.

Refactor direction:

- Keep jobs, but separate static config from per-frame state.
- Introduce one-frame latency where safe so the simulation does not immediately block on completion.
- Add distance-based LOD so far vehicles do not run the full sensor stack.

### 2. Runtime query methods allocate lists repeatedly

Evidence:

- `RunTime/Core/TrafficSpatialGrid.cs:26-68`
- `RunTime/Core/TrafficSpatialGrid.cs:71-120`
- `RunTime/Core/TrafficManager.Spawner.cs:22`
- `RunTime/Core/TrafficManager.Spawner.cs:280`

Why it matters:

- `GetNearbyWaypoints` and `GetNearbySpawnWaypoints` allocate new `List<AIWaypoint>` objects on every call.
- Those methods sit on paths used by spawn/pooling logic and API queries.
- Allocation pressure is still lower than physics cost, but it is an avoidable source of GC churn.

Refactor direction:

- Replace return-by-new-list with a non-alloc API:
  - `void FillNearbyWaypoints(Vector3 position, List<AIWaypoint> results)`
- Reuse per-manager scratch lists or pooled collections.

### 3. Startup and spawning do more work than necessary

Evidence:

- `RunTime/Core/TrafficManager.cs:103-127`
- `RunTime/Core/VehiclePool.cs:78-101`
- `RunTime/ScriptableObjects/VehicleCollection.cs:11-29`
- `RunTime/Core/TrafficManager.Spawner.cs:64-86`

Why it matters:

- Pool prewarm instantiates `maxVehicleCountInGame` vehicles up front, even if `densityControl` is much lower.
- `GetRandomPrefabOfType` linearly scans the whole prefab list every spawn.
- `IsSpawnPointHidden` recalculates camera frustum planes per candidate.

Refactor direction:

- Prewarm lazily or by configurable budget.
- Build a dictionary cache once:
  - `Dictionary<VehicleType, List<AIVehicle>>`
- Cache frustum planes once per pooling tick instead of once per spawn candidate.

### 4. Spawn clearance can be wrong for scaled vehicle prefabs

Evidence:

- `RunTime/Components/AIVehicle.cs:86-105`
- `RunTime/Core/TrafficManager.Spawner.cs:98-104`

Why it matters:

- `GetSpawnBoxHalfExtents` uses `BoxCollider.size`, which is local-space.
- `Physics.CheckBox` expects world-space half extents.
- `GetSpawnBoxCenterOffset` also ignores scale when rotated into spawn space.
- Scaled vehicle prefabs can therefore get incorrect spawn clearance checks.

Refactor direction:

- Convert collider size and center into world-space using `transform.lossyScale`.
- Consider using `Collider.bounds.extents` for spawn clearance, then add padding.

### 5. There is dead or unfinished runtime code that should not stay in the hot path assembly

Evidence:

- `RunTime/Jobs/BehaviorDecisionJob.cs:1-36`
- `RunTime/Structs/VehicleState.cs:6-20`
- `RunTime/Core/TrafficManager.Jobs.cs:310-332`

Why it matters:

- `BehaviorDecisionJob` is incomplete and unused.
- `AIState.PreparingToOvertake` is declared but not actually used.
- `VehicleEventType.BrakesReleased`, `TurnSignalLeft`, `TurnSignalRight`, and `TurnSignalsOff` are declared but never emitted.
- The event handler is effectively stub code today.

Refactor direction:

- Remove dead code now, or finish it and wire it fully.
- Do not keep half-implemented runtime branches in the shipping assembly.

### 6. Editor tools repeatedly rescan the scene and force repaints

Evidence:

- `Editor/DMTS_TOOL/Pages/ConnectRoadPage.cs:28-30`
- `Editor/DMTS_TOOL/ConnectRoad/ConnectRoadSceneTool.cs:27-30`
- `Editor/DMTS_TOOL/DMTS_Window.cs:196-225`
- `Editor/DMTS_TOOL/CreateRoad/RoadSceneTool.cs:37-51`
- `Editor/DMTS_TOOL/Static/RoadSceneVisibilityUtility.cs:15-35`

Why it matters:

- The connect-road flow refreshes lane terminals and rebuilds connection records in both `OnGUI` and `OnSceneGUI`.
- Global scene gizmos can do similar work again.
- `RoadSceneTool` calls `sceneView.Repaint()` every scene GUI pass.
- This is the main editor scalability bottleneck once scenes get large.

Refactor direction:

- Add a `TrafficEditorCache` with dirty flags.
- Refresh caches on hierarchy change, undo, delete, road generation, or connection mutation.
- Remove unconditional scene repaints and only repaint on interaction or cache invalidation.

### 7. Editor graph-building logic is duplicated across multiple files

Evidence:

- `Editor/DMTS_TOOL/ConnectRoad/ConnectRoadToolState.cs:547-616`
- `Editor/DMTS_TOOL/Static/WaypointConnectionBuilder.cs:439-590`
- `Editor/DMTS_TOOL/Static/RoadBuilder.cs:148-210`
- `Editor/DMTS_TOOL/Static/WaypointConnectionBuilder.cs:225-288`
- `Editor/DMTS_TOOL/Static/RoadBuilder.cs:253-321`
- `Editor/DMTS_TOOL/Static/WaypointConnectionBuilder.cs:293-399`

Why it matters:

- Link-array filtering and equality logic are duplicated.
- Curve sampling and evenly spaced waypoint generation are duplicated.
- Waypoint object creation and link rebuild logic are conceptually parallel but implemented separately.
- This increases maintenance cost and makes bugs harder to fix consistently.

Refactor direction:

- Create shared editor utilities:
  - `WaypointLinkUtility`
  - `CurveSamplingUtility`
  - `GeneratedWaypointFactory`

### 8. Runtime pathfinding is functional but not scalable

Evidence:

- `RunTime/Navigation/TrafficPathfinder.cs:8-77`

Why it matters:

- The open set is a plain `List<AIWaypoint>`.
- Lowest-cost node selection is a linear scan every iteration.
- Neighbor access still walks object references and `Transform.position`.
- This is fine for occasional API use, but not for heavy or repeated routing.

Refactor direction:

- Convert to waypoint-id-based graph nodes.
- Use a binary heap or priority queue.
- Cache adjacency and costs from baked data rather than `Transform` access.

### 9. There are no automated tests protecting the editor graph builders or runtime smoke cases

Evidence:

- No test scripts were found under `Assets` using file discovery.

Why it matters:

- `RoadBuilder` and `WaypointConnectionBuilder` mutate large serialized graphs.
- Refactoring them without tests is risky.

Refactor direction:

- Add EditMode tests for:
  - road generation
  - lane-link creation
  - connection regeneration
  - cleanup/removal
- Add PlayMode smoke tests for:
  - initial spawn
  - despawn/swap-back behavior
  - stop-point toggling

## Redundant Or Removable Code Candidates

- Remove `RunTime/Jobs/BehaviorDecisionJob.cs` unless you plan to revive a separate behavior stage soon.
- Remove or implement `AIState.PreparingToOvertake` in `RunTime/Structs/VehicleState.cs`.
- Remove or implement unused `VehicleEventType` members:
  - `BrakesReleased`
  - `TurnSignalLeft`
  - `TurnSignalRight`
  - `TurnSignalsOff`
- Consider removing `AIVehicle.isChangingLanes` because the real source of truth is already `VehicleState.isChangingLanes` and the value is also mirrored into `debugData`.
- Merge duplicate `BuildFilteredWaypointLinkArray` and `WaypointArraysEqual` logic into one shared utility.
- Merge duplicated curve sampling and generated-waypoint creation logic between `RoadBuilder` and `WaypointConnectionBuilder`.
- Remove `Assets/TutorialInfo` from the package if it is not needed in production deliveries.

## Recommended Refactor Targets By Area

### Runtime

- Split `VehicleState` into smaller data blocks:
  - immutable config
  - sensor data
  - mutable simulation state
- Move more runtime graph traversal from `AIWaypoint` object references to baked waypoint ids.
- Pre-index vehicle prefabs by `VehicleType`.
- Fix spawn volume calculations for scaled prefabs.
- Add simulation LOD:
  - far vehicles use simpler obstacle logic
  - only nearby vehicles use side sensors and full wheel suspension

### Editor

- Create a persistent scene cache for:
  - roads
  - lane starts
  - lane ends
  - waypoint connections
  - visible roads
- Convert repeated object discovery from repaint-driven to invalidation-driven.
- Extract common graph mutation helpers into shared utilities.
- Reduce unconditional `SceneView.Repaint` calls.

### API And Query Layer

- Give `TrafficSpatialGrid` non-alloc query methods.
- Cache a baked adjacency graph for pathfinding and external API queries.
- Keep `DMTS_API` thin and move heavier operations behind cached manager services.

## Suggested Refactor Plan

### Phase 1. Cleanup And Baseline Protection

- Remove dead runtime code and unused enum values.
- Add EditMode tests for `RoadBuilder` and `WaypointConnectionBuilder`.
- Add one PlayMode smoke test scene for spawn, despawn, and intersection stop behavior.
- Freeze public API names before deeper changes.

### Phase 2. Runtime Data Model Simplification

- Split `VehicleState`.
- Remove duplicated lane-change state that lives both on `AIVehicle` and inside NativeArray state.
- Introduce explicit `VehicleStaticConfig` data built once at spawn time.

### Phase 3. Spawning And Pooling Optimization

- Replace full prewarm with staged or lazy prewarm.
- Cache `VehicleCollection` by type.
- Cache frustum planes once per pooling tick.
- Fix scaled collider spawn clearance.

### Phase 4. Simulation Scaling

- Add vehicle LOD tiers.
- Reduce physics query count for far vehicles.
- Investigate one-frame-late job completion instead of same-frame completion everywhere.
- Keep high-fidelity simulation only for near vehicles or traffic visible to gameplay.

### Phase 5. Query And Pathfinding Optimization

- Refactor `TrafficSpatialGrid` to non-alloc fill methods.
- Build a baked runtime waypoint graph with ids and adjacency lists.
- Replace A* open list with a proper priority queue.

### Phase 6. Editor Performance Refactor

- Add `TrafficEditorCache`.
- Refresh caches only when:
  - roads are generated
  - connections are created/deleted
  - hierarchy changes
  - undo/redo runs
- Share all link-array and curve sampling utilities.
- Remove unconditional scene repaints.

### Phase 7. Packaging And Polish

- Remove tutorial scripts if they are not part of the final asset.
- Add profiler notes and recommended setup ranges in docs:
  - safe vehicle counts
  - pooling radius guidelines
  - editor gizmo recommendations

## Practical Execution Order

1. Delete dead code and add tests first.
2. Fix spawn correctness and prefab lookup caching.
3. Split `VehicleState` and simplify runtime ownership.
4. Add non-alloc spatial queries.
5. Add editor caches and repaint throttling.
6. Revisit heavy pathfinding only if your gameplay will actually call it often.

## Final Recommendation

The system is solid enough to ship in its current feature scope, but it should be treated as a medium-scale traffic framework, not a city-scale traffic simulator yet.

If I were refactoring this next, I would start here:

- Runtime: `TrafficManager.Jobs.cs`, `TrafficManager.Spawner.cs`, `TrafficSpatialGrid.cs`, `VehiclePool.cs`, `VehicleCollection.cs`
- Editor: `ConnectRoadToolState.cs`, `ConnectRoadSceneTool.cs`, `DMTS_Window.cs`, `RoadBuilder.cs`, `WaypointConnectionBuilder.cs`

That order gives the highest return with the lowest risk.
