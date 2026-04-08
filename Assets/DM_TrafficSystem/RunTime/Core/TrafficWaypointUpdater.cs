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

                UpdateCooldowns(vehicle);
                ProcessLaneChangeIntent(vehicle, ref state, waypointBuffer);
                
                if (state.reachedCurrentWaypoint)
                {
                    AdvanceWaypointQueue(vehicle, ref state, waypointBuffer);
                }
                
                // Write back
                vehicleStates[vehicle.arrayIndex] = state;
            }
        }

        private void UpdateCooldowns(AIVehicle vehicle)
        {
            if (vehicle.laneChangeCooldownTimer > 0)
            {
                vehicle.laneChangeCooldownTimer -= Time.deltaTime;
            }
        }

        private void ProcessLaneChangeIntent(AIVehicle vehicle, ref VehicleState state, NativeArray<Vector3> waypointBuffer)
        {
            if (state.wantsToOvertake && vehicle.laneChangeCooldownTimer <= 0 && state.isLaneChangingVehicle)
            {
                if (!state.leftLaneBlocked || !state.rightLaneBlocked)
                {
                    state.readyToChangeLane = true;
                }
            }

            if (!state.readyToChangeLane || vehicle.laneChangeCooldownTimer > 0) return;

            // Unset ready flag immediately
            state.readyToChangeLane = false;
            state.wantsToOvertake = false;
            state.impatienceTimer = state.frustrationTime;
            
            bool tryLeft = !state.leftLaneBlocked;
            bool tryRight = !state.rightLaneBlocked;

            if (tryLeft || tryRight)
            {
                if (TryFindLaneChangeWaypoint(vehicle, tryLeft, tryRight, out AIWaypoint targetLaneWaypoint))
                {
                    ExecuteLaneSwitch(vehicle, ref state, targetLaneWaypoint, waypointBuffer);
                }
            }
        }

        private bool TryFindLaneChangeWaypoint(AIVehicle vehicle, bool tryLeft, bool tryRight, out AIWaypoint targetLaneWaypoint)
        {
            targetLaneWaypoint = null;
            AIWaypoint currentTarget = vehicle.lookaheadWaypoints[vehicle.activeWaypointIndex];
            
            if (currentTarget == null || currentTarget.settings.laneChangePoints == null || currentTarget.settings.laneChangePoints.Length == 0)
                return false;

            foreach (AIWaypoint lp in currentTarget.settings.laneChangePoints)
            {
                if (lp == null) continue;

                // Check type validity
                bool isTypeValid = true;
                if (lp.settings.vehicleType != null && lp.settings.vehicleType.Length > 0)
                {
                    isTypeValid = false;
                    for (int j = 0; j < lp.settings.vehicleType.Length; j++)
                    {
                        if (lp.settings.vehicleType[j] == vehicle.vehicleType)
                        {
                            isTypeValid = true;
                            break;
                        }
                    }
                }

                if (!isTypeValid) continue;

                Vector3 dirToLane = (lp.transform.position - vehicle.transform.position).normalized;
                float lateralSignedAngle = Vector3.SignedAngle(vehicle.transform.forward, dirToLane, Vector3.up);

                if (tryLeft && lateralSignedAngle < -5f)
                {
                    targetLaneWaypoint = lp;
                    return true;
                }
                if (tryRight && lateralSignedAngle > 5f)
                {
                    targetLaneWaypoint = lp;
                    return true;
                }
            }
            return false;
        }

        private void ExecuteLaneSwitch(AIVehicle vehicle, ref VehicleState state, AIWaypoint targetLaneWaypoint, NativeArray<Vector3> waypointBuffer)
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
            vehicle.laneChangeCooldownTimer = state.laneChangeCooldown; // Cooldown from personality

            // Write directly to buffers
            for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
            {
                if (vehicle.lookaheadWaypoints[j] != null)
                {
                    waypointBuffer[state.waypointBufferStartIndex + j] = vehicle.lookaheadWaypoints[j].transform.position;
                }
            }
        }

        private void AdvanceWaypointQueue(AIVehicle vehicle, ref VehicleState state, NativeArray<Vector3> waypointBuffer)
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
                vehicle.laneChangeCooldownTimer = state.laneChangeCooldown; // Cooldown before changing again
            }

            // Update stop state for the new target
            if (vehicle.lookaheadWaypoints[0] != null)
            {
                state.isApproachingStopPoint = vehicle.lookaheadWaypoints[0].settings.isStopPoint;
                float wpSpeed = vehicle.lookaheadWaypoints[0].settings.speed;
                float adjustedWpSpeed = wpSpeed > 0 ? (wpSpeed * state.speedMultiplier) : state.engineMaxSpeed;
                state.localMaxSpeed = Mathf.Min(state.engineMaxSpeed, adjustedWpSpeed);
            }

            // 4. Write back to Native Memory so jobs can read the newly queued target
            for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
            {
                if (vehicle.lookaheadWaypoints[j] != null)
                {
                    waypointBuffer[state.waypointBufferStartIndex + j] = vehicle.lookaheadWaypoints[j].transform.position;
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
                    
                    float wpSpeed = vehicle.lookaheadWaypoints[0].settings.speed;
                    float adjustedWpSpeed = wpSpeed > 0 ? (wpSpeed * state.speedMultiplier) : state.engineMaxSpeed;
                    state.localMaxSpeed = Mathf.Min(state.engineMaxSpeed, adjustedWpSpeed);
                    
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