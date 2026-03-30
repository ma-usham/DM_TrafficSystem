# Optimized Mobile Traffic System Architecture Roadmap

A high-performance, reusable traffic system toolkit explicitly targeting Android devices. The architecture relies completely on Data-Oriented Design (DOD), Unity's C# Job System, Burst Compiler, and flat data buffers to eliminate Garbage Collection and CPU bottlenecks.

## Folder Architecture

- `Assets/DM_TrafficSystem/`
  - `Darkmatter.TrafficSystem.asmdef` *(Runtime Module)*
  - `Runtime/`
    - `Core/` — Unmanaged structs (`Waypoint.cs`, `Lane.cs`, `VehicleData.cs`).
    - `Jobs/` — Burst-compiled struct jobs (`VehicleSteeringJob.cs`, `NavigationJob.cs`, `SensorJob.cs`).
    - `Managers/` — Global singletons managing native arrays (`RoadNetwork.cs`, `TrafficManager.cs`).
    - `Components/` — MonoBehaviours binding data (`VehicleRenderer.cs`).
    - `Authoring/` — Splines, Intersections for Editor data before baking.
  - `Data/` — ScriptableObjects for configuration (`TrafficSettings.asset`, `VehicleProfile.asset`).
  - `Editor/`
    - `Editor.DM.TrafficSystem.asmdef` *(Editor Module)*
    - `Tools/` — Custom windows/gizmos (`SplineRoadCreator.cs`, `TrafficBakeTool.cs`).
    - `Inspectors/`
  - `Prefabs/` — Pooled vehicle variants.

## Roadmap Phases & Time Estimates (Total: ~6-8 Weeks)

**Phase 1: DOD Core Data Graph (Runtime)** *(~1 Week)*
*   **Goal:** 0-GC memory model for road paths. Create `Waypoint`, `Lane`, and `RoadSegment` as unmanaged structs holding integer IDs, not object references. Build `RoadNetwork` to store `NativeArray<Waypoint>` and `NativeArray<Lane>`.

**Phase 2: Offline Authoring & Data Baking (Editor)** *(~1.5 Weeks)*
*   **Goal:** Reusable, artist-friendly mapping tools that don't bloat runtime. Expand `SplineRoadCreator` for drawing paths visually. Develop a `TrafficBakeTool` to convert spline data into flat NativeArrays for fast runtime loading.

**Phase 3: Job-Based AI & Vehicle Simulation** *(~2 Weeks)*
*   **Goal:** Move 100% of vehicle movement logic off the main thread. Create `VehicleData` struct. Write `NavigationJob` and `SteeringJob` (IJobParallelFor, Burst-enabled) to handle all acceleration, braking, routing, and steering computations asynchronously.

**Phase 4: Intersections, Sensors & Traffic Control** *(~1.5 Weeks)*
*   **Goal:** Math-based collision avoidance at junctions without heavy Physics colliders. Build `TrafficLightSystem` that updates native array states. Write `SensorJob` using basic math (dot product/distance) or Unity `RaycastCommand` across NativeArrays to detect vehicles ahead.

**Phase 5: Rendering & Object Pooling** *(~1 Week)*
*   **Goal:** Render massively and eliminate instantiation hitches. Create an aggressive Object Pool system linking GameObjects to `VehicleData` positions. Utilize GPU Instancing (`Graphics.DrawMeshInstanced`) if rendering hundreds of distant cars. Implement aggressive LOD routing for vehicles far from the camera.

**Phase 6: Optimization & Toolkit Packaging** *(~1 Week)*
*   **Goal:** Release-ready asset. Profiling on low-end Android devices targeting 60fps with 500+ active cars. Verify modularity by testing drag-and-drop into a fresh Unity project. Document code and setup simple preset `VehicleProfiles`.

## Script Breakdown & Responsibilities

This section details the specific data and logic each script is responsible for. Following Data-Oriented Design (DOD) principles, all `Runtime/Core` scripts must be pure unmanaged `structs`, while `Runtime/Jobs` contain the isolated math logic.

### 1. Runtime / Core (Unmanaged Structs)
These structs hold raw data only. **No methods, no classes, no object references.**
*   **`Waypoint.cs` (struct):** 
    *   `float3 Position`: The world-space position.
    *   `float3 Forward`: The direction to the next waypoint.
    *   `float SpeedLimit`: Maximum allowed speed at this node.
    *   `int NextWaypointId`, `int PrevWaypointId`: Integer indices pointing to adjacent waypoints in the global array. (Use small fixed-size arrays `fixed int NextWaypointIds[3]` for intersections).
    *   `int LaneId`: The lane this waypoint belongs to.
    *   `byte IsStopLine`: Flag for traffic lights or stop signs.
*   **`Lane.cs` (struct):**
    *   `int StartWaypointId`, `int EndWaypointId`: The bounds of this lane within the global waypoint array.
    *   `int LeftLaneId`, `int RightLaneId`: References for lane-changing logic.
    *   `int RoadSegmentId`: What road this lane belongs to.
*   **`VehicleData.cs` (struct):**
    *   `float3 Position`, `quaternion Rotation`: Current spatial data.
    *   `float3 Velocity`: Current movement vector.
    *   `float CurrentSpeed`, `float MaxSpeed`, `float Acceleration`, `float Braking`: Movement variables.
    *   `int CurrentWaypointId`, `int TargetWaypointId`: Navigation tracking.
    *   `int CurrentLaneId`: Current lane tracking.

### 2. Runtime / Jobs (Burst-Compiled)
These are `IJobParallelFor` structs that execute on worker threads. They take in `NativeArray` buffers and perform calculations.
*   **`NavigationJob.cs`**: 
    *   Reads `VehicleData.Position` and checks the distance to `Waypoint[TargetWaypointId].Position`.
    *   When close enough, updates `CurrentWaypointId` and assigns the next `TargetWaypointId`.
    *   Handles branching logic (choosing which `NextWaypointId` to take at an intersection).
*   **`VehicleSteeringJob.cs`**:
    *   Calculates the required steering angle to face `TargetWaypointId`.
    *   Applies acceleration or braking based on distance to the target or obstacles.
    *   Updates `VehicleData.Position` and `VehicleData.Rotation` using `deltaTime`.
*   **`SensorJob.cs`**:
    *   Uses pure math (distance/dot product) against other vehicles in the `VehicleData` array to detect cars ahead in the same `LaneId`.
    *   Optionally uses `RaycastCommand` to batch raycasts for detecting non-traffic physics objects (like the player).
    *   Outputs a command to brake or slow down.

### 3. Runtime / Managers (MonoBehaviours)
These act as the bridge between Unity's main thread and the Job System.
*   **`RoadNetwork.cs` (Singleton):**
    *   Holds the master `NativeArray<Waypoint>`, `NativeArray<Lane>`.
    *   Handles allocation (`Allocator.Persistent`) on `Awake` and `.Dispose()` on `OnDestroy`.
    *   Loads baked data from ScriptableObjects into the native arrays at startup.
*   **`TrafficManager.cs` (Singleton):`
    *   Manages the `NativeArray<VehicleData>`.
    *   In `Update()`, it schedules `NavigationJob`, then `SensorJob`, then `VehicleSteeringJob`.
    *   Calls `JobHandle.Complete()` in `LateUpdate()` to ensure calculations finish off the main thread.
    *   Handles Object Pooling for vehicle GameObjects.

### 4. Runtime / Components (MonoBehaviours)
*   **`VehicleRenderer.cs`**:
    *   Attached to the actual 3D model prefabs (car, truck).
    *   Holds an `int VehicleId`.
    *   In `LateUpdate()` (after jobs complete), it reads `TrafficManager.Instance.VehicleDataArray[VehicleId]` and simply syncs its `transform.position` and `transform.rotation` to match the data.

### 5. Runtime / Authoring & Editor
These exist solely to help artists build the roads. They are stripped out or sit dormant during gameplay.
*   **`SplineAuthoring.cs` & `IntersectionAuthoring.cs`**: MonoBehaviours that hold Bezier curve data, lane configurations, and connection points in the scene.
*   **`SplineRoadCreator.cs` (Editor):** Custom inspector and scene GUI to draw and manipulate the splines natively in the Unity editor.
*   **`TrafficBakeTool.cs` (Editor):** 
    *   A custom editor window with a "Bake" button.
    *   When clicked, it walks along the `SplineAuthoring` curves, generates mathematical points at set intervals, and creates the flat `Waypoint` and `Lane` arrays.
    *   Saves this output to a binary file or a `RoadNetworkData` ScriptableObject.

### 6. Data (ScriptableObjects)
*   **`TrafficSettings.asset`**: Global settings (Max Vehicle Count, Global Speed Limit, LOD Distance thresholds).
*   **`VehicleProfile.asset`**: Profiles for vehicle types (e.g., "Sports Car" has high max speed and acceleration, "Dump Truck" has low speed and braking). `TrafficManager` reads these when instantiating vehicles.