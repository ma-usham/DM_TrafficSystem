using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Global Public API for external gameplay scripts to interact with the DM_TrafficSystem.
    /// Call these methods from anywhere using DMTS_API.MethodName()
    /// </summary>
    public static class DMTS_API
    {
        private static TrafficManager _manager;

        /// <summary>
        /// Retrieves the active TrafficManager in the scene securely.
        /// </summary>
        public static TrafficManager Manager
        {
            get
            {
                if (_manager == null)
                {
#pragma warning disable CS0618
                    _manager = Object.FindObjectOfType<TrafficManager>();
#pragma warning restore CS0618
                    if (_manager == null)
                    {
                        Debug.LogWarning("DMTS_API: TrafficManager not found in the current scene!");
                    }
                }
                return _manager;
            }
        }

        /// <summary>
        /// Changes the target amount of flowing traffic. Safe to call at runtime.
        /// </summary>
        /// <param name="newDensity">The target number of cars. Clamps automatically to the hard memory limit.</param>
        public static void SetTrafficDensity(int newDensity)
        {
            if (Manager != null)
            {
                Manager.densityControl = Mathf.Clamp(newDensity, 0, Manager.maxVehicleCountInGame);
            }
        }

        /// <summary>
        /// Toggles whether the player proximity pooling system is active.
        /// </summary>
        public static void SetPlayerPoolingActive(bool isActive)
        {
            if (Manager != null)
            {
                Manager.usePlayerPooling = isActive;
            }
        }

        /// <summary>
        /// Updates the core reference to the Player for the pooling system.
        /// Useful if your player character is destroyed and respawns.
        /// </summary>
        public static void UpdatePlayerReference(Transform newPlayerTransform, Camera newMainCamera = null)
        {
            if (Manager != null)
            {
                Manager.playerTransform = newPlayerTransform;
                if (newMainCamera != null)
                {
                    Manager.mainCamera = newMainCamera;
                }
            }
        }

        /// <summary>
        /// Gets the LayerMask used to identify the Player in the traffic system.
        /// </summary>
        public static LayerMask PlayerLayerMask
        {
            get
            {
                if (Manager != null) return Manager.playerMask;
                return new LayerMask(); // Default empty
            }
        }

        /// <summary>
        /// Gets the LayerMask used to identify AI Traffic vehicles.
        /// </summary>
        public static LayerMask TrafficLayerMask
        {
            get
            {
                if (Manager != null) return Manager.trafficMask;
                return new LayerMask();
            }
        }

        /// <summary>
        /// Gets the LayerMask used to identify the ground/road surface.
        /// </summary>
        public static LayerMask GroundLayerMask
        {
            get
            {
                if (Manager != null) return Manager.groundMask;
                return new LayerMask();
            }
        }

        /// <summary>
        /// Gets the integer ID (index) for a specific waypoint.
        /// Returns true if the waypoint was found, false otherwise.
        /// </summary>
        public static bool TryGetWaypointIndex(AIWaypoint waypoint, out int index)
        {
            if (Manager != null && waypoint != null)
            {
                List<AIWaypoint> allWaypoints = Manager.GetAllWaypointsInMap();
                if (allWaypoints != null)
                {
                    index = allWaypoints.IndexOf(waypoint);
                    return index >= 0;
                }
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Retrieves the waypoint object associated with a specific ID (index).
        /// Returns null if the index is invalid.
        /// </summary>
        public static AIWaypoint GetWaypointFromIndex(int index)
        {
            if (Manager != null && index >= 0)
            {
                List<AIWaypoint> allWaypoints = Manager.GetAllWaypointsInMap();
                if (allWaypoints != null && index < allWaypoints.Count)
                {
                    return allWaypoints[index];
                }
            }

            return null;
        }



        /// <summary>
        /// Gets the exact world position of the nearest traffic waypoint to a given point.
        /// Returns the original position if no waypoint is found.
        /// </summary>
        public static AIWaypoint GetNearestWaypoint(Vector3 searchPosition)
        {
            if (Manager != null)
            {
                AIWaypoint closestNode = Manager.GetClosestWaypoint(searchPosition);
                if (closestNode != null)
                {
                    return closestNode;
                }
            }

            Debug.LogWarning("DMTS_API: Could not find a nearby waypoint. Returning null.");
            return null; // Fallback
        }

        /// <summary>
        /// Gets the exact world position of the nearest traffic waypoint in a specific direction.
        /// Returns the original position if no waypoint is found.
        /// </summary>
        public static AIWaypoint GetNearestWaypointInDirection(Vector3 searchPosition, Vector3 direction)
        {
            if (Manager != null)
            {
                AIWaypoint closestNode = Manager.GetNearestWaypointInDirection(searchPosition, direction);
                if (closestNode != null)
                {
                    return closestNode;
                }
            }

            Debug.LogWarning("DMTS_API: Could not find a nearby waypoint in the given direction. Returning null.");
            return null; // Fallback
        }

        /// <summary>
        /// Retrieves the master list of all waypoints currently active in the traffic system.
        /// </summary>
        public static List<AIWaypoint> GetAllWaypoints()
        {
            if (Manager != null)
            {
                return Manager.GetAllWaypointsInMap();
            }

            Debug.LogWarning("DMTS_API: TrafficManager not found. Returning empty list.");
            return new List<AIWaypoint>();
        }

        /// <summary>
        /// Calculates the shortest path between two world positions.
        /// Returns an ordered list of Vector3 coordinate points representing the route.
        /// </summary>
        public static List<Vector3> GetShortestPath(Vector3 startPosition, Vector3 endPosition)
        {
            if (Manager == null) return null;

            // 1. Map positions to actual waypoints
            AIWaypoint startNode = Manager.GetClosestWaypoint(startPosition);
            AIWaypoint endNode = Manager.GetClosestWaypoint(endPosition);

            if (startNode == null || endNode == null)
            {
                Debug.LogWarning("DMTS_API: Could not map positions to waypoints.");
                return null;
            }

            // 2. Execute A* Math
            List<AIWaypoint> pathWaypoints = TrafficPathfinder.FindPath(startNode, endNode);

            // 3. Convert results back to Vector3
            if (pathWaypoints != null)
            {
                List<Vector3> routePositions = new List<Vector3>();
                foreach (var wp in pathWaypoints)
                {
                    routePositions.Add(wp.transform.position);
                }
                return routePositions;
            }

            return null;
        }

        /// <summary>
        /// Calculates the shortest path between two specific waypoints.
        /// Returns an ordered list of AIWaypoint objects representing the route.
        /// </summary>
        public static List<AIWaypoint> GetShortestPath(AIWaypoint startWaypoint, AIWaypoint endWaypoint)
        {
            if (Manager == null) return null;

            if (startWaypoint == null || endWaypoint == null)
            {
                Debug.LogWarning("DMTS_API: Invalid waypoints provided for pathfinding.");
                return null;
            }

            return TrafficPathfinder.FindPath(startWaypoint, endWaypoint);
        }


        /// <summary>
        /// Clears all existing traffic within a given radius around a specific center point.
        /// </summary>
        /// <param name="center">The world location center.</param>
        /// <param name="radius">The radius in Unity units.</param>
        public static void ClearTrafficInArea(Vector3 center, float radius)
        {
            if (Manager != null)
            {
                Manager.ClearVehiclesInArea(center, radius);
            }
            else
            {
                Debug.LogWarning("DMTS_API: TrafficManager not found. Cannot clear traffic.");
            }
        }

        /// <summary>
        /// Returns the current traffic light state for a specific road index at a given intersection.
        /// </summary>
        public static TrafficLightState GetRoadTrafficLightState(TrafficLightIntersection intersection, int roadIndex)
        {
            if (intersection == null)
            {
                Debug.LogWarning("DMTS_API: Invalid intersection reference.");
                return TrafficLightState.Red; // Default safe state
            }

            return intersection.GetRoadLightState(roadIndex);
        }
    }
}
