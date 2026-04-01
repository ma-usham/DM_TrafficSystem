using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public static class RoadBuilder
    {
        private const float GroundProjectionLift = 500f;
        private const float GroundProjectionDistance = 3000f;
        private const float MinDirectionSqrMagnitude = 0.001f;
        private const float MaxLaneChangeTurnAngle = 20f;

        public static void ClearGeneratedWaypoints(Road road)
        {
            if (road == null)
                return;

            EnsureCollections(road);
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

                LinkAdjacentLanes(road.laneObjects[laneIndex], road.laneObjects[laneIndex + 1]);
            }

            EditorUtility.SetDirty(road);
            Undo.CollapseUndoOperations(undoGroup);
        }

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

        private static void BuildLane(
            Road road,
            int laneIndex,
            IReadOnlyList<Vector3> centerPositions,
            IReadOnlyList<Vector3> tangents)
        {
            float laneOffset = GetLaneOffset(laneIndex, road.lanes, road.laneWidth);
            List<Vector3> lanePositions = BuildLanePositions(road, centerPositions, tangents, laneOffset);

            if (!LaneTravelsWithSpline(road, laneOffset))
                lanePositions.Reverse();

            AILane lane = road.laneObjects[laneIndex];
            road.generatedLanes.Add(CreateWaypoints(lane, lanePositions));
        }

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

        private static List<Transform> CreateWaypoints(AILane lane, IReadOnlyList<Vector3> lanePositions)
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

                AIWaypoint waypoint = Undo.AddComponent<AIWaypoint>(waypointObject);
                WaypointSettings settings = waypoint.settings;
                settings.speed = lane.laneSpeedLimit;
                settings.vehicleType = lane.laneVehicleType;
                waypoint.settings = settings;

                lane.waypoints.Add(waypoint);
                createdWaypoints.Add(waypoint);
                waypointTransforms.Add(waypoint.transform);

                EditorUtility.SetDirty(waypoint);
            }

            LinkWaypoints(createdWaypoints);
            EditorUtility.SetDirty(lane);

            return waypointTransforms;
        }

        private static void LinkWaypoints(IReadOnlyList<AIWaypoint> waypoints)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                AIWaypoint waypoint = waypoints[i];
                WaypointSettings settings = waypoint.settings;
                settings.previousWaypoint = i > 0 ? new AIWaypoint[] { waypoints[i - 1] } : new AIWaypoint[0];
                settings.nextWaypoint = i < waypoints.Count - 1 ? new AIWaypoint[] { waypoints[i + 1] } : new AIWaypoint[0];
                settings.laneChangePoints = new AIWaypoint[0];
                waypoint.settings = settings;
            }
        }

        private static void EnsureCollections(Road road)
        {
            road.generatedLanes ??= new List<List<Transform>>();
            road.laneObjects ??= new List<AILane>();
        }

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
                    settings.laneChangePoints = new AIWaypoint[0];
                    waypoint.settings = settings;
                    EditorUtility.SetDirty(waypoint);
                }
            }
        }

        private static bool CanLinkAdjacentLanes(Road road, int currentLaneIndex, int adjacentLaneIndex, int laneCount)
        {
            if (currentLaneIndex < 0
                || adjacentLaneIndex < 0
                || currentLaneIndex >= road.laneObjects.Count
                || adjacentLaneIndex >= road.laneObjects.Count)
            {
                return false;
            }

            float currentLaneOffset = GetLaneOffset(currentLaneIndex, laneCount, road.laneWidth);
            float adjacentLaneOffset = GetLaneOffset(adjacentLaneIndex, laneCount, road.laneWidth);

            return LaneTravelsWithSpline(road, currentLaneOffset) == LaneTravelsWithSpline(road, adjacentLaneOffset);
        }

        private static void LinkAdjacentLanes(AILane currentLane, AILane adjacentLane)
        {
            if (currentLane == null
                || adjacentLane == null
                || currentLane.waypoints == null
                || adjacentLane.waypoints == null)
            {
                return;
            }

            int waypointCount = Mathf.Min(currentLane.waypoints.Count, adjacentLane.waypoints.Count);
            for (int waypointIndex = 0; waypointIndex < waypointCount; waypointIndex++)
            {
                if (!CanLinkWaypointForLaneChange(currentLane.waypoints, waypointIndex)
                    || !CanLinkWaypointForLaneChange(adjacentLane.waypoints, waypointIndex))
                {
                    continue;
                }

                AIWaypoint currentWaypoint = currentLane.waypoints[waypointIndex];
                AIWaypoint adjacentWaypoint = adjacentLane.waypoints[waypointIndex];
                AddLaneChangePoint(currentWaypoint, adjacentWaypoint);
                AddLaneChangePoint(adjacentWaypoint, currentWaypoint);
            }
        }

        private static bool CanLinkWaypointForLaneChange(IReadOnlyList<AIWaypoint> waypoints, int waypointIndex)
        {
            if (waypoints == null || waypointIndex <= 0 || waypointIndex >= waypoints.Count - 1)
                return false;

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

            return Vector3.Angle(incomingDirection, outgoingDirection) <= MaxLaneChangeTurnAngle;
        }

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

        private static float GetLaneOffset(int laneIndex, int laneCount, float laneWidth)
        {
            return (laneIndex - (laneCount - 1) / 2f) * laneWidth;
        }

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

        private static Vector3 GetRightSide(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < MinDirectionSqrMagnitude)
                right = Vector3.Cross(Vector3.forward, forward);
            if (right.sqrMagnitude < MinDirectionSqrMagnitude)
                right = Vector3.right;

            return right.normalized;
        }

        private static bool LaneTravelsWithSpline(Road road, float laneOffset)
        {
            if (Mathf.Abs(laneOffset) < 0.001f)
                return road.drivingDirection == DrivingDirection.Left;

            return road.drivingDirection == DrivingDirection.Left
                ? laneOffset < 0f
                : laneOffset > 0f;
        }
    }
}
