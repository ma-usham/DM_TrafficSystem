using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public enum AIState : byte
    {
        Cruising,
        ChangingLanes,
        Stopping
    }

    public enum VehicleEventType : byte
    {
        HonkHorn,
        BrakesApplied,
        BrakesReleased,
        TurnSignalLeft,
        TurnSignalRight,
        TurnSignalsOff
    }

    public struct VehicleEvent
    {
        public int vehicleIndex;
        public VehicleEventType eventType;
    }
    
    /// <summary>
    /// Read-Only configuration data passed into the Job System. 
    /// Holds static vehicle capabilities that never change during runtime to save memory bandwidth.
    /// </summary>
    public struct VehicleConfig
    {
        public float engineMaxSpeed;
        public float speedMultiplier;
        public float acceleration;
        public float brakingPower;
        public float turnSpeed;
        public float stoppingDistance;

        public Vector3 sensorSize;
        public Vector3 sensorOffset;
        public int obstacleMask;

        public Vector3 leftSensorSize;
        public Vector3 leftSensorOffset;
        public Vector3 rightSensorSize;
        public Vector3 rightSensorOffset;

        public float frustrationTime;
        public float laneChangeCooldown;
        public float aiOvertakeProbability;
    }

    /// <summary>
    /// Mutable runtime data struct used by the C# Job System to process live vehicle movement and AI.
    /// </summary>
    public struct VehicleState
    {
        public AIState currentBehavior;

        public float currentSpeed;
        public float physicalSpeed; // True Rigidbody forward velocity
        public float localMaxSpeed;
        
        // Output from the job to be applied to Rigidbody
        
        public Vector3 desiredVelocity;
        public Quaternion desiredRotation;
        public float steeringAngle;

        // Index pointing to the start of this vehicle's 5-waypoint block in the global waypoint buffer
        public int waypointBufferStartIndex;
        
        // In a 5-waypoint buffer, which one are we currently driving towards? (0 to 4)
        public int currentTargetIndexOffset;
        
        [System.Flags]
        public enum StateFlags : uint
        {
            ReachedCurrentWaypoint = 1 << 0,
            IsApproachingStopPoint = 1 << 1,
            IsSensorActive = 1 << 2,
            SensorFacesWaypoint = 1 << 3,
            IsSideSensorActive = 1 << 4,
            TrafficDetected = 1 << 5,
            DetectedTrafficFar = 1 << 6,
            LeftLaneBlocked = 1 << 7,
            RightLaneBlocked = 1 << 8,
            IsChangingLanes = 1 << 9,
            IsLaneChangingVehicle = 1 << 10,
            WantsToChangeLane = 1 << 11,
            HasMadeFarDecision = 1 << 12
        }
        
        public uint stateFlags;

        // Waypoint Flags
        public bool reachedCurrentWaypoint
        {
            get => (stateFlags & (uint)StateFlags.ReachedCurrentWaypoint) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.ReachedCurrentWaypoint) : (stateFlags & ~(uint)StateFlags.ReachedCurrentWaypoint);
        }
        public bool isApproachingStopPoint
        {
            get => (stateFlags & (uint)StateFlags.IsApproachingStopPoint) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.IsApproachingStopPoint) : (stateFlags & ~(uint)StateFlags.IsApproachingStopPoint);
        }

        // Sensor configuration for obstacle detection
        public bool isSensorActive
        {
            get => (stateFlags & (uint)StateFlags.IsSensorActive) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.IsSensorActive) : (stateFlags & ~(uint)StateFlags.IsSensorActive);
        }
        public bool sensorFacesWaypoint
        {
            get => (stateFlags & (uint)StateFlags.SensorFacesWaypoint) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.SensorFacesWaypoint) : (stateFlags & ~(uint)StateFlags.SensorFacesWaypoint);
        }

        public bool isSideSensorActive
        {
            get => (stateFlags & (uint)StateFlags.IsSideSensorActive) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.IsSideSensorActive) : (stateFlags & ~(uint)StateFlags.IsSideSensorActive);
        }

        // Sensor status output
        public bool trafficDetected
        {
            get => (stateFlags & (uint)StateFlags.TrafficDetected) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.TrafficDetected) : (stateFlags & ~(uint)StateFlags.TrafficDetected);
        }
        public bool detectedTrafficFar
        {
            get => (stateFlags & (uint)StateFlags.DetectedTrafficFar) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.DetectedTrafficFar) : (stateFlags & ~(uint)StateFlags.DetectedTrafficFar);
        }
        public float obstacleDistance;
        
        // Side sensor status
        public bool leftLaneBlocked
        {
            get => (stateFlags & (uint)StateFlags.LeftLaneBlocked) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.LeftLaneBlocked) : (stateFlags & ~(uint)StateFlags.LeftLaneBlocked);
        }
        public bool rightLaneBlocked
        {
            get => (stateFlags & (uint)StateFlags.RightLaneBlocked) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.RightLaneBlocked) : (stateFlags & ~(uint)StateFlags.RightLaneBlocked);
        }

        // Internal Lane Changing States
        public bool isChangingLanes
        {
            get => (stateFlags & (uint)StateFlags.IsChangingLanes) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.IsChangingLanes) : (stateFlags & ~(uint)StateFlags.IsChangingLanes);
        }

        // Overtaking & Personality
        public bool isLaneChangingVehicle
        {
            get => (stateFlags & (uint)StateFlags.IsLaneChangingVehicle) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.IsLaneChangingVehicle) : (stateFlags & ~(uint)StateFlags.IsLaneChangingVehicle);
        }

        // Runtime states
        public float impatienceTimer;
        public bool wantsToChangeLane
        {
            get => (stateFlags & (uint)StateFlags.WantsToChangeLane) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.WantsToChangeLane) : (stateFlags & ~(uint)StateFlags.WantsToChangeLane);
        }

        public bool hasMadeFarDecision
        {
            get => (stateFlags & (uint)StateFlags.HasMadeFarDecision) != 0;
            set => stateFlags = value ? (stateFlags | (uint)StateFlags.HasMadeFarDecision) : (stateFlags & ~(uint)StateFlags.HasMadeFarDecision);
        }
    }
}