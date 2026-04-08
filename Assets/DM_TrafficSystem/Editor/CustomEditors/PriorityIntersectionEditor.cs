using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws runtime stop-point state overlays for priority intersections.
    /// </summary>
    [CustomEditor(typeof(PriorityIntersection))]
    public class PriorityIntersectionEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Draws red or green markers over the stop points owned by the selected priority intersection.
        /// </summary>
        private void OnSceneGUI()
        {
            if (!DMTS_Window.DrawIntersectionState)
                return;

            PriorityIntersection intersection = (PriorityIntersection)target;
            if (intersection == null || !Application.isPlaying)
                return;

            foreach (PriorityStopRoad road in intersection.priorityStopRoads)
            {
                if (road == null || road.stopPoints == null)
                    continue;

                foreach (AIWaypoint waypoint in road.stopPoints)
                {
                    if (waypoint == null)
                        continue;

                    bool isStopping = waypoint.settings.isStopPoint;
                    Color fillColor = isStopping ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);
                    Color outlineColor = isStopping ? Color.red : Color.green;
                    IntersectionSceneUtility.DrawStopPoint(waypoint, fillColor, outlineColor);
                }
            }
        }

        /// <summary>
        /// Draws the default inspector and a runtime-only scene-view status hint.
        /// </summary>
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
