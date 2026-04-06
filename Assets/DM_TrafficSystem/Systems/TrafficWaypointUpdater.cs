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

                if (vehicle.laneChangeCooldownTimer > 0)
                {
                    vehicle.laneChangeCooldownTimer -= Time.deltaTime;
                }

                // Provide a new path if job requests lane change
                if (state.readyToChangeLane && vehicle.laneChangeCooldownTimer <= 0)
                {
                    // Unset ready flag immediately
                    state.readyToChangeLane = false;
                    
                    // Which lane direction should we attempt?
                    // Look at current obstacle layout matching side sensors
                    bool tryLeft = !state.leftLaneBlocked;
                    bool tryRight = !state.rightLaneBlocked;

                    if (tryLeft || tryRight)
                    {
                        AIWaypoint currentTarget = vehicle.lookaheadWaypoints[vehicle.activeWaypointIndex];
                        if (currentTarget != null && currentTarget.settings.laneChangePoints != null && currentTarget.settings.laneChangePoints.Length > 0)
                        {
                            AIWaypoint targetLaneWaypoint = null;

                            // Identify the correct left/right waypoint using Dot product / SignedAngle of lateral offset
                            foreach (AIWaypoint lp in currentTarget.settings.laneChangePoints)
                            {
                                if (lp == null) continue;

                                // Check if this lane change waypoint supports the current vehicle type
                                bool isTypeValid = false;
                                if (lp.settings.vehicleType != null && lp.settings.vehicleType.Length > 0)
                                {
                                    for (int j = 0; j < lp.settings.vehicleType.Length; j++)
                                    {
                                        if (lp.settings.vehicleType[j] == vehicle.vehicleType)
                                        {
                                            isTypeValid = true;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    // If no specific vehicle types are defined, assume it's valid for all
                                    isTypeValid = true; 
                                }

                                if (!isTypeValid) continue;

                                Vector3 dirToLane = (lp.transform.position - vehicle.transform.position).normalized;
                                float lateralSignedAngle = Vector3.SignedAngle(vehicle.transform.forward, dirToLane, Vector3.up);

                                if (tryLeft && lateralSignedAngle < -5f) // To our Left
                                {
                                    targetLaneWaypoint = lp;
                                    break;
                                }
                                else if (tryRight && lateralSignedAngle > 5f) // To our Right
                                {
                                    targetLaneWaypoint = lp;
                                    break;
                                }
                            }

                            if (targetLaneWaypoint != null)
                            {
                                // Regenerate the lookahead queue starting from the new lane change point
                                vehicle.lookaheadWaypoints[0] = targetLaneWaypoint;
                                for (int j = 1; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
                                {
                                    vehicle.lookaheadWaypoints[j] = GetNextValidWaypoint(vehicle, vehicle.lookaheadWaypoints[j - 1]);
                                }

                                // Trigger logic swap
                                state.isChangingLanes = true;
                                vehicle.isChangingLanes = true;
                                vehicle.laneChangeCooldownTimer = 10f; // Add a bit of delay before the next jump
                                
                                // Write back to Native Memory so jobs can read the newly queued target immediately
                                vehicleStates[vehicle.arrayIndex] = state;
                                for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
                                {
                                    if (vehicle.lookaheadWaypoints[j] != null)
                                    {
                                        waypointBuffer[state.waypointBufferStartIndex + j] = vehicle.lookaheadWaypoints[j].transform.position;
                                    }
                                }
                                continue; // Skip normal waypoint update this frame
                            }
                        }
                    }
                    
                    // If we get here, lane change failed (no valid lane change points), but we must clear readyToChangeLane 
                    vehicleStates[vehicle.arrayIndex] = state;
                }

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

                    // Reset job state flags
                    state.reachedCurrentWaypoint = false;
                    state.currentTargetIndexOffset = 0;
                    
                    if (state.isChangingLanes)
                    {
                        // Once we reach a lane change destination, we're no longer "changing" lanes
                        state.isChangingLanes = false;
                        vehicle.isChangingLanes = false;
                        vehicle.laneChangeCooldownTimer = 10f; // 10 second cooldown before changing again
                    }

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