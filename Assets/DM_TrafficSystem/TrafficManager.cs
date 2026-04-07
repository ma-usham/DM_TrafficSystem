using UnityEngine;
using UnityEngine.Serialization;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public struct WaypointGridCell
    {
        public Vector2Int cellCoordinate;
        public List<AIWaypoint> waypoints;
    }

    /// <summary>
    /// Stores scene-wide traffic configuration values and manages the Job System memory loops.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public const int WAYPOINT_LOOKAHEAD = 5;

        [FormerlySerializedAs("VehicleCount")]
        [Header("Hard Memory Limits (Do Not Change At Runtime)")]
        [Tooltip("Absolute maximum RAM allocation and Pool size.")]
        [Min(0)]
        public int maxVehicleCountInGame = 50;

        [Header("Density Control (Safe to Change At Runtime)")]
        [Tooltip("Target number of active vehicles. Will be clamped to maxVehicleCountInGame.")]
        public int densityControl = 20;

        [Header("Global Physics Layers")]
        public LayerMask groundMask;
        public LayerMask trafficMask;
        public LayerMask playerMask;

        [Header("Vehicle Collection Setup")]
        public VehicleCollection vehicleCollection;
        public AIWaypoint[] spawnWaypoints; // Assign in inspector to test spawning

        [Header("Player Pooling Setup")]
        public bool usePlayerPooling = false;
        public float innerSpawnRadius = 50f;
        public float outerSpawnRadius = 150f;
        public float despawnRadius = 200f;
        [Tooltip("If unassigned, Camera.main will be used instead.")]
        public Camera mainCamera;

        public Transform playerTransform; // Assign your Player in the Inspector

        [Header("Grid Spawning Setup must be larger than outer spawn radius")]
        public float gridSize = 200f; // 100x100 meter squares

        // Unity will save this list in the editor
        [HideInInspector]
        public List<WaypointGridCell> serializedGrid = new List<WaypointGridCell>();

        // At runtime, we convert the list into this dictionary for fast O(1) lookups
        private Dictionary<Vector2Int, List<AIWaypoint>> _runtimeGrid;

        // Native memory arrays for the Jobs
        private NativeArray<VehicleState> _vehicleStates;
        private NativeArray<Vector3> _waypointBuffer;
        private TransformAccessArray _transformAccessArray;

        // Sensor memory
        private NativeArray<BoxcastCommand> _boxcastCommands;
        private NativeArray<RaycastHit> _raycastHits;

        private NativeArray<BoxcastCommand> _playerBoxcastCommands;
        private NativeArray<RaycastHit> _playerRaycastHits;

        private NativeArray<BoxcastCommand> _leftBoxcastCommands;
        private NativeArray<RaycastHit> _leftRaycastHits;

        private NativeArray<BoxcastCommand> _rightBoxcastCommands;
        private NativeArray<RaycastHit> _rightRaycastHits;

        // Suspension Raycasts
        private NativeArray<RaycastCommand> _wheelRaycastCommands;
        private NativeArray<RaycastHit> _wheelRaycastHits;

        // Wheel Job setup Data
        private NativeArray<int> _wheelCounts;
        private NativeArray<Vector3> _wheelLocalOffsets;
        private NativeArray<float> _wheelRayLengths;

        // Job execution handling
        private JobHandle _finalJobHandle;

        // Helper Classes
        private TrafficWaypointUpdater _trafficWaypointUpdater;

        // Main thread references required to traverse the actual AIWaypoint graph
        private List<AIVehicle> _activeVehicles = new List<AIVehicle>();
        private bool _isInitialized = false;
        private VehiclePool _vehiclePool;

        void Start()
        {
            _trafficWaypointUpdater = new TrafficWaypointUpdater();

            GameObject poolContainer = new GameObject("VehiclePoolContainer");
            poolContainer.transform.SetParent(this.transform);
            _vehiclePool = new VehiclePool(vehicleCollection, poolContainer.transform);

            _vehiclePool.Prepopulate(maxVehicleCountInGame);

            if (usePlayerPooling && mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            InitializeRuntimeGrid();
            InitializeBuffers();
            SpawnInitialVehicles();

            if (usePlayerPooling)
            {
                StartCoroutine(PlayerPoolingRoutine());
            }
        }

        private void InitializeRuntimeGrid()
        {
            _runtimeGrid = new Dictionary<Vector2Int, List<AIWaypoint>>();

            foreach (var cell in serializedGrid)
            {
                _runtimeGrid[cell.cellCoordinate] = cell.waypoints;
            }
        }

        private List<AIWaypoint> GetNearbyWaypoints(Vector3 playerPosition)
        {
            List<AIWaypoint> nearbyWaypoints = new List<AIWaypoint>();

            int centerX = Mathf.FloorToInt(playerPosition.x / gridSize);
            int centerZ = Mathf.FloorToInt(playerPosition.z / gridSize);
            Vector2Int centerCell = new Vector2Int(centerX, centerZ);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector2Int checkCell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                    if (_runtimeGrid.TryGetValue(checkCell, out List<AIWaypoint> cellWaypoints))
                    {
                        nearbyWaypoints.AddRange(cellWaypoints);
                    }
                }
            }
            return nearbyWaypoints;
        }

        /// <summary>
        /// Finds the closest AIWaypoint to a given position using the optimized spatial grid.
        /// </summary>
        public AIWaypoint GetClosestWaypoint(Vector3 position)
        {
            if (!_isInitialized) return null;

            // Use the existing optimized grid lookup
            List<AIWaypoint> localWaypoints = GetNearbyWaypoints(position);
            
            if (localWaypoints == null || localWaypoints.Count == 0) 
                return null;

            AIWaypoint closest = null;
            float closestSqrDist = float.MaxValue;

            for (int i = 0; i < localWaypoints.Count; i++)
            {
                float sqrDist = (localWaypoints[i].transform.position - position).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    closest = localWaypoints[i];
                }
            }

            return closest;
        }

        private void InitializeBuffers()
        {
            _vehicleStates = new NativeArray<VehicleState>(maxVehicleCountInGame, Allocator.Persistent);
            _waypointBuffer = new NativeArray<Vector3>(maxVehicleCountInGame * WAYPOINT_LOOKAHEAD, Allocator.Persistent);
            _transformAccessArray = new TransformAccessArray(maxVehicleCountInGame);

            _boxcastCommands = new NativeArray<BoxcastCommand>(maxVehicleCountInGame, Allocator.Persistent);
            _raycastHits = new NativeArray<RaycastHit>(maxVehicleCountInGame, Allocator.Persistent);

            _playerBoxcastCommands = new NativeArray<BoxcastCommand>(maxVehicleCountInGame, Allocator.Persistent);
            _playerRaycastHits = new NativeArray<RaycastHit>(maxVehicleCountInGame, Allocator.Persistent);

            _leftBoxcastCommands = new NativeArray<BoxcastCommand>(maxVehicleCountInGame, Allocator.Persistent);
            _leftRaycastHits = new NativeArray<RaycastHit>(maxVehicleCountInGame, Allocator.Persistent);

            _rightBoxcastCommands = new NativeArray<BoxcastCommand>(maxVehicleCountInGame, Allocator.Persistent);
            _rightRaycastHits = new NativeArray<RaycastHit>(maxVehicleCountInGame, Allocator.Persistent);

            _wheelRaycastCommands = new NativeArray<RaycastCommand>(maxVehicleCountInGame * 4, Allocator.Persistent);
            _wheelRaycastHits = new NativeArray<RaycastHit>(maxVehicleCountInGame * 4, Allocator.Persistent);

            _wheelCounts = new NativeArray<int>(maxVehicleCountInGame, Allocator.Persistent);
            _wheelLocalOffsets = new NativeArray<Vector3>(maxVehicleCountInGame * 4, Allocator.Persistent);
            _wheelRayLengths = new NativeArray<float>(maxVehicleCountInGame * 4, Allocator.Persistent);

            _isInitialized = true;
        }

        private void SpawnInitialVehicles()
        {
            if (spawnWaypoints == null || spawnWaypoints.Length == 0) return;

            int spawnedCount = 0;
            int startAmount = Mathf.Min(densityControl, maxVehicleCountInGame);

            for (int i = 0; i < startAmount; i++)
            {
                AIWaypoint spawnPoint = null;

                if (usePlayerPooling && mainCamera != null && playerTransform != null)
                {
                    Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
                    List<AIWaypoint> localWaypoints = GetNearbyWaypoints(playerTransform.position);
                    if (localWaypoints.Count > 0)
                    {
                        for (int attempt = 0; attempt < 50; attempt++)
                        {
                            AIWaypoint candidate = localWaypoints[Random.Range(0, localWaypoints.Count)];
                            float dist = Vector3.Distance(candidate.transform.position, playerTransform.position);
                            if (dist >= innerSpawnRadius && dist <= outerSpawnRadius)
                            {
                                if (IsSpawnPointHidden(candidate.transform.position))
                                {
                                    spawnPoint = candidate;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    spawnPoint = spawnWaypoints[Random.Range(0, spawnWaypoints.Length)];
                }

                if (spawnPoint != null)
                {
                    if (SpawnVehicle(spawnPoint))
                    {
                        spawnedCount++;
                    }
                }
            }
        }

        private bool IsSpawnPointHidden(Vector3 pos)
        {
            Camera cam = mainCamera != null ? mainCamera : Camera.main;
            if (cam == null) return false;

            Bounds bounds = new Bounds(pos, new Vector3(3f, 3f, 5f));
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);

            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
            {
                return true; // Outside frustum
            }

            // Inside frustum. Check if occluded by environment
            Vector3 camPos = cam.transform.position;
            Vector3 dir = pos - camPos;
            int mask = groundMask.value; // environment should be on groundMask
            if (Physics.Raycast(camPos, dir.normalized, out RaycastHit hit, dir.magnitude, mask))
            {
                return true; // Occluded
            }

            return false; // Visible
        }

        private bool SpawnVehicle(AIWaypoint spawnPoint)
        {
            if (_activeVehicles.Count >= maxVehicleCountInGame) return false;

            int newIndex = _activeVehicles.Count;

            AIVehicle vehicle = _vehiclePool.Spawn(spawnPoint);
            if (vehicle == null) return false;

            Vector3 halfExtents = vehicle.GetSpawnBoxHalfExtents();
            Vector3 offset = vehicle.GetSpawnBoxCenterOffset();
            Vector3 boxCenter = spawnPoint.transform.position + (spawnPoint.transform.rotation * offset);

            if (vehicle.vehicleCollider != null) vehicle.vehicleCollider.enabled = false;
            int combinedMask = trafficMask.value | playerMask.value;
            if (Physics.CheckBox(boxCenter, halfExtents, spawnPoint.transform.rotation, combinedMask))
            {
                _vehiclePool.Despawn(vehicle);
                if (vehicle.vehicleCollider != null) vehicle.vehicleCollider.enabled = true;
                return false;
            }
            if (vehicle.vehicleCollider != null) vehicle.vehicleCollider.enabled = true;

            vehicle.arrayIndex = newIndex;
            vehicle.lookaheadWaypoints[0] = spawnPoint;

            _activeVehicles.Add(vehicle);
            _transformAccessArray.Add(vehicle.transform);

            bool sensorActive = vehicle.frontSensor != null && vehicle.frontSensor.gameObject.activeInHierarchy;
            bool sideSensorActive = vehicle.leftSensor != null && vehicle.rightSensor != null &&
                                    vehicle.leftSensor.gameObject.activeInHierarchy && vehicle.rightSensor.gameObject.activeInHierarchy;

            float randomFrustration = Random.Range(vehicle.driverBehaviour.frustrationTime.x, vehicle.driverBehaviour.frustrationTime.y);
            float randomCooldown = Random.Range(vehicle.driverBehaviour.laneChangeCooldown.x, vehicle.driverBehaviour.laneChangeCooldown.y);
            float randomMultiplier = Random.Range(vehicle.driverBehaviour.speedMultiplierRange.x, vehicle.driverBehaviour.speedMultiplierRange.y);
            float wpLimit = spawnPoint.settings.speed > 0 ? (spawnPoint.settings.speed * randomMultiplier) : vehicle.driverBehaviour.engineMaxSpeed;

            VehicleState state = new VehicleState
            {
                currentSpeed = 1f,
                physicalSpeed = 0f,
                localMaxSpeed = Mathf.Min(vehicle.driverBehaviour.engineMaxSpeed * randomMultiplier, wpLimit),
                engineMaxSpeed = vehicle.driverBehaviour.engineMaxSpeed * randomMultiplier,
                speedMultiplier = randomMultiplier,
                acceleration = vehicle.driverBehaviour.acceleration,
                brakingPower = vehicle.driverBehaviour.brakingPower,
                turnSpeed = vehicle.driverBehaviour.turnSpeed,
                stoppingDistance = vehicle.driverBehaviour.stoppingDistance,
                waypointBufferStartIndex = newIndex * WAYPOINT_LOOKAHEAD,
                currentTargetIndexOffset = 0,
                reachedCurrentWaypoint = false,
                isApproachingStopPoint = false,
                desiredVelocity = Vector3.zero,
                desiredRotation = vehicle.transform.rotation,
                isSensorActive = sensorActive,
                sensorFacesWaypoint = vehicle.sensorFacesWaypoint,
                sensorSize = sensorActive ? vehicle.frontSensor.localScale : Vector3.zero,
                sensorOffset = sensorActive ? vehicle.frontSensor.localPosition : Vector3.zero,
                isSideSensorActive = sideSensorActive,
                leftSensorSize = sideSensorActive ? vehicle.leftSensor.localScale : Vector3.zero,
                leftSensorOffset = sideSensorActive ? vehicle.leftSensor.localPosition : Vector3.zero,
                rightSensorSize = sideSensorActive ? vehicle.rightSensor.localScale : Vector3.zero,
                rightSensorOffset = sideSensorActive ? vehicle.rightSensor.localPosition : Vector3.zero,
                obstacleMask = trafficMask.value,
                playerMask = playerMask.value,
                trafficDetected = false,
                detectedTrafficFar = false,
                leftLaneBlocked = false,
                rightLaneBlocked = false,
                emergencySideStop = false,
                isLaneChangingVehicle = vehicle.driverBehaviour.willChangeLane,
                frustrationTime = randomFrustration,
                laneChangeCooldown = randomCooldown,
                overtakeProbability = vehicle.driverBehaviour.aiOvertakeProbability,
                playerOvertakeProbability = vehicle.driverBehaviour.playerOvertakeProbability,
                impatienceTimer = randomFrustration,
                wantsToOvertake = false,
                wantsToHonk = false
            };

            // Update state
            _vehicleStates[newIndex] = state;
            WarmupWaypoints(vehicle, state);

            int wCount = vehicle.wheels != null ? Mathf.Min(vehicle.wheels.Length, 4) : 0;
            _wheelCounts[newIndex] = wCount;

            int startWIndex = newIndex * 4;
            for (int w = 0; w < wCount; w++)
            {
                _wheelLocalOffsets[startWIndex + w] = vehicle.wheels[w].localPosition;
                _wheelRayLengths[startWIndex + w] = vehicle.wheels[w].restLength + vehicle.wheels[w].radius;
            }

            return true;
        }

        private void DespawnVehicleAt(int index)
        {
            if (index < 0 || index >= _activeVehicles.Count) return;

            AIVehicle vehicleToRemove = _activeVehicles[index];
            _vehiclePool.Despawn(vehicleToRemove);

            int lastIndex = _activeVehicles.Count - 1;

            if (index != lastIndex)
            {
                // Move the last vehicle to this spot
                _activeVehicles[index] = _activeVehicles[lastIndex];
                _activeVehicles[index].arrayIndex = index;

                // State update
                VehicleState movedState = _vehicleStates[lastIndex];
                int oldStart = movedState.waypointBufferStartIndex;
                int newStart = index * WAYPOINT_LOOKAHEAD;
                movedState.waypointBufferStartIndex = newStart;

                // Copy buffer
                for (int i = 0; i < WAYPOINT_LOOKAHEAD; i++)
                {
                    _waypointBuffer[newStart + i] = _waypointBuffer[oldStart + i];
                }

                _vehicleStates[index] = movedState;

                // Wheel update
                _wheelCounts[index] = _wheelCounts[lastIndex];
                for (int w = 0; w < 4; w++)
                {
                    _wheelLocalOffsets[index * 4 + w] = _wheelLocalOffsets[lastIndex * 4 + w];
                    _wheelRayLengths[index * 4 + w] = _wheelRayLengths[lastIndex * 4 + w];
                }
            }

            _activeVehicles.RemoveAt(lastIndex);

            // Re-create the transform array dropping the removed element effectively by SwapBack!
            _transformAccessArray.RemoveAtSwapBack(index);
        }

        private System.Collections.IEnumerator PlayerPoolingRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(0.5f);
            while (true)
            {
                yield return wait;

                if (playerTransform == null || spawnWaypoints == null || spawnWaypoints.Length == 0)
                    continue;

                Vector3 playerPos = playerTransform.position;

                // 1. Despawn vehicles outside despawn radius
                for (int i = _activeVehicles.Count - 1; i >= 0; i--)
                {
                    float dist = Vector3.Distance(_activeVehicles[i].transform.position, playerPos);
                    if (dist > despawnRadius)
                    {
                        DespawnVehicleAt(i);
                    }
                }

                // 2. Spawn vehicles if we are below capacity
                List<AIWaypoint> localWaypoints = GetNearbyWaypoints(playerPos);
                if (localWaypoints.Count > 0)
                {
                    int spawnAttempts = 0;
                    int safeDensityTarget = Mathf.Min(densityControl, maxVehicleCountInGame);

                    while (_activeVehicles.Count < safeDensityTarget && spawnAttempts < 5)
                    {
                        spawnAttempts++;
                        AIWaypoint cand = localWaypoints[Random.Range(0, localWaypoints.Count)];
                        float dist = Vector3.Distance(cand.transform.position, playerPos);

                        if (dist >= innerSpawnRadius && dist <= outerSpawnRadius)
                        {
                            if (IsSpawnPointHidden(cand.transform.position))
                            {
                                if (SpawnVehicle(cand))
                                {
                                    break; // Spawn max 1 vehicle per coroutine tick to avoid freezing
                                }
                            }
                        }
                    }
                }
            }
        }

        private void WarmupWaypoints(AIVehicle vehicle, VehicleState state)
        {
            for (int i = 0; i < WAYPOINT_LOOKAHEAD - 1; i++)
            {
                AIWaypoint currentObj = vehicle.lookaheadWaypoints[i];
                AIWaypoint nextObj = _trafficWaypointUpdater.GetNextValidWaypoint(vehicle, currentObj);
                if (nextObj != null)
                {
                    vehicle.lookaheadWaypoints[i + 1] = nextObj;
                }
            }

            for (int i = 0; i < WAYPOINT_LOOKAHEAD; i++)
            {
                if (vehicle.lookaheadWaypoints[i] != null)
                {
                    _waypointBuffer[state.waypointBufferStartIndex + i] = vehicle.lookaheadWaypoints[i].transform.position;
                }
            }

            if (vehicle.lookaheadWaypoints[0] != null)
            {
                state.isApproachingStopPoint = vehicle.lookaheadWaypoints[0].settings.isStopPoint;
            }
            _vehicleStates[vehicle.arrayIndex] = state;
        }
        void FixedUpdate()
        {
            if (!_isInitialized) return;

            // 1. Route Management System processes completed waypoints and reads stop points
            _trafficWaypointUpdater.UpdateWaypoint(_activeVehicles, _vehicleStates, _waypointBuffer);
            _trafficWaypointUpdater.UpdateStopWaypoints(_activeVehicles, _vehicleStates);

            // Sync the real physics speed to the Job memory
            for (int i = 0; i < _activeVehicles.Count; i++)
            {
                VehicleState state = _vehicleStates[i];
                if (_activeVehicles[i].rb != null)
                {
                    state.physicalSpeed = Vector3.Dot(_activeVehicles[i].rb.linearVelocity, _activeVehicles[i].transform.forward);
                }
                _vehicleStates[i] = state;
            }

            // --- 2. Schedule Jobs ---

            // Job 1: Build Boxcast Commands
            VehicleSensorJob sensorJob = new VehicleSensorJob
            {
                vehicleStates = _vehicleStates,
                boxcastCommands = _boxcastCommands,
                leftBoxcastCommands = _leftBoxcastCommands,
                rightBoxcastCommands = _rightBoxcastCommands,
                playerBoxcastCommands = _playerBoxcastCommands,
                waypointBuffer = _waypointBuffer
            };
            JobHandle sensorJobHandle = sensorJob.Schedule(_transformAccessArray);

            // Job 2: Process Physics Overlaps
            JobHandle physicsJobHandle = BoxcastCommand.ScheduleBatch(
                _boxcastCommands,
                _raycastHits,
                64,
                sensorJobHandle
            );

            // Process Player Overlaps
            JobHandle playerPhysicsJobHandle = BoxcastCommand.ScheduleBatch(
                _playerBoxcastCommands,
                _playerRaycastHits,
                64,
                sensorJobHandle
            );

            // Schedule Left side
            JobHandle leftPhysicsJobHandle = BoxcastCommand.ScheduleBatch(
                _leftBoxcastCommands,
                _leftRaycastHits,
                64,
                sensorJobHandle
            );

            // Schedule Right side
            JobHandle rightPhysicsJobHandle = BoxcastCommand.ScheduleBatch(
                _rightBoxcastCommands,
                _rightRaycastHits,
                64,
                sensorJobHandle
            );

            // Combine both side jobs and the front job
            JobHandle combinedPhysicsHandle = JobHandle.CombineDependencies(JobHandle.CombineDependencies(physicsJobHandle, playerPhysicsJobHandle), leftPhysicsJobHandle, rightPhysicsJobHandle);

            // Job 2.5: Build Wheel Raycasts using Job System
            BuildWheelRaycastCommandsJob buildWheelJob = new BuildWheelRaycastCommandsJob
            {
                wheelCounts = _wheelCounts,
                wheelLocalOffsets = _wheelLocalOffsets,
                wheelRayLengths = _wheelRayLengths,
                groundMask = groundMask.value,
                wheelRaycastCommands = _wheelRaycastCommands
            };
            JobHandle buildWheelHandle = buildWheelJob.Schedule(_transformAccessArray);

            JobHandle wheelPhysicsHandle = RaycastCommand.ScheduleBatch(
                _wheelRaycastCommands,
                _wheelRaycastHits,
                64,
                buildWheelHandle
            );

            // Combine the handles
            combinedPhysicsHandle = JobHandle.CombineDependencies(combinedPhysicsHandle, wheelPhysicsHandle);

            // Job 3: Movement Simulation
            TrafficSimulationJob simulationJob = new TrafficSimulationJob
            {
                vehicleStates = _vehicleStates,
                waypointBuffer = _waypointBuffer,
                sensorHits = _raycastHits,
                leftSensorHits = _leftRaycastHits,
                rightSensorHits = _rightRaycastHits,
                playerSensorHits = _playerRaycastHits,
                playerForward = playerTransform != null ? playerTransform.forward : Vector3.forward,
                deltaTime = Time.fixedDeltaTime,
                arrivalDistance = 2f,
                timeSinceLevelLoad = Time.timeSinceLevelLoad
            };

            // Final handle allows the Main Thread to wait for all simulation
            _finalJobHandle = simulationJob.Schedule(_transformAccessArray, combinedPhysicsHandle);

            // Wait for everything to complete before applying
            _finalJobHandle.Complete();

            // --- 3. Apply the Calculated Physics Results ---
            // Apply Rigidbody movement using the computed values
            for (int i = 0; i < _activeVehicles.Count; i++)
            {
                AIVehicle vehicle = _activeVehicles[i];
                VehicleState state = _vehicleStates[i];

                // --- SYNC DEBUG DATA TO MAIN THREAD ---
                vehicle.debugData.currentSpeed = state.currentSpeed;
                vehicle.debugData.localMaxSpeed = state.localMaxSpeed;
                vehicle.debugData.engineMaxSpeed = state.engineMaxSpeed;
                vehicle.debugData.isChangingLanes = state.isChangingLanes;
                vehicle.debugData.wantsToOvertake = state.wantsToOvertake;
                vehicle.debugData.wantsToHonk = state.wantsToHonk;
                vehicle.debugData.leftLaneBlocked = state.leftLaneBlocked;
                vehicle.debugData.rightLaneBlocked = state.rightLaneBlocked;

                int wCount = _wheelCounts[vehicle.arrayIndex];
                int startWIndex = vehicle.arrayIndex * 4;

                bool isGrounded = false;
                for (int w = 0; w < wCount; w++)
                {
                    RaycastHit hit = _wheelRaycastHits[startWIndex + w];

                    // ONLY run physics math if the wheel actually hit the ground
                    if (hit.distance > 0f)
                    {
                        isGrounded = true;

                        // Read the pre-calculated Native origin and direction 
                        RaycastCommand cmd = _wheelRaycastCommands[startWIndex + w];
                        Vector3 origin = cmd.from;
                        Vector3 springDir = -cmd.direction; // Inverse of down is up

                        if (state.currentSpeed < 0.1f)
                        {
                            springDir = Vector3.up;
                        }
                        // Pass the fast Math
                        vehicle.ApplySuspensionFast(
                            hit,
                            origin,
                            springDir,
                            vehicle.wheels[w].restLength,
                            vehicle.wheels[w].radius
                        );
                    }
                }
                vehicle.isGrounded = isGrounded;

                Rigidbody rb = vehicle.rb;

                //copy Steering Angle to the vehicle for visual purposes
                vehicle.steeringAngle = state.steeringAngle;

                // Stop AI forces if the vehicle is airborne. Let gravity take over fully.
                if (!vehicle.isGrounded)
                {
                    if (rb.isKinematic) rb.isKinematic = false;
                    continue; // Skip the rest of the loop for this car
                }

                //Anti-Roll/ Braking
                if (state.currentSpeed < 0.1f)
                {
                    // Freeze rotation so uneven suspension doesn't spin it
                    rb.constraints = RigidbodyConstraints.FreezeRotation;

                    // Dampen horizontal velocity to simulate heavy tire friction.
                    // This lets the player push it (spiking the velocity), but quickly brings it back to a dead stop.
                    Vector3 vel = rb.linearVelocity;
                    vel.x = Mathf.Lerp(vel.x, 0f, Time.fixedDeltaTime * 15f);
                    vel.z = Mathf.Lerp(vel.z, 0f, Time.fixedDeltaTime * 15f);
                    rb.linearVelocity = vel;

                    continue;
                }
                else
                {
                    rb.constraints = RigidbodyConstraints.None;
                    // rb.linearDamping = 0f;
                    // rb.angularDamping = 0.05f; // Standard small drag amount
                    //rb.linearDamping = 0f; // Reset damping when not stopped
                    // The car is moving normally. 
                    // Calculate velocity difference on X and Z axis to allow physics to keep gravity and collision forces intact
                    Vector3 velocityDifference = state.desiredVelocity - rb.linearVelocity;
                    velocityDifference.y = 0; // Don't interfere with gravity/suspension

                    // Add force as a velocity change for stable, mass-independent movement that works with the physics solver
                    rb.AddForce(velocityDifference, ForceMode.VelocityChange);
                }

                // Calculate the rotation difference to use AddTorque instead of MoveRotation (stops physics fighting)
                Quaternion rotDifference = state.desiredRotation * Quaternion.Inverse(rb.rotation);
                rotDifference.ToAngleAxis(out float angle, out Vector3 axis);

                if (angle > 180f) angle -= 360f;

                if (Mathf.Abs(angle) > 0.01f)
                {
                    Vector3 desiredAngularVelocity = (axis * (angle * Mathf.Deg2Rad)) / Time.fixedDeltaTime;
                    Vector3 angularVelocityDifference = desiredAngularVelocity - rb.angularVelocity;

                    rb.AddTorque(angularVelocityDifference, ForceMode.VelocityChange);
                }
            }
        }

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (usePlayerPooling && playerTransform != null)
            {
                Vector3 pos = playerTransform.position;
                Vector3 up = Vector3.up;

                // Draw largest first so it doesn't hide smaller ones

                // Despawn Radius (Blue)
                UnityEditor.Handles.color = new Color(0f, 0f, 1f, 0.1f);
                UnityEditor.Handles.DrawSolidDisc(pos, up, despawnRadius);
                UnityEditor.Handles.color = Color.blue;
                UnityEditor.Handles.DrawWireDisc(pos, up, despawnRadius);

                // Outer Spawn Radius (Yellow)
                UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
                UnityEditor.Handles.DrawSolidDisc(pos, up, outerSpawnRadius);
                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.DrawWireDisc(pos, up, outerSpawnRadius);

                // Inner Spawn Radius (Red)
                UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.2f);
                UnityEditor.Handles.DrawSolidDisc(pos, up, innerSpawnRadius);
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.DrawWireDisc(pos, up, innerSpawnRadius);
            }
#endif
        }

        void OnDestroy()
        {
            // IMPORTANT: Unmanaged Collections MUST be disposed on destroy or you create a nasty memory leak.
            if (_isInitialized)
            {
                if (_vehicleStates.IsCreated) _vehicleStates.Dispose();
                if (_waypointBuffer.IsCreated) _waypointBuffer.Dispose();
                if (_transformAccessArray.isCreated) _transformAccessArray.Dispose(); // Note the lowercase 'i' on isCreated here

                if (_boxcastCommands.IsCreated) _boxcastCommands.Dispose();
                if (_raycastHits.IsCreated) _raycastHits.Dispose();

                if (_playerBoxcastCommands.IsCreated) _playerBoxcastCommands.Dispose();
                if (_playerRaycastHits.IsCreated) _playerRaycastHits.Dispose();

                if (_leftBoxcastCommands.IsCreated) _leftBoxcastCommands.Dispose();
                if (_leftRaycastHits.IsCreated) _leftRaycastHits.Dispose();

                if (_rightBoxcastCommands.IsCreated) _rightBoxcastCommands.Dispose();
                if (_rightRaycastHits.IsCreated) _rightRaycastHits.Dispose();

                if (_wheelRaycastCommands.IsCreated) _wheelRaycastCommands.Dispose();
                if (_wheelRaycastHits.IsCreated) _wheelRaycastHits.Dispose();

                if (_wheelCounts.IsCreated) _wheelCounts.Dispose();
                if (_wheelLocalOffsets.IsCreated) _wheelLocalOffsets.Dispose();
                if (_wheelRayLengths.IsCreated) _wheelRayLengths.Dispose();
            }
        }
    }
}

