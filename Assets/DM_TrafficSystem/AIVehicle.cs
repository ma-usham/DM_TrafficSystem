using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Attached to the dummy car GameObjects. Registers with the TrafficManager.
    /// This keeps track of the MonoBehaviour Waypoints for the Main Thread to trace the graph.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AIVehicle : MonoBehaviour
    {
        [Header("Driving Behavior")]
        public float maxSpeed = 15f;
        public float acceleration = 5f;
        public float brakingPower = 10f;
        public float turnSpeed = 5f;
        public float stoppingDistance = 2.5f;

        [Header("Raycast Suspension")]
        public Transform[] wheels;
        public float suspensionRestLength = 0.5f;
        public float wheelRadius = 0.35f;
        public float springStrength = 30000f;
        public float springDamper = 3000f;
        public LayerMask groundMask;
        
        // We'll store the actual MonoBehaviour waypoints here so the Main Thread
        // can traverse the graph and feed 'Vector3' positions to the Job System.
        public AIWaypoint[] lookaheadWaypoints = new AIWaypoint[TrafficManager.WAYPOINT_LOOKAHEAD];
        
        // Track our current target index inside the lookahead buffer (0 to 4)
        public int activeWaypointIndex = 0;
        
        // The index assigned to this vehicle in the NativeArrays by the TrafficManager
        [HideInInspector] 
        public int arrayIndex = -1; 
        
        [HideInInspector] 
        public Rigidbody rb;

        private void Awake ()
        {
            rb = GetComponent<Rigidbody>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (wheels == null) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;

                Vector3 origin = wheels[i].position;
                float totalRayLength = suspensionRestLength + wheelRadius;

                // Ray line
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin - transform.up * totalRayLength);

                // Wheel radius visualization at the lowest point
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(origin - transform.up * suspensionRestLength, wheelRadius);

                // Hit point preview (optional)
                if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, totalRayLength, groundMask))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(hit.point, 0.05f);
                }
            }
        }
#endif
    }
}