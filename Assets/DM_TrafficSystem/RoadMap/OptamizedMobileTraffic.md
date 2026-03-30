
# Optimized Mobile Traffic System Architecture Roadmap

## Phase 1: Core Data Graph (Runtime)

1. **Refactor `Waypoint`:** Create a lightweight `Waypoint` class/struct. Contains: `Position`, `Direction`, `NextWaypoint(s)`, `LaneIndex`, `SpeedLimit`. 
   Multiple `NextWaypoints` are crucial for intersections and lane changes. 
   - **Optimization:** Use structs instead of classes to minimize heap allocations and reduce memory overhead.

2. **Create `Lane`:** A `Lane` data structure that holds a list of ordered `Waypoints`. 
   - **Optimization:** Use simple arrays and avoid dynamic allocations where possible.

3. **Create `RoadSegment`:** A container for multiple lanes, containing logical properties like directionality and connections. 
   - **Optimization:** Store road segments in a spatial partitioning system (e.g., a quadtree) to enable fast lookups during pathfinding.

4. **Create `RoadNetwork`:** A singleton/manager that holds all `RoadSegment`s, intersections, and can efficiently query paths. 
   - **Optimization:** Implement a caching mechanism for frequently used paths to avoid repetitive computations.

## Phase 2: Decoupled Authoring (Editor)

1. **Modify `SplineRoadCreator`:** Update the editor tool to decouple the spline logic from runtime pathfinding. 
   - **Optimization:** Use lightweight data structures for runtime, such as waypoint lists, to reduce memory consumption during pathfinding.

2. **Introduce Baking Mechanism:** Bake spline data into lightweight waypoint-based structures during the authoring phase. 
   - **Optimization:** Generate baked data asynchronously and in chunks to avoid runtime delays during gameplay.

## Phase 3: Intersections & Traffic Control

1. **Create `Intersection`:** A component managing connections between road segments at intersections. 
   - **Optimization:** Store intersection data as a lightweight adjacency list for quick graph traversal during pathfinding.

2. **Create `TrafficLight`:** Manages light state changes (Red/Yellow/Green) at intersections. 
   - **Optimization:** Traffic light updates should happen asynchronously via a Job System to keep the main thread free for other tasks.

3. **Create `StopLine`:** A designated `Waypoint` where vehicles stop when necessary. 
   - **Optimization:** Only activate `StopLine` checks for vehicles close to the intersection to minimize unnecessary collision checks.

## Phase 4: Vehicle AI (Navigation & Steering)

1. **Create `VehicleNavigation`:** Macro-level pathfinding, determining optimal routes and lane choices (e.g., from Road A to Road Z). 
   - **Optimization:** Use A* with priority queues for pathfinding, and cache paths where possible. Job System: Offload pathfinding calculations to background jobs on separate threads.

2. **Create `VehicleController`:** Handles micro-level steering, acceleration, and braking towards waypoints. 
   - **Optimization:** Simplify vehicle physics and steering logic to minimize computational overhead on the CPU.

3. **Sensor System:** Raycast or trigger-based sensors for detecting obstacles and other vehicles. 
   - **Optimization:** Limit sensor range based on vehicle speed to reduce the number of checks. Use spatial partitioning (e.g., grid-based hashing) for nearby objects to improve performance.

4. **Job System for AI Agents:** Use Unity's Job System to parallelize AI agent tasks, such as steering, pathfinding, and collision detection on worker threads. 
   - **Optimization:** Offload as much AI logic as possible to background threads (e.g., AI pathfinding and obstacle detection).

## Phase 5: Advanced Features

1. **Lane Changing:** Allow AI vehicles to perform lane changes based on available space. 
   - **Optimization:** Use sensor-based checks (raycasts) to detect the presence of vehicles in adjacent lanes and implement simple curve interpolation to adjust lanes smoothly.

2. **Traffic Manager:** Manage vehicle spawning, despawning, and traffic flow. 
   - **Optimization:** Use object pooling for vehicles to minimize instantiation/destruction overhead. Vehicles should be pooled when they go out of the player’s view or fall off the path.

## Phase 6: Optimization Techniques

1. **Job System:** Use Unity's Job System to handle heavy AI computations (pathfinding, steering, collision detection) on worker threads. 
   - **Optimization:** Break down complex tasks into smaller jobs, like pathfinding for each vehicle, to ensure smooth execution on lower-end devices.

2. **Object Pooling:** Implement pooling for vehicles, waypoints, and other frequently instantiated objects. 
   - **Optimization:** Pool objects instead of destroying and instantiating them. This prevents memory fragmentation and improves performance.

3. **Data Caching & Preloading:** Cache frequently used data, like precomputed paths and waypoints, to avoid recalculating them at runtime. 
   - **Optimization:** Use a lazy-loading approach to load data only when needed, and implement data streaming for larger road networks.

4. **Efficient Memory Management:** Minimize memory usage by using structs over classes where possible, and avoid using dynamic memory allocations in performance-critical sections. 
   - **Optimization:** Use memory pools and chunk-based memory management to ensure memory is allocated in a way that minimizes garbage collection spikes.

5. **LOD (Level of Detail) for AI:** Implement LOD for AI vehicles and road networks. High-detail behavior is only necessary when the player is nearby. 
   - **Optimization:** Vehicles far from the player should use simplified AI logic (e.g., basic waypoint-following without advanced collision detection or lane changing).

6. **Render Optimization:** Limit the number of vehicles rendered at any given time. 
   - **Optimization:** Use culling techniques to avoid rendering vehicles outside the camera's view.

## Verification Steps

1. **Road Network Creation:** Create a simple road with 2 lanes and 3 intersections in the editor. Verify that it can bake into a lightweight data structure with minimal memory usage.
2. **Vehicle Pathfinding:** Spawn a vehicle and ensure it follows the baked path (waypoints) without using spline-based logic at runtime.
3. **AI Performance Check:** Spawn multiple vehicles with basic sensors (e.g., collision avoidance) and ensure that performance remains smooth on low-end devices.
4. **Traffic Light Simulation:** Test traffic light logic, ensuring that vehicles stop and go based on traffic light states without causing performance bottlenecks.
5. **Mobile Optimization Testing:** Run the system on low-end devices, checking for frame rate stability, memory usage, and CPU/GPU load.

## Key Design Decisions

1. **Decoupling Editor and Runtime Logic:** Splines are only used during the authoring phase; at runtime, the system uses lightweight waypoint data structures, optimizing performance.
2. **Mobile-First Approach:** All architecture decisions prioritize low CPU/GPU usage, memory management, and mobile-specific optimizations (e.g., job systems and object pooling).
3. **Job System for AI:** Using Unity’s Job System for pathfinding, steering, and collision detection offloads heavy computations from the main thread, ensuring smooth performance on lower-end devices.
4. **Memory Management:** Avoid heap allocations in performance-critical areas, and implement object pooling and efficient data caching to minimize garbage collection overhead.
