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
        [ReadOnly] public NativeArray<Vector3> waypointBuffer;

        [WriteOnly] public NativeArray<BoxcastCommand> boxcastCommands;
        
        [WriteOnly] public NativeArray<BoxcastCommand> leftBoxcastCommands;
        [WriteOnly] public NativeArray<BoxcastCommand> rightBoxcastCommands;

        [WriteOnly] public NativeArray<BoxcastCommand> playerBoxcastCommands;

        public void Execute(int index, TransformAccess transform)
        {
            VehicleState state = vehicleStates[index];
            QueryParameters queryParams = new QueryParameters(state.obstacleMask, false, QueryTriggerInteraction.Ignore, false);
            QueryParameters playerQueryParams = new QueryParameters(state.playerMask, false, QueryTriggerInteraction.Ignore, false);

            if (!state.isSensorActive)
            {
                // Generate a dummy command that does nothing if sensor is off
                boxcastCommands[index] = new BoxcastCommand();
                playerBoxcastCommands[index] = new BoxcastCommand();
            }
            else
            {
                // Sweep from the rear of the sensor bounds up to the front based on the desired depth (sensorSize.z)
                Vector3 centerOffset = state.sensorOffset;
                centerOffset.z -= state.sensorSize.z * 0.5f; // Pull back by half the depth

                Vector3 origin = transform.position + transform.rotation * centerOffset;
                Vector3 direction = transform.rotation * Vector3.forward;
                Quaternion boxRotation = transform.rotation;

                if (state.sensorFacesWaypoint)
                {
                    int targetBufferIndex = state.waypointBufferStartIndex + state.currentTargetIndexOffset;
                    Vector3 targetPos = waypointBuffer[targetBufferIndex];
                    Vector3 waypontDir = targetPos - transform.position;
                    waypontDir.y = 0; // Ignore vertical difference
                    
                    if (waypontDir.sqrMagnitude > 0.001f)
                    {
                        direction = waypontDir.normalized;
                        // Avoid using Quaternion.LookRotation if current up vector is zero or not perfectly vertical.
                        // We use transform.rotation * Vector3.up to maintain the local up vector
                        boxRotation = Quaternion.LookRotation(direction, transform.rotation * Vector3.up);
                    }
                }

                Vector3 halfExtents = new Vector3(state.sensorSize.x * 0.5f, state.sensorSize.y * 0.5f, 0.01f);
                
                // [FIX] Removed dangerous hardcoded padding; now bases safety sweep on driver braking profile
                float extendedDistance = state.sensorSize.z + state.stoppingDistance;
                boxcastCommands[index] = new BoxcastCommand(
                    origin,
                    halfExtents,
                    boxRotation,
                    direction,
                    queryParams,
                    extendedDistance 
                );
                
                playerBoxcastCommands[index] = new BoxcastCommand(
                    origin,
                    halfExtents,
                    boxRotation,
                    direction,
                    playerQueryParams,
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