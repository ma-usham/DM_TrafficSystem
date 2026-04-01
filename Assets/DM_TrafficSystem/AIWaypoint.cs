using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Represents a generated navigation point used by vehicles to follow lanes and lane changes.
    /// </summary>
    public class AIWaypoint : MonoBehaviour
    {
        public WaypointSettings settings = new WaypointSettings { speed = 10f };
    }
}
