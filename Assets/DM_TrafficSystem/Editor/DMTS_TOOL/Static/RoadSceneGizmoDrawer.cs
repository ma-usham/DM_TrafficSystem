using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Centralizes passive road and connection Scene view drawing shared across editor tools.
    /// </summary>
    public static class RoadSceneGizmoDrawer
    {
        private static readonly Color LaneChangeLineColor = new Color(1f, 0.45f, 0.1f, 0.9f);
        private const float LaneChangeLineScreenSize = 4f;
        private const float ConnectionGizmoScreenSize = 5f;

        /// <summary>
        /// Draws the road spline using the provided color and line width.
        /// </summary>
        public static void DrawRoadCurve(Road road, Color curveColor, float curveWidth)
        {
            if (road == null || road.controlPointsList == null || road.controlPointsList.Count < 2)
                return;

            Color previousColor = Handles.color;

            for (int i = 0; i < road.controlPointsList.Count - 1; i++)
            {
                Vector3 start = road.controlPointsList[i];
                Vector3 end = road.controlPointsList[i + 1];
                SplineMathUtils.GetSegmentHandles(road.controlPointsList, i, out Vector3 handle1, out Vector3 handle2);
                Handles.DrawBezier(start, end, handle1, handle2, curveColor, null, curveWidth);
            }

            Handles.color = previousColor;
        }

        /// <summary>
        /// Draws the editable control points for one road.
        /// </summary>
        public static void DrawControlPoints(Road road)
        {
            if (road == null || road.controlPointsList == null || road.controlPointsList.Count == 0)
                return;

            Color previousColor = Handles.color;
            Handles.color = DMTSPrefs.ControlPointColor;

            for (int i = 0; i < road.controlPointsList.Count; i++)
            {
                Handles.SphereHandleCap(
                    0,
                    road.controlPointsList[i],
                    Quaternion.identity,
                    DMTSPrefs.ControlPointHandleSize * 2f,
                    EventType.Repaint);
            }

            Handles.color = previousColor;
        }

        /// <summary>
        /// Draws road labels at the first and last control points.
        /// </summary>
        public static void DrawRoadLabels(Road road)
        {
            if (road == null || road.controlPointsList == null || road.controlPointsList.Count == 0)
                return;

            string roadName = road.gameObject.name;
            Vector3 firstPoint = road.controlPointsList[0];
            Handles.Label(firstPoint, roadName, EditorStyles.whiteMiniLabel);

            if (road.controlPointsList.Count > 1)
            {
                Vector3 lastPoint = road.controlPointsList[road.controlPointsList.Count - 1];
                Handles.Label(lastPoint, roadName, EditorStyles.whiteMiniLabel);
            }
        }

        /// <summary>
        /// Draws generated waypoint lines, direction arrows, and optional lane-change links.
        /// </summary>
        public static void DrawGeneratedWaypointGizmos(Road road, bool drawWaypoints, bool drawLaneChangeLinks)
        {
            if ((!drawWaypoints && !drawLaneChangeLinks)
                || road == null
                || road.laneObjects == null
                || road.laneObjects.Count == 0)
            {
                return;
            }

            Color previousColor = Handles.color;
            HashSet<ulong> drawnLaneChangeLines = drawLaneChangeLinks ? new HashSet<ulong>() : null;

            foreach (AILane lane in road.laneObjects)
            {
                if (lane == null || lane.waypoints == null || lane.waypoints.Count == 0)
                    continue;

                IReadOnlyList<AIWaypoint> waypoints = lane.waypoints;
                int count = waypoints.Count;
                var waypointPositions = new Vector3[count];

                for (int i = 0; i < count; i++)
                {
                    if (waypoints[i] != null)
                        waypointPositions[i] = waypoints[i].transform.position;
                }

                if (drawWaypoints)
                {
                    if (count > 1)
                    {
                        Handles.color = DMTSPrefs.WaypointLineColor;
                        Handles.DrawPolyLine(waypointPositions);
                    }

                    Handles.color = DMTSPrefs.WaypointColor;
                    float baseSize = HandleUtility.GetHandleSize(waypointPositions[count / 2]) * DMTSPrefs.WaypointSizeMultiplier;
                    var batchedArrowLines = new List<Vector3>(count * 4);

                    for (int i = 0; i < count; i++)
                    {
                        Vector3 position = waypointPositions[i];
                        DrawDirectionArrow(position, GetWaypointForward(waypointPositions, i), baseSize, batchedArrowLines);
                    }

                    if (batchedArrowLines.Count > 0)
                        Handles.DrawLines(batchedArrowLines.ToArray());
                }

                if (drawLaneChangeLinks)
                    DrawLaneChangeGizmos(waypoints, drawnLaneChangeLines);
            }

            Handles.color = previousColor;
        }

        /// <summary>
        /// Draws passive dotted connection lines between linked road endpoints.
        /// </summary>
        public static void DrawRoadConnections(IReadOnlyList<ConnectRoadToolState.ConnectionRecord> connectionRecords)
        {
            if (connectionRecords == null)
                return;

            Color previousColor = Handles.color;
            Handles.color = DMTSPrefs.ConnectRoadExistingConnectionColor;

            for (int i = 0; i < connectionRecords.Count; i++)
            {
                ConnectRoadToolState.ConnectionRecord connection = connectionRecords[i];
                if (connection.sourceWaypoint == null || connection.targetWaypoint == null)
                    continue;

                if (connection.connection != null)
                {
                    DrawConnectionCurve(
                        connection.connection,
                        DMTSPrefs.ConnectRoadExistingConnectionColor,
                        DMTSPrefs.ConnectRoadCurveWidth);
                    continue;
                }

                Handles.DrawDottedLine(
                    connection.sourceWaypoint.transform.position,
                    connection.targetWaypoint.transform.position,
                    ConnectionGizmoScreenSize);
            }

            Handles.color = previousColor;
        }

        /// <summary>
        /// Draws one connection spline using its stored editable control points.
        /// </summary>
        public static void DrawConnectionCurve(AIWaypointConnection connection, Color color, float lineWidth)
        {
            if (connection == null)
                return;

            List<Vector3> curvePoints = GetConnectionCurvePoints(connection);
            if (curvePoints.Count >= 2)
            {
                Color previousColor = Handles.color;
                Handles.color = color;
                Handles.DrawAAPolyLine(lineWidth, curvePoints.ToArray());
                Handles.color = previousColor;
                return;
            }

            if (connection.sourceWaypoint != null && connection.targetWaypoint != null)
            {
                Color previousColor = Handles.color;
                Handles.color = color;
                Handles.DrawDottedLine(
                    connection.sourceWaypoint.transform.position,
                    connection.targetWaypoint.transform.position,
                    ConnectionGizmoScreenSize);
                Handles.color = previousColor;
            }
        }

        /// <summary>
        /// Draws the generated transition waypoints that belong to one connection object.
        /// </summary>
        public static void DrawConnectionTransitionWaypoints(AIWaypointConnection connection)
        {
            if (connection == null || connection.transitionWaypoints == null || connection.transitionWaypoints.Count == 0)
                return;

            Color previousColor = Handles.color;
            Handles.color = DMTSPrefs.ConnectRoadTransitionWaypointColor;

            for (int i = 0; i < connection.transitionWaypoints.Count; i++)
            {
                AIWaypoint waypoint = connection.transitionWaypoints[i];
                if (waypoint == null)
                    continue;

                Vector3 position = waypoint.transform.position;
                float size = HandleUtility.GetHandleSize(position) * DMTSPrefs.WaypointSizeMultiplier * 0.9f;
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
            }

            Handles.color = previousColor;
        }

        /// <summary>
        /// Returns the sampled scene points used to render one connection spline.
        /// </summary>
        public static List<Vector3> GetConnectionCurvePoints(AIWaypointConnection connection)
        {
            if (connection == null || connection.controlPointsList == null || connection.controlPointsList.Count < 2)
                return new List<Vector3>();

            connection.SyncEndpointControlPoints();
            return SplineMathUtils.GetCurvePoints(
                connection.controlPointsList,
                Mathf.Max(4, connection.curveResolution));
        }

        /// <summary>
        /// Returns the best forward direction for a waypoint based on its neighbors in the sampled lane.
        /// </summary>
        private static Vector3 GetWaypointForward(IReadOnlyList<Vector3> waypointPositions, int waypointIndex)
        {
            if (waypointPositions == null || waypointPositions.Count == 0)
                return Vector3.forward;

            Vector3 position = waypointPositions[waypointIndex];
            if (waypointIndex < waypointPositions.Count - 1)
                return (waypointPositions[waypointIndex + 1] - position).normalized;

            if (waypointIndex > 0)
                return (position - waypointPositions[waypointIndex - 1]).normalized;

            return Vector3.forward;
        }

        /// <summary>
        /// Appends the line segments needed to draw one direction arrow into a shared batch list.
        /// </summary>
        private static void DrawDirectionArrow(Vector3 position, Vector3 forward, float size, List<Vector3> batchedLines)
        {
            Vector3 right = GetArrowRight(forward);
            float headLength = size * 0.7f;
            float headWidth = size * 0.4f;
            Vector3 tip = position;
            Vector3 headBase = tip - forward * headLength;

            batchedLines.Add(tip);
            batchedLines.Add(headBase + right * headWidth);
            batchedLines.Add(tip);
            batchedLines.Add(headBase - right * headWidth);
        }

        /// <summary>
        /// Returns a stable right vector for one arrow head based on its forward direction.
        /// </summary>
        private static Vector3 GetArrowRight(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(Vector3.forward, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;

            return right.normalized;
        }

        /// <summary>
        /// Draws dotted lateral links between waypoints that can lane-change to each other.
        /// </summary>
        private static void DrawLaneChangeGizmos(IReadOnlyList<AIWaypoint> waypoints, HashSet<ulong> drawnLaneChangeLines)
        {
            if (waypoints == null || drawnLaneChangeLines == null)
                return;

            for (int waypointIndex = 0; waypointIndex < waypoints.Count; waypointIndex++)
            {
                AIWaypoint waypoint = waypoints[waypointIndex];
                if (waypoint == null || waypoint.settings.laneChangePoints == null)
                    continue;

                for (int laneChangeIndex = 0; laneChangeIndex < waypoint.settings.laneChangePoints.Length; laneChangeIndex++)
                {
                    AIWaypoint laneChangeTarget = waypoint.settings.laneChangePoints[laneChangeIndex];
                    if (laneChangeTarget == null)
                        continue;

                    ulong laneChangeKey = GetLaneChangeKey(waypoint, laneChangeTarget);
                    if (!drawnLaneChangeLines.Add(laneChangeKey))
                        continue;

                    Handles.color = LaneChangeLineColor;
                    Handles.DrawDottedLine(
                        waypoint.transform.position,
                        laneChangeTarget.transform.position,
                        LaneChangeLineScreenSize);
                }
            }
        }

        /// <summary>
        /// Builds an order-independent key so one lane-change link is only drawn once.
        /// </summary>
        private static ulong GetLaneChangeKey(AIWaypoint firstWaypoint, AIWaypoint secondWaypoint)
        {
            uint firstId = unchecked((uint)RuntimeHelpers.GetHashCode(firstWaypoint));
            uint secondId = unchecked((uint)RuntimeHelpers.GetHashCode(secondWaypoint));

            if (firstId > secondId)
            {
                uint temp = firstId;
                firstId = secondId;
                secondId = temp;
            }

            return ((ulong)firstId << 32) | secondId;
        }
    }

    /// <summary>
    /// Provides shared scene-view drawing and waypoint picking helpers for intersection tools.
    /// </summary>
    public static class IntersectionSceneUtility
    {
        private const float StopPointHalfExtent = 0.35f;
        private const float WaypointPickRadius = 0.5f;
        private const float MaxWaypointPickScreenDistance = 10f;

        /// <summary>
        /// Draws one flat stop-point marker at the waypoint position.
        /// </summary>
        public static void DrawStopPoint(AIWaypoint waypoint, Color fillColor, Color outlineColor)
        {
            if (waypoint == null)
                return;

            Vector3 position = waypoint.transform.position;
            Vector3[] corners =
            {
                position + new Vector3(-StopPointHalfExtent, 0f, -StopPointHalfExtent),
                position + new Vector3(StopPointHalfExtent, 0f, -StopPointHalfExtent),
                position + new Vector3(StopPointHalfExtent, 0f, StopPointHalfExtent),
                position + new Vector3(-StopPointHalfExtent, 0f, StopPointHalfExtent)
            };

            Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);
        }

        /// <summary>
        /// Draws one marker for each provided stop-point waypoint.
        /// </summary>
        public static void DrawStopPoints(IEnumerable<AIWaypoint> stopPoints, Color fillColor, Color outlineColor)
        {
            if (stopPoints == null)
                return;

            foreach (AIWaypoint waypoint in stopPoints)
            {
                DrawStopPoint(waypoint, fillColor, outlineColor);
            }
        }

        /// <summary>
        /// Returns the waypoint nearest to the mouse cursor in scene-view screen space.
        /// </summary>
        public static AIWaypoint FindWaypointAtMouse(Vector2 mousePosition)
        {
            AIWaypoint bestMatch = null;
            float closestDistance = float.MaxValue;
            AIWaypoint[] allWaypoints = Object.FindObjectsByType<AIWaypoint>(FindObjectsInactive.Exclude);

            for (int i = 0; i < allWaypoints.Length; i++)
            {
                AIWaypoint waypoint = allWaypoints[i];
                if (waypoint == null)
                    continue;

                float distanceToWaypoint = HandleUtility.DistanceToCircle(waypoint.transform.position, WaypointPickRadius);
                if (distanceToWaypoint <= MaxWaypointPickScreenDistance && distanceToWaypoint < closestDistance)
                {
                    closestDistance = distanceToWaypoint;
                    bestMatch = waypoint;
                }
            }

            return bestMatch;
        }
    }
}
