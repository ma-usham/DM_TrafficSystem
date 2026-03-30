namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public struct WaypointSettings
    {
        public float speed;
        public AIWaypoint previousWaypoint;
        public AIWaypoint nextWaypoint;
    }
}
