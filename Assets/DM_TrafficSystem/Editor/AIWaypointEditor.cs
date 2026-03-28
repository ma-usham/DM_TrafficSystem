using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(AIWaypoint))]
    public class AIWaypointEditor : UnityEditor.Editor
    {
        private AIWaypoint Waypoint => (AIWaypoint)target;

        private SerializedProperty settingsProp;

        private void OnEnable()
        {
            settingsProp = serializedObject.FindProperty("settings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Waypoint Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("speed"));
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            Handles.Label(
                Waypoint.transform.position + Vector3.up,
                $"Speed: {Waypoint.settings.speed}",
                EditorStyles.boldLabel
            );
        }
    }
}
