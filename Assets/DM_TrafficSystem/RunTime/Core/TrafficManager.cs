﻿﻿﻿using UnityEngine;
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
        public List<AIWaypoint> allWaypoints; //Used for API Queries (closest point, points in direction, etc)
        public List<AIWaypoint> spawnWaypoints;//Used for Spawning only, a subset of allWaypoints that are designated as spawn points in the editor
    }

    /// <summary>
    /// Stores scene-wide traffic configuration values and manages the Job System memory loops.
    /// </summary>
    public partial class TrafficManager : MonoBehaviour
    {
        public const int WAYPOINT_LOOKAHEAD = 5;
        public const int MAX_WHEELS = 10; // The absolute maximum wheels any single vehicle can have. Increase if you add 18-wheelers!

        [FormerlySerializedAs("VehicleCount")]
        [Header("Hard Memory Limits")]
        [Tooltip("Absolute maximum RAM allocation and Pool size.")]
        [Min(0)]
        public int maxVehicleCountInGame = 50;

        [Header("Density Control")]
        [Tooltip("Target number of active vehicles. Will be clamped to maxVehicleCountInGame.")]
        public int densityControl = 20;

        [Header("Global Physics Layers")]
        public LayerMask groundMask;
        public LayerMask trafficMask;
        public LayerMask playerMask;

        [Header("Vehicle Collection Setup")]
        public VehicleCollection vehicleCollection;

        [Header("Player Pooling Setup")]
        public bool usePlayerPooling = false;
        public float innerSpawnRadius = 50f;
        public float outerSpawnRadius = 150f;
        public float despawnRadius = 200f;
        [Tooltip("If unassigned, Camera.main will be used instead.")]
        public Camera mainCamera;

        public Transform playerTransform; // Assign your Player in the Inspector

        [Header("Grid Spawning Setup")]
        public float gridSize = 200f; // 100x100 meter squares

        // Unity will save this list in the editor
        [HideInInspector]
        public List<WaypointGridCell> serializedGrid = new List<WaypointGridCell>(); //used to save the spatial grid data in the editor, which is then loaded into a runtime-optimized dictionary in the TrafficSpatialGrid class

        public TrafficSpatialGrid spatialGrid { get; private set; }

        // Native memory arrays for the Jobs
        private NativeArray<VehicleConfig> _vehicleConfigs;
        private NativeArray<VehicleState> _vehicleStates;
        private NativeArray<Vector3> _waypointBuffer;
        private TransformAccessArray _transformAccessArray;

        // Sensor memory
        private NativeArray<BoxcastCommand> _frontBoxcastCommands;
        private NativeArray<RaycastHit> _frontRaycastHits;

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
        private NativeQueue<VehicleEvent> _jobEventQueue;
        private JobHandle _finalJobHandle;

        // Helper Classes
        private TrafficWaypointUpdater _trafficWaypointUpdater;

        // Main thread references required to traverse the actual AIWaypoint graph
        private List<AIVehicle> _activeVehicles = new List<AIVehicle>();
        private bool _isInitialized = false;
        private VehiclePool _vehiclePool;
        private readonly List<AIWaypoint> _allWaypointsInMap = new List<AIWaypoint>();
        private readonly Dictionary<AIWaypoint, int> _waypointIndexLookup = new Dictionary<AIWaypoint, int>();

        void Awake()
        {
            DMTS_API.RegisterManager(this);
        }

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

            spatialGrid = new TrafficSpatialGrid(this);
            spatialGrid.InitializeRuntimeGrid();
            RebuildWaypointCache();
            InitializeBuffers();
            SpawnInitialVehicles();

            if (usePlayerPooling)
            {
                StartCoroutine(PlayerPoolingRoutine());
            }
        }

        #region API
        /// <summary>
        /// Finds the closest AIWaypoint to a given position using the optimized spatial grid.
        /// </summary>
        public AIWaypoint GetClosestWaypoint(Vector3 position)
        {
            if (!_isInitialized || spatialGrid == null) return null;
            return spatialGrid.GetClosestWaypoint(position);
        }

        /// <summary>
        /// Finds the closest AIWaypoint to a given position that is in the specified direction.
        /// </summary>
        public AIWaypoint GetNearestWaypointInDirection(Vector3 position, Vector3 direction)
        {
            if (!_isInitialized || spatialGrid == null) return null;
            return spatialGrid.GetNearestWaypointInDirection(position, direction);
        }

        /// <summary>
        /// Flattens the spatial grid and returns every single waypoint in the map.
        /// </summary>
        public IReadOnlyList<AIWaypoint> GetAllWaypointsInMap()
        {
            EnsureWaypointCacheBuilt();
            return _allWaypointsInMap;
        }

        internal bool TryGetWaypointIndexCached(AIWaypoint waypoint, out int index)
        {
            EnsureWaypointCacheBuilt();
            if (waypoint == null)
            {
                index = -1;
                return false;
            }

            return _waypointIndexLookup.TryGetValue(waypoint, out index);
        }

        internal AIWaypoint GetWaypointByCachedIndex(int index)
        {
            EnsureWaypointCacheBuilt();
            if (index < 0 || index >= _allWaypointsInMap.Count)
            {
                return null;
            }

            return _allWaypointsInMap[index];
        }

        /// <summary>
        /// Despawns all active vehicles within a given radius from a center point.
        /// </summary>
        public void ClearVehiclesInArea(Vector3 center, float radius)
        {
            float sqrRadius = radius * radius;

            // Iterate backwards because DespawnVehicleAt removes elements 
            // and swaps the last element into the removed slot
            for (int i = _activeVehicles.Count - 1; i >= 0; i--)
            {
                if (_activeVehicles[i] != null)
                {
                    // Use sqrMagnitude as it is heavily optimized compared to Vector3.Distance
                    if ((_activeVehicles[i].transform.position - center).sqrMagnitude <= sqrRadius)
                    {
                        DespawnVehicleAt(i);
                    }
                }
            }
        }

        private void EnsureWaypointCacheBuilt()
        {
            if (_allWaypointsInMap.Count == 0 && serializedGrid != null && serializedGrid.Count > 0)
            {
                RebuildWaypointCache();
            }
        }

        private void RebuildWaypointCache()
        {
            _allWaypointsInMap.Clear();
            _waypointIndexLookup.Clear();

            if (serializedGrid == null) return;

            foreach (WaypointGridCell cell in serializedGrid)
            {
                if (cell.allWaypoints == null) continue;

                for (int i = 0; i < cell.allWaypoints.Count; i++)
                {
                    AIWaypoint waypoint = cell.allWaypoints[i];
                    if (waypoint == null || _waypointIndexLookup.ContainsKey(waypoint))
                    {
                        continue;
                    }

                    _waypointIndexLookup.Add(waypoint, _allWaypointsInMap.Count);
                    _allWaypointsInMap.Add(waypoint);
                }
            }
        }
        #endregion
    }
}
