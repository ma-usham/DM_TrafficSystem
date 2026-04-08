# DarkMatter Traffic System (DMTS) - User Guide

This guide covers everything you need to create roads, connect them, configure traffic flow, and spawn AI vehicles dynamically at runtime using the DMTS Tool. It separates heavy editor-time generation from lightweight runtime simulation (utilizing the Unity Job System).

## 1. Opening the Tool
To open the Traffic System main editor window:
- In the top Unity menu, navigate to **Tools > DarkMatter Traffic System Tool** (or similar, depending on the menu path defined in `DMTS_Window.cs`).
- A dockable window will appear. From this main window, you can access global scene view gizmos (displaying waypoints, connections, spawn points, and intersection status) and navigate to the various core editor modules like **Create Road**, **Connect Road**, and the **Traffic Manager**.

## 2. Creating & Generating Roads
You can shape and define standard road structures through the **Create Road** page.

1. **Draw the Road:** Enter the **Create Road** page. In the Scene View, use the tool's curve controls (splines / control points) to start structuring your road.
2. **Generate Structure:** Once you are happy with the shape, click the **Generate Road** button. This calculates Bezier math, raycasts against the terrain, and compiles the visual road into underlying `AIWaypoint` objects grouped into `AILane`s. *(Note: This generation happens entirely in the Editor to ensure zero runtime overhead).*
3. **Configure Lanes:** In the tool window, manage individual **Lane Configurations**, controlling settings directly on the `AILane` and `Road` objects.
4. **Lane Changing:** If your road has multiple lanes, use the **Link Offset** and **Max Turn Angle** settings, then click **Link Lanes**. This generates traversable lane-change links between the lanes so vehicles can dynamically switch lanes. You can remove these anytime using **Unlink Lanes**.

## 3. Connecting Roads (Intersections & Turns)
Connecting the end of one road to the start of another is done through the **Connect Roads** page. 

1. **Selection:**
   - **Step 1:** In the Scene View, click any ending waypoint of a road line.
   - **Step 2:** Click the beginning waypoint of your target road. This automatically bridges a curved visual connection between them.
2. **Shape the Connection:** 
   - **Edit Curve:** Drag the connection curve points in the Scene View to form the intersection path.
   - **Insert/Remove Points:** `Ctrl+Click` the curve to insert a new middle point. `Right-Click` a middle point to delete it.
3. **Connection Properties:** In the Editor window while a connection is active, you can define standard connection rules such as **Speed Limit** and restricted **Vehicle Types**.
4. **Finalize:** Click **Apply** to save the generated `AIWaypointConnection`. (You can also hit **Delete** to discard it.)

## 4. Spawning Vehicles & Traffic Manager
The system relies on a global `TrafficManager` singleton and a `VehicleCollection` ScriptableObject to orchestrate the AI.

### Creating the Traffic Manager
1. Navigate to the **Traffic Manager** page in the DMTS Window.
2. Click **Create Traffic Manager**. This automatically generates a properly parented manager GameObject in your scene hierarchy with the `TrafficManager` component.
3. Define the setup using the built-in tabs:
   - **Layer Setup:** Assign the Physics Layers for *Ground*, *Traffic*, and *Player* raycasts and sensors.
   - **Traffic Setting:** Control global capacity parameters such as *Max Vehicle Count In Game* and overall *Density Control*.
   - **Pooling System (Optimization):** Enable logic to spawn/despawn vehicles dynamically in a spatial grid based on distance from targets like the *Main Camera* or *Player Transform* (Inner/Outer/Despawn radius).

### Setting up Vehicle Prefabs
1. Attach the `AIVehicle` component to your vehicle prefabs alongside a `Rigidbody` and a `BoxCollider`. 
   - *Colliders:* Ensure the BoxCollider encompasses the vehicle accurately; the system uses a **Spawning Clearance** parameter (`spawnPadding`) alongside the collider to ensure safe, collision-free spawning distances.
   - *Suspension & Mechanics:* Configure Raycast Suspension settings (Wheels, Spring Strength, Spring Damper) to handle uneven terrain. Assign sensor transforms (Front, Left, Right) to handle obstacle detection.
   - *Behaviors:* Adjust the `DriverBehavior` struct (acceleration, frustration times, lane changing probability, etc.)
2. Create a vehicle collection: Right-click in the Project Window and select **Create > DM Traffic System > Vehicle Collection**.
3. Add your `AIVehicle` prefabs to this newly created list. 
4. Assign the `VehicleCollection` asset into the corresponding slot on the **Traffic Manager** (Traffic Setting tab).

### Finalizing Spawn Logic
5. In the Traffic Manager page, click **Bake Spawn Points & Grid**. This scans all `AILane` waypoints in your scene and bakes safe target spots (based on road straightness) where vehicles can initially enter the game world.

## 5. Traffic Lights & Priority Intersections
To control traffic flow at intersections:
1. Use the **Traffic Lights** or **Priority Intersection** pages/tools to define regions where cars must yield or stop.
2. These tools apply specialized components (`TrafficLightIntersection.cs` or `PriorityIntersection.cs`) that temporarily block or modify the ``.next`` waypoint pointers dynamically, forcing AI cars to queue and wait their turn.

--- 

*Note: Global gizmos for inspecting traffic connectivity, spawned waypoints, and green/red intersection statuses can be toggled on or off from the "Scene Gizmos" foldout on the root page of the DMTS Tool window.*
