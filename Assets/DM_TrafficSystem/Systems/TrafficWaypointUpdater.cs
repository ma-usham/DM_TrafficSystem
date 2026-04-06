using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Handles iterating the waypoint graph and reading node properties on the Main Thread.
    /// Passes the updated target positions down into the Job System's buffers.
    /// </summary>
    public class TrafficWaypointUpdater
    {
        // 1. Cache the list here so we never allocate memory during runtime
        private List<AIWaypoint> _validWaypoints = new List<AIWaypoint>();
        public void UpdateWaypoint(List<AIVehicle> activeVehicles, NativeArray<VehicleState> vehicleStates, NativeArray<Vector3> waypointBuffer)
        {
            // Process the graph logic for vehicles that finished driving to their target point
            for (int i = 0; i < activeVehicles.Count; i++)
            {
                AIVehicle vehicle = activeVehicles[i];
                VehicleState state = vehicleStates[vehicle.arrayIndex];

                if (state.reachedCurrentWaypoint)
                {
                    // 1. Shift the entire queue back by 1
                    for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD - 1; j++)
                    {
                        vehicle.lookaheadWaypoints[j] = vehicle.lookaheadWaypoints[j + 1];
                    }

                    // 2. Fetch the next AIWaypoint to put into the last position
                    vehicle.lookaheadWaypoints[TrafficManager.WAYPOINT_LOOKAHEAD - 1] = null;
                    AIWaypoint lastValidPoint = vehicle.lookaheadWaypoints[TrafficManager.WAYPOINT_LOOKAHEAD - 2];

                    AIWaypoint nextPoint = GetNextValidWaypoint(vehicle, lastValidPoint);
                    if (nextPoint != null)
                    {
                        vehicle.lookaheadWaypoints[TrafficManager.WAYPOINT_LOOKAHEAD - 1] = nextPoint;
                    }

                    // 3. Reset job state flags
                    state.reachedCurrentWaypoint = false;
                    state.currentTargetIndexOffset = 0;

                    // Update stop state for the new target
                    if (vehicle.lookaheadWaypoints[0] != null)
                    {
                        state.isApproachingStopPoint = vehicle.lookaheadWaypoints[0].settings.isStopPoint;
                    }

                    // 4. Write back to Native Memory so jobs can read the newly queued target
                    vehicleStates[vehicle.arrayIndex] = state;
                    for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
                    {
                        if (vehicle.lookaheadWaypoints[j] != null)
                        {
                            waypointBuffer[state.waypointBufferStartIndex + j] = vehicle.lookaheadWaypoints[j].transform.position;
                        }
                    }
                }
            }
        }

        public void UpdateStopWaypoints(List<AIVehicle> activeVehicles, NativeArray<VehicleState> vehicleStates)
        {
            // Poll dynamic stop states so we can toggle lights in real-time in the Inspector
            for (int i = 0; i < activeVehicles.Count; i++)
            {
                AIVehicle vehicle = activeVehicles[i];
                if (vehicle.lookaheadWaypoints[0] != null)
                {
                    VehicleState state = vehicleStates[vehicle.arrayIndex];
                    state.isApproachingStopPoint = vehicle.lookaheadWaypoints[0].settings.isStopPoint;
                    vehicleStates[vehicle.arrayIndex] = state;
                }
            }
        }

        public AIWaypoint GetNextValidWaypoint(AIVehicle vehicle, AIWaypoint currentPoint)
        {
            if (currentPoint == null || currentPoint.settings.nextWaypoint == null || currentPoint.settings.nextWaypoint.Length == 0)
                return null;

            AIWaypoint[] nextWaypoints = currentPoint.settings.nextWaypoint;

            // 2. Clear the cached list instead of making a new one
            _validWaypoints.Clear();

            // First, try to find waypoints matching the vehicle's type
            for (int i = 0; i < nextWaypoints.Length; i++)
            {
                AIWaypoint wp = nextWaypoints[i];
                if (wp != null && wp.settings.vehicleType != null && wp.settings.vehicleType.Length > 0)
                {
                    for (int j = 0; j < wp.settings.vehicleType.Length; j++)
                    {
                        if (wp.settings.vehicleType[j] == vehicle.vehicleType)
                        {
                            _validWaypoints.Add(wp);
                            break;
                        }
                    }
                }
            }

            // Fallback: If no waypoints matched the specific type, we just pick from any valid next waypoint.
            if (_validWaypoints.Count == 0)
            {
                return nextWaypoints[Random.Range(0, nextWaypoints.Length)];
            }

            // Otherwise, pick randomly from the matched type waypoints
            return _validWaypoints[Random.Range(0, _validWaypoints.Count)];
        }
    }
}