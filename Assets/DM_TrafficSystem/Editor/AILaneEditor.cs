

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Darkmatter.TrafficSystem.Editor
{
    public class AILaneEditor : UnityEditor.Editor
    {
        public void DrawLaneSettings(AILane lane)
        {
            if (lane == null) return;
            
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(lane.gameObject.name, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

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
                UpdateWaypointsSpeedLimit(lane);
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

        private void UpdateWaypointsSpeedLimit(AILane lane)
        {
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
