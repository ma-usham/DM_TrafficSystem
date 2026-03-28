using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public class AIWaypoint : MonoBehaviour
    {
        public WaypointSettings settings;
        void Start()
        {
            settings.speed = 10f;
        }
    }
}
