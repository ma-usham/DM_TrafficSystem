using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Processes vehicle personality, speed adjustments, braking, and lane change decisions.
    /// This is the "Think" phase of the AI pipeline.
    /// </summary>
    public struct BehaviorDecisionJob : IJobParallelFor
    {
        public NativeArray<VehicleState> vehicleStates;
        [ReadOnly] public NativeArray<Vector3> waypointBuffer;

        public NativeQueue<VehicleEvent>.ParallelWriter eventQueue;

        public Vector3 playerForward;
        public float deltaTime;
        public float arrivalDistance;
        public float timeSinceLevelLoad;

        public void Execute(int index)
        {
            VehicleState state = vehicleStates[index];

            if (state.reachedCurrentWaypoint)
            {
                vehicleStates[index] = state;
                return;
            }

            if (state.currentTargetIndexOffset >= TrafficManager.WAYPOINT_LOOKAHEAD)
            {
                state.currentTargetIndexOffset = TrafficManager.WAYPOINT_LOOKAHEAD - 1;
            }

            // We can't access transform.position directly inside an IJobParallelFor, 
            // but we can use physicalSpeed or rely on MovementResolverJob to do the spatial vector math. 
            // Wait, DetermineSpeedAndPersonality needs distance and transform!
        }
    }
}