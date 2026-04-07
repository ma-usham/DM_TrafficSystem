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
        /// Gets the exact world position of the nearest traffic waypoint to a given point.
        /// Returns the original position if no waypoint is found.
        /// </summary>
        public static Vector3 GetNearestWaypointPosition(Vector3 searchPosition)
        {
            if (Manager != null)
            {
                AIWaypoint closestNode = Manager.GetClosestWaypoint(searchPosition);
                if (closestNode != null)
                {
                    return closestNode.transform.position;
                }
            }

            Debug.LogWarning("DMTS_API: Could not find a nearby waypoint. Returning original position.");
            return searchPosition; // Fallback
        }

        /// <summary>
        /// Gets the exact world position of the nearest traffic waypoint in a specific direction.
        /// Returns the original position if no waypoint is found.
        /// </summary>
        public static Vector3 GetNearestWaypointInDirection(Vector3 searchPosition, Vector3 direction)
        {
            if (Manager != null)
            {
                AIWaypoint closestNode = Manager.GetNearestWaypointInDirection(searchPosition, direction);
                if (closestNode != null)
                {
                    return closestNode.transform.position;
                }
            }

            Debug.LogWarning("DMTS_API: Could not find a nearby waypoint in the given direction. Returning original position.");
            return searchPosition; // Fallback
        }
    }
}
