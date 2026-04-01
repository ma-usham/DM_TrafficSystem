using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(Road))]
    public class RoadEditor : UnityEditor.Editor
    {
        private static readonly Color LaneChangeLineColor = new Color(1f, 0.45f, 0.1f, 0.9f);
        private const float LaneChangeLineScreenSize = 4f;

        private Road road => (Road)target;


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

        private void OnSceneGUI() //This just shows the bezier curve and control points of that road object int he scene.
        {
            if (CreateRoadPage.isActive) return;

            var pts = road.controlPointsList;
            if (pts.Count < 2) return;

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
        [DrawGizmo(GizmoType.Selected)] //This will only draw the gizmos when the object is selected, which can help reduce clutter in the scene view.
        private static void DrawGeneratedWaypointGizmos(Road road, GizmoType gizmoType) // This function is automatically called by Unity to draw gizmos in the scene view. It will draw arrows at each waypoint to indicate direction, and lines between waypoints to show the path.
        {
            if (road.laneObjects == null || road.laneObjects.Count == 0) return;

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
                
                // Calculate size once per lane to save performance 
                float baseSize = HandleUtility.GetHandleSize(waypointPosition[count / 2]) * DMTSPrefs.WaypointSizeMultiplier;

                List<Vector3> batchedArrowLines = new List<Vector3>(count * 6);

                for (int i = 0; i < count; i++)
                {
                    Vector3 position = waypointPosition[i];
                    Vector3 forward;
                    
                    if (i < count - 1)
                    {
                        forward = (waypointPosition[i + 1] - position).normalized;
                    }
                    else if (i > 0)
                    {
                        forward = (position - waypointPosition[i - 1]).normalized;
                    }
                    else
                    {
                        forward = Vector3.forward;
                    }

                    DrawDirectionArrow(position, forward, baseSize, batchedArrowLines);
                }

                Handles.DrawLines(batchedArrowLines.ToArray());
                DrawLaneChangeGizmos(waypoints, drawnLaneChangeLines);
            }

            Handles.color = previousColor;
        }

        private static void DrawDirectionArrow(Vector3 position, Vector3 forward, float size, List<Vector3> batchedLines)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(Vector3.forward, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            float shaftLength = size * 1.3f;
            float headLength = size * 0.55f;
            float headWidth = size * 0.4f;

            Vector3 tail = position - forward * shaftLength * 0.5f;
            Vector3 tip = position + forward * shaftLength * 0.5f;
            Vector3 headBase = tip - forward * headLength;

            batchedLines.Add(tail);
            batchedLines.Add(tip);
            batchedLines.Add(tip);
            batchedLines.Add(headBase + right * headWidth);
            batchedLines.Add(tip);
            batchedLines.Add(headBase - right * headWidth);
        }

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
