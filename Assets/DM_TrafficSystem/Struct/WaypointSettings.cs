namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Stores the navigation links and movement metadata for a generated waypoint.
    /// </summary>
    [System.Serializable]
    public struct WaypointSettings
    {
        /// <summary>
        /// Defines the preferred travel speed at this waypoint.
        /// </summary>
        public float speed;

        /// <summary>
        /// Restricts which vehicle categories can use this waypoint.
        /// </summary>
        public VehicleType[] vehicleType;

        /// <summary>
        /// Stores the previous waypoint links that can arrive at this waypoint.
        /// </summary>
        public AIWaypoint[] previousWaypoint;

        /// <summary>
        /// Stores the next waypoint links that can be used to continue forward.
        /// </summary>
        public AIWaypoint[] nextWaypoint;

        /// <summary>
        /// Stores optional lateral links used for lane changes and overtaking.
        /// </summary>
        public AIWaypoint[] laneChangePoints;
    }
}
