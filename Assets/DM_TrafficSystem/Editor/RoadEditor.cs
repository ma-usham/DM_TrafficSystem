using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(Road))]
    public class RoadEditor : UnityEditor.Editor
    {
        private const float MinLaneWidth = 0.1f;
        private const float MinSpeedLimit = 0f;
        private Road road => (Road)target;


        public override void OnInspectorGUI()
        {
            DrawRoadHelpBox();
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Traffic System Window", GUILayout.Height(24)))
            {
                DMTS_Window.ShowWindow(new CreateRoadPage(road));
            }
            SceneView.RepaintAll();
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

        public void DrawRoadHelpBox()
        {
            EditorGUILayout.HelpBox(
                "\u2022 Shift + Left-click in the scene to place control points at the spline end\n" +
                "\u2022 Ctrl + Left-click near a curve segment to insert a point\n" +
                "\u2022 Right-click a control point to delete it\n" +
                "\u2022 Drag any control point to reposition it in Move2D\n" +
                "\u2022 Use the move handle to reposition points on x, y, z in Move3D",
                MessageType.Info);
        }

        public void DrawRoadSettings(Road road)
        {
             EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Road Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            SplineMoveMode newMoveMode =
                (SplineMoveMode)EditorGUILayout.EnumPopup("Move Mode", road.splineMoveMode);
            int newLaneCount = EditorGUILayout.IntSlider("Lanes", road.lanes, 1, 8);
            float newLaneWidth = Mathf.Max(MinLaneWidth, EditorGUILayout.FloatField("Lane Width", road.laneWidth));
            int newWaypointDistance = EditorGUILayout.IntSlider("Waypoint Distance", road.waypointDistance, 1, 15);
            float newSpeedLimit = Mathf.Max(MinSpeedLimit, EditorGUILayout.FloatField("Speed Limit", road.speedLimitForAllRoads));
            int newCurveResolution = EditorGUILayout.IntSlider("Curve Smoothness", road.curveResolution, 10, 100);
            DrivingDirection newDrivingDirection =
                (DrivingDirection)EditorGUILayout.EnumPopup("Driving Direction", road.drivingDirection);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(road, "Change Road Settings");
                road.splineMoveMode = newMoveMode;
                road.lanes = newLaneCount;
                road.laneWidth = newLaneWidth;
                road.waypointDistance = newWaypointDistance;
                road.speedLimitForAllRoads = newSpeedLimit;
                road.curveResolution = newCurveResolution;
                road.drivingDirection = newDrivingDirection;
                EditorUtility.SetDirty(road);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Draw Direction", "End ->", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Points", road.controlPointsList.Count.ToString());

            EditorGUILayout.EndVertical();
        }

        #region Gizmos
        [DrawGizmo(GizmoType.Selected)] //This will only draw the gizmos when the object is selected, which can help reduce clutter in the scene view.
        private static void DrawGeneratedWaypointGizmos(Road creator, GizmoType gizmoType) // This function is automatically called by Unity to draw gizmos in the scene view. It will draw arrows at each waypoint to indicate direction, and lines between waypoints to show the path.
        {
            if (creator.laneObjects == null || creator.laneObjects.Count == 0) return;

            Color previousColor = Handles.color;

            foreach (var lane in creator.laneObjects)
            {
                if (lane == null) continue;
                Transform laneTransform = lane.transform;

                Vector3 prevWaypointPosition = default;
                bool hasPrevWaypoint = false;

                for (int waypointIndex = 0; waypointIndex < laneTransform.childCount; waypointIndex++)
                {
                    Transform waypointTransform = laneTransform.GetChild(waypointIndex);
                    if (waypointTransform == null) continue;

                    Vector3 waypointPosition = waypointTransform.position;
                    float waypointSize = HandleUtility.GetHandleSize(waypointPosition) *
                                         DMTSPrefs.WaypointSizeMultiplier;

                    Vector3 forward;
                    if (waypointIndex < laneTransform.childCount - 1)
                    {
                        forward = (laneTransform.GetChild(waypointIndex + 1).position - waypointPosition).normalized;
                    }
                    else if (hasPrevWaypoint)
                    {
                        forward = (waypointPosition - prevWaypointPosition).normalized;
                    }
                    else
                    {
                        forward = Vector3.forward;
                    }

                    Handles.color = DMTSPrefs.WaypointColor;
                    DrawDirectionArrow(waypointPosition, forward, waypointSize);

                    if (hasPrevWaypoint)
                    {
                        Handles.color = DMTSPrefs.WaypointLineColor;
                        Handles.DrawLine(prevWaypointPosition, waypointPosition);
                    }

                    prevWaypointPosition = waypointPosition;
                    hasPrevWaypoint = true;
                }
            }

            Handles.color = previousColor;
        }

        private static void DrawDirectionArrow(Vector3 position, Vector3 forward, float size)
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

            Handles.DrawLine(tail, tip);
            Handles.DrawLine(tip, headBase + right * headWidth);
            Handles.DrawLine(tip, headBase - right * headWidth);
        }

       #endregion

    }
}
