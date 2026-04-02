using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Attached to the dummy car GameObjects. Registers with the TrafficManager.
    /// This keeps track of the MonoBehaviour Waypoints for the Main Thread to trace the graph.
    /// </summary>
    public class AIVehicle : MonoBehaviour
    {
        public float maxSpeed = 10f;
        
        // We'll store the actual MonoBehaviour waypoints here so the Main Thread
        // can traverse the graph and feed 'Vector3' positions to the Job System.
        public AIWaypoint[] lookaheadWaypoints = new AIWaypoint[TrafficManager.WAYPOINT_LOOKAHEAD];
        
        // Track our current target index inside the lookahead buffer (0 to 4)
        public int activeWaypointIndex = 0;
        
        // The index assigned to this vehicle in the NativeArrays by the TrafficManager
        [HideInInspector] 
        public int arrayIndex = -1; 
    }
}