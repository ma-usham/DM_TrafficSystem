# DM Traffic System: Architecture & Design Plan

## 1. Core Concept
The system follows a strict **Separation of Authoring and Runtime** paradigm. 
- **Editor-Time**: Complex spline mathematics, physics raycasting (terrain snapping), and heavy object instantiation are performed exclusively within Editor scripts.
- **Run-Time**: AI vehicles and the Traffic Manager consume lightweight, pre-generated node networks (`AIWaypoint` connections) to navigate. No generation overhead exists during gameplay.

---

## 2. Core Architecture

The architecture is divided into three distinct layers: Data (Runtime), Tooling (Editor), and Simulation (Runtime).

### Layer A: Data Models (Runtime Components)
These components exist on GameObjects to store serialized configuration and spatial data. **They contain zero generation logic.**
*   `Road` (MonoBehaviour): Represents a segment of road. Holds pure configurations (`speedLimit`, `lanes`, `drivingDirection`) and a hidden raw data list (`List<Vector3> controlPointsList`), plus a list of its generated `AILane` game objects.
*   `AILane` (MonoBehaviour): Represents a single lane. Holds a list of strictly ordered `AIWaypoint` objects.
*   `AIWaypoint` (MonoBehaviour): A singular point in space. Holds a `WaypointSettings` struct detailing its connections (`previous`, `next`, and future intersection links like `branchLeft`, `mergeRight`) and its specific speed limit.

### Layer B: Generation & Tooling (Editor Only)
These tools read user input, process geometry, and modify the Data Models.
*   `TrafficSystemWindow` / `Pages` (EditorWindow): The GUI where the designer clicks and interacts. Exposes buttons like "Generate" and intercepts mouse events.
*   `SplineMathUtils` (Static C# Class): A pure math library mapping cubic Bezier formulas, tangents, and point distribution. Agnostic to GameObjects.
*   `RoadBuilder` (Static Editor Class): The core generator. It:
    1. Reads `List<Vector3>` from a `Road` component.
    2. Uses `SplineMathUtils` to calculate thousands of dense points.
    3. Triggers `Physics.Raycast` to snap points to the terrain.
    4. Instantiates standard GameObjects with `AIWaypoint` components.
    5. Links the `.next` and `.previous` variables between newly created sequential waypoints.
*   `IntersectionBuilder` (Static Editor Class) *[Future]*: Finds overlapping or connected `Road` endpoints and generates specialized transition waypoints linking Lane A on Road 1 to Lane B on Road 2.

### Layer C: Simulation (Runtime AI)
*   `TrafficManager` (MonoBehaviour): A central singleton-like struct that controls vehicle object-pooling, global simulation states (traffic lights, start/stop), and manages the array of all valid `Road` references.
*   `CarAI` (MonoBehaviour): The agent script. It asks for a lane starting point, accelerates, handles collision avoidance (checking distance to cars ahead), and queries the current `AIWaypoint`'s `.next` field to continuously update its steering target.

---

## 3. Implementation Process & Phases

### Phase 1: Decoupling Data & Logic (Current Step)
1. Extract `GenerateRoadWaypoints` and `ClearGeneratedWaypoints` out of `Road.cs`.
2. Move them into a new `RoadBuilder.cs` script placed inside the `Editor` folder.
3. Clean `Road.cs` until you can safely remove `using UnityEditor;` and `[ExecuteInEditMode]`.

### Phase 2: Refine the Tools
1. Implement pure manual editing (moving `controlPointsList` Handles).
2. Wire the "Generate Road" button inside the `CreateRoadPage.cs` to explicitly call `RoadBuilder.Build(selection)`.
3. Separate Bezier mathematics into `SplineMathUtils.cs` to make everything testable.

### Phase 3: Intersections
1. Add node-linking variables to `WaypointSettings` (e.g., branching).
2. Build an Editor visualizer that draws lines between linked waypoints representing actual navigation flow.
3. Create an Editor tool to stitch two roads together smoothly.

### Phase 4: AI & Traversal
1. Create a `CarAI` script that relies entirely on `Transform.position` of `AIWaypoint`.
2. Add a look-ahead system so the car checks the *next* waypoint's angle to preemptively steer and brake.
3. Implement traffic light logic that temporarily nullifies or blocks `.next` pointers dynamically, forcing cars to queue.