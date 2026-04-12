using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Represents a single generated lane and the ordered waypoints that belong to it.
    /// </summary>
    public class AILane : MonoBehaviour
    {
        public List<AIWaypoint> waypoints = new List<AIWaypoint>();
        public float laneSpeedLimit = 30f;
        public VehicleType[] laneVehicleType = new VehicleType[] { VehicleType.Default };
        public DrivingDirection drivingDirection = DrivingDirection.Left;
    }
}
