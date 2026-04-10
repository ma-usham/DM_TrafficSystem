using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem
{
    public class TrafficSpatialGrid
    {
        private TrafficManager _manager;
        private Dictionary<Vector2Int, WaypointGridCell> _runtimeGrid; //the runtime-optimized grid for fast lookups, loaded from the serializedGrid data created in the editor
        private List<WaypointGridCell> _spawnableCells;
        private static Stack<List<AIWaypoint>> _waypointListPool = new Stack<List<AIWaypoint>>();
        public TrafficSpatialGrid(TrafficManager manager)
        {
            _manager = manager;
        }

        public void InitializeRuntimeGrid()
        {
            _runtimeGrid = new Dictionary<Vector2Int, WaypointGridCell>();
            _spawnableCells = new List<WaypointGridCell>();

            foreach (var cell in _manager.serializedGrid)
            {
                _runtimeGrid[cell.cellCoordinate] = cell;

                // Cache cells that contain spawn points so we can pick them instantly later
                if (cell.spawnWaypoints != null && cell.spawnWaypoints.Count > 0)
                {
                    _spawnableCells.Add(cell);
                }
            }
        }

        public bool HasSpawnableCells()
        {
            return _spawnableCells != null && _spawnableCells.Count > 0;
        }

        public AIWaypoint GetRandomSpawnWaypoint()
        {
            if (!HasSpawnableCells()) return null;
            
            WaypointGridCell randomCell = _spawnableCells[Random.Range(0, _spawnableCells.Count)];
            return randomCell.spawnWaypoints[Random.Range(0, randomCell.spawnWaypoints.Count)];
        }


        public void ReturnWaypointList(List<AIWaypoint> list)
        {
            if (list != null)
            {
                list.Clear();
                _waypointListPool.Push(list);
            }
        }



        public List<AIWaypoint> GetNearbyWaypoints(Vector3 position)
        {
            List<AIWaypoint> nearbyWaypoints = _waypointListPool.Count > 0 ? _waypointListPool.Pop() : new List<AIWaypoint>();
            nearbyWaypoints.Clear();

            int centerX = Mathf.FloorToInt(position.x / _manager.gridSize);
            int centerZ = Mathf.FloorToInt(position.z / _manager.gridSize);
            Vector2Int centerCell = new Vector2Int(centerX, centerZ);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector2Int checkCell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                    if (_runtimeGrid.TryGetValue(checkCell, out WaypointGridCell cellInfo))
                    {
                        nearbyWaypoints.AddRange(cellInfo.allWaypoints);
                    }
                }
            }
            return nearbyWaypoints;
        }

        public List<AIWaypoint> GetNearbySpawnWaypoints(Vector3 position)
        {
            List<AIWaypoint> nearbySpawnWaypoints = _waypointListPool.Count > 0 ? _waypointListPool.Pop() : new List<AIWaypoint>();
            nearbySpawnWaypoints.Clear();

            int centerX = Mathf.FloorToInt(position.x / _manager.gridSize);
            int centerZ = Mathf.FloorToInt(position.z / _manager.gridSize);
            Vector2Int centerCell = new Vector2Int(centerX, centerZ);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector2Int checkCell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                    if (_runtimeGrid.TryGetValue(checkCell, out WaypointGridCell cellInfo))
                    {
                        if (cellInfo.spawnWaypoints != null)
                            nearbySpawnWaypoints.AddRange(cellInfo.spawnWaypoints);
                    }
                }
            }
            return nearbySpawnWaypoints;
        }

        public AIWaypoint GetClosestWaypoint(Vector3 position)
        {
            List<AIWaypoint> localWaypoints = GetNearbyWaypoints(position);

            if (localWaypoints == null || localWaypoints.Count == 0)
            {
                ReturnWaypointList(localWaypoints);
                return null;
            }

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

            ReturnWaypointList(localWaypoints);
            return closest;
        }

        public AIWaypoint GetNearestWaypointInDirection(Vector3 position, Vector3 direction)
        {
            List<AIWaypoint> localWaypoints = GetNearbyWaypoints(position);

            if (localWaypoints == null || localWaypoints.Count == 0)
            {
                ReturnWaypointList(localWaypoints);
                return null;
            }

            AIWaypoint closest = null;
            float closestSqrDist = float.MaxValue;
            Vector3 normDir = direction.normalized;

            for (int i = 0; i < localWaypoints.Count; i++)
            {
                Vector3 dirToWaypoint = localWaypoints[i].transform.position - position;

                if (Vector3.Dot(dirToWaypoint.normalized, normDir) > 0f)
                {
                    float sqrDist = dirToWaypoint.sqrMagnitude;
                    if (sqrDist < closestSqrDist)
                    {
                        closestSqrDist = sqrDist;
                        closest = localWaypoints[i];
                    }
                }
            }

            ReturnWaypointList(localWaypoints);
            return closest;
        }
    }
}