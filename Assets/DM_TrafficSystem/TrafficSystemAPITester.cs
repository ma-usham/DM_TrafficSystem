using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem.Testing
{
    public class TrafficSystemAPITester : MonoBehaviour
    {
        [Header("Testing Settings")]
        [Tooltip("The direction to check for the nearest directional waypoint. Local space.")]
        public Vector3 searchDirection = Vector3.forward;

        [Tooltip("Target destination to test pathfinding route.")]
        public Transform targetDestination;

        void Update()
        {
            // ----------------------------------------------------
            // TEST 1: Nearest Waypoint Overall
            // ----------------------------------------------------
            var closestPos = DMTS_API.GetNearestWaypoint(transform.position);
            
            if (closestPos.transform.position != transform.position)
            {
                // Draw a GREEN line to the absolute closest waypoint position
                Debug.DrawLine(transform.position, closestPos.transform.position, Color.green);
            }

            // ----------------------------------------------------
            // TEST 2: Nearest Waypoint in a Specific Direction
            // ----------------------------------------------------
            // Convert local direction to world space
            Vector3 worldDirection = transform.TransformDirection(searchDirection);
            
            // Draw a RED ray showing where we are looking
            Debug.DrawRay(transform.position, worldDirection * 10f, Color.red);

            var directionalPos = DMTS_API.GetNearestWaypointInDirection(transform.position, worldDirection);
            
            if (directionalPos.transform.position != transform.position)
            {
                // Draw a BLUE line to the waypoint found in that direction
                Debug.DrawLine(transform.position, directionalPos.transform.position, Color.blue);
            }

            // ----------------------------------------------------
            // TEST 3: Shortest Path Pathfinding
            // ----------------------------------------------------
            if (targetDestination != null)
            {
                // Use Vector3 overload
                List<Vector3> pathCoords = DMTS_API.GetShortestPath(transform.position, targetDestination.position);

                if (pathCoords != null && pathCoords.Count > 0)
                {
                    // Draw lines between every node in the path (Yellow)
                    for (int i = 0; i < pathCoords.Count - 1; i++)
                    {
                        Debug.DrawLine(pathCoords[i], pathCoords[i + 1], Color.yellow);
                    }
                }
            }
        }

        void OnGUI()
        {
            GUILayout.Space(20);
            GUILayout.Label("--- Traffic System Output ---");
            
            var closestPos = DMTS_API.GetNearestWaypoint(transform.position);
            GUILayout.Label($"Closest Pos: {closestPos.transform.position}");

            Vector3 worldDirection = transform.TransformDirection(searchDirection);
            var directionalPos = DMTS_API.GetNearestWaypointInDirection(transform.position, worldDirection);
            GUILayout.Label($"Directional Pos: {directionalPos.transform.position}");

            if (targetDestination != null)
            {
                List<Vector3> pathCoords = DMTS_API.GetShortestPath(transform.position, targetDestination.position);
                GUILayout.Label($"Path Length: {(pathCoords != null ? pathCoords.Count.ToString() + " nodes" : "No Path Found")}");
            }
        }
    }
}
