using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public class SuspensionWheel
    {
        public Transform wheelTransform;
        public Transform visualMesh; // The actual wheel mesh to rotate and steer
        public bool isFrontWheel;
        public float radius = 0.35f;
        public float restLength = 0.5f;
        
        [HideInInspector]
        public float currentRotation = 0f; // Track cumulative rotation for rolling
    }

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
        public SuspensionWheel[] wheels;
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

        private void FixedUpdate()
        {
            ApplySuspension();
        }

        private void Update()
        {
            UpdateWheelVisuals();
        }

        private void UpdateWheelVisuals()
        {
            if (wheels == null || wheels.Length == 0) return;

            // Calculate steering angle based on angular velocity and forward speed (Ackermann approximation)
            float localSpeed = transform.InverseTransformDirection(rb.linearVelocity).z;
            float localTurnVelocity = transform.InverseTransformDirection(rb.angularVelocity).y; // rad/s
            float wheelbase = 2.5f; // Estimated wheelbase
            float steerAngle = 0f;

            if (Mathf.Abs(localSpeed) > 0.1f)
            {
                steerAngle = Mathf.Atan2(wheelbase * localTurnVelocity, localSpeed) * Mathf.Rad2Deg;
            }
            else
            {
                steerAngle = Mathf.Clamp(localTurnVelocity * 10f * Mathf.Rad2Deg, -45f, 45f); // Approximation for stationary steering
            }
            steerAngle = Mathf.Clamp(steerAngle, -45f, 45f);

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].visualMesh == null) continue;

                // 1. Rotate the wheel according to speed
                float wheelCircumference = 2f * Mathf.PI * wheels[i].radius;
                float rotationDelta = (localSpeed / wheelCircumference) * 360f * Time.deltaTime;
                wheels[i].currentRotation += rotationDelta;

                // 2. Turn the front two wheels according to the steer angle
                float currentSteer = wheels[i].isFrontWheel ? steerAngle : 0f;

                // Combine the continuous rolling with the steering angle
                // Note: The X axis is typically the roll/pitch axis for rolling wheels,
                // and the Y axis is the yaw for steering.
                wheels[i].visualMesh.localRotation = Quaternion.Euler(wheels[i].currentRotation, currentSteer, 0f);
            }
        }

        private void ApplySuspension()
        {
            if (wheels == null || wheels.Length == 0) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].wheelTransform == null) continue;

                Vector3 origin = wheels[i].wheelTransform.position;
                float currentRestLength = wheels[i].restLength;
                float currentRadius = wheels[i].radius;
                float rayLength = currentRestLength + currentRadius;

                if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, rayLength, groundMask))
                {
                    // Calculate spring force
                    Vector3 springDir = transform.up;
                    
                    Vector3 wheelWorldVel = rb.GetPointVelocity(origin);
                    float relVel = Vector3.Dot(springDir, wheelWorldVel);
                    
                    float offset = currentRestLength - (hit.distance - currentRadius);
                    
                    float suspensionForce = (offset * springStrength) - (relVel * springDamper);
                    
                    rb.AddForceAtPosition(springDir * suspensionForce, origin);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (wheels == null) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].wheelTransform == null) continue;

                Vector3 origin = wheels[i].wheelTransform.position;
                float currentRestLength = wheels[i].restLength;
                float currentRadius = wheels[i].radius;
                float totalRayLength = currentRestLength + currentRadius;

                // Ray line
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin - transform.up * totalRayLength);

                // Wheel radius visualization at the lowest point
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(origin - transform.up * currentRestLength, currentRadius);

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