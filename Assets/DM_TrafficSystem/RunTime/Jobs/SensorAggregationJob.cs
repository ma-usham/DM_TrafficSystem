using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Evaluates raw raycast hits from sensors and converts them into boolean states and distances.
    /// This is the "Sense" phase of the AI pipeline.
    /// </summary>
    public struct SensorAggregationJob : IJobParallelFor
    {
        public NativeArray<VehicleState> vehicleStates;
        
        [ReadOnly] public NativeArray<RaycastHit> frontSensorHits;
        [ReadOnly] public NativeArray<RaycastHit> leftSensorHits;
        [ReadOnly] public NativeArray<RaycastHit> rightSensorHits;
        [ReadOnly] public NativeArray<RaycastHit> playerSensorHits;

        public void Execute(int index)
        {
            VehicleState state = vehicleStates[index];

            // Behavior 0: Obstacle Collision Check
            RaycastHit hit = frontSensorHits[index];
            bool hitSomething = (hit.distance > 0f && hit.normal != Vector3.zero);

            RaycastHit p_hit = playerSensorHits[index];
            bool hitPlayerObj = (p_hit.distance > 0f && p_hit.normal != Vector3.zero);

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
                if (lHit.distance > 0f && lHit.normal != Vector3.zero)
                {
                    state.leftLaneBlocked = true;
                }

                RaycastHit rHit = rightSensorHits[index];
                if (rHit.distance > 0f && rHit.normal != Vector3.zero)
                {
                    state.rightLaneBlocked = true;
                }
            }

            // Write back to the array
            vehicleStates[index] = state;
        }
    }
}