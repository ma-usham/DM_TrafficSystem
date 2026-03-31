using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public static class LaneSettingsPanel
    {
        public static void Draw(AILane lane)
        {
            if (lane == null)
                return;

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField(lane.gameObject.name, EditorStyles.boldLabel);

            SerializedObject serializedLane = new SerializedObject(lane);
            SerializedProperty waypointsProperty = serializedLane.FindProperty("waypoints");
            SerializedProperty speedLimitProperty = serializedLane.FindProperty("laneSpeedLimit");

            serializedLane.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(speedLimitProperty, new GUIContent("Speed Limit"));
            if (GUILayout.Button("Apply", GUILayout.Width(50)))
            {
                serializedLane.ApplyModifiedProperties();
                ApplyWaypointSpeedLimit(lane);
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

        private static void ApplyWaypointSpeedLimit(AILane lane)
        {
            if (lane == null || lane.waypoints == null)
                return;

            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                AIWaypoint waypoint = lane.waypoints[i];
                if (waypoint == null)
                    continue;

                WaypointSettings settings = waypoint.settings;
                settings.speed = lane.laneSpeedLimit;
                waypoint.settings = settings;
                EditorUtility.SetDirty(waypoint);
            }
        }
    }
}
