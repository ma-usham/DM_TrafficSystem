using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws editable road-wide settings and applies shared values back onto generated content.
    /// </summary>
    public static class RoadSettingsPanel
    {
        private const float MinLaneWidth = 0.1f;
        private const float MinSpeedLimit = 0f;

        /// <summary>
        /// Shows the usage help for the road editing workflow.
        /// </summary>
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

        /// <summary>
        /// Draws the editable settings for the active road and handles validation plus bulk apply actions.
        /// </summary>
        public static void DrawSettings(Road road)
        {
            if (road == null)
                return;

            SerializedObject serializedRoad = new SerializedObject(road);
            SerializedProperty controlPointsProperty = serializedRoad.FindProperty("controlPointsList");
            SerializedProperty moveModeProperty = serializedRoad.FindProperty("splineMoveMode");
            SerializedProperty laneCountProperty = serializedRoad.FindProperty("lanes");
            SerializedProperty waypointDistanceProperty = serializedRoad.FindProperty("waypointDistance");
            SerializedProperty laneWidthProperty = serializedRoad.FindProperty("laneWidth");
            SerializedProperty speedLimitProperty = serializedRoad.FindProperty("speedLimitForAllLanes");
            SerializedProperty curveResolutionProperty = serializedRoad.FindProperty("curveResolution");
            SerializedProperty drivingDirectionProperty = serializedRoad.FindProperty("drivingDirection");

            serializedRoad.Update();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Road Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(moveModeProperty, new GUIContent("Move Mode"));
            EditorGUILayout.PropertyField(laneCountProperty, new GUIContent("Lanes"));
            EditorGUILayout.PropertyField(laneWidthProperty, new GUIContent("Lane Width"));
            EditorGUILayout.PropertyField(waypointDistanceProperty, new GUIContent("Waypoint Distance"));
            EditorGUILayout.PropertyField(curveResolutionProperty, new GUIContent("Curve Smoothness"));
            EditorGUILayout.PropertyField(drivingDirectionProperty, new GUIContent("Driving Direction"));
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(speedLimitProperty, new GUIContent("Global Speed Limit"));
            bool applyGlobalSpeed = GUILayout.Button("Apply", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            bool settingsChanged = EditorGUI.EndChangeCheck();

            if (settingsChanged)
            {
                ClampRoadSettings(laneWidthProperty, speedLimitProperty);
                ApplyRoadSettings(serializedRoad, road);
            }

            if (applyGlobalSpeed)
            {
                ClampRoadSettings(laneWidthProperty, speedLimitProperty);
                ApplyRoadSettings(serializedRoad, road);
                ApplyGlobalSpeedLimit(road, speedLimitProperty.floatValue);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Draw Direction", "End ->", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Points", controlPointsProperty.arraySize.ToString());

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Clamps road settings that must remain positive before they are saved.
        /// </summary>
        private static void ClampRoadSettings(SerializedProperty laneWidthProperty, SerializedProperty speedLimitProperty)
        {
            laneWidthProperty.floatValue = Mathf.Max(MinLaneWidth, laneWidthProperty.floatValue);
            speedLimitProperty.floatValue = Mathf.Max(MinSpeedLimit, speedLimitProperty.floatValue);
        }

        /// <summary>
        /// Saves the serialized road settings and marks the road dirty for persistence.
        /// </summary>
        private static void ApplyRoadSettings(SerializedObject serializedRoad, Road road)
        {
            serializedRoad.ApplyModifiedProperties();
            EditorUtility.SetDirty(road);
        }

        /// <summary>
        /// Pushes the shared road speed limit onto all generated lanes and waypoints.
        /// </summary>
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
