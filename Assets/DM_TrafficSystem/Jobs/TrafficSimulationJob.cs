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

            // Behavior 2: Intersecting the Waypoint
            if (distance <= arrivalDistance)
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

            Vector3 currentPos = transform.position;

            // Instead of translating transform directly, calculate desired velocity output
            dir.Normalize();
            state.desiredVelocity = dir * state.currentSpeed;

            // Smooth Rotation interpolation target
            if (dir.sqrMagnitude > 0.001f)
            {
                // In TransformAccess, 'up' is calculated by rotating Vector3.up by the current rotation
                Vector3 currentUp = transform.rotation * Vector3.up;
                Quaternion targetRot = Quaternion.LookRotation(dir, currentUp);
                // We keep the spherical interpolation to give realistic turning limits based on turnSpeed
                state.desiredRotation = Quaternion.Slerp(transform.rotation, targetRot, deltaTime * state.turnSpeed); 
            }
            else
            {
                state.desiredRotation = transform.rotation;
            }
        }
    }
}
