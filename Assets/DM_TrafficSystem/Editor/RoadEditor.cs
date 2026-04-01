using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws the road inspector plus scene gizmos for spline editing and generated waypoints.
    /// </summary>
    [CustomEditor(typeof(Road))]
    public class RoadEditor : UnityEditor.Editor
    {
        private static readonly Color LaneChangeLineColor = new Color(1f, 0.45f, 0.1f, 0.9f);
        private const float LaneChangeLineScreenSize = 4f;

        private Road road => (Road)target;

        /// <summary>
        /// Draws the default road inspector and a shortcut into the custom traffic system window.
        /// </summary>
        public override void OnInspectorGUI()
        {
            RoadSettingsPanel.DrawHelpBox();
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Traffic System Window", GUILayout.Height(24)))
            {
                DMTS_Window.ShowWindow(new CreateRoadPage(road));
            }
            //SceneView.RepaintAll();
        }

        /// <summary>
        /// Draws the spline and control points in the scene when the dedicated create-road page is not active.
        /// </summary>
        private void OnSceneGUI()
        {
            if (CreateRoadPage.isActive)
                return;

            var pts = road.controlPointsList;
            if (pts.Count < 2)
                return;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[i + 1];
                SplineMathUtils.GetSegmentHandles(road.controlPointsList, i, out Vector3 h1, out Vector3 h2);
                //Creator.GetSegmentHandles(i, out Vector3 h1, out Vector3 h2);
                Handles.DrawBezier(a, b, h1, h2, DMTSPrefs.CurveColor, null, DMTSPrefs.CurveWidth);
            }

            Handles.color = DMTSPrefs.ControlPointColor;
            for (int i = 0; i < pts.Count; i++)
            {
                Handles.SphereHandleCap(0, pts[i], Quaternion.identity,
                    DMTSPrefs.ControlPointHandleSize * 2f, EventType.Repaint);

            }
        }

        #region Gizmos
        /// <summary>
        /// Draws generated waypoint lines, arrow heads, and lane-change links for the selected road.
        /// </summary>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGeneratedWaypointGizmos(Road road, GizmoType gizmoType)
        {
            if (road.laneObjects == null || road.laneObjects.Count == 0)
                return;

            Color previousColor = Handles.color;
            var drawnLaneChangeLines = new HashSet<ulong>();

            foreach (var lane in road.laneObjects)
            {
                if (lane == null || lane.waypoints == null || lane.waypoints.Count == 0) continue;

                var waypoints = lane.waypoints;
                int count = waypoints.Count;

                Vector3[] waypointPosition = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    if (waypoints[i] != null)
                        waypointPosition[i] = waypoints[i].transform.position;
                }

                if (count > 1)
                {
                    Handles.color = DMTSPrefs.WaypointLineColor;
                    Handles.DrawPolyLine(waypointPosition);
                }

                Handles.color = DMTSPrefs.WaypointColor;

                float baseSize = HandleUtility.GetHandleSize(waypointPosition[count / 2]) * DMTSPrefs.WaypointSizeMultiplier;

                List<Vector3> batchedArrowLines = new List<Vector3>(count * 6);

                for (int i = 0; i < count; i++)
                {
                    Vector3 position = waypointPosition[i];
                    DrawDirectionArrow(position, GetWaypointForward(waypointPosition, i), baseSize, batchedArrowLines);
                }

                if (batchedArrowLines.Count > 0)
                    Handles.DrawLines(batchedArrowLines.ToArray());

                DrawLaneChangeGizmos(waypoints, drawnLaneChangeLines);
            }

            Handles.color = previousColor;
        }

        /// <summary>
        /// Returns the travel direction used to orient the arrow head for one waypoint.
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
        /// Adds only the arrow-head lines for one waypoint, with the tip anchored exactly on the waypoint.
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
        /// Returns a stable right vector for the arrow head even when the waypoint direction is nearly vertical.
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
        /// Draws dotted lane-change links while avoiding duplicate lines between reciprocal waypoint pairs.
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
        /// Builds an order-independent key so the same lane-change pair is only drawn once.
        /// </summary>
        private static ulong GetLaneChangeKey(AIWaypoint firstWaypoint, AIWaypoint secondWaypoint)
        {
            uint firstId = unchecked((uint)firstWaypoint.GetInstanceID());
            uint secondId = unchecked((uint)secondWaypoint.GetInstanceID());

            if (firstId > secondId)
            {
                uint temp = firstId;
                firstId = secondId;
                secondId = temp;
            }

            return ((ulong)firstId << 32) | secondId;
        }

#endregion
    }
}
