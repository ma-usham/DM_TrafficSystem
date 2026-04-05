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

        public void Execute(int index, TransformAccess transform)
        {
            VehicleState state = vehicleStates[index];
            
            if (!state.isSensorActive)
            {
                // Generate a dummy command that does nothing if sensor is off
                boxcastCommands[index] = new BoxcastCommand();
                return;
            }

            // Sweep from the rear of the sensor bounds up to the front based on the desired depth (sensorSize.z)
            Vector3 centerOffset = state.sensorOffset;
            centerOffset.z -= state.sensorSize.z * 0.5f; // Pull back by half the depth

            Vector3 origin = transform.position + transform.rotation * centerOffset;
            Vector3 direction = transform.rotation * Vector3.forward;

            // Use X and Y width/height unchanged, but make the box incredibly thin in the Z-axis 
            // so we don't start the sweep already inside a collider, but rather sweep through the space.
            Vector3 halfExtents = new Vector3(state.sensorSize.x * 0.5f, state.sensorSize.y * 0.5f, 0.01f);
            
            // Generate the command with an extra 5m for far player detection
            float extendedDistance = state.sensorSize.z + 5f;
            boxcastCommands[index] = new BoxcastCommand(
                origin,
                halfExtents,
                transform.rotation,
                direction,
                new QueryParameters(state.obstacleMask, false, QueryTriggerInteraction.Ignore, false),
                extendedDistance // The sweep depth plus the 10m extension
            );
        }
    }
}