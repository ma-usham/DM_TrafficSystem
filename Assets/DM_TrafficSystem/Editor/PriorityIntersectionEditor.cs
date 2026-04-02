using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(PriorityIntersection))]
    public class PriorityIntersectionEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            if (!DMTS_Window.DrawIntersectionState) return;

            PriorityIntersection intersection = (PriorityIntersection)target;
            if (intersection == null || !Application.isPlaying) return;

            foreach (var road in intersection.priorityStopRoads)
            {
                if (road == null || road.stopPoints == null) continue;

                foreach (var wp in road.stopPoints)
                {
                    if (wp == null) continue;

                    // Determine color based on whether the waypoint is currently a stop point
                    bool isStopping = wp.settings.isStopPoint;
                    Color boxColor = isStopping ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);
                    Color outlineColor = isStopping ? Color.red : Color.green;

                    Handles.color = boxColor;
                    Vector3 pos = wp.transform.position;
                    
                    // Draw 2D-like horizontal rectangle at the waypoint position
                    Vector3[] corners = new Vector3[]
                    {
                        pos + new Vector3(-0.4f, 0, -0.4f),
                        pos + new Vector3(0.4f, 0, -0.4f),
                        pos + new Vector3(0.4f, 0, 0.4f),
                        pos + new Vector3(-0.4f, 0, 0.4f)
                    };

                    Handles.DrawSolidRectangleWithOutline(corners, boxColor, outlineColor);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Viewing active stop point status in Scene View (Red = Stop, Green = Go)", MessageType.Info);
            }
        }
    }
}