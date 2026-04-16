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
    [RequireComponent(typeof(BoxCollider))]
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
        public bool showDebugStats = false;
        public LiveDebugData debugData;

        //Helpers
        [HideInInspector] public float laneChangeCooldownTimer;
        [HideInInspector] public bool isChangingLanes;

        // Crash State
        [HideInInspector] public float crashSleepTimer = 0f;

        [Header("Spawning Clearance")]
        [Tooltip("Extra space added around the collider to ensure safe spawning distance.")]
        public float spawnPadding = 2f;

        // Event timers
        private float _hornTimer = 0f;
        private int _turnSignalState = 0; // 0=Off, -1=Left, 1=Right

        [Header("Fake Physics")]
        public Transform bodyTransform;
        [Range(0f, 10f)] public float tiltAmount = 1.5f;    // strength
        [Range(1f, 20f)] public float smooth = 5f;          // smoothing
        [Range(0f, 45f)] public float maxTiltAngle = 2f;   // maximum degrees of tilt

        private float currentTilt;
        private float _previousForwardSpeed;

        // Visibility Caching
        private Camera _mainCamera;
        private bool _isVisible = true;
        private float _nextVisibilityCheckTime = 0f;


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

            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (gameObject.activeSelf && IsVisibleToCamera())
            {
                UpdateWheelVisuals();
                UpdateFakePhysics();
            }


            if (_hornTimer > 0)
            {
                _hornTimer -= Time.deltaTime;
            }

            if (crashSleepTimer > 0f)
            {
                crashSleepTimer -= Time.deltaTime;
            }
        }

        private void UpdateFakePhysics()
        {
            // Forward velocity (local space)
            float forwardSpeed = transform.InverseTransformDirection(rb.linearVelocity).z;

            // Calculate acceleration (change in speed over time)
            float acceleration = Time.deltaTime > 0f ? (forwardSpeed - _previousForwardSpeed) / Time.deltaTime : 0f;
            _previousForwardSpeed = forwardSpeed;

            // Target tilt (negative = tilt backward on accel, positive = tilt forward on brake)
            float targetTilt = -acceleration * tiltAmount;

            // Clamp the target tilt to prevent extreme spikes (especially on the first frame of spawning)
            targetTilt = Mathf.Clamp(targetTilt, -maxTiltAngle, maxTiltAngle);

            // Smooth (Use Time.deltaTime in Update, not fixedDeltaTime)
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * smooth);

            // Apply (X axis = forward/back tilt)
            bodyTransform.localRotation = Quaternion.Euler(currentTilt, 0f, 0f);
        }

        private bool IsVisibleToCamera()
        {
            // Throttle the heavy frustum math to only run 5 times a second per vehicle
            if (Time.time < _nextVisibilityCheckTime)
            {
                return _isVisible;
            }

            // Offset the next check slightly so not all cars check on the exact same frame
            _nextVisibilityCheckTime = Time.time + 0.2f + UnityEngine.Random.Range(0f, 0.05f);

            if (_mainCamera == null) return true;

            Bounds bounds = new Bounds(transform.position, GetSpawnBoxHalfExtents() * 2f);
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_mainCamera);

            _isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
            return _isVisible;
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
            crashSleepTimer = 0f;

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

        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log("Collided with relative velocity: " + collision.relativeVelocity.magnitude);

            // Combine both layer masks into a single bitmask
            int targetMasks = DMTS_API.TrafficLayerMask.value | DMTS_API.PlayerLayerMask.value;

            // Check if the collided object's layer is contained within our target masks
            if (((1 << collision.gameObject.layer) & targetMasks) != 0)
            {
                    // Put the car to sleep for 8 seconds
                    crashSleepTimer = 8f;
            }
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
    }
}
