using UnityEngine;
using UnityEngine.Serialization;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Stores scene-wide traffic configuration values and manages the Job System memory loops.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public const int WAYPOINT_LOOKAHEAD = 5;

        [FormerlySerializedAs("VehicleCount")]
        [Min(0)]
        public int vehicleCount = 1;

        [Header("Global Physics Layers")]
        public LayerMask obstacleMask;

        [Header("Testing setup")]
        public VehicleCollection vehicleCollection;
        public AIWaypoint[] spawnWaypoints; // Assign in inspector to test spawning
        [Min(0)] public int initialPoolSize = 50; // Pre-instantiate this many vehicles in the pool at Start for better performance when spawning during gameplay

        // Native memory arrays for the Jobs
        private NativeArray<VehicleState> _vehicleStates;
        private NativeArray<Vector3> _waypointBuffer;
        private TransformAccessArray _transformAccessArray;

        // Sensor memory
        private NativeArray<BoxcastCommand> _boxcastCommands;
        private NativeArray<RaycastHit> _raycastHits;

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

            _vehiclePool.Prepopulate(initialPoolSize);

            InitializeBuffers();
            SpawnInitialVehicles();
        }

        private void InitializeBuffers()
        {
            _vehicleStates = new NativeArray<VehicleState>(vehicleCount, Allocator.Persistent);
            _waypointBuffer = new NativeArray<Vector3>(vehicleCount * WAYPOINT_LOOKAHEAD, Allocator.Persistent);
            _transformAccessArray = new TransformAccessArray(vehicleCount);

            _boxcastCommands = new NativeArray<BoxcastCommand>(vehicleCount, Allocator.Persistent);
            _raycastHits = new NativeArray<RaycastHit>(vehicleCount, Allocator.Persistent);

            _isInitialized = true;
        }

        private void SpawnInitialVehicles()
        {
            if (spawnWaypoints == null || spawnWaypoints.Length == 0) return;

            int spawnedCount = 0;
            // Simple spawner for now: spawn one car per waypoint until we hit vehicleCount
            for (int i = 0; i < vehicleCount; i++)
            {
                AIWaypoint spawnPoint = spawnWaypoints[Random.Range(0, spawnWaypoints.Length)];

                // Perform the Physics check before spawning
                // If the area is blocked, we just continue (which moves to the next attempt)
                if (Physics.CheckSphere(spawnPoint.transform.position, 5f, obstacleMask))
                {
                    Debug.Log($"Skipping spawn at {spawnPoint.name} - area is blocked.");
                    continue;
                }

                AIVehicle vehicle = _vehiclePool.Spawn(spawnPoint);

                if (vehicle == null) continue;

                vehicle.arrayIndex = spawnedCount;
                vehicle.lookaheadWaypoints[0] = spawnPoint;

                _activeVehicles.Add(vehicle);
                _transformAccessArray.Add(vehicle.transform);

                bool sensorActive = vehicle.frontSensor != null && vehicle.frontSensor.gameObject.activeInHierarchy;

                // Initialize state
                VehicleState state = new VehicleState
                {
                    currentSpeed = 1f, // test speed
                    maxSpeed = vehicle.maxSpeed,
                    acceleration = vehicle.acceleration,
                    brakingPower = vehicle.brakingPower,
                    turnSpeed = vehicle.turnSpeed,
                    stoppingDistance = vehicle.stoppingDistance,
                    waypointBufferStartIndex = spawnedCount * WAYPOINT_LOOKAHEAD,
                    currentTargetIndexOffset = 0,
                    reachedCurrentWaypoint = false,
                    isApproachingStopPoint = false,
                    desiredVelocity = Vector3.zero,
                    desiredRotation = vehicle.transform.rotation,
                    isSensorActive = sensorActive,
                    sensorSize = sensorActive ? vehicle.frontSensor.localScale : Vector3.zero,
                    sensorOffset = sensorActive ? vehicle.frontSensor.localPosition : Vector3.zero,
                    obstacleMask = obstacleMask.value,
                    obstacleDetected = false
                };

                _vehicleStates[i] = state;

                // Warm up the 5 waypoint buffer 
                WarmupWaypoints(vehicle, state);

                // Only increase the index if a spawn successfully went through
                spawnedCount++;
            }
        }

        private void WarmupWaypoints(AIVehicle vehicle, VehicleState state)
        {
            // Traverse from the spawn point and fill out 5 next points ahead of time
            for (int i = 0; i < WAYPOINT_LOOKAHEAD - 1; i++)
            {
                AIWaypoint currentObj = vehicle.lookaheadWaypoints[i];
                if (currentObj != null && currentObj.settings.nextWaypoint != null && currentObj.settings.nextWaypoint.Length > 0)
                {
                    // Pick the first connection for now
                    vehicle.lookaheadWaypoints[i + 1] = currentObj.settings.nextWaypoint[0];
                }
            }

            // Sync main thread array mapping to the struct Memory
            for (int i = 0; i < WAYPOINT_LOOKAHEAD; i++)
            {
                if (vehicle.lookaheadWaypoints[i] != null)
                {
                    _waypointBuffer[state.waypointBufferStartIndex + i] = vehicle.lookaheadWaypoints[i].transform.position;
                }
            }

            // Initialize stop state for the current target (index 0 initially)
            if (vehicle.lookaheadWaypoints[0] != null)
            {
                state.isApproachingStopPoint = vehicle.lookaheadWaypoints[0].settings.isStopPoint;
            }
            // Update the struct in the array!
            _vehicleStates[vehicle.arrayIndex] = state;
        }

        void FixedUpdate()
        {
            if (!_isInitialized) return;

            // 1. Route Management System processes completed waypoints and reads stop points
            _trafficWaypointUpdater.UpdateWaypoint(_activeVehicles, _vehicleStates, _waypointBuffer);
            _trafficWaypointUpdater.UpdateStopWaypoints(_activeVehicles, _vehicleStates);

            // --- 2. Schedule Jobs ---

            // Job 1: Build Boxcast Commands
            VehicleSensorJob sensorJob = new VehicleSensorJob
            {
                vehicleStates = _vehicleStates,
                boxcastCommands = _boxcastCommands
            };
            JobHandle sensorJobHandle = sensorJob.Schedule(_transformAccessArray);

            // Job 2: Process Physics Overlaps
            JobHandle physicsJobHandle = BoxcastCommand.ScheduleBatch(
                _boxcastCommands,
                _raycastHits,
                64,
                sensorJobHandle
            );

            // Job 3: Movement Simulation
            TrafficSimulationJob simulationJob = new TrafficSimulationJob
            {
                vehicleStates = _vehicleStates,
                waypointBuffer = _waypointBuffer,
                sensorHits = _raycastHits,
                deltaTime = Time.fixedDeltaTime,
                arrivalDistance = 2.0f
            };

            // Final handle allows the Main Thread to wait for all simulation
            _finalJobHandle = simulationJob.Schedule(_transformAccessArray, physicsJobHandle);

            // Wait for everything to complete before applying
            _finalJobHandle.Complete();

            // --- 3. Apply the Calculated Physics Results ---

            // Apply Rigidbody movement using the computed values
            for (int i = 0; i < _activeVehicles.Count; i++)
            {
                AIVehicle vehicle = _activeVehicles[i];
                VehicleState state = _vehicleStates[i];

                Rigidbody rb = vehicle.rb;

                //copy Steering Angle to the vehicle for visual purposes
                vehicle.steeringAngle = state.steeringAngle;

                //Anti-Roll/ Braking
                if (state.currentSpeed < 0.1f)
                {
                    rb.isKinematic = true; // Freeze the car when stopped to prevent sliding
                }
                else
                {
                    if (rb.isKinematic) rb.isKinematic = false; // Unfreeze when we start moving again
                    // The car is moving normally. 
                    // Keep the Rigidbody's current vertical velocity (for gravity/suspension)
                    Vector3 finalVelocity = new Vector3(state.desiredVelocity.x, rb.linearVelocity.y, state.desiredVelocity.z);
                    rb.linearVelocity = finalVelocity;
                }

                // Keep the Rigidbody's current up-vector slightly blended or directly apply rotation
                // Apply rotation via Rigidbody to keep the physics intact
                rb.MoveRotation(state.desiredRotation);
            }
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
            }
        }
    }
}
