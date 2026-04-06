using System;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public class SuspensionWheel
    {
        public Transform raycastTransform;
        public Transform visualMesh; // The actual wheel mesh to rotate and steer
        public bool isFrontWheel;
        public float radius = 0.35f;
        public float restLength = 0.5f;
    }

    /// <summary>
    /// Attached to the dummy car GameObjects. Registers with the TrafficManager.
    /// This keeps track of the MonoBehaviour Waypoints for the Main Thread to trace the graph.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AIVehicle : MonoBehaviour
    {

        public VehicleType vehicleType = VehicleType.Car;
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


        [Header("Front Sensor")]
        public Transform frontSensor;

        [Header("Side Sensors")]
        public Transform leftSensor;
        public Transform rightSensor;

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
        
        [HideInInspector]
        public BoxCollider vehicleCollider;
        
        [HideInInspector]
        public float steeringAngle;

        public bool isGrounded;

        [Header("Spawning Clearance")]
        [Tooltip("Extra space added around the collider to ensure safe spawning distance.")]
        public float spawnPadding = 2f;

        public Vector3 GetSpawnBoxHalfExtents()
        {
            if (vehicleCollider == null) vehicleCollider = GetComponent<BoxCollider>();
            
            if (vehicleCollider != null)
            {
                return (vehicleCollider.size / 2f) + new Vector3(spawnPadding, 0f, spawnPadding);
            }
            return new Vector3(1f, 1f, 2.5f);
        }

        public Vector3 GetSpawnBoxCenterOffset()
        {
            if (vehicleCollider == null) vehicleCollider = GetComponent<BoxCollider>();
            
            if (vehicleCollider != null)
            {
                return vehicleCollider.center;
            }
            return new Vector3(0f, 1f, 0f);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            vehicleCollider = GetComponent<BoxCollider>();
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
            float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
            float steerAngle = Mathf.Clamp(steeringAngle, -45f, 45f);
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].visualMesh == null) continue;

                // Rotate the wheel based on forward speed
                float rotationAmount = (forwardSpeed / (2 * Mathf.PI * wheels[i].radius)) * 360f * Time.deltaTime;
                wheels[i].visualMesh.Rotate(Vector3.right, rotationAmount, Space.Self);

                if (wheels[i].isFrontWheel && wheels[i].raycastTransform != null)
                {
                    // Steer the front wheels
                    Vector3 currentEuler = wheels[i].raycastTransform.localEulerAngles;
                    wheels[i].raycastTransform.localEulerAngles = new Vector3(currentEuler.x, steerAngle, currentEuler.z);
                }
            }
        }

        private void ApplySuspension()
        {
            isGrounded = false;
            if (wheels == null || wheels.Length == 0) return;

            int groundedCount = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].raycastTransform == null) continue;

                Vector3 origin = wheels[i].raycastTransform.position;
                float currentRestLength = wheels[i].restLength;
                float currentRadius = wheels[i].radius;
                float rayLength = currentRestLength + currentRadius;

                if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, rayLength, groundMask))
                {
                    groundedCount++;

                    // Calculate spring force
                    Vector3 springDir = transform.up;

                    Vector3 wheelWorldVel = rb.GetPointVelocity(origin);
                    float relVel = Vector3.Dot(springDir, wheelWorldVel);

                    float offset = currentRestLength - (hit.distance - currentRadius);

                    float suspensionForce = (offset * springStrength) - (relVel * springDamper);

                    rb.AddForceAtPosition(springDir * suspensionForce, origin);
                }
            }

            isGrounded = groundedCount > 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw the Spawning Box based on BoxCollider
            Vector3 halfExtents = GetSpawnBoxHalfExtents();
            Vector3 centerOffset = GetSpawnBoxCenterOffset();

            // Draw the Spawning Box
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f); // Cyan outline
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            
            // DrawWireCube takes FULL size, so multiply half extents by 2
            Gizmos.DrawWireCube(centerOffset, halfExtents * 2f);
            
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f); // Cyan semi-transparent fill
            Gizmos.DrawCube(centerOffset, halfExtents * 2f);

            // Draw filled cubes for the sensors
            if (frontSensor != null)
            {
                Gizmos.color = new Color(1.0f, 0.6f, 0.0f, 0.25f); // Orange semi-transparent fill
                Gizmos.DrawCube(frontSensor.localPosition, frontSensor.localScale);
            }
            if (leftSensor != null)
            {
                Gizmos.color = new Color(1.0f, 0.0f, 1.0f, 0.25f); // Magenta semi-transparent fill
                Gizmos.DrawCube(leftSensor.localPosition, leftSensor.localScale);
            }
            if (rightSensor != null)
            {
                Gizmos.color = new Color(1.0f, 0.0f, 1.0f, 0.25f); // Magenta semi-transparent fill
                Gizmos.DrawCube(rightSensor.localPosition, rightSensor.localScale);
            }

            Gizmos.matrix = oldMatrix;

            if (wheels == null) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].raycastTransform == null) continue;

                Vector3 origin = wheels[i].raycastTransform.position;
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

            // Visualize vehicle's arrival distance trigger sphere
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 4.0f);

            // Visualize Front Sensor
            if (frontSensor != null)
            {
                Matrix4x4 oldGizmoMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

                // Normal Sensor Box
                Gizmos.color = new Color(1.0f, 0.5f, 0.0f, 0.3f); // Semi-Transparent Orange
                Gizmos.DrawCube(frontSensor.localPosition, frontSensor.localScale);
                Gizmos.color = new Color(1.0f, 0.5f, 0.0f, 0.8f);
                Gizmos.DrawWireCube(frontSensor.localPosition, frontSensor.localScale);

                // Extended Sensor Box (5m ahead of normal sensor)
                Vector3 extendedSize = new Vector3(frontSensor.localScale.x, frontSensor.localScale.y, 5f);
                Vector3 extendedCenter = frontSensor.localPosition + new Vector3(0, 0, frontSensor.localScale.z * 0.5f + extendedSize.z * 0.5f);

                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f); // Semi-Transparent Light Blue
                Gizmos.DrawCube(extendedCenter, extendedSize);
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireCube(extendedCenter, extendedSize);

                Gizmos.matrix = oldGizmoMatrix;
            }

            // Visualize lookahead waypoints
            if (lookaheadWaypoints != null)
            {
                for (int i = 0; i < lookaheadWaypoints.Length; i++)
                {
                    if (lookaheadWaypoints[i] == null) continue;

                    Gizmos.color = i == activeWaypointIndex ? Color.green : Color.cyan;
                    Gizmos.DrawWireSphere(lookaheadWaypoints[i].transform.position, 0.75f);

                    if (i > 0 && lookaheadWaypoints[i - 1] != null)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(lookaheadWaypoints[i - 1].transform.position, lookaheadWaypoints[i].transform.position);
                    }
                    else if (i == 0)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawLine(transform.position, lookaheadWaypoints[0].transform.position);
                    }
                }
            }
        }
#endif
    }
}