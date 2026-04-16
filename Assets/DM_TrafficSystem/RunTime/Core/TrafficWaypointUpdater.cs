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
        // private List<AIWaypoint> _fallbackWaypoints = new List<AIWaypoint>();

        public void UpdateWaypoint(List<AIVehicle> activeVehicles, NativeArray<VehicleState> vehicleStates, NativeArray<VehicleConfig> vehicleConfigs, NativeArray<Vector3> waypointBuffer, float deltaTime)
        {
            // Process the graph logic for vehicles that finished driving to their target point
            for (int i = 0; i < activeVehicles.Count; i++)
            {
                AIVehicle vehicle = activeVehicles[i];
                VehicleState state = vehicleStates[vehicle.arrayIndex];
                VehicleConfig config = vehicleConfigs[vehicle.arrayIndex];

                UpdateCooldowns(vehicle, deltaTime);

                switch (state.currentBehavior)
                {
                    case AIState.Cruising:
                        ProcessLaneChangeIntent(vehicle, ref state, in config, waypointBuffer);
                        if (state.reachedCurrentWaypoint && !state.isChangingLanes)
                        {
                            AdvanceWaypointQueue(vehicle, ref state, in config, waypointBuffer);
                        }
                        if (state.isApproachingStopPoint && state.reachedCurrentWaypoint)
                        {
                            state.currentBehavior = AIState.Stopping;
                        }
                        break;

                    case AIState.ChangingLanes:
                        // No lane change intent processing, just finish the lane change
                        if (state.reachedCurrentWaypoint)
                        {
                            AdvanceWaypointQueue(vehicle, ref state, in config, waypointBuffer);
                        }
                        break;

                    case AIState.Stopping:
                        if (!state.isApproachingStopPoint)
                        {
                            state.currentBehavior = AIState.Cruising;
                            if (state.reachedCurrentWaypoint)
                            {
                                AdvanceWaypointQueue(vehicle, ref state, in config, waypointBuffer);
                            }
                        }
                        break;
                }

                // Write back
                vehicleStates[vehicle.arrayIndex] = state;
            }
        }

        private void UpdateCooldowns(AIVehicle vehicle, float deltaTime)
        {
            if (vehicle.laneChangeCooldownTimer > 0)
            {
                vehicle.laneChangeCooldownTimer = Mathf.Max(0f, vehicle.laneChangeCooldownTimer - deltaTime);
            }
        }

        private void ProcessLaneChangeIntent(AIVehicle vehicle, ref VehicleState state, in VehicleConfig config, NativeArray<Vector3> waypointBuffer)
        {
            // 1. Check if we have the intent, the cooldown is ready, and the vehicle is allowed to change lanes
            bool canChange = state.wantsToChangeLane && vehicle.laneChangeCooldownTimer <= 0 && state.isLaneChangingVehicle;
            
            // 2. Abort if we can't change, or if both side lanes are currently blocked
            if (!canChange || (state.leftLaneBlocked && state.rightLaneBlocked)) return;

            state.wantsToChangeLane = false;
            state.impatienceTimer = config.frustrationTime;

            bool tryLeft = !state.leftLaneBlocked;
            bool tryRight = !state.rightLaneBlocked;

            if (tryLeft || tryRight)
            {
                if (TryFindLaneChangeWaypoint(vehicle, tryLeft, tryRight, out AIWaypoint targetLaneWaypoint, out int turnDirection))
                {
                    ExecuteLaneSwitch(vehicle, ref state, targetLaneWaypoint, waypointBuffer, turnDirection);
                }
            }
        }

        private bool TryFindLaneChangeWaypoint(AIVehicle vehicle, bool tryLeft, bool tryRight, out AIWaypoint targetLaneWaypoint, out int turnDirection)
        {
            targetLaneWaypoint = null;
            turnDirection = 0;
            AIWaypoint currentTarget = vehicle.lookaheadWaypoints[0];

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
                    turnDirection = -1; // Left
                    return true;
                }
                if (tryRight && lateralSignedAngle > 5f)
                {
                    targetLaneWaypoint = lp;
                    turnDirection = 1; // Right
                    return true;
                }
            }
            return false;
        }

        private void ExecuteLaneSwitch(AIVehicle vehicle, ref VehicleState state, AIWaypoint targetLaneWaypoint, NativeArray<Vector3> waypointBuffer, int turnDirection)
        {
            // Regenerate the lookahead queue starting from the new lane change point
            vehicle.lookaheadWaypoints[0] = targetLaneWaypoint;
            for (int j = 1; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
            {
                vehicle.lookaheadWaypoints[j] = GetNextValidWaypoint(vehicle, vehicle.lookaheadWaypoints[j - 1]);
            }

            // Trigger logic swap
            state.isChangingLanes = true;
            state.currentBehavior = AIState.ChangingLanes;
            vehicle.isChangingLanes = true;
            
            // Turn on the blinkers!
            vehicle.SetTurnSignals(turnDirection);

            WriteLookaheadToBuffer(vehicle, state.waypointBufferStartIndex, waypointBuffer);
        }

        private void AdvanceWaypointQueue(AIVehicle vehicle, ref VehicleState state, in VehicleConfig config, NativeArray<Vector3> waypointBuffer)
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
                state.currentBehavior = AIState.Cruising;
                vehicle.isChangingLanes = false;
                vehicle.laneChangeCooldownTimer = config.laneChangeCooldown; // Cooldown before changing again
                
                // Turn off the blinkers!
                vehicle.SetTurnSignals(0);
            }

            // Update stop state for the new target
            if (vehicle.lookaheadWaypoints[0] != null)
            {
                state.isApproachingStopPoint = vehicle.lookaheadWaypoints[0].settings.isStopPoint;
                float wpSpeed = vehicle.lookaheadWaypoints[0].settings.speed;
                float adjustedWpSpeed = wpSpeed > 0 ? (wpSpeed * config.speedMultiplier) : config.engineMaxSpeed;
                state.localMaxSpeed = Mathf.Min(config.engineMaxSpeed, adjustedWpSpeed);
            }

            // 4. Write back to Native Memory so jobs can read the newly queued target
            WriteLookaheadToBuffer(vehicle, state.waypointBufferStartIndex, waypointBuffer);
        }

        private void WriteLookaheadToBuffer(AIVehicle vehicle, int bufferStartIndex, NativeArray<Vector3> waypointBuffer)
        {
            for (int j = 0; j < TrafficManager.WAYPOINT_LOOKAHEAD; j++)
            {
                waypointBuffer[bufferStartIndex + j] = Vector3.zero;

                if (vehicle.lookaheadWaypoints[j] != null)
                {
                    waypointBuffer[bufferStartIndex + j] = vehicle.lookaheadWaypoints[j].transform.position;
                }
            }
        }

        public void UpdateStopWaypoints(List<AIVehicle> activeVehicles, NativeArray<VehicleState> vehicleStates, NativeArray<VehicleConfig> vehicleConfigs)
        {
            // Poll dynamic stop states so we can toggle lights in real-time in the Inspector
            for (int i = 0; i < activeVehicles.Count; i++)
            {
                AIVehicle vehicle = activeVehicles[i];
                if (vehicle.lookaheadWaypoints[0] != null)
                {
                    VehicleState state = vehicleStates[vehicle.arrayIndex];
                    VehicleConfig config = vehicleConfigs[vehicle.arrayIndex];
                    state.isApproachingStopPoint = vehicle.lookaheadWaypoints[0].settings.isStopPoint;

                    float wpSpeed = vehicle.lookaheadWaypoints[0].settings.speed;
                    float adjustedWpSpeed = wpSpeed > 0 ? (wpSpeed * config.speedMultiplier) : config.engineMaxSpeed;
                    state.localMaxSpeed = Mathf.Min(config.engineMaxSpeed, adjustedWpSpeed);

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
            if (_validWaypoints.Count > 0) return _validWaypoints[Random.Range(0, _validWaypoints.Count)];
            else
            {
                return nextWaypoints[Random.Range(0, nextWaypoints.Length)];
            }
        }
    }
}
