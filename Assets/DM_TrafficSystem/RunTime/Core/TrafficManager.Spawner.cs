using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem
{
    public partial class TrafficManager
    {
        private void SpawnInitialVehicles()
        {
            if (spatialGrid == null || !spatialGrid.HasSpawnableCells()) return;

            int startAmount = Mathf.Min(densityControl, maxVehicleCountInGame);
            float innerSpawnRadiusSqr = innerSpawnRadius * innerSpawnRadius;
            float outerSpawnRadiusSqr = outerSpawnRadius * outerSpawnRadius;

            for (int i = 0; i < startAmount; i++)
            {
                AIWaypoint spawnPoint = null;

                if (usePlayerPooling && mainCamera != null && playerTransform != null)
                {
                    List<AIWaypoint> localWaypoints = spatialGrid.GetNearbySpawnWaypoints(playerTransform.position);
                    if (localWaypoints.Count > 0)
                    {
                        Camera cam = mainCamera != null ? mainCamera : Camera.main;
                        Plane[] frustumPlanes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

                        //Attempted 50 times because for pooling camera and frustum should be checked and there might not be many spawn points that are hidden from the camera in the starting area, so it might fail to find a spawn point and spawn no vehicles if the attempt count is too low.
                        //Its the initial spawn attempt so no worrises.
                        for (int attempt = 0; attempt < 50; attempt++)
                        {
                            AIWaypoint candidate = localWaypoints[Random.Range(0, localWaypoints.Count)];
                            float sqrDist = (candidate.transform.position - playerTransform.position).sqrMagnitude;
                            if (sqrDist >= innerSpawnRadiusSqr && sqrDist <= outerSpawnRadiusSqr)
                            {
                                if (IsSpawnPointHidden(candidate.transform.position, cam, frustumPlanes))
                                {
                                    spawnPoint = candidate;
                                    break;
                                }
                            }
                        }
                    }
                    spatialGrid.ReturnWaypointList(localWaypoints);
                }
                else
                {
                    // Fetch instantly from the runtime grid with a 100% success rate
                    spawnPoint = spatialGrid.GetRandomSpawnWaypoint();
                }

                if (spawnPoint != null)
                {
                    SpawnVehicle(spawnPoint);
                }
            }
        }

        private bool IsSpawnPointHidden(Vector3 pos, Camera cam, Plane[] frustumPlanes)
        {
            if (cam == null || frustumPlanes == null) return false;

            Bounds bounds = new Bounds(pos, new Vector3(3f, 3f, 5f));

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
            if (_activeVehicles.Count >= Mathf.Max(10, densityControl)) return false;

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
            vehicle.activeWaypointIndex = 0;
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

            VehicleConfig config = new VehicleConfig
            {
                engineMaxSpeed = vehicle.driverBehaviour.engineMaxSpeed * randomMultiplier,
                speedMultiplier = randomMultiplier,
                acceleration = vehicle.driverBehaviour.acceleration,
                brakingPower = vehicle.driverBehaviour.brakingPower,
                turnSpeed = vehicle.driverBehaviour.turnSpeed,
                stoppingDistance = vehicle.driverBehaviour.stoppingDistance,
                sensorSize = sensorActive ? vehicle.frontSensor.localScale : Vector3.zero,
                sensorOffset = sensorActive ? vehicle.frontSensor.localPosition : Vector3.zero,
                obstacleMask = trafficMask.value | playerMask.value,
                leftSensorSize = sideSensorActive ? vehicle.leftSensor.localScale : Vector3.zero,
                leftSensorOffset = sideSensorActive ? vehicle.leftSensor.localPosition : Vector3.zero,
                rightSensorSize = sideSensorActive ? vehicle.rightSensor.localScale : Vector3.zero,
                rightSensorOffset = sideSensorActive ? vehicle.rightSensor.localPosition : Vector3.zero,
                frustrationTime = randomFrustration,
                laneChangeCooldown = randomCooldown,
                aiOvertakeProbability = vehicle.driverBehaviour.aiOvertakeProbability
            };

            VehicleState state = new VehicleState
            {
                currentBehavior = AIState.Cruising,
                currentSpeed = 1f,
                localMaxSpeed = Mathf.Min(vehicle.driverBehaviour.engineMaxSpeed * randomMultiplier, wpLimit),
                waypointBufferStartIndex = newIndex * WAYPOINT_LOOKAHEAD,
                currentTargetIndexOffset = 0,
                reachedCurrentWaypoint = false,
                isApproachingStopPoint = false,
                desiredVelocity = Vector3.zero,
                desiredRotation = vehicle.transform.rotation,
                isSensorActive = sensorActive,
                sensorFacesWaypoint = vehicle.sensorFacesWaypoint,
                isSideSensorActive = sideSensorActive,
                trafficDetected = false,
                detectedTrafficFar = false,
                leftLaneBlocked = false,
                rightLaneBlocked = false,
                isChangingLanes = false,
                isLaneChangingVehicle = vehicle.driverBehaviour.willChangeLane,
                impatienceTimer = randomFrustration,
                wantsToChangeLane = false
            };

            // Update state
            _vehicleConfigs[newIndex] = config;
            _vehicleStates[newIndex] = state;
            WarmupWaypoints(vehicle, state);

            int wCount = vehicle.wheels != null ? Mathf.Min(vehicle.wheels.Length, MAX_WHEELS) : 0;
            _wheelCounts[newIndex] = wCount;

            int startWIndex = newIndex * MAX_WHEELS;
            for (int w = 0; w < wCount; w++)
            {
                _wheelLocalOffsets[startWIndex + w] = vehicle.wheels[w].localPosition;
                _wheelRayLengths[startWIndex + w] = vehicle.wheels[w].restLength + vehicle.wheels[w].radius;
            }

            return true;
        }

        private void DespawnVehicleAt(int vehicleIndex)
        {
            if (vehicleIndex < 0 || vehicleIndex >= _activeVehicles.Count) return;

            int lastIndex = _activeVehicles.Count - 1;
            AIVehicle vehicleToRemove = _activeVehicles[vehicleIndex];
            _vehiclePool.Despawn(vehicleToRemove);

            if (vehicleIndex != lastIndex)
            {
                // Move the last vehicle to this spot
                _activeVehicles[vehicleIndex] = _activeVehicles[lastIndex];
                _activeVehicles[vehicleIndex].arrayIndex = vehicleIndex;

                // State update
                VehicleState movedState = _vehicleStates[lastIndex];
                int oldStart = movedState.waypointBufferStartIndex;
                int newStart = vehicleIndex * WAYPOINT_LOOKAHEAD;
                movedState.waypointBufferStartIndex = newStart;

                // Copy buffer
                for (int i = 0; i < WAYPOINT_LOOKAHEAD; i++)
                {
                    _waypointBuffer[newStart + i] = _waypointBuffer[oldStart + i];
                }

                _vehicleStates[vehicleIndex] = movedState;
                _vehicleConfigs[vehicleIndex] = _vehicleConfigs[lastIndex];

                // Wheel update
                _wheelCounts[vehicleIndex] = _wheelCounts[lastIndex];
                for (int w = 0; w < MAX_WHEELS; w++)
                {
                    _wheelLocalOffsets[vehicleIndex * MAX_WHEELS + w] = _wheelLocalOffsets[lastIndex * MAX_WHEELS + w];
                    _wheelRayLengths[vehicleIndex * MAX_WHEELS + w] = _wheelRayLengths[lastIndex * MAX_WHEELS + w];
                }
            }

            _activeVehicles.RemoveAt(lastIndex);

            // Re-create the transform array dropping the removed element effectively by SwapBack!
            _transformAccessArray.RemoveAtSwapBack(vehicleIndex);
            ClearVehicleSlotData(lastIndex);
        }

        private void ClearVehicleSlotData(int vehicleIndex)
        {
            _vehicleStates[vehicleIndex] = default;
            _vehicleConfigs[vehicleIndex] = default;

            int waypointStartIndex = vehicleIndex * WAYPOINT_LOOKAHEAD;
            for (int i = 0; i < WAYPOINT_LOOKAHEAD; i++)
            {
                _waypointBuffer[waypointStartIndex + i] = Vector3.zero;
            }

            _wheelCounts[vehicleIndex] = 0;
            int wheelStartIndex = vehicleIndex * MAX_WHEELS;
            for (int w = 0; w < MAX_WHEELS; w++)
            {
                _wheelLocalOffsets[wheelStartIndex + w] = Vector3.zero;
                _wheelRayLengths[wheelStartIndex + w] = 0f;
            }
        }

        private System.Collections.IEnumerator PlayerPoolingRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(0.5f);
            while (true)
            {
                yield return wait;

                if (playerTransform == null || spatialGrid == null)
                    continue;

                Vector3 playerPos = playerTransform.position;
                float despawnRadiusSqr = despawnRadius * despawnRadius;
                float innerSpawnRadiusSqr = innerSpawnRadius * innerSpawnRadius;
                float outerSpawnRadiusSqr = outerSpawnRadius * outerSpawnRadius;

                // 1. Despawn vehicles outside despawn radius
                for (int i = _activeVehicles.Count - 1; i >= 0; i--)
                {
                    float sqrDist = (_activeVehicles[i].transform.position - playerPos).sqrMagnitude;
                    if (sqrDist > despawnRadiusSqr)
                    {
                        DespawnVehicleAt(i);
                    }
                }

                // 2. Spawn vehicles if we are below capacity
                List<AIWaypoint> localWaypoints = spatialGrid.GetNearbySpawnWaypoints(playerPos);
                if (localWaypoints.Count > 0)
                {
                    Camera cam = mainCamera != null ? mainCamera : Camera.main;
                    Plane[] frustumPlanes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

                    int spawnAttempts = 0;
                    int safeDensityTarget = Mathf.Min(densityControl, maxVehicleCountInGame);

                    while (_activeVehicles.Count < safeDensityTarget && spawnAttempts < 5)
                    {
                        spawnAttempts++;
                        AIWaypoint cand = localWaypoints[Random.Range(0, localWaypoints.Count)];
                        float sqrDist = (cand.transform.position - playerPos).sqrMagnitude;

                        if (sqrDist >= innerSpawnRadiusSqr && sqrDist <= outerSpawnRadiusSqr)
                        {
                            if (IsSpawnPointHidden(cand.transform.position, cam, frustumPlanes))
                            {
                                if (SpawnVehicle(cand))
                                {
                                    break; // Spawn max 1 vehicle per coroutine tick to avoid freezing
                                }
                            }
                        }
                    }
                }
                spatialGrid.ReturnWaypointList(localWaypoints);
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
    }
}
