using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Handles iterating the waypoint graph and reading node properties on the Main Thread.
    /// Passes the updated target positions down into the Job System's buffers.
    /// </summary>
    public class TrafficRouteSystem
    {
        AIVehicle _vehicle1;
        AIVehicle _vehicle2;
        VehicleState _state1;
        VehicleState _state2;


        public void HandleRouteRefills(List<AIVehicle> activeVehicles, NativeArray<VehicleState> vehicleStates, NativeArray<Vector3> waypointBuffer)
        {
            // Process the graph logic for vehicles that finished driving to their target point
            for (int i = 0; i < activeVehicles.Count; i++)
            {
                _vehicle1 = activeVehicles[i];
                _state1 = vehicleStates[_vehicle1.arrayIndex];

                if (_state1.reachedCurrentWaypoint)
                {
                    // 1. Shift the entire queue back by 1
                    for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD - 1; j++)
                    {
                        _vehicle1.lookaheadWaypoints[j] = _vehicle1.lookaheadWaypoints[j + 1];
                    }

                    // 2. Fetch the next AIWaypoint to put into the last position
                    _vehicle1.lookaheadWaypoints[TrafficManager.WAYPOINT_LOOKAHEAD - 1] = null;
                    AIWaypoint lastValidPoint = _vehicle1.lookaheadWaypoints[TrafficManager.WAYPOINT_LOOKAHEAD - 2];

                    if (lastValidPoint != null && lastValidPoint.settings.nextWaypoint != null && lastValidPoint.settings.nextWaypoint.Length > 0)
                    {
                        int randomPathIndex = Random.Range(0, lastValidPoint.settings.nextWaypoint.Length);
                        _vehicle1.lookaheadWaypoints[TrafficManager.WAYPOINT_LOOKAHEAD - 1] = lastValidPoint.settings.nextWaypoint[randomPathIndex];
                    }

                    // 3. Reset job state flags
                    _state1.reachedCurrentWaypoint = false;
                    _state1.currentTargetIndexOffset = 0;
                    
                    // Update stop state for the new target
                    if (_vehicle1.lookaheadWaypoints[0] != null)
                    {
                        _state1.isApproachingStopPoint = _vehicle1.lookaheadWaypoints[0].settings.isStopPoint;
                    }

                    // 4. Write back to Native Memory so jobs can read the newly queued target
                    vehicleStates[_vehicle1.arrayIndex] = _state1;
                    for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
                    {
                        if (_vehicle1.lookaheadWaypoints[j] != null)
                        {
                            waypointBuffer[_state1.waypointBufferStartIndex + j] = _vehicle1.lookaheadWaypoints[j].transform.position;
                        }
                    }
                }
            }
        }

        public void PollDynamicStopStates(List<AIVehicle> activeVehicles, NativeArray<VehicleState> vehicleStates)
        {
            // Poll dynamic stop states so we can toggle lights in real-time in the Inspector
            for (int i = 0; i < activeVehicles.Count; i++)
            {
                _vehicle2 = activeVehicles[i];
                if (_vehicle2.lookaheadWaypoints[0] != null)
                {
                    _state2= vehicleStates[_vehicle2.arrayIndex];
                    _state2.isApproachingStopPoint = _vehicle2.lookaheadWaypoints[0].settings.isStopPoint;
                    vehicleStates[_vehicle2.arrayIndex] = _state2;
                }
            }
        }
    }
}