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
        public int vehicleCount = 5;

        [Header("Testing setup")]
        public GameObject dummyCarPrefab;
        public AIWaypoint[] spawnWaypoints; // Assign in inspector to test spawning

        // Native memory arrays for the Jobs
        private NativeArray<VehicleState> _vehicleStates;
        private NativeArray<Vector3> _waypointBuffer; 
        private TransformAccessArray _transformAccessArray;
        
        // Job execution handling
        private JobHandle _finalJobHandle;
        
        // Systems
        private TrafficRouteSystem _routeSystem;

        // Main thread references required to traverse the actual AIWaypoint graph
        private List<AIVehicle> _activeVehicles = new List<AIVehicle>();
        private bool _isInitialized = false;

        void Start()
        {
            _routeSystem = new TrafficRouteSystem();
            InitializeBuffers();
            SpawnInitialVehicles();
        }

        private void InitializeBuffers()
        {
            _vehicleStates = new NativeArray<VehicleState>(vehicleCount, Allocator.Persistent);
            _waypointBuffer = new NativeArray<Vector3>(vehicleCount * WAYPOINT_LOOKAHEAD, Allocator.Persistent);
            _transformAccessArray = new TransformAccessArray(vehicleCount);
            
            _isInitialized = true;
        }

        private void SpawnInitialVehicles()
        {
            if (spawnWaypoints == null || spawnWaypoints.Length == 0) return;

            // Simple spawner for now: spawn one car per waypoint until we hit vehicleCount
            for (int i = 0; i < vehicleCount; i++)
            {
                AIWaypoint spawnPoint = spawnWaypoints[i % spawnWaypoints.Length];
                GameObject carObj = Instantiate(dummyCarPrefab, spawnPoint.transform.position, spawnPoint.transform.rotation);
                
               AIVehicle vehicle = carObj.GetComponent<AIVehicle>();
                vehicle.arrayIndex = i;
                vehicle.lookaheadWaypoints[0] = spawnPoint;
                
                _activeVehicles.Add(vehicle);
                _transformAccessArray.Add(carObj.transform);

                // Initialize state
                VehicleState state = new VehicleState
                {
                    currentSpeed = 10f, // test speed
                    maxSpeed = 15f,
                    waypointBufferStartIndex = i * WAYPOINT_LOOKAHEAD,
                    currentTargetIndexOffset = 0,
                    reachedCurrentWaypoint = false,
                    isApproachingStopPoint = false
                };
                
                _vehicleStates[i] = state;
                
                // Warm up the 5 waypoint buffer 
                WarmupWaypoints(vehicle, state);
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

        void Update()
        {
            if (!_isInitialized) return;
            
            // 1. Route Management System processes completed waypoints and reads stop points
            _routeSystem.HandleRouteRefills(_activeVehicles, _vehicleStates, _waypointBuffer);
            _routeSystem.PollDynamicStopStates(_activeVehicles, _vehicleStates);

            // Schedule Single Monolithic Job (Optimized for Mobile/Android to minimize ram bandwidth)
            TrafficSimulationJob simulationJob = new TrafficSimulationJob
            {
                vehicleStates = _vehicleStates,
                waypointBuffer = _waypointBuffer,
                deltaTime = Time.deltaTime,
                arrivalDistance = 1.0f
            };

            // Final handle allows the Main Thread to wait for ALL jobs to finish
            _finalJobHandle = simulationJob.Schedule(_transformAccessArray);
        }

        void LateUpdate()
        {
            if (!_isInitialized) return;

            // Wait for the end of the Job chain
            _finalJobHandle.Complete();
        }

        void OnDestroy()
        {
            // IMPORTANT: Unmanaged Collections MUST be disposed on destroy or you create a nasty memory leak.
            if (_isInitialized)
            {
                if (_vehicleStates.IsCreated) _vehicleStates.Dispose();
                if (_waypointBuffer.IsCreated) _waypointBuffer.Dispose();
                if (_transformAccessArray.isCreated) _transformAccessArray.Dispose(); // Note the lowercase 'i' on isCreated here
            }
        }
    }
}
