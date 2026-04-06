using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Prepares BoxcastCommands for the Job System to detect obstacles in front of the vehicle.
    /// </summary>
    public struct VehicleSensorJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<VehicleState> vehicleStates;
        [WriteOnly] public NativeArray<BoxcastCommand> boxcastCommands;
        
        [WriteOnly] public NativeArray<BoxcastCommand> leftBoxcastCommands;
        [WriteOnly] public NativeArray<BoxcastCommand> rightBoxcastCommands;

        public void Execute(int index, TransformAccess transform)
        {
            VehicleState state = vehicleStates[index];
            QueryParameters queryParams = new QueryParameters(state.obstacleMask, false, QueryTriggerInteraction.Ignore, false);

            if (!state.isSensorActive)
            {
                // Generate a dummy command that does nothing if sensor is off
                boxcastCommands[index] = new BoxcastCommand();
            }
            else
            {
                // Sweep from the rear of the sensor bounds up to the front based on the desired depth (sensorSize.z)
                Vector3 centerOffset = state.sensorOffset;
                centerOffset.z -= state.sensorSize.z * 0.5f; // Pull back by half the depth

                Vector3 origin = transform.position + transform.rotation * centerOffset;
                Vector3 direction = transform.rotation * Vector3.forward;

                Vector3 halfExtents = new Vector3(state.sensorSize.x * 0.5f, state.sensorSize.y * 0.5f, 0.01f);
                
                float extendedDistance = state.sensorSize.z + 5f;
                boxcastCommands[index] = new BoxcastCommand(
                    origin,
                    halfExtents,
                    transform.rotation,
                    direction,
                    queryParams,
                    extendedDistance 
                );
            }
            
            if (!state.isSideSensorActive)
            {
                leftBoxcastCommands[index] = new BoxcastCommand();
                rightBoxcastCommands[index] = new BoxcastCommand();
            }
            else
            {
                // Left Sensor setup
                Vector3 leftOffset = state.leftSensorOffset;
                leftOffset.x += state.leftSensorSize.x * 0.5f; // pull inward
                Vector3 leftOrigin = transform.position + transform.rotation * leftOffset;
                // Move in the -X local direction
                Vector3 leftDirection = transform.rotation * Vector3.left;
                
                Vector3 leftHalfExtents = new Vector3(0.01f, state.leftSensorSize.y * 0.5f, state.leftSensorSize.z * 0.5f);
                float leftExtendedDistance = state.leftSensorSize.x;
                
                leftBoxcastCommands[index] = new BoxcastCommand(
                    leftOrigin,
                    leftHalfExtents,
                    transform.rotation, // Keep rotation same as car
                    leftDirection, 
                    queryParams,
                    leftExtendedDistance
                );

                // Right Sensor setup
                Vector3 rightOffset = state.rightSensorOffset;
                rightOffset.x -= state.rightSensorSize.x * 0.5f; // pull inward
                Vector3 rightOrigin = transform.position + transform.rotation * rightOffset;
                // Move in the +X local direction
                Vector3 rightDirection = transform.rotation * Vector3.right;
                
                Vector3 rightHalfExtents = new Vector3(0.01f, state.rightSensorSize.y * 0.5f, state.rightSensorSize.z * 0.5f);
                float rightExtendedDistance = state.rightSensorSize.x;

                rightBoxcastCommands[index] = new BoxcastCommand(
                    rightOrigin,
                    rightHalfExtents,
                    transform.rotation,
                    rightDirection,
                    queryParams,
                    rightExtendedDistance
                );
            }
        }
    }
}