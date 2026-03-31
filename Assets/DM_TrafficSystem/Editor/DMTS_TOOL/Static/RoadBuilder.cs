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
                if (lane != null)
                    Undo.DestroyObjectImmediate(lane.gameObject);
            }

            road.laneObjects.Clear();

            for (int i = road.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = road.transform.GetChild(i);
                if (child != null && child.TryGetComponent<AILane>(out _))
                    Undo.DestroyObjectImmediate(child.gameObject);
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

            for (int laneIndex = 0; laneIndex < road.lanes; laneIndex++)
            {
                BuildLane(road, laneIndex, centerPositions, tangents);
            }

            EditorUtility.SetDirty(road);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void BuildLane(
            Road road,
            int laneIndex,
            IReadOnlyList<Vector3> centerPositions,
            IReadOnlyList<Vector3> tangents)
        {
            float laneOffset = (laneIndex - (road.lanes - 1) / 2f) * road.laneWidth;
            List<Vector3> lanePositions = BuildLanePositions(road, centerPositions, tangents, laneOffset);

            if (!LaneTravelsWithSpline(road, laneOffset))
                lanePositions.Reverse();

            AILane lane = CreateLaneObject(road, laneIndex);
            road.laneObjects.Add(lane);
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
            var createdWaypoints = new List<AIWaypoint>(lanePositions.Count);
            var waypointTransforms = new List<Transform>(lanePositions.Count);

            for (int waypointIndex = 0; waypointIndex < lanePositions.Count; waypointIndex++)
            {
                GameObject waypointObject = new GameObject($"Waypoint_{waypointIndex}");
                Undo.RegisterCreatedObjectUndo(waypointObject, "Generate Road Waypoints");
                waypointObject.transform.SetParent(lane.transform);
                waypointObject.transform.position = lanePositions[waypointIndex];

                AIWaypoint waypoint = Undo.AddComponent<AIWaypoint>(waypointObject);
                waypoint.settings.speed = lane.laneSpeedLimit;

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
                settings.previousWaypoint = i > 0 ? waypoints[i - 1] : null;
                settings.nextWaypoint = i < waypoints.Count - 1 ? waypoints[i + 1] : null;
                waypoint.settings = settings;

                EditorUtility.SetDirty(waypoint);
            }
        }

        private static void EnsureCollections(Road road)
        {
            road.generatedLanes ??= new List<List<Transform>>();
            road.laneObjects ??= new List<AILane>();
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
