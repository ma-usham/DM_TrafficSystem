namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public struct WaypointSettings
    {
        public float speed;
        public VehicleType[] vehicleType;
        public AIWaypoint[] previousWaypoint;
        public AIWaypoint[] nextWaypoint;
        public AIWaypoint[] laneChangePoints; // Optional: Waypoints that can be used for lane changing. This can be used to create more complex road networks where vehicles can switch lanes at certain points.
    }
}
