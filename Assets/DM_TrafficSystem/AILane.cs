using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public class AILane : MonoBehaviour
    {
        public List<AIWaypoint> waypoints = new List<AIWaypoint>();
        public float laneSpeedLimit = 30f;
    }
}
