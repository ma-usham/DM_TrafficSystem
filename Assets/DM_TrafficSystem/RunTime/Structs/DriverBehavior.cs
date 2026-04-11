using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public struct DriverBehavior
    {
        [Tooltip("The absolute maximum speed this vehicle's engine can achieve.")]
        public float engineMaxSpeed;

        [Tooltip("How much this driver adheres to the waypoint speed limit (e.g. 0.85 = 15% under, 1.15 = 15% over).")]
        public Vector2 speedMultiplierRange;

        [Tooltip("How quickly the vehicle speeds up.")]
        public float acceleration;

        [Tooltip("How quickly the vehicle slows down.")]
        public float brakingPower;

        [Tooltip("How fast the vehicle turns towards waypoints.")]
        public float turnSpeed;

        [Tooltip("Distance at which the vehicle stops before a stop point or obstacle.")]
        public float stoppingDistance;

        [Header("Personality")]
        [Tooltip("Can this vehicle ever change lanes? (Set false for heavy vehicles)")]
        public bool willChangeLane;

        [Tooltip("Min and Max time to follow a slow vehicle before trying to overtake. (X = Min, Y = Max)")]
        public Vector2 frustrationTime;

        [Tooltip("Min and Max cooldown after changing a lane before it can change again. (X = Min, Y = Max)")]
        public Vector2 laneChangeCooldown;

        [Tooltip("Probability (0.0 to 1.0) of overtaking when an AI is detected far away.")]
        [Range(0f, 1f)] public float aiOvertakeProbability;
    }
}