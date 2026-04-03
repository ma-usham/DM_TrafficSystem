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

                    if (lastValidPoint != null && lastValidPoint.settings.nextWaypoint != null && lastValidPoint.settings.nextWaypoint.Length > 0)
                    {
                        int randomPathIndex = Random.Range(0, lastValidPoint.settings.nextWaypoint.Length);
                        vehicle.lookaheadWaypoints[TrafficManager.WAYPOINT_LOOKAHEAD - 1] = lastValidPoint.settings.nextWaypoint[randomPathIndex];
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
    }
}