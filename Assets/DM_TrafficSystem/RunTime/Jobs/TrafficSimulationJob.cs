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
        [ReadOnly] public NativeArray<Vector3> waypointBuffer;
        [ReadOnly] public NativeArray<RaycastHit> sensorHits;
        [ReadOnly] public NativeArray<RaycastHit> leftSensorHits;
        [ReadOnly] public NativeArray<RaycastHit> rightSensorHits;
        [ReadOnly] public NativeArray<RaycastHit> playerSensorHits;

        public Vector3 playerForward;
        public float deltaTime;
        public float arrivalDistance;
        public float timeSinceLevelLoad;

        public void Execute(int index, TransformAccess transform)
        {
            VehicleState state = vehicleStates[index];

            // Helper 1: Braking and target selection
            ProcessBraking(index, ref state, transform, out float distance, out Vector3 dir, out Vector3 targetPos);

            // Helper 2: Movement
            ProcessMovement(ref state, transform, distance, dir, targetPos);

            // Write back to Native memory
            vehicleStates[index] = state;
        }

        private void ProcessBraking(int index, ref VehicleState state, TransformAccess transform, out float distance, out Vector3 dir, out Vector3 targetPos)
        {
            if (state.reachedCurrentWaypoint)
            {
                distance = 0f;
                dir = Vector3.zero;
                targetPos = transform.position;
                return;
            }

            int targetBufferIndex = state.waypointBufferStartIndex + state.currentTargetIndexOffset;
            targetPos = waypointBuffer[targetBufferIndex];

            dir = targetPos - transform.position;
            distance = dir.magnitude;

            // 1. Gather environmental state from sensors
            EvaluateSensors(index, ref state);

            // 2. Decide what to do based on the environmental state
            DetermineSpeedAndPersonality(index, ref state, distance, transform);

            // Calculate the vehicle's forward vector (TransformAccess doesn't have .forward)
            Vector3 currentForward = transform.rotation * Vector3.forward;

            // If the dot product is less than 0, the waypoint is behind the vehicle.
            // We also add a reasonable distance check so it doesn't accidentally skip waypoints 
            // that are far away just because it's facing away from them temporarily.
            bool passedWaypoint = Vector3.Dot(currentForward, dir) < 0f && distance < (arrivalDistance * 3f);

            // Behavior 2: Intersecting the Waypoint
            if (distance <= arrivalDistance || passedWaypoint)
            {
                if (state.isApproachingStopPoint)
                {
                    // Fully halt if we hit the red light trigger distance
                    state.currentSpeed = 0f;
                }
                else
                {
                    // Trigger the Main Thread to give us our next point
                    state.reachedCurrentWaypoint = true;
                }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void EvaluateSensors(int index, ref VehicleState state)
        {
            // Behavior 0: Obstacle Collision Check
            RaycastHit hit = sensorHits[index];
            bool hitSomething = (hit.distance > 0f || hit.normal != Vector3.zero);

            RaycastHit p_hit = playerSensorHits[index];
            bool hitPlayerObj = (p_hit.distance > 0f || p_hit.normal != Vector3.zero);

            state.trafficDetected = false;
            state.detectedTrafficFar = false;
            state.detectedPlayerFar = false;
            state.obstacleDistance = 999f; // Default high distance

            if (hitSomething)
            {
                state.obstacleDistance = hit.distance; // Store actual distance!

                if (hit.distance <= state.sensorSize.z && hit.distance > 0f) state.trafficDetected = true;
                else if (hit.distance > state.sensorSize.z) state.detectedTrafficFar = true;
            }

            if (hitPlayerObj)
            {
                if (p_hit.distance < state.obstacleDistance && p_hit.distance > 0f)
                    state.obstacleDistance = p_hit.distance; // Player is closer

                if (p_hit.distance <= state.sensorSize.z && p_hit.distance > 0f) state.trafficDetected = true;
                else if (p_hit.distance > state.sensorSize.z) state.detectedPlayerFar = true;
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
        private void DetermineSpeedAndPersonality(int index, ref VehicleState state, float distance, TransformAccess transform)
        {
            bool isDeciding = state.isChangingLanes && state.isLaneChangingVehicle && state.wantsToOvertake && state.wantsToHonk;

            // --- 1. FAR ZONE ENCOUNTER: Decision Making Only ---
            if (!isDeciding && (state.detectedPlayerFar || state.detectedTrafficFar))
            {
                uint seed = (uint)(index * 1000 + (timeSinceLevelLoad * 100) + 1);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);

                if (state.detectedPlayerFar)
                {
                    // Check if player is facing us or away
                    float facingDot = Vector3.Dot(playerForward, transform.rotation * Vector3.forward);
                    bool playerIsComing = facingDot < 0f;

                    if (!playerIsComing)
                    {
                        // Player is going (away) -> according to personality, either immediately overtake or do nothing and wait
                        if (rng.NextFloat() < state.playerOvertakeProbability)
                        {
                            state.wantsToOvertake = true;
                        }

                    }
                    else
                    {
                        // Player is coming towards AI -> brake+honk OR change lane based on probability
                        if (rng.NextFloat() < state.playerOvertakeProbability)
                        {
                            state.wantsToOvertake = true; // change lane
                        }
                        else
                        {
                            state.wantsToHonk = true; //Honk and Brake
                            BrakeHalt(ref state, 2f); // Apply brake
                            return;
                        }
                    }
                }
                else if (state.detectedTrafficFar)
                {
                    // Traffic detected: use aiOvertakeProbability
                    if (rng.NextFloat() < state.aiOvertakeProbability)
                    {
                        state.wantsToOvertake = true;
                    }
                }
            }

            // --- 3. NORMAL ZONE: Smooth Braking & Frustration ---
            if (state.trafficDetected)
            {
                // Smooth Braking ONLY happens inside the Normal Sensor length
                float maxSensorRange = state.sensorSize.z;

                // Calculate how close it is (0f = touched it, 1f = far end of the sensor)
                float proximityRatio = Mathf.Clamp01((state.obstacleDistance - state.stoppingDistance) / (maxSensorRange - state.stoppingDistance));

                // Desired speed decreases the closer we get
                float dynamicTargetSpeed = state.localMaxSpeed * proximityRatio;

                // Move toward that speed gradually
                state.currentSpeed = Mathf.Lerp(state.currentSpeed, dynamicTargetSpeed, deltaTime * state.brakingPower);

                // Stop entirely if we're safely within the actual stopping distance boundary
                if (state.obstacleDistance <= state.stoppingDistance + 0.1f)
                {
                    if (state.isChangingLanes)
                    {
                        // Force a slow crawl (e.g., 2m/s) to push past the clipping bumper
                        state.currentSpeed = Mathf.Max(state.currentSpeed, 2f);
                    }
                    else
                    {
                        // Normal behavior: Slam on breaks
                        state.currentSpeed = 0f;
                    }
                }

                // Wait completely in frustration if following in the normal zone and not already trying to overtake
                if (!isDeciding)
                {
                    state.impatienceTimer -= deltaTime;
                    if (state.impatienceTimer <= 0f)
                    {
                        state.wantsToOvertake = true;
                    }
                }
                return;
            }

            // Early Return 4: Traffic Light / Stop Point Braking
            if (state.isApproachingStopPoint && distance < state.stoppingDistance * 2f)
            {
                BrakeHalt(ref state, 1f);
                ResetPersonality(ref state);
                return;
            }

            // Default: Clear road, go fast
            AccelerateNormal(ref state);
            ResetPersonality(ref state);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void BrakeHalt(ref VehicleState state, float brakeMultiplier)
        {
            state.currentSpeed = Mathf.Lerp(state.currentSpeed, 0f, deltaTime * state.brakingPower * brakeMultiplier);
            if (state.currentSpeed < 0.1f) state.currentSpeed = 0f;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void AccelerateNormal(ref VehicleState state)
        {
            state.currentSpeed = Mathf.Lerp(state.currentSpeed, state.localMaxSpeed, deltaTime * state.acceleration);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void ResetPersonality(ref VehicleState state)
        {
            state.impatienceTimer = state.frustrationTime;
            state.wantsToOvertake = false;
            state.wantsToHonk = false;
        }

        private void ProcessMovement(ref VehicleState state, TransformAccess transform, float distance, Vector3 dir, Vector3 targetPos)
        {
            // If we are fully stopped or waiting for the graph, skip the heavy math
            if (state.reachedCurrentWaypoint || state.currentSpeed <= 0.001f)
            {
                state.desiredVelocity = Vector3.zero;
                state.desiredRotation = transform.rotation;
                return;
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

                // Give it a base 0.3f multiplier so it can still steer out of a dead stop!
                float baseTurn = state.isChangingLanes ? 0.5f : 0.1f;
                float turnSpeedMultiplier = Mathf.Clamp(state.physicalSpeed / 1f, baseTurn, 1f);
                // ... proceed with Quaternion.Slerp

                // Make the car "look" at the waypoint, but strictly maintain its current physical pitch and roll
                Quaternion targetRotation = Quaternion.LookRotation(projectedDir, currentUp);
                state.desiredRotation = Quaternion.Slerp(transform.rotation, targetRotation, deltaTime * state.turnSpeed * turnSpeedMultiplier);

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
