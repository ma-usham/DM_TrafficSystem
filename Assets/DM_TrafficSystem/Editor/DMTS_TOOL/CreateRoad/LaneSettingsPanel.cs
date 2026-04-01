using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws editable lane properties and applies them back onto generated waypoints.
    /// </summary>
    public static class LaneSettingsPanel
    {
        /// <summary>
        /// Renders the inspector-style controls for one generated lane.
        /// </summary>
        public static void Draw(AILane lane)
        {
            if (lane == null)
                return;

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField(lane.gameObject.name, EditorStyles.boldLabel);

            SerializedObject serializedLane = new SerializedObject(lane);
            SerializedProperty waypointsProperty = serializedLane.FindProperty("waypoints");
            SerializedProperty speedLimitProperty = serializedLane.FindProperty("laneSpeedLimit");
            SerializedProperty vehicleTypeProperty = serializedLane.FindProperty("laneVehicleType");

            serializedLane.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(speedLimitProperty, new GUIContent("Speed Limit"));
            EditorGUILayout.PropertyField(vehicleTypeProperty, new GUIContent("Vehicle Type"));
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Apply", GUILayout.Width(100)))
            {
                serializedLane.ApplyModifiedProperties();
                ApplyWaypointSettingsFromLane(lane);
                EditorUtility.SetDirty(lane);
            }
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.LabelField("Waypoint Count", waypointsProperty.arraySize.ToString());
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(waypointsProperty, true);
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                serializedLane.ApplyModifiedProperties();
                EditorUtility.SetDirty(lane);
            }

            if (GUILayout.Button("Select Lane Object"))
            {
                Selection.activeGameObject = lane.gameObject;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Copies lane-level speed and vehicle filters onto every waypoint in the lane.
        /// </summary>
        private static void ApplyWaypointSettingsFromLane(AILane lane)
        {
            if (lane == null || lane.waypoints == null)
                return;

            Undo.RecordObject(lane, "Apply Lane Settings");

            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                AIWaypoint waypoint = lane.waypoints[i];
                if (waypoint == null)
                    continue;

                Undo.RecordObject(waypoint, "Apply Lane Settings");
                WaypointSettings settings = waypoint.settings;
                settings.speed = lane.laneSpeedLimit;
                settings.vehicleType = lane.laneVehicleType;
                waypoint.settings = settings;
                EditorUtility.SetDirty(waypoint);
            }
        }
    }
}
