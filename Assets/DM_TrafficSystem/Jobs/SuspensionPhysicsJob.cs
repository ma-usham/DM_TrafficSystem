using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public struct WheelPhysicsData
    {
        public int vehicleIndex;
        public Vector3 origin;
        public Vector3 upDir;
        public Vector3 wheelWorldVel;
        
        public float suspensionRestLength;
        public float wheelRadius;
        public float springStrength;
        public float springDamper;
        
        // Results
        public Vector3 resultingForce;
        public bool isGrounded;
    }

    public struct SuspensionPhysicsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RaycastHit> raycastHits;
        public NativeArray<WheelPhysicsData> wheelDataArray;

        public void Execute(int index)
        {
            RaycastHit hit = raycastHits[index];
            WheelPhysicsData wheel = wheelDataArray[index];

            if (hit.distance > 0) // Valid hit
            {
                wheel.isGrounded = true;

                // Calculate spring force based on Hooke's Law
                float offset = wheel.suspensionRestLength - (hit.distance - wheel.wheelRadius);
                float relVel = Vector3.Dot(wheel.upDir, wheel.wheelWorldVel);
                
                float suspensionForce = (offset * wheel.springStrength) - (relVel * wheel.springDamper);
                
                wheel.resultingForce = wheel.upDir * suspensionForce;
            }
            else
            {
                wheel.isGrounded = false;
                wheel.resultingForce = Vector3.zero;
            }

            wheelDataArray[index] = wheel;
        }
    }
}