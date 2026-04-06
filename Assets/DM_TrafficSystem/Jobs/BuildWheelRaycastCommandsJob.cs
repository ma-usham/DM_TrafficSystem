using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Darkmatter.TrafficSystem
{
    [BurstCompile]
    public struct BuildWheelRaycastCommandsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<int> wheelCounts;
        [ReadOnly] public NativeArray<Vector3> wheelLocalOffsets;
        [ReadOnly] public NativeArray<float> wheelRayLengths;
        public int groundMask;
        
        [WriteOnly] 
        [NativeDisableParallelForRestriction] 
        public NativeArray<RaycastCommand> wheelRaycastCommands;

        public void Execute(int index, TransformAccess transform)
        {
            int wheelCount = wheelCounts[index];
            if (wheelCount == 0) return;

            // The data in the 1D arrays are stored sequentially for each vehicle's wheels
            // Maximum supported wheels per vehicle is hardcoded via indexing formula (e.g. index * 4) 
            int startIndex = index * 4; 

            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            Vector3 down = rotation * Vector3.down; // Same as -transform.up
            
            QueryParameters queryParameters = new QueryParameters(groundMask, false, QueryTriggerInteraction.UseGlobal, false);

            for (int w = 0; w < wheelCount; w++)
            {
                int dataIndex = startIndex + w;
                
                // Calculate wheel origin in world space natively
                Vector3 origin = position + (rotation * wheelLocalOffsets[dataIndex]);
                float rayLength = wheelRayLengths[dataIndex];

                wheelRaycastCommands[dataIndex] = new RaycastCommand(origin, down, queryParameters, rayLength);
            }
        }
    }
}