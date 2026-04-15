using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Processes both braking logic and physical transform updates in a single job.
    /// This unified approach is highly optimized for Android to minimize memory bandwidth 
    /// bottlenecks caused by chaining multiple jobs reading the same data.
    /// </summary>
    public struct TrafficSimulationJob : IJobParallelForTransform
    {
        public NativeArray<VehicleState> vehicleStates;
        [ReadOnly] public NativeArray<VehicleConfig> vehicleConfigs;
        [ReadOnly] public NativeArray<Vector3> waypointBuffer;
        [ReadOnly] public NativeArray<RaycastHit> sensorHits;
        [ReadOnly] public NativeArray<RaycastHit> leftSensorHits;
        [ReadOnly] public NativeArray<RaycastHit> rightSensorHits;

        public NativeQueue<VehicleEvent>.ParallelWriter eventQueue;

        public float deltaTime;
        public float arrivalDistance;
        public float timeSinceLevelLoad;

        public void Execute(int index, TransformAccess transform)
        {
            VehicleState state = vehicleStates[index];
            VehicleConfig config = vehicleConfigs[index];

            // Ensure we don't overflow the buffer if offset is out of bounds
            if (state.currentTargetIndexOffset >= TrafficManager.WAYPOINT_LOOKAHEAD)
            {
                state.currentTargetIndexOffset = TrafficManager.WAYPOINT_LOOKAHEAD - 1;
            }

            // 1. Gather environmental state from sensors
            EvaluateSensors(index, ref state, in config);

            // 2. Job-Based State Machine for scalable, isolated logic paths
            switch (state.currentBehavior)
            {
                case AIState.Cruising:
                case AIState.ChangingLanes:
                    ProcessCruising(index, ref state, in config, transform);
                    break;
                case AIState.Stopping:
                    ProcessStopping(index, ref state, in config, transform);
                    break;
            }

            // Write back to Native memory
            vehicleStates[index] = state;
        }

        private void GetTargetWaypointData(ref VehicleState state, TransformAccess transform, out float distance, out Vector3 dir, out Vector3 targetPos)
        {
            int targetBufferIndex = state.waypointBufferStartIndex + state.currentTargetIndexOffset;
            targetPos = waypointBuffer[targetBufferIndex];

            dir = targetPos - transform.position;
            distance = dir.magnitude;
        }

        private void ProcessCruising(int index, ref VehicleState state, in VehicleConfig config, TransformAccess transform)
        {
            GetTargetWaypointData(ref state, transform, out float distance, out Vector3 dir, out Vector3 targetPos);

            // 2. Decide what to do based on the environmental state
            DetermineSpeedAndPersonality(index, ref state, in config, distance, transform);

            // Calculate the vehicle's forward vector (TransformAccess doesn't have .forward)
            Vector3 currentForward = transform.rotation * Vector3.forward;

            bool passedWaypoint = Vector3.Dot(currentForward, dir) < 0f && distance < (arrivalDistance * 3f);

            // Behavior 2: Intersecting the Waypoint
            if (distance <= arrivalDistance || passedWaypoint)
            {
                state.reachedCurrentWaypoint = true;

                if (state.isApproachingStopPoint)
                {
                    // Fully halt if we hit the red light trigger distance
                    state.currentSpeed = 0f;
                }
                else
                {
                    // [FIX] Double-Buffering Waypoints: Instantly shift the local target to the next 
                    // buffer element to prevent steering micro-stutters while waiting for Main Thread
                    if (state.currentTargetIndexOffset < TrafficManager.WAYPOINT_LOOKAHEAD - 1)
                    {
                        state.currentTargetIndexOffset++;
                        GetTargetWaypointData(ref state, transform, out distance, out dir, out targetPos);
                    }
                }
            }

            // Helper 2: Movement
            ProcessMovement(ref state, in config, transform, distance, dir, targetPos);
        }

        private void ProcessStopping(int index, ref VehicleState state, in VehicleConfig config, TransformAccess transform)
        {
            GetTargetWaypointData(ref state, transform, out float distance, out Vector3 dir, out Vector3 targetPos);

            // Hard halt logic while waiting at a red light or stop sign
            BrakeHalt(ref state, in config, 2f);
            ResetPersonality(ref state, in config);

            // Keep wheels aligned to the stop line
            ProcessMovement(ref state, in config, transform, distance, dir, targetPos);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void EvaluateSensors(int index, ref VehicleState state, in VehicleConfig config)
        {
            // Behavior 0: Obstacle Collision Check
            RaycastHit hit = sensorHits[index];
            bool hitSomething = (hit.distance > 0f || hit.normal != Vector3.zero);

            state.trafficDetected = false;
            state.detectedTrafficFar = false;
            state.obstacleDistance = 999f; // Default high distance

            if (hitSomething)
            {
                state.obstacleDistance = hit.distance; // Store actual distance!

                if (hit.distance <= config.sensorSize.z && hit.distance > 0f) state.trafficDetected = true;
                else if (hit.distance > config.sensorSize.z) state.detectedTrafficFar = true;
            }

            // Side sensors logic
            state.leftLaneBlocked = false;
            state.rightLaneBlocked = false;

            if (state.isSideSensorActive)
            {
                RaycastHit lHit = leftSensorHits[index];
                if (lHit.distance > 0f || lHit.normal != Vector3.zero)
                {
                    state.leftLaneBlocked = true;
                }

                RaycastHit rHit = rightSensorHits[index];
                if (rHit.distance > 0f || rHit.normal != Vector3.zero)
                {
                    state.rightLaneBlocked = true;
                }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DetermineSpeedAndPersonality(int index, ref VehicleState state, in VehicleConfig config, float distance, TransformAccess transform)
        {
            // A vehicle is ready to make a decision if it's not already in the middle of a lane change/overtake sequence
            bool isDecidingStatus = !state.isChangingLanes && state.isLaneChangingVehicle && !state.wantsToChangeLane;

            // --- 1. FAR ZONE ENCOUNTER: Decision Making Only ---
            if (isDecidingStatus && !state.isApproachingStopPoint && state.detectedTrafficFar && !state.hasMadeFarDecision)
            {
                // Mark the decision as made immediately so we don't roll again next frame for this obstacle!
                state.hasMadeFarDecision = true;

                // [FIX] Improved RNG seed via spatial hash to prevent deterministic synchronized lane-changes
                uint seed = (uint)(index * 1337 + (timeSinceLevelLoad * 10000) + (transform.position.sqrMagnitude * 100) + 1);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);
                bool sideLanesClear = !state.leftLaneBlocked || !state.rightLaneBlocked;

                // Traffic detected: use aiOvertakeProbability
                if (sideLanesClear && rng.NextFloat() < config.aiOvertakeProbability)
                {
                    state.wantsToChangeLane = true;
                }
            }

            // --- 3. NORMAL ZONE: Smooth Braking & Frustration ---
            if (state.trafficDetected)
            {
                // Smooth Braking ONLY happens inside the Normal Sensor length
                float maxSensorRange = config.sensorSize.z;
                // 3. Proximity ratio will reach 0.0 when obstacleDistance == actualStopLine
                float proximityRatio = Mathf.Clamp01((state.obstacleDistance - config.stoppingDistance) / (maxSensorRange-config.stoppingDistance));
                // Desired speed decreases the closer we get
                float dynamicTargetSpeed = state.localMaxSpeed * proximityRatio;

                // Move toward that speed gradually
                state.currentSpeed = Mathf.Lerp(state.currentSpeed, dynamicTargetSpeed, deltaTime * config.brakingPower);

                // Stop entirely if we're safely within the actual stopping distance boundary
                if (state.obstacleDistance < config.stoppingDistance)
                {
                    // This allows them to bypass the stopped car to pull out, but prevents ramming a new car in the next lane.
                    state.currentSpeed = Mathf.Lerp(state.currentSpeed, 0f, deltaTime * config.brakingPower * 2f);
                }

                // Re-evaluate decision status in case the Far Zone logic changed state.wantsToOvertake
                bool canFrustrate = !state.isChangingLanes && state.isLaneChangingVehicle && !state.wantsToChangeLane;
                // Wait completely in frustration if following in the normal zone and not already trying to overtake
                if (canFrustrate && !state.isApproachingStopPoint)
                {
                    state.impatienceTimer -= deltaTime;
                    if (state.impatienceTimer <= 0f)
                    {
                        // STRICT CHECK: The car must be outside the stopping distance to turn.
                        // If it has reached the stopping line, it cannot overtake and must wait.
                        bool hasRoomToTurn = state.obstacleDistance > config.stoppingDistance*0.9f; // 90% of stopping distance as a safety buffer to prevent ramming
                        bool sideLanesClear = !state.leftLaneBlocked || !state.rightLaneBlocked;

                        // ONLY trigger overtake if at least one side lane is actually clear, we have room, AND we win the probability roll
                        if (sideLanesClear && hasRoomToTurn)
                        {
                            uint seed = (uint)(index * 777 + (timeSinceLevelLoad * 10000) + (transform.position.sqrMagnitude * 100) + 1);
                            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);

                            if (rng.NextFloat() < config.aiOvertakeProbability)
                            {
                                state.wantsToChangeLane = true;
                            }
                        }

                        // If they didn't decide to overtake (lanes blocked, no room, or failed probability), reset the retry timer
                        if (!state.wantsToChangeLane)
                        {
                            state.impatienceTimer = 5f; // Wait 5 seconds before getting impatient again.(retry time)
                        }
                    }
                }
                else if (state.isApproachingStopPoint)
                {
                    // If we are at a stop point, reset impatience (we are waiting patiently at a red light/stop sign)
                    state.impatienceTimer = config.frustrationTime;
                }

                return;
            }

            // Early Return 4: Traffic Light / Stop Point Braking
            if (state.isApproachingStopPoint && distance < config.stoppingDistance * 2f)
            {
                BrakeHalt(ref state, in config, 1f);
                ResetPersonality(ref state, in config);
                return;
            }

            // Default: Clear road, go fast
            AccelerateNormal(ref state, in config);
            //only reset personality if there is absolutely nothing in front of us.
            if (!state.detectedTrafficFar)
            {
                ResetPersonality(ref state, in config);
            }

        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void BrakeHalt(ref VehicleState state, in VehicleConfig config, float brakeMultiplier)
        {
            state.currentSpeed = Mathf.Lerp(state.currentSpeed, 0f, deltaTime * config.brakingPower * brakeMultiplier);
            if (state.currentSpeed < 0.1f) state.currentSpeed = 0f;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void AccelerateNormal(ref VehicleState state, in VehicleConfig config)
        {
            state.currentSpeed = Mathf.Lerp(state.currentSpeed, state.localMaxSpeed, deltaTime * config.acceleration);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void ResetPersonality(ref VehicleState state, in VehicleConfig config)
        {
            state.impatienceTimer = config.frustrationTime;
            state.wantsToChangeLane = false;
            state.hasMadeFarDecision = false;
        }

        private void ProcessMovement(ref VehicleState state, in VehicleConfig config, TransformAccess transform, float distance, Vector3 dir, Vector3 targetPos)
        {
            // If we are fully stopped, skip the heavy math. 
            // (ReachedCurrentWaypoint flag is no longer checked here due to Double-Buffering)
            if (state.currentSpeed <= 0.001f)
            {
                //return;
            }

            dir.Normalize();


            // calculate current up vector since TransfromAccess doesnt have .up property
            Vector3 currentUp = transform.rotation * Vector3.up;
            Vector3 projectedDir = Vector3.ProjectOnPlane(dir, currentUp);

            if (projectedDir.sqrMagnitude > 0.001f)
            {
                //Calculate the visual Steering Angle
                Vector3 localTarget = Quaternion.Inverse(transform.rotation) * projectedDir;
                state.steeringAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

                // Scale rotation capability by the vehicle's speed ratio
                float speedRatio = Mathf.Clamp01(state.currentSpeed / Mathf.Max(state.localMaxSpeed, 1f));

                if (state.currentSpeed > 0.5f)
                {
                    // Make the car "look" at the waypoint, but strictly maintain its current physical pitch and roll
                    Quaternion targetRotation = Quaternion.LookRotation(projectedDir, currentUp);

                    // Multiply by speedRatio so turn speed dies out as you brake
                    state.desiredRotation = Quaternion.Slerp(transform.rotation, targetRotation, deltaTime * (config.turnSpeed * speedRatio));
                }
                else
                {
                    // Lock rotation completely when crawling to a halt to prevent wobbling
                    state.desiredRotation = transform.rotation;
                    state.steeringAngle = 0f;
                }

            }
            else
            {
                state.desiredRotation = transform.rotation;
            }

            // Move strictly in the direction the vehicle is currently facing
            // This prevents sideways drifting / crab-walking when turn speed is low!
            Vector3 currentForward = state.desiredRotation * Vector3.forward;
            state.desiredVelocity = currentForward * state.currentSpeed;

        }
    }
}
