using System;
using System.Collections;
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
            stoppingDistance = 5f,
            willChangeLane = true,
            frustrationTime = new Vector2(5f, 15f),
            laneChangeCooldown = new Vector2(5f, 10f),
            aiOvertakeProbability = 0.5f,
        };

        [Header("Raycast Suspension")]
        public SuspensionWheel[] wheels;
        public float springStrength = 30000f;
        public float springDamper = 3000f;

        [Header("Lights")]
        [Tooltip("Assign the mesh renderer used for brake lights. Ensure it uses a shared material for batching.")]
        public Renderer brakeLightRenderer;
        public Renderer leftTurnSignalRenderer;
        public Renderer rightTurnSignalRenderer;
        public Renderer headlightRenderer;

        [Header("Sensors")]
        [Tooltip("If true, the front sensor rotates to face the next waypoint")]
        public bool sensorFacesWaypoint = true;
        public Transform frontSensor;
        public Transform leftSensor;
        public Transform rightSensor;
        public const float ExtendedSensorLength = 2f;

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
        private Coroutine _turnSignalCoroutine;
        private Coroutine _flashCoroutine;

        [Header("Fake Physics")]
        public Transform bodyTransform;
        [Range(0f, 10f)] public float tiltAmount = 5f;    // strength
        [Range(1f, 10f)] public float smooth = 5f;          // smoothing
        [Range(0f, 10f)] public float maxTiltAngle = 5f;   // maximum degrees of tilt

        private float currentForwardTilt;
        private float currentSideTilt;
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
        private void OnValidate()
        {
            CacheRequiredComponents();
        }

        private void CacheRequiredComponents()
        {
            rb = GetComponent<Rigidbody>();
            vehicleCollider = GetComponent<BoxCollider>();
        }

        private void Awake()
        {
            CacheRequiredComponents();
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

            // Target Forward tilt (negative = tilt backward on accel, positive = tilt forward on brake)
            float targetForwardTilt = -acceleration * tiltAmount;
            targetForwardTilt = Mathf.Clamp(targetForwardTilt, -maxTiltAngle, maxTiltAngle);
            currentForwardTilt = Mathf.Lerp(currentForwardTilt, targetForwardTilt, Time.deltaTime * smooth);

            // Target Side tilt (negative = turning left, positive = turning right)
            float targetSideTilt = steeringAngle * forwardSpeed * tiltAmount*0.002f;
            targetSideTilt = Mathf.Clamp(targetSideTilt, -maxTiltAngle, maxTiltAngle);
            currentSideTilt = Mathf.Lerp(currentSideTilt, targetSideTilt, Time.deltaTime * smooth);

            // Apply (X axis = forward/back tilt)
            bodyTransform.localRotation = Quaternion.Euler(currentForwardTilt, 0f, currentSideTilt);
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
            
            if (_turnSignalCoroutine != null && gameObject.activeInHierarchy)
            {
                StopCoroutine(_turnSignalCoroutine);
            }
            _turnSignalCoroutine = null;
            
            if (_flashCoroutine != null && gameObject.activeInHierarchy)
            {
                StopCoroutine(_flashCoroutine);
            }
            _flashCoroutine = null;
            _turnSignalState = 0;
            if (leftTurnSignalRenderer != null) leftTurnSignalRenderer.enabled = false;
            if (rightTurnSignalRenderer != null) rightTurnSignalRenderer.enabled = false;
            if(headlightRenderer != null) headlightRenderer.enabled = false;
            
            crashSleepTimer = 0f;
            SetBrakeLights(false);

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

            // TODO: Play AudioSource clip here
            
            if (gameObject.activeInHierarchy && headlightRenderer != null)
            {
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                _flashCoroutine = StartCoroutine(FlashHeadlightsRoutine());
            }
        }

        private IEnumerator FlashHeadlightsRoutine()
        {
            bool originalState = headlightRenderer.enabled;

            // Flash the lights rapidly 3 times
            for (int i = 0; i < 3; i++)
            {
                headlightRenderer.enabled = !originalState;
                yield return new WaitForSeconds(0.15f);
                headlightRenderer.enabled = originalState;
                yield return new WaitForSeconds(0.15f);
            }
        }

        public void SetBrakeLights(bool active)
        {
            if (brakeLightRenderer != null)
            {
                brakeLightRenderer.enabled = active;
            }
        }

        public void SetTurnSignals(int direction)
        {
            if (_turnSignalState == direction) return;
            _turnSignalState = direction;
            
            if (_turnSignalCoroutine != null)
            {
                StopCoroutine(_turnSignalCoroutine);
            }
            
            // Ensure both are off initially when switching or stopping
            if (leftTurnSignalRenderer != null) leftTurnSignalRenderer.enabled = false;
            if (rightTurnSignalRenderer != null) rightTurnSignalRenderer.enabled = false;

            if (_turnSignalState != 0 && gameObject.activeInHierarchy)
            {
                _turnSignalCoroutine = StartCoroutine(BlinkTurnSignals());
            }
        }

        private IEnumerator BlinkTurnSignals()
        {
            bool isOn = false;
            while (_turnSignalState != 0)
            {
                isOn = !isOn;
                if (_turnSignalState == -1 && leftTurnSignalRenderer != null)
                {
                    leftTurnSignalRenderer.enabled = isOn;
                }
                else if (_turnSignalState == 1 && rightTurnSignalRenderer != null)
                {
                    rightTurnSignalRenderer.enabled = isOn;
                }
                yield return new WaitForSeconds(0.5f); // Blinker interval
            }
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
#if UNITY_EDITOR
        private static readonly Color FrontSensorDebugColor = new Color(1f, 0.85f, 0f, 1f);
        private static readonly Color SideSensorDebugColor = new Color(0.78f, 0.35f, 1f, 1f);
        private static readonly Color ExtendedSensorDebugColor = new Color(0.35f, 0.9f, 1f, 1f);
        private static readonly Color StoppingDistanceDebugColor = new Color(1f, 0.25f, 0.25f, 1f);
        private const float RuntimeDashedLineScreenSize = 4f;
        private const float RuntimeSolidSensorFillAlpha = 0.05f;
        private const float RuntimeDashedSensorFillAlpha = 0.035f;

        private void OnDrawGizmosSelected()
        {
            DrawRuntimeSensorDebug();
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

            if (showDebugStats)
            {
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
                    $"Target Offset: {debugData.currentTargetIndexOffset}\n" +
                    $"Local Max: {debugData.localMaxSpeed:F1}\n" +
                    $"Current Spd: {debugData.currentSpeed:F1}\n" +
                    $"Changing Lanes: {debugData.isChangingLanes}\n" +
                    $"Wants To Overtake: {debugData.wantsToChangeLane}\n" +
                    $"Left Blocked: {debugData.leftLaneBlocked}\n" +
                    $"Right Blocked: {debugData.rightLaneBlocked}\n" +
                    $"Impatience: {debugData.impatienceTimer:F1} / {debugData.frustrationTime:F1}\n" +
                    $"Lane Cooldown: {debugData.laneChangeCooldownTimer:F1}";

                Vector3 labelPosition = transform.position + Vector3.up * 10f;

                // Create a small dark box behind text
                UnityEditor.Handles.BeginGUI();
                Vector2 screenPos = UnityEditor.HandleUtility.WorldToGUIPoint(labelPosition);

                // Note: This relies on the Scene view camera viewing it, it sets a dark box
                GUI.color = new Color(0, 0, 0, 0.7f);
                Vector2 size = labelStyle.CalcSize(new GUIContent(debugText));
                GUI.Box(new Rect(screenPos.x - (size.x / 2f) - 10, screenPos.y - (size.y / 2f) - 10, size.x + 20, size.y + 20), "", bgStyle);
                GUI.color = Color.white;
                UnityEditor.Handles.EndGUI();

                // Draw the actual text
                UnityEditor.Handles.Label(labelPosition, debugText, labelStyle);
            }
        }

        private void DrawRuntimeSensorDebug()
        {
            DrawRuntimeFrontSensorDebug();
            DrawRuntimeSensorTransform(leftSensor, SideSensorDebugColor);
            DrawRuntimeSensorTransform(rightSensor, SideSensorDebugColor);
        }

        private void DrawRuntimeSensorTransform(Transform sensorTransform, Color sensorColor)
        {
            if (sensorTransform == null) return;

            Vector3 worldCenter = transform.position + transform.rotation * sensorTransform.localPosition;
            DrawRuntimeSensorOutline(worldCenter, transform.rotation, sensorTransform.localScale, sensorColor);
        }

        private void DrawRuntimeFrontSensorDebug()
        {
            if (frontSensor == null) return;
            if (!TryGetRuntimeFrontSensorPose(out Vector3 origin, out Vector3 direction, out Quaternion boxRotation)) return;

            Vector3 sensorSize = frontSensor.localScale;
            Vector3 frontCenter = origin + direction * (sensorSize.z * 0.5f);
            DrawRuntimeSensorOutline(frontCenter, boxRotation, sensorSize, FrontSensorDebugColor, Vector3.forward);

            Vector3 extendedSize = new Vector3(sensorSize.x, sensorSize.y, ExtendedSensorLength);
            Vector3 extendedCenter = origin + direction * (sensorSize.z + (ExtendedSensorLength * 0.5f));
            DrawRuntimeSensorOutline(extendedCenter, boxRotation, extendedSize, ExtendedSensorDebugColor, Vector3.forward, true);

            float stoppingDistance = driverBehaviour.stoppingDistance;
            if (stoppingDistance <= 0f) return;

            Vector3 stoppingDistanceCenter = origin + direction * (stoppingDistance * 0.5f);
            Vector3 stoppingDistanceSize = new Vector3(sensorSize.x, sensorSize.y, stoppingDistance);
            DrawRuntimeSensorOutline(stoppingDistanceCenter, boxRotation, stoppingDistanceSize, StoppingDistanceDebugColor, Vector3.forward);
        }

        private bool TryGetRuntimeFrontSensorPose(out Vector3 origin, out Vector3 direction, out Quaternion boxRotation)
        {
            origin = Vector3.zero;
            direction = transform.rotation * Vector3.forward;
            boxRotation = transform.rotation;

            if (frontSensor == null)
            {
                return false;
            }

            Vector3 centerOffset = frontSensor.localPosition;
            centerOffset.z -= frontSensor.localScale.z * 0.5f;

            origin = transform.position + transform.rotation * centerOffset;

            if (sensorFacesWaypoint && TryGetCurrentSensorTarget(out AIWaypoint targetWaypoint))
            {
                Vector3 waypointDirection = targetWaypoint.transform.position - transform.position;
                waypointDirection.y = 0f;

                if (waypointDirection.sqrMagnitude > 0.001f)
                {
                    direction = waypointDirection.normalized;
                    boxRotation = Quaternion.LookRotation(direction, transform.rotation * Vector3.up);
                }
            }

            return true;
        }

        private bool TryGetCurrentSensorTarget(out AIWaypoint targetWaypoint)
        {
            targetWaypoint = null;

            if (lookaheadWaypoints == null || lookaheadWaypoints.Length == 0)
            {
                return false;
            }

            int targetIndex = Mathf.Clamp(debugData.currentTargetIndexOffset, 0, lookaheadWaypoints.Length - 1);
            targetWaypoint = lookaheadWaypoints[targetIndex];
            return targetWaypoint != null;
        }

        private static void DrawRuntimeSensorOutline(Vector3 worldCenter, Quaternion rotation, Vector3 size, Color outlineColor)
        {
            DrawRuntimeSensorOutline(worldCenter, rotation, size, outlineColor, GetRuntimeDominantAxis(size));
        }

        private static void DrawRuntimeSensorOutline(Vector3 worldCenter, Quaternion rotation, Vector3 size, Color outlineColor, Vector3 guideAxis, bool dashed = false)
        {
            using (new UnityEditor.Handles.DrawingScope(Matrix4x4.TRS(worldCenter, rotation, Vector3.one)))
            {
                DrawRuntimeLocalSensorOutline(Vector3.zero, size, outlineColor, guideAxis, dashed);
            }
        }

        private static void DrawRuntimeLocalSensorOutline(Vector3 center, Vector3 size, Color outlineColor, Vector3 guideAxis, bool dashed = false)
        {
            DrawRuntimeSensorFill(center, size, outlineColor, dashed ? RuntimeDashedSensorFillAlpha : RuntimeSolidSensorFillAlpha);

            UnityEditor.Handles.color = outlineColor;
            if (dashed)
            {
                DrawRuntimeDashedWireCube(center, size);
            }
            else
            {
                UnityEditor.Handles.DrawWireCube(center, size);
            }

            Vector3 normalizedAxis = guideAxis.normalized;
            Vector3 halfAxis = Vector3.Scale(size, normalizedAxis) * 0.5f;
            if (dashed)
            {
                UnityEditor.Handles.DrawDottedLine(center - halfAxis, center + halfAxis, RuntimeDashedLineScreenSize);
            }
            else
            {
                UnityEditor.Handles.DrawLine(center - halfAxis, center + halfAxis);
            }
        }

        private static void DrawRuntimeSensorFill(Vector3 center, Vector3 size, Color baseColor, float alpha)
        {
            Color fillColor = baseColor;
            fillColor.a = alpha;
            UnityEditor.Handles.color = fillColor;

            if (Event.current == null || Event.current.type == EventType.Repaint)
            {
                Matrix4x4 previousMatrix = UnityEditor.Handles.matrix;
                UnityEditor.Handles.matrix = previousMatrix * Matrix4x4.TRS(center, Quaternion.identity, size);
                UnityEditor.Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
                UnityEditor.Handles.matrix = previousMatrix;
            }
        }

        private static void DrawRuntimeDashedWireCube(Vector3 center, Vector3 size)
        {
            Vector3 extents = size * 0.5f;

            Vector3 c0 = center + new Vector3(-extents.x, -extents.y, -extents.z);
            Vector3 c1 = center + new Vector3(extents.x, -extents.y, -extents.z);
            Vector3 c2 = center + new Vector3(extents.x, extents.y, -extents.z);
            Vector3 c3 = center + new Vector3(-extents.x, extents.y, -extents.z);
            Vector3 c4 = center + new Vector3(-extents.x, -extents.y, extents.z);
            Vector3 c5 = center + new Vector3(extents.x, -extents.y, extents.z);
            Vector3 c6 = center + new Vector3(extents.x, extents.y, extents.z);
            Vector3 c7 = center + new Vector3(-extents.x, extents.y, extents.z);

            DrawRuntimeDashedEdge(c0, c1);
            DrawRuntimeDashedEdge(c1, c2);
            DrawRuntimeDashedEdge(c2, c3);
            DrawRuntimeDashedEdge(c3, c0);

            DrawRuntimeDashedEdge(c4, c5);
            DrawRuntimeDashedEdge(c5, c6);
            DrawRuntimeDashedEdge(c6, c7);
            DrawRuntimeDashedEdge(c7, c4);

            DrawRuntimeDashedEdge(c0, c4);
            DrawRuntimeDashedEdge(c1, c5);
            DrawRuntimeDashedEdge(c2, c6);
            DrawRuntimeDashedEdge(c3, c7);
        }

        private static void DrawRuntimeDashedEdge(Vector3 start, Vector3 end)
        {
            UnityEditor.Handles.DrawDottedLine(start, end, RuntimeDashedLineScreenSize);
        }

        private static Vector3 GetRuntimeDominantAxis(Vector3 size)
        {
            Vector3 absoluteSize = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            if (absoluteSize.x >= absoluteSize.y && absoluteSize.x >= absoluteSize.z) return Vector3.right;
            if (absoluteSize.y >= absoluteSize.x && absoluteSize.y >= absoluteSize.z) return Vector3.up;
            return Vector3.forward;
        }

#endif
    }
}
