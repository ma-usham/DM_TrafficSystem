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

        public float deltaTime;
        public float arrivalDistance;

        public void Execute(int index, TransformAccess transform)
        {
            VehicleState state = vehicleStates[index];

            // Helper 1: Braking and target selection
            ProcessBraking(ref state, transform, out float distance, out Vector3 dir, out Vector3 targetPos);

            // Helper 2: Movement
            ProcessMovement(ref state, transform, distance, dir, targetPos);

            // Write back to Native memory
            vehicleStates[index] = state;
        }

        private void ProcessBraking(ref VehicleState state, TransformAccess transform, out float distance, out Vector3 dir, out Vector3 targetPos)
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

            // Behavior 1: Traffic Light / Stop Point Braking
            if (state.isApproachingStopPoint && distance < state.stoppingDistance * 2f)
            {
                state.currentSpeed = Mathf.Lerp(state.currentSpeed, 0f, deltaTime * state.brakingPower);
                if (state.currentSpeed < 0.1f) state.currentSpeed = 0f;
            }
            else
            {
                // Accelerate up to speed limit
                state.currentSpeed = Mathf.Lerp(state.currentSpeed, state.maxSpeed, deltaTime * state.acceleration);
            }

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

            //calculate current up vector since TransfromAccess doesnt have .up property
            Vector3 currentUp = transform.rotation * Vector3.up;
            Vector3 projectedDir = Vector3.ProjectOnPlane(dir, currentUp);

            if (projectedDir.sqrMagnitude > 0.001f)
            {
                //Calculate the visual Steering Angle
                Vector3 localTarget = Quaternion.Inverse(transform.rotation) * projectedDir;
                state.steeringAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;



                // Make the car "look" at the waypoint, but strictly maintain its current physical pitch and roll
                Quaternion targetRotation = Quaternion.LookRotation(projectedDir, currentUp);
                state.desiredRotation = Quaternion.Slerp(transform.rotation, targetRotation, deltaTime * state.turnSpeed);
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
