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

        [HideInInspector] public Vector3 localPosition; // Cached during Awake
    }

    /// <summary>
    /// Attached to the dummy car GameObjects. Registers with the TrafficManager.
    /// This keeps track of the MonoBehaviour Waypoints for the Main Thread to trace the graph.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AIVehicle : MonoBehaviour
    {

        public VehicleType vehicleType = VehicleType.Car;

        public DriverBehavior driverBehaviour = new DriverBehavior
        {
            engineMaxSpeed = 15f,
            speedMultiplierRange = new Vector2(0.85f, 1.15f),
            acceleration = 5f,
            brakingPower = 10f,
            turnSpeed = 5f,
            stoppingDistance = 2.5f,
            willChangeLane = true,
            frustrationTime = new Vector2(5f, 15f),
            laneChangeCooldown = new Vector2(5f, 10f),
            aiOvertakeProbability = 0.5f,
        };

        [Header("Raycast Suspension")]
        public SuspensionWheel[] wheels;
        public float springStrength = 30000f;
        public float springDamper = 3000f;


        [Header("Sensors")]
        [Tooltip("If true, the front sensor rotates to face the next waypoint")]
        public bool sensorFacesWaypoint = true;
        public Transform frontSensor;
        public Transform leftSensor;
        public Transform rightSensor;

        // We'll store the actual MonoBehaviour waypoints here so the Main Thread
        // can traverse the graph and feed 'Vector3' positions to the Job System.
        [HideInInspector] public AIWaypoint[] lookaheadWaypoints = new AIWaypoint[TrafficManager.WAYPOINT_LOOKAHEAD];

        // Track our current target index inside the lookahead buffer (0 to 4)
        [HideInInspector] public int activeWaypointIndex = 0;

        // The index assigned to this vehicle in the NativeArrays by the TrafficManager
        [HideInInspector] public int arrayIndex = -1;

        [HideInInspector] public Rigidbody rb;

        [HideInInspector] public BoxCollider vehicleCollider;

        [HideInInspector] public float steeringAngle;

        [HideInInspector] public bool isGrounded;

        // Internal logic
        [Header("Job System Debug Sync")]
        public LiveDebugData debugData;

        //Helpers
        [HideInInspector] public float laneChangeCooldownTimer;
        [HideInInspector] public bool isChangingLanes;

        [Header("Spawning Clearance")]
        [Tooltip("Extra space added around the collider to ensure safe spawning distance.")]
        public float spawnPadding = 2f;
        
        // Event timers
        private float _hornTimer = 0f;
        private int _turnSignalState = 0; // 0=Off, -1=Left, 1=Right

        public Vector3 GetSpawnBoxHalfExtents()
        {
            if (vehicleCollider != null)
            {
                return (vehicleCollider.size / 2f) + new Vector3(spawnPadding, 0f, spawnPadding);
            }
            return new Vector3(1f, 1f, 2.5f);
        }

        public Vector3 GetSpawnBoxCenterOffset()
        {
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

            if (wheels != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null && wheels[i].raycastTransform != null)
                    {
                        // Cache the local position relative to the main vehicle transform
                        wheels[i].localPosition = transform.InverseTransformPoint(wheels[i].raycastTransform.position);
                    }
                }
            }
        }

        private void Update()
        {
            if (gameObject.activeSelf && IsVisibleToCamera())
                UpdateWheelVisuals();
                
            if (_hornTimer > 0)
            {
                _hornTimer -= Time.deltaTime;
            }
        }

        private bool IsVisibleToCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return true;

            Bounds bounds = new Bounds(transform.position, GetSpawnBoxHalfExtents() * 2f);
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);

            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        public void ResetRuntimeState()
        {
            if (lookaheadWaypoints != null)
            {
                Array.Clear(lookaheadWaypoints, 0, lookaheadWaypoints.Length);
            }

            activeWaypointIndex = 0;
            arrayIndex = -1;
            steeringAngle = 0f;
            isGrounded = false;
            debugData = default;
            laneChangeCooldownTimer = 0f;
            isChangingLanes = false;
            _hornTimer = 0f;
            _turnSignalState = 0;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.None;
            }
        }

        public void HonkHorn()
        {
            // Prevent spamming the horn every frame
            if (_hornTimer > 0f) return;
            _hornTimer = 2f; // Cooldown
            
            Debug.Log($"Vehicle {gameObject.name} is HONKING and FLASHING LIGHTS!");
            // TODO: Play AudioSource clip here
            // TODO: Enable Headlight GameObject here, and use a Coroutine to turn it off after 0.5s
        }
        
        public void SetBrakeLights(bool active)
        {
            // TODO: Toggle red brake light materials/GameObjects
        }
        
        public void SetTurnSignals(int direction)
        {
            _turnSignalState = direction;
            // direction == -1 (Left), 1 (Right), 0 (Off)
            // TODO: Start a Coroutine that blinks the respective yellow light GameObjects
            if (direction == 0) Debug.Log($"Vehicle {gameObject.name} turned signals OFF");
            else Debug.Log($"Vehicle {gameObject.name} turned {(direction == -1 ? "LEFT" : "RIGHT")} signal ON");
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

        public void ApplySuspensionFast(RaycastHit hit, Vector3 origin, Vector3 springDir, float restLength, float radius)
        {
            // The hit distance check is moved to TrafficManager so we only call this if grounded

            // Get velocity at the wheel's world position
            Vector3 wheelWorldVel = rb.GetPointVelocity(origin);

            // Calculate spring compression relative velocity
            float relVel = Vector3.Dot(springDir, wheelWorldVel);
            float offset = restLength - (hit.distance - radius);

            // Hooke's Law
            float suspensionForce = (offset * springStrength) - (relVel * springDamper);

            // Apply the force
            rb.AddForceAtPosition(springDir * suspensionForce, origin);
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
                Quaternion frontSensorRot = transform.rotation;
                if (sensorFacesWaypoint && lookaheadWaypoints != null && activeWaypointIndex >= 0 && activeWaypointIndex < lookaheadWaypoints.Length)
                {
                    AIWaypoint targetWP = lookaheadWaypoints[activeWaypointIndex];
                    if (targetWP != null)
                    {
                        Vector3 dir = targetWP.transform.position - transform.position;
                        dir.y = 0;
                        if (dir.sqrMagnitude > 0.001f)
                        {
                            frontSensorRot = Quaternion.LookRotation(dir.normalized, transform.rotation * Vector3.up);
                        }
                    }
                }

                Matrix4x4 currentMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, frontSensorRot, Vector3.one);
                Gizmos.color = new Color(1.0f, 0.6f, 0.0f, 0.25f); // Orange semi-transparent fill
                Gizmos.DrawCube(frontSensor.localPosition, frontSensor.localScale);
                Gizmos.matrix = currentMatrix;
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
                LayerMask mask = FindAnyObjectByType<TrafficManager>() != null ? FindAnyObjectByType<TrafficManager>().groundMask : LayerMask.GetMask("Default");
                if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, totalRayLength, mask))
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

                Quaternion sensorRotation = transform.rotation;

                if (sensorFacesWaypoint && lookaheadWaypoints != null && activeWaypointIndex >= 0 && activeWaypointIndex < lookaheadWaypoints.Length)
                {
                    AIWaypoint targetWP = lookaheadWaypoints[activeWaypointIndex];
                    if (targetWP != null)
                    {
                        Vector3 dir = targetWP.transform.position - transform.position;
                        dir.y = 0;
                        if (dir.sqrMagnitude > 0.001f)
                        {
                            sensorRotation = Quaternion.LookRotation(dir.normalized, transform.rotation * Vector3.up);
                        }
                    }
                }

                // The Job anchors the origin to the vehicle's actual rotation, NOT the sensor look rotation
                Vector3 worldOrigin = transform.position + transform.rotation * frontSensor.localPosition;

                // Now rotate around that precise worldOrigin based on the sensor's look rotation
                Gizmos.matrix = Matrix4x4.TRS(worldOrigin, sensorRotation, Vector3.one);

                // Normal Sensor Box (origin is now zero because we pushed worldOrigin into the matrix)
                Gizmos.color = new Color(1.0f, 0.5f, 0.0f, 0.3f); // Semi-Transparent Orange
                Gizmos.DrawCube(Vector3.zero, frontSensor.localScale);
                Gizmos.color = new Color(1.0f, 0.5f, 0.0f, 0.8f);
                Gizmos.DrawWireCube(Vector3.zero, frontSensor.localScale);

                // Extended Sensor Box (5m ahead of normal sensor)
                Vector3 extendedSize = new Vector3(frontSensor.localScale.x, frontSensor.localScale.y, 5f);
                Vector3 extendedCenterPath = new Vector3(0, 0, frontSensor.localScale.z * 0.5f + extendedSize.z * 0.5f);

                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f); // Semi-Transparent Light Blue
                Gizmos.DrawCube(extendedCenterPath, extendedSize);
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireCube(extendedCenterPath, extendedSize);

                // Stopping Distance Box (Red)
                // The BoxCast starts sweeping from the BACK of the normal sensor, so the gizmo must start there too
                float zStart = -frontSensor.localScale.z * 0.5f;
                Vector3 stoppingSize = new Vector3(frontSensor.localScale.x, frontSensor.localScale.y, driverBehaviour.stoppingDistance);
                Vector3 stoppingCenter = new Vector3(0, 0, zStart + driverBehaviour.stoppingDistance * 0.5f);
                
                Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.3f); // Semi-Transparent Red
                Gizmos.DrawCube(stoppingCenter, stoppingSize);
                Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.9f);
                Gizmos.DrawWireCube(stoppingCenter, stoppingSize);

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

            // Ensure UnityEditor is available before calling Handles
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            // Draw a dark background box for readability
            GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
            bgStyle.normal.background = UnityEditor.EditorGUIUtility.whiteTexture;

            string debugText = 
                $"Engine Max: {debugData.engineMaxSpeed:F1}\n" +
                $"Local Max: {debugData.localMaxSpeed:F1}\n" +
                $"Current Spd: {debugData.currentSpeed:F1}\n" +
                $"Changing Lanes: {debugData.isChangingLanes}\n" +
                $"Wants To Overtake: {debugData.wantsToChangeLane}\n" +
                $"Left Blocked: {debugData.leftLaneBlocked}\n" +
                $"Right Blocked: {debugData.rightLaneBlocked}\n" +
                $"Impatience: {debugData.impatienceTimer:F1} / {debugData.frustrationTime:F1}\n" +
                $"Lane Cooldown: {debugData.laneChangeCooldownTimer:F1}";

            Vector3 labelPosition = transform.position + Vector3.up * 4.5f;

            // Create a small dark box behind text
            UnityEditor.Handles.BeginGUI();
            Vector2 screenPos = UnityEditor.HandleUtility.WorldToGUIPoint(labelPosition);
            
            // Note: This relies on the Scene view camera viewing it, it sets a dark box
            GUI.color = new Color(0, 0, 0, 0.7f); 
            Vector2 size = labelStyle.CalcSize(new GUIContent(debugText));
            GUI.Box(new Rect(screenPos.x - (size.x/2f) - 10, screenPos.y - (size.y/2f) - 10, size.x + 20, size.y + 20), "", bgStyle);
            GUI.color = Color.white;
            UnityEditor.Handles.EndGUI();

            // Draw the actual text
            UnityEditor.Handles.Label(labelPosition, debugText, labelStyle);
        }
#endif
    }
}
