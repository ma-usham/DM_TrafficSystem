using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public static class RoadSettingsPanel
    {
        private const float MinLaneWidth = 0.1f;
        private const float MinSpeedLimit = 0f;

        public static void DrawHelpBox()
        {
            EditorGUILayout.HelpBox(
                "\u2022 Shift + Left-click in the scene to place control points at the spline end\n" +
                "\u2022 Ctrl + Left-click near a curve segment to insert a point\n" +
                "\u2022 Right-click a control point to delete it\n" +
                "\u2022 Drag any control point to reposition it in Move2D\n" +
                "\u2022 Use the move handle to reposition points on x, y, z in Move3D",
                MessageType.Info);
        }

        public static void DrawSettings(Road road)
        {
            if (road == null)
                return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Road Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            SplineMoveMode newMoveMode =
                (SplineMoveMode)EditorGUILayout.EnumPopup("Move Mode", road.splineMoveMode);
            int newLaneCount = EditorGUILayout.IntSlider("Lanes", road.lanes, 1, 8);
            float newLaneWidth = Mathf.Max(MinLaneWidth, EditorGUILayout.FloatField("Lane Width", road.laneWidth));
            int newWaypointDistance = EditorGUILayout.IntSlider("Waypoint Distance", road.waypointDistance, 1, 15);
            EditorGUILayout.BeginHorizontal();
            float newSpeedLimit = Mathf.Max(MinSpeedLimit, EditorGUILayout.FloatField("Global Speed Limit", road.speedLimitForAllLanes));
            bool applyGlobalSpeed = GUILayout.Button("Apply", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            int newCurveResolution = EditorGUILayout.IntSlider("Curve Smoothness", road.curveResolution, 10, 100);
            DrivingDirection newDrivingDirection =
                (DrivingDirection)EditorGUILayout.EnumPopup("Driving Direction", road.drivingDirection);

            bool settingsChanged = EditorGUI.EndChangeCheck();

            if (settingsChanged)
            {
                Undo.RecordObject(road, "Change Road Settings");
                road.splineMoveMode = newMoveMode;
                road.lanes = newLaneCount;
                road.laneWidth = newLaneWidth;
                road.waypointDistance = newWaypointDistance;
                road.speedLimitForAllLanes = newSpeedLimit;
                road.curveResolution = newCurveResolution;
                road.drivingDirection = newDrivingDirection;
                EditorUtility.SetDirty(road);
            }

            if (applyGlobalSpeed)
            {
                ApplyGlobalSpeedLimit(road, newSpeedLimit);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Draw Direction", "End ->", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Points", road.controlPointsList.Count.ToString());

            EditorGUILayout.EndVertical();
        }

        private static void ApplyGlobalSpeedLimit(Road road, float speedLimit)
        {
            Undo.RegisterFullObjectHierarchyUndo(road.gameObject, "Apply Global Speed Limit");
            road.speedLimitForAllLanes = speedLimit;
            EditorUtility.SetDirty(road);

            if (road.laneObjects == null)
                return;

            for (int laneIndex = 0; laneIndex < road.laneObjects.Count; laneIndex++)
            {
                AILane lane = road.laneObjects[laneIndex];
                if (lane == null)
                    continue;

                lane.laneSpeedLimit = speedLimit;
                EditorUtility.SetDirty(lane);

                if (lane.waypoints == null)
                    continue;

                for (int waypointIndex = 0; waypointIndex < lane.waypoints.Count; waypointIndex++)
                {
                    AIWaypoint waypoint = lane.waypoints[waypointIndex];
                    if (waypoint == null)
                        continue;

                    WaypointSettings settings = waypoint.settings;
                    settings.speed = speedLimit;
                    waypoint.settings = settings;
                    EditorUtility.SetDirty(waypoint);
                }
            }
        }
    }
}
