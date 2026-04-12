using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Builds generated lanes and waypoint links from editable road spline data.
    /// </summary>
    public static class RoadBuilder
    {
        private const float GroundProjectionLift = 500f;
        private const float GroundProjectionDistance = 3000f;
        private const float MinDirectionSqrMagnitude = 0.001f;
        private const float DefaultMaxLaneChangeTurnAngle = 10f;
        private const float MinLaneChangeForwardDot = 0.1f;
        private static readonly AIWaypoint[] EmptyWaypointLinks = System.Array.Empty<AIWaypoint>();

        /// <summary>
        /// Removes all generated waypoint objects while preserving the lane objects and their lane-level settings.
        /// </summary>
        public static void ClearGeneratedWaypoints(Road road)
        {
            if (road == null)
                return;

            EnsureCollections(road);
            RemoveAttachedConnections(road, "Clear Road Waypoints");
            Undo.RecordObject(road, "Clear Road Waypoints");

            road.generatedLanes.Clear();

            for (int i = road.laneObjects.Count - 1; i >= 0; i--)
            {
                AILane lane = road.laneObjects[i];
                if (lane == null)
                {
                    road.laneObjects.RemoveAt(i);
                    continue;
                }

                ClearLaneWaypoints(lane);
            }

            EditorUtility.SetDirty(road);
        }

        /// <summary>
        /// Rebuilds all generated waypoints from the current spline while preserving lane-level lane objects when possible.
        /// </summary>
        public static void GenerateRoadWaypoints(Road road)
        {
            if (road == null || road.controlPointsList == null || road.controlPointsList.Count < 2)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Road Waypoints");
            Undo.RecordObject(road, "Generate Road Waypoints");

            ClearGeneratedWaypoints(road);

            if (!TryBuildCenterlineSamples(road, out List<Vector3> centerPositions, out List<Vector3> tangents))
            {
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            EnsureLaneObjectsMatchRoad(road);

            for (int laneIndex = 0; laneIndex < road.lanes; laneIndex++)
            {
                BuildLane(road, laneIndex, centerPositions, tangents);
            }

            EditorUtility.SetDirty(road);
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Generates same-direction lane-change links between adjacent lanes using the configured waypoint offset.
        /// </summary>
        public static void LinkLanes(Road road)
        {
            if (road == null || road.laneObjects == null || road.laneObjects.Count < 2)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Link Lanes");

            ClearLaneChangeLinks(road, "Link Lanes");

            int laneCount = road.laneObjects.Count;
            for (int laneIndex = 0; laneIndex < laneCount - 1; laneIndex++)
            {
                if (!CanLinkAdjacentLanes(road, laneIndex, laneIndex + 1, laneCount))
                    continue;

                LinkAdjacentLanes(
                    road,
                    road.laneObjects[laneIndex],
                    road.laneObjects[laneIndex + 1],
                    Mathf.Max(1, road.laneChangeLinkOffset));
            }

            EditorUtility.SetDirty(road);
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Clears all generated lane-change links without affecting forward lane traversal links.
        /// </summary>
        public static void UnlinkLanes(Road road)
        {
            if (road == null)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Unlink Lanes");

            ClearLaneChangeLinks(road, "Unlink Lanes");

            EditorUtility.SetDirty(road);
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Builds one generated lane by offsetting the centerline samples and creating waypoints along it.
        /// </summary>
        private static void BuildLane(
            Road road,
            int laneIndex,
            IReadOnlyList<Vector3> centerPositions,
            IReadOnlyList<Vector3> tangents)
        {
            float laneOffset = GetLaneOffset(laneIndex, road.lanes, road.laneWidth);
            List<Vector3> lanePositions = BuildLanePositions(road, centerPositions, tangents, laneOffset);

            AILane lane = road.laneObjects[laneIndex];

            if (!LaneTravelsWithSpline(road, laneOffset))
                lanePositions.Reverse();

            road.generatedLanes.Add(CreateWaypoints(lane, lanePositions, road.laneWidth));
        }

        /// <summary>
        /// Samples the road spline into centerline positions and tangent directions used for lane generation.
        /// </summary>
        private static bool TryBuildCenterlineSamples(
            Road road,
            out List<Vector3> centerPositions,
            out List<Vector3> tangents)
        {
            centerPositions = new List<Vector3>();
            tangents = new List<Vector3>();

            List<Vector3> densePoints = SplineMathUtils.GetCurvePoints(
                road.controlPointsList,
                Mathf.Max(1, road.curveResolution));

            if (densePoints.Count < 2)
                return false;

            centerPositions.Add(densePoints[0]);
            tangents.Add((densePoints[1] - densePoints[0]).normalized);

            float spacing = Mathf.Max(1f, road.waypointDistance);
            float distanceSinceLastWaypoint = 0f;
            Vector3 segmentStart = densePoints[0];

            for (int i = 1; i < densePoints.Count; i++)
            {
                Vector3 segmentEnd = densePoints[i];
                Vector3 segmentVector = segmentEnd - segmentStart;
                float segmentLength = segmentVector.magnitude;

                if (segmentLength <= Mathf.Epsilon)
                {
                    segmentStart = segmentEnd;
                    continue;
                }

                Vector3 segmentDirection = segmentVector / segmentLength;
                Vector3 sampleStart = segmentStart;
                float remainingSegmentLength = segmentLength;

                while (distanceSinceLastWaypoint + remainingSegmentLength >= spacing)
                {
                    float distanceToNextWaypoint = spacing - distanceSinceLastWaypoint;
                    Vector3 waypointPosition = sampleStart + segmentDirection * distanceToNextWaypoint;

                    centerPositions.Add(waypointPosition);
                    tangents.Add(segmentDirection);

                    sampleStart = waypointPosition;
                    remainingSegmentLength -= distanceToNextWaypoint;
                    distanceSinceLastWaypoint = 0f;
                }

                distanceSinceLastWaypoint += remainingSegmentLength;
                segmentStart = segmentEnd;
            }

            Vector3 lastPoint = densePoints[densePoints.Count - 1];
            if (Vector3.Distance(centerPositions[centerPositions.Count - 1], lastPoint) > 0.01f)
            {
                centerPositions.Add(lastPoint);
                tangents.Add((lastPoint - densePoints[densePoints.Count - 2]).normalized);
            }

            return true;
        }

        /// <summary>
        /// Offsets centerline samples sideways to create the waypoint positions for one lane.
        /// </summary>
        private static List<Vector3> BuildLanePositions(
            Road road,
            IReadOnlyList<Vector3> centerPositions,
            IReadOnlyList<Vector3> tangents,
            float laneOffset)
        {
            var lanePositions = new List<Vector3>(centerPositions.Count);

            for (int waypointIndex = 0; waypointIndex < centerPositions.Count; waypointIndex++)
            {
                Vector3 right = GetRightSide(tangents[waypointIndex]);
                Vector3 lanePosition = ProjectOntoGround(centerPositions[waypointIndex] + right * laneOffset);
                lanePositions.Add(lanePosition);
            }

            return lanePositions;
        }

        /// <summary>
        /// Creates a lane object under the road and seeds it with the road-wide default speed limit.
        /// </summary>
        private static AILane CreateLaneObject(Road road, int laneIndex)
        {
            GameObject laneObject = new GameObject($"Lane_{laneIndex}");
            Undo.RegisterCreatedObjectUndo(laneObject, "Generate Road Waypoints");
            laneObject.transform.SetParent(road.transform);
            laneObject.transform.localPosition = Vector3.zero;

            AILane lane = Undo.AddComponent<AILane>(laneObject);
            lane.laneSpeedLimit = road.speedLimitForAllLanes;
            EditorUtility.SetDirty(lane);
            return lane;
        }

        /// <summary>
        /// Creates waypoint objects for one lane and applies the lane-level settings to each waypoint.
        /// </summary>
        private static List<Transform> CreateWaypoints(AILane lane, IReadOnlyList<Vector3> lanePositions, float laneWidth)
        {
            Undo.RecordObject(lane, "Generate Road Waypoints");

            lane.waypoints.Clear();

            var createdWaypoints = new List<AIWaypoint>(lanePositions.Count);
            var waypointTransforms = new List<Transform>(lanePositions.Count);

            for (int waypointIndex = 0; waypointIndex < lanePositions.Count; waypointIndex++)
            {
                GameObject waypointObject = new GameObject($"Waypoint_{waypointIndex}");
                Undo.RegisterCreatedObjectUndo(waypointObject, "Generate Road Waypoints");
                waypointObject.transform.SetParent(lane.transform);
                waypointObject.transform.position = lanePositions[waypointIndex];
                
                // Align Waypoint Transform Z axis to face the next waypoint, or keep the last one's direction
                if (waypointIndex < lanePositions.Count - 1)
                {
                    Vector3 direction = (lanePositions[waypointIndex + 1] - lanePositions[waypointIndex]).normalized;
                    if (direction != Vector3.zero)
                    {
                        waypointObject.transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
                else if (waypointIndex > 0)
                {
                    // If it's the last waypoint, use the direction from the previous point
                    Vector3 direction = (lanePositions[waypointIndex] - lanePositions[waypointIndex - 1]).normalized;
                    if (direction != Vector3.zero)
                    {
                        waypointObject.transform.rotation = Quaternion.LookRotation(direction);
                    }
                }

                AIWaypoint waypoint = Undo.AddComponent<AIWaypoint>(waypointObject);
                WaypointSettings settings = waypoint.settings;
                settings.speed = lane.laneSpeedLimit;
                settings.vehicleType = lane.laneVehicleType;
                settings.LaneWidth = laneWidth;
                waypoint.settings = settings;

                lane.waypoints.Add(waypoint);
                createdWaypoints.Add(waypoint);
                waypointTransforms.Add(waypoint.transform);

                EditorUtility.SetDirty(waypoint);
            }

            LinkPrevAndNextWaypoints(createdWaypoints);
            EditorUtility.SetDirty(lane);

            return waypointTransforms;
        }

        /// <summary>
        /// Rebuilds the forward and backward waypoint links for a newly generated lane.
        /// </summary>
        private static void LinkPrevAndNextWaypoints(IReadOnlyList<AIWaypoint> waypoints)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                AIWaypoint waypoint = waypoints[i];
                WaypointSettings settings = waypoint.settings;
                settings.previousWaypoint = i > 0 ? CreateSingleWaypointLink(waypoints[i - 1]) : EmptyWaypointLinks;
                settings.nextWaypoint = i < waypoints.Count - 1 ? CreateSingleWaypointLink(waypoints[i + 1]) : EmptyWaypointLinks;
                settings.laneChangePoints = EmptyWaypointLinks;
                waypoint.settings = settings;
            }
        }

        /// <summary>
        /// Removes road-to-road connections before this road destroys and recreates its generated waypoints.
        /// </summary>
        private static void RemoveAttachedConnections(Road road, string undoLabel)
        {
            List<AIWaypoint> roadWaypoints = CollectRoadWaypoints(road);
            if (roadWaypoints.Count == 0)
                return;

            WaypointConnectionBuilder.RemoveConnectionsForWaypoints(roadWaypoints, undoLabel);
        }

        /// <summary>
        /// Ensures the generated collections exist before generation or cleanup code accesses them.
        /// </summary>
        private static void EnsureCollections(Road road)
        {
            road.generatedLanes ??= new List<List<Transform>>();
            road.laneObjects ??= new List<AILane>();
        }

        /// <summary>
        /// Collects all currently generated waypoints that belong to the provided road.
        /// </summary>
        private static List<AIWaypoint> CollectRoadWaypoints(Road road)
        {
            var roadWaypoints = new List<AIWaypoint>();
            if (road == null || road.laneObjects == null)
                return roadWaypoints;

            for (int laneIndex = 0; laneIndex < road.laneObjects.Count; laneIndex++)
            {
                AILane lane = road.laneObjects[laneIndex];
                if (lane == null || lane.waypoints == null)
                    continue;

                for (int waypointIndex = 0; waypointIndex < lane.waypoints.Count; waypointIndex++)
                {
                    AIWaypoint waypoint = lane.waypoints[waypointIndex];
                    if (waypoint != null)
                        roadWaypoints.Add(waypoint);
                }
            }

            return roadWaypoints;
        }

        /// <summary>
        /// Clears lane-change links from every waypoint on the road.
        /// </summary>
        private static void ClearLaneChangeLinks(Road road, string undoLabel)
        {
            EnsureCollections(road);
            Undo.RecordObject(road, undoLabel);

            for (int laneIndex = 0; laneIndex < road.laneObjects.Count; laneIndex++)
            {
                AILane lane = road.laneObjects[laneIndex];
                if (lane == null || lane.waypoints == null)
                    continue;

                for (int waypointIndex = 0; waypointIndex < lane.waypoints.Count; waypointIndex++)
                {
                    AIWaypoint waypoint = lane.waypoints[waypointIndex];
                    if (waypoint == null)
                        continue;

                    Undo.RecordObject(waypoint, undoLabel);
                    WaypointSettings settings = waypoint.settings;
                    settings.laneChangePoints = EmptyWaypointLinks;
                    waypoint.settings = settings;
                    EditorUtility.SetDirty(waypoint);
                }
            }
        }

        /// <summary>
        /// Returns whether two adjacent lanes travel in the same global direction.
        /// </summary>
        private static bool CanLinkAdjacentLanes(Road road, int currentLaneIndex, int adjacentLaneIndex, int laneCount)
        {
            if (currentLaneIndex < 0
                || adjacentLaneIndex < 0
                || currentLaneIndex >= road.laneObjects.Count
                || adjacentLaneIndex >= road.laneObjects.Count)
            {
                return false;
            }

            AILane currentLane = road.laneObjects[currentLaneIndex];
            AILane adjacentLane = road.laneObjects[adjacentLaneIndex];

            if (currentLane == null || adjacentLane == null || 
                currentLane.waypoints == null || adjacentLane.waypoints == null ||
                currentLane.waypoints.Count < 2 || adjacentLane.waypoints.Count < 2)
            {
                return false;
            }

            Vector3 currentDir = (currentLane.waypoints[currentLane.waypoints.Count - 1].transform.position - currentLane.waypoints[0].transform.position).normalized;
            Vector3 adjacentDir = (adjacentLane.waypoints[adjacentLane.waypoints.Count - 1].transform.position - adjacentLane.waypoints[0].transform.position).normalized;

            return Vector3.Dot(currentDir, adjacentDir) > 0.5f;
        }

        /// <summary>
        /// Builds lane-change links between two adjacent lanes using the configured waypoint-index offset.
        /// </summary>
        private static void LinkAdjacentLanes(Road road, AILane currentLane, AILane adjacentLane, int linkOffset)
        {
            if (road == null
                || currentLane == null
                || adjacentLane == null
                || currentLane.waypoints == null
                || adjacentLane.waypoints == null)
            {
                return;
            }

            int waypointCount = Mathf.Min(currentLane.waypoints.Count, adjacentLane.waypoints.Count);
            for (int waypointIndex = 0; waypointIndex < waypointCount; waypointIndex++)
            {
                AIWaypoint currentWaypoint = currentLane.waypoints[waypointIndex];
                int adjacentTargetIndex = FindOffsetLaneChangeTargetIndex(
                    road,
                    currentLane.waypoints,
                    adjacentLane.waypoints,
                    waypointIndex,
                    linkOffset);

                if (adjacentTargetIndex >= 0)
                    AddLaneChangePoint(currentWaypoint, adjacentLane.waypoints[adjacentTargetIndex]);

                int currentTargetIndex = FindOffsetLaneChangeTargetIndex(
                    road,
                    adjacentLane.waypoints,
                    currentLane.waypoints,
                    waypointIndex,
                    linkOffset);

                if (currentTargetIndex >= 0)
                    AddLaneChangePoint(adjacentLane.waypoints[waypointIndex], currentLane.waypoints[currentTargetIndex]);
            }
        }

        /// <summary>
        /// Returns the target waypoint index in the adjacent lane using a forward index offset and safety checks.
        /// </summary>
        private static int FindOffsetLaneChangeTargetIndex(
            Road road,
            IReadOnlyList<AIWaypoint> sourceWaypoints,
            IReadOnlyList<AIWaypoint> targetWaypoints,
            int sourceWaypointIndex,
            int linkOffset)
        {
            if (road == null
                || sourceWaypoints == null
                || targetWaypoints == null
                || sourceWaypointIndex < 0
                || sourceWaypointIndex >= sourceWaypoints.Count)
            {
                return -1;
            }

            AIWaypoint sourceWaypoint = sourceWaypoints[sourceWaypointIndex];
            if (sourceWaypoint == null)
                return -1;

            if (!CanLinkWaypointForLaneChange(road, sourceWaypoints, sourceWaypointIndex))
                return -1;

            if (!TryGetWaypointTravelDirection(sourceWaypoints, sourceWaypointIndex, out Vector3 sourceForward))
                return -1;

            int targetWaypointIndex = sourceWaypointIndex + linkOffset;
            if (targetWaypointIndex <= 0 || targetWaypointIndex >= targetWaypoints.Count)
                return -1;

            if (!CanLinkWaypointForLaneChange(road, targetWaypoints, targetWaypointIndex))
                return -1;

            AIWaypoint targetWaypoint = targetWaypoints[targetWaypointIndex];
            if (targetWaypoint == null)
                return -1;

            Vector3 laneChangeDirection = targetWaypoint.transform.position - sourceWaypoint.transform.position;
            if (!TryNormalizeDirection(laneChangeDirection, out Vector3 laneChangeDirectionNormalized))
                return -1;

            if (Vector3.Dot(sourceForward, laneChangeDirectionNormalized) <= MinLaneChangeForwardDot)
                return -1;

            return targetWaypointIndex;
        }

        /// <summary>
        /// Returns whether a waypoint is on a straight enough section to be used as a lane-change source or target.
        /// </summary>
        private static bool CanLinkWaypointForLaneChange(Road road, IReadOnlyList<AIWaypoint> waypoints, int waypointIndex)
        {
            if (road == null || waypoints == null || waypointIndex <= 0 || waypointIndex >= waypoints.Count - 1)
                return false;

            if (!TryGetWaypointTurnAngle(waypoints, waypointIndex, out float turnAngle))
            {
                return false;
            }

            return turnAngle <= GetMaxLaneChangeTurnAngle(road);
        }

        /// <summary>
        /// Returns the configured lane-change turn limit while preserving the legacy default for older road assets.
        /// </summary>
        private static float GetMaxLaneChangeTurnAngle(Road road)
        {
            if (road == null)
                return DefaultMaxLaneChangeTurnAngle;

            return road.laneChangeMaxTurnAngle > 0f
                ? road.laneChangeMaxTurnAngle
                : DefaultMaxLaneChangeTurnAngle;
        }

        /// <summary>
        /// Measures the local turn angle around a waypoint using its previous and next neighbors.
        /// </summary>
        private static bool TryGetWaypointTurnAngle(IReadOnlyList<AIWaypoint> waypoints, int waypointIndex, out float turnAngle)
        {
            turnAngle = 0f;

            AIWaypoint previousWaypoint = waypoints[waypointIndex - 1];
            AIWaypoint currentWaypoint = waypoints[waypointIndex];
            AIWaypoint nextWaypoint = waypoints[waypointIndex + 1];

            if (previousWaypoint == null || currentWaypoint == null || nextWaypoint == null)
                return false;

            if (!TryGetWaypointDirection(previousWaypoint.transform.position, currentWaypoint.transform.position, out Vector3 incomingDirection)
                || !TryGetWaypointDirection(currentWaypoint.transform.position, nextWaypoint.transform.position, out Vector3 outgoingDirection))
            {
                return false;
            }

            turnAngle = Vector3.Angle(incomingDirection, outgoingDirection);
            return true;
        }

        /// <summary>
        /// Builds a normalized travel direction from two waypoint positions while handling near-vertical segments.
        /// </summary>
        private static bool TryGetWaypointDirection(Vector3 start, Vector3 end, out Vector3 direction)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(end - start, Vector3.up);
            if (planarDirection.sqrMagnitude >= MinDirectionSqrMagnitude)
            {
                direction = planarDirection.normalized;
                return true;
            }

            Vector3 worldDirection = end - start;
            if (worldDirection.sqrMagnitude >= MinDirectionSqrMagnitude)
            {
                direction = worldDirection.normalized;
                return true;
            }

            direction = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Returns the best forward travel direction for a waypoint using its neighbors.
        /// </summary>
        private static bool TryGetWaypointTravelDirection(IReadOnlyList<AIWaypoint> waypoints, int waypointIndex, out Vector3 direction)
        {
            direction = Vector3.zero;

            if (waypoints == null || waypointIndex < 0 || waypointIndex >= waypoints.Count)
                return false;

            AIWaypoint currentWaypoint = waypoints[waypointIndex];
            if (currentWaypoint == null)
                return false;

            if (waypointIndex < waypoints.Count - 1 && waypoints[waypointIndex + 1] != null)
                return TryGetWaypointDirection(currentWaypoint.transform.position, waypoints[waypointIndex + 1].transform.position, out direction);

            if (waypointIndex > 0 && waypoints[waypointIndex - 1] != null)
                return TryGetWaypointDirection(waypoints[waypointIndex - 1].transform.position, currentWaypoint.transform.position, out direction);

            return false;
        }

        /// <summary>
        /// Normalizes a direction vector using the horizontal plane first and a full 3D fallback if needed.
        /// </summary>
        private static bool TryNormalizeDirection(Vector3 direction, out Vector3 normalizedDirection)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planarDirection.sqrMagnitude >= MinDirectionSqrMagnitude)
            {
                normalizedDirection = planarDirection.normalized;
                return true;
            }

            if (direction.sqrMagnitude >= MinDirectionSqrMagnitude)
            {
                normalizedDirection = direction.normalized;
                return true;
            }

            normalizedDirection = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Appends a lane-change target to a waypoint while avoiding duplicate links.
        /// </summary>
        private static void AddLaneChangePoint(AIWaypoint waypoint, AIWaypoint laneChangeTarget)
        {
            if (waypoint == null || laneChangeTarget == null)
                return;

            WaypointSettings settings = waypoint.settings;
            if (ContainsWaypoint(settings.laneChangePoints, laneChangeTarget))
                return;

            int existingCount = settings.laneChangePoints != null ? settings.laneChangePoints.Length : 0;
            AIWaypoint[] laneChangePoints = new AIWaypoint[existingCount + 1];

            for (int i = 0; i < existingCount; i++)
            {
                laneChangePoints[i] = settings.laneChangePoints[i];
            }

            laneChangePoints[existingCount] = laneChangeTarget;

            Undo.RecordObject(waypoint, "Link Lanes");
            settings.laneChangePoints = laneChangePoints;
            waypoint.settings = settings;
            EditorUtility.SetDirty(waypoint);
        }

        /// <summary>
        /// Returns whether the candidate waypoint already exists inside the provided waypoint array.
        /// </summary>
        private static bool ContainsWaypoint(IReadOnlyList<AIWaypoint> waypoints, AIWaypoint candidate)
        {
            if (waypoints == null || candidate == null)
                return false;

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == candidate)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Clears the generated waypoint objects that belong to a single lane.
        /// </summary>
        private static void ClearLaneWaypoints(AILane lane)
        {
            if (lane == null)
                return;

            Undo.RecordObject(lane, "Clear Road Waypoints");

            for (int i = lane.waypoints.Count - 1; i >= 0; i--)
            {
                AIWaypoint waypoint = lane.waypoints[i];
                if (waypoint != null)
                    Undo.DestroyObjectImmediate(waypoint.gameObject);
            }

            for (int i = lane.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = lane.transform.GetChild(i);
                if (child != null && child.TryGetComponent<AIWaypoint>(out _))
                    Undo.DestroyObjectImmediate(child.gameObject);
            }

            lane.waypoints.Clear();
            EditorUtility.SetDirty(lane);
        }

        /// <summary>
        /// Grows or shrinks the generated lane object list so it matches the road lane count.
        /// </summary>
        private static void EnsureLaneObjectsMatchRoad(Road road)
        {
            EnsureCollections(road);

            for (int laneIndex = road.laneObjects.Count - 1; laneIndex >= road.lanes; laneIndex--)
            {
                DestroyLaneObject(road, laneIndex);
            }

            for (int laneIndex = 0; laneIndex < road.lanes; laneIndex++)
            {
                AILane lane = laneIndex < road.laneObjects.Count ? road.laneObjects[laneIndex] : null;
                if (lane == null)
                {
                    lane = CreateLaneObject(road, laneIndex);
                    if (laneIndex < road.laneObjects.Count)
                        road.laneObjects[laneIndex] = lane;
                    else
                        road.laneObjects.Add(lane);
                }

                lane.gameObject.name = $"Lane_{laneIndex}";

                EditorUtility.SetDirty(lane);
            }
        }

        /// <summary>
        /// Destroys one generated lane object and removes it from the road lane list.
        /// </summary>
        private static void DestroyLaneObject(Road road, int laneIndex)
        {
            AILane lane = road.laneObjects[laneIndex];
            if (lane != null)
            {
                ClearLaneWaypoints(lane);
                Undo.DestroyObjectImmediate(lane.gameObject);
            }

            road.laneObjects.RemoveAt(laneIndex);
        }

        /// <summary>
        /// Returns the lateral world-space offset for a lane index around the road centerline.
        /// </summary>
        private static float GetLaneOffset(int laneIndex, int laneCount, float laneWidth)
        {
            return (laneIndex - (laneCount - 1) / 2f) * laneWidth;
        }

        /// <summary>
        /// Creates a single-element waypoint array used by serialized prev/next lane links.
        /// </summary>
        private static AIWaypoint[] CreateSingleWaypointLink(AIWaypoint waypoint)
        {
            return new[] { waypoint };
        }

        /// <summary>
        /// Projects a lane sample onto the ground using colliders when available.
        /// </summary>
        private static Vector3 ProjectOntoGround(Vector3 worldPosition)
        {
            Vector3 origin = worldPosition + Vector3.up * GroundProjectionLift;

            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    GroundProjectionDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return worldPosition;
        }

        /// <summary>
        /// Returns a stable right vector for lane offsetting based on the spline forward direction.
        /// </summary>
        private static Vector3 GetRightSide(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < MinDirectionSqrMagnitude)
                right = Vector3.Cross(Vector3.forward, forward);
            if (right.sqrMagnitude < MinDirectionSqrMagnitude)
                right = Vector3.right;

            return right.normalized;
        }

        /// <summary>
        /// Returns whether a lane offset should follow the spline order or be reversed based on driving direction.
        /// <summary>
        /// Determines whether the forward travel direction of the given lane follows or opposes the spline's sample order.
        /// </summary>
        private static bool LaneTravelsWithSpline(Road road, float laneOffset)
        {
            if (Mathf.Abs(laneOffset) < 0.001f)
                return road.drivingDirection == DrivingDirection.Left;

            return road.drivingDirection == DrivingDirection.Left
                ? laneOffset <= 0.001f
                : laneOffset > -0.001f;
        }
    }
}
