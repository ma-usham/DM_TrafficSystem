using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem
{
    public partial class TrafficManager
    {
        private void SpawnInitialVehicles()
        {
            if (spatialGrid == null || serializedGrid == null || serializedGrid.Count == 0) return;

            int spawnedCount = 0;
            int startAmount = Mathf.Min(densityControl, maxVehicleCountInGame);

            for (int i = 0; i < startAmount; i++)
            {
                AIWaypoint spawnPoint = null;

                if (usePlayerPooling && mainCamera != null && playerTransform != null)
                {
                    Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
                    List<AIWaypoint> localWaypoints = spatialGrid.GetNearbySpawnWaypoints(playerTransform.position);
                    if (localWaypoints.Count > 0)
                    {
                        //Attempted 50 times because for pooling camera and frustum should be checked and there might not be many spawn points that are hidden from the camera in the starting area, so it might fail to find a spawn point and spawn no vehicles if the attempt count is too low.
                        //Its the initial spawn attempt so no worrises.
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
                    //The attempt 10 is checked because there arent spawn point in edges of map, so the car might not get spawnned.
                    //Even if under 10 attemp it fails then no vehicel will spawn (total vehicle -1).
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        var randomCell = serializedGrid[Random.Range(0, serializedGrid.Count)];
                        if (randomCell.spawnWaypoints != null && randomCell.spawnWaypoints.Count > 0)
                        {
                            spawnPoint = randomCell.spawnWaypoints[Random.Range(0, randomCell.spawnWaypoints.Count)];
                        }
                    }

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

            vehicle.transform.position = spawnPoint.transform.position;
            vehicle.transform.rotation = spawnPoint.transform.rotation;
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
                isLaneChangingVehicle = vehicle.driverBehaviour.willChangeLane,
                frustrationTime = randomFrustration,
                laneChangeCooldown = randomCooldown,
                aiOvertakeProbability = vehicle.driverBehaviour.aiOvertakeProbability,
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

        private void DespawnVehicleAt(int vehicleIndex)
        {
            if (vehicleIndex < 0 || vehicleIndex >= _activeVehicles.Count) return;

            AIVehicle vehicleToRemove = _activeVehicles[vehicleIndex];
            _vehiclePool.Despawn(vehicleToRemove);

            int lastIndex = _activeVehicles.Count - 1;

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

                // Wheel update
                _wheelCounts[vehicleIndex] = _wheelCounts[lastIndex];
                for (int w = 0; w < 4; w++)
                {
                    _wheelLocalOffsets[vehicleIndex * 4 + w] = _wheelLocalOffsets[lastIndex * 4 + w];
                    _wheelRayLengths[vehicleIndex * 4 + w] = _wheelRayLengths[lastIndex * 4 + w];
                }
            }

            _activeVehicles.RemoveAt(lastIndex);

            // Re-create the transform array dropping the removed element effectively by SwapBack!
            _transformAccessArray.RemoveAtSwapBack(vehicleIndex);
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
                List<AIWaypoint> localWaypoints = spatialGrid.GetNearbyWaypoints(playerPos);
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