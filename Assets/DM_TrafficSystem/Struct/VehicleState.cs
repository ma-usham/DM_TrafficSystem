using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Pure data struct used by the C# Job System to process vehicle movement and AI.
    /// Needs to be passable into NativeArrays, so it contains no classes (like Transforms).
    /// </summary>
    public struct VehicleState
    {
        public float currentSpeed;
        public float maxSpeed;
        public float acceleration;
        public float brakingPower;
        public float turnSpeed;
        public float stoppingDistance;
        
        // Output from the job to be applied to Rigidbody
        public Vector3 desiredVelocity;
        public Quaternion desiredRotation;
        public float steeringAngle;

        // Index pointing to the start of this vehicle's 5-waypoint block in the global waypoint buffer
        public int waypointBufferStartIndex;
        
        // In a 5-waypoint buffer, which one are we currently driving towards? (0 to 4)
        public int currentTargetIndexOffset;
        
        // Flag set by the Job to tell the Main Thread "I need the next waypoint!"
        public bool reachedCurrentWaypoint;
        
        // Flag set by the Main Thread if the currently requested waypoint is a stop point (like a traffic light)
        public bool isApproachingStopPoint;
    }
}