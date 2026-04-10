using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace Darkmatter.TrafficSystem
{
    public partial class TrafficManager
    {
        private void InitializeBuffers()
        {
            // Set capacity dynamically. Buffer it up minimally.
            int workingCapacity = Mathf.Max(10, densityControl); // Ensures minimum size
            
            _vehicleStates = new NativeArray<VehicleState>(workingCapacity, Allocator.Persistent);
            _waypointBuffer = new NativeArray<Vector3>(workingCapacity * WAYPOINT_LOOKAHEAD, Allocator.Persistent);
            _transformAccessArray = new TransformAccessArray(workingCapacity);

            _frontBoxcastCommands = new NativeArray<BoxcastCommand>(workingCapacity, Allocator.Persistent);
            _frontRaycastHits = new NativeArray<RaycastHit>(workingCapacity, Allocator.Persistent);

            _playerBoxcastCommands = new NativeArray<BoxcastCommand>(workingCapacity, Allocator.Persistent);
            _playerRaycastHits = new NativeArray<RaycastHit>(workingCapacity, Allocator.Persistent);

            _leftBoxcastCommands = new NativeArray<BoxcastCommand>(workingCapacity, Allocator.Persistent);
            _leftRaycastHits = new NativeArray<RaycastHit>(workingCapacity, Allocator.Persistent);

            _rightBoxcastCommands = new NativeArray<BoxcastCommand>(workingCapacity, Allocator.Persistent);
            _rightRaycastHits = new NativeArray<RaycastHit>(workingCapacity, Allocator.Persistent);

            _wheelCounts = new NativeArray<int>(workingCapacity, Allocator.Persistent);
            _wheelLocalOffsets = new NativeArray<Vector3>(workingCapacity * 4, Allocator.Persistent);
            _wheelRayLengths = new NativeArray<float>(workingCapacity * 4, Allocator.Persistent);
            _wheelRaycastCommands = new NativeArray<RaycastCommand>(workingCapacity * 4, Allocator.Persistent);
            _wheelRaycastHits = new NativeArray<RaycastHit>(workingCapacity * 4, Allocator.Persistent);

            _jobEventQueue = new NativeQueue<VehicleEvent>(Allocator.Persistent);

            _isInitialized = true;
        }

        private void WarmupWaypoints(AIVehicle vehicle, VehicleState state)
        {
            ClearWaypointBufferBlock(state.waypointBufferStartIndex);

            for (int i = 1; i < WAYPOINT_LOOKAHEAD; i++)
            {
                vehicle.lookaheadWaypoints[i] = null;
            }

            for (int i = 0; i < WAYPOINT_LOOKAHEAD - 1; i++)
            {
                AIWaypoint currentObj = vehicle.lookaheadWaypoints[i];
                if (currentObj == null) break;

                AIWaypoint nextObj = _trafficWaypointUpdater.GetNextValidWaypoint(vehicle, currentObj);
                vehicle.lookaheadWaypoints[i + 1] = nextObj;
                if (nextObj == null) break;
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

        private void ClearWaypointBufferBlock(int startIndex)
        {
            for (int i = 0; i < WAYPOINT_LOOKAHEAD; i++)
            {
                _waypointBuffer[startIndex + i] = Vector3.zero;
            }
        }

        void FixedUpdate()
        {
            if (!_isInitialized) return;

            // 1. Route Management System processes completed waypoints and reads stop points
            _trafficWaypointUpdater.UpdateWaypoint(_activeVehicles, _vehicleStates, _waypointBuffer, Time.fixedDeltaTime);
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
                boxcastCommands = _frontBoxcastCommands,
                leftBoxcastCommands = _leftBoxcastCommands,
                rightBoxcastCommands = _rightBoxcastCommands,
                playerBoxcastCommands = _playerBoxcastCommands,
                waypointBuffer = _waypointBuffer
            };
            JobHandle sensorJobHandle = sensorJob.Schedule(_transformAccessArray);

            // Job 2: Process Physics Overlaps
            JobHandle physicsJobHandle = BoxcastCommand.ScheduleBatch(
                _frontBoxcastCommands,
                _frontRaycastHits,
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

            // Job 2.75: Aggregate Sensor Data
            SensorAggregationJob sensorAggregationJob = new SensorAggregationJob
            {
                vehicleStates = _vehicleStates,
                frontSensorHits = _frontRaycastHits,
                leftSensorHits = _leftRaycastHits,
                rightSensorHits = _rightRaycastHits,
                playerSensorHits = _playerRaycastHits
            };
            JobHandle aggregationJobHandle = sensorAggregationJob.Schedule(_activeVehicles.Count, 64, combinedPhysicsHandle);

            // Job 3: Movement Simulation
            TrafficSimulationJob simulationJob = new TrafficSimulationJob
            {
                vehicleStates = _vehicleStates,
                waypointBuffer = _waypointBuffer,
                eventQueue = _jobEventQueue.AsParallelWriter(),
                playerForward = playerTransform != null ? playerTransform.forward : Vector3.forward,
                deltaTime = Time.fixedDeltaTime,
                arrivalDistance = 2f,
                timeSinceLevelLoad = Time.timeSinceLevelLoad
            };

            // Final handle allows the Main Thread to wait for all simulation
            _finalJobHandle = simulationJob.Schedule(_transformAccessArray, aggregationJobHandle);

            // Wait for everything to complete before applying
            _finalJobHandle.Complete();

            // Process Vehicle Events
            while (_jobEventQueue.TryDequeue(out VehicleEvent vehicleEvent))
            {
                HandleVehicleEvent(vehicleEvent);
            }

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
                    // Calculate the difference between desired and current velocity
                    Vector3 velocityDifference = state.desiredVelocity - rb.linearVelocity;
                    velocityDifference.y = 0f;
                    // Apply the difference as a velocity change so suspension/gravity are preserved
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

        private void HandleVehicleEvent(VehicleEvent vehicleEvent)
        {
            if (vehicleEvent.vehicleIndex < 0 || vehicleEvent.vehicleIndex >= _activeVehicles.Count) return;
            
            AIVehicle vehicle = _activeVehicles[vehicleEvent.vehicleIndex];
            switch (vehicleEvent.eventType)
            {
                case VehicleEventType.HonkHorn:
                    // vehicle.HonkHorn();
                    break;
                case VehicleEventType.BrakesApplied:
                    // vehicle.SetBrakeLights(true);
                    break;
                case VehicleEventType.BrakesReleased:
                    // vehicle.SetBrakeLights(false);
                    break;
                case VehicleEventType.TurnSignalLeft:
                    break;
                case VehicleEventType.TurnSignalRight:
                    break;
                case VehicleEventType.TurnSignalsOff:
                    break;
            }
        }

        void OnDestroy()
        {
            // IMPORTANT: Unmanaged Collections MUST be disposed on destroy or you create a nasty memory leak.
            if (_isInitialized)
            {
                _finalJobHandle.Complete();

                if (_vehicleStates.IsCreated) _vehicleStates.Dispose();
                if (_waypointBuffer.IsCreated) _waypointBuffer.Dispose();
                if (_transformAccessArray.isCreated) _transformAccessArray.Dispose(); // Note the lowercase 'i' on isCreated here

                if (_frontBoxcastCommands.IsCreated) _frontBoxcastCommands.Dispose();
                if (_frontRaycastHits.IsCreated) _frontRaycastHits.Dispose();

                if (_playerBoxcastCommands.IsCreated) _playerBoxcastCommands.Dispose();
                if (_playerRaycastHits.IsCreated) _playerRaycastHits.Dispose();

                if (_leftBoxcastCommands.IsCreated) _leftBoxcastCommands.Dispose();
                if (_leftRaycastHits.IsCreated) _leftRaycastHits.Dispose();

                if (_rightBoxcastCommands.IsCreated) _rightBoxcastCommands.Dispose();
                if (_rightRaycastHits.IsCreated) _rightRaycastHits.Dispose();

                if (_wheelRaycastCommands.IsCreated) _wheelRaycastCommands.Dispose();
                if (_wheelRaycastHits.IsCreated) _wheelRaycastHits.Dispose();
                if (_jobEventQueue.IsCreated) _jobEventQueue.Dispose();

                if (_wheelCounts.IsCreated) _wheelCounts.Dispose();
                if (_wheelLocalOffsets.IsCreated) _wheelLocalOffsets.Dispose();
                if (_wheelRayLengths.IsCreated) _wheelRayLengths.Dispose();
            }
        }
    }
}
