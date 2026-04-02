using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws runtime stop-point state overlays for traffic-light intersections.
    /// </summary>
    [CustomEditor(typeof(TrafficLightIntersection))]
    public class TrafficLightIntersectionEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Draws red or green markers over the stop points owned by the selected traffic-light intersection.
        /// </summary>
        private void OnSceneGUI()
        {
            if (!DMTS_Window.DrawIntersectionState)
                return;

            TrafficLightIntersection intersection = (TrafficLightIntersection)target;
            if (intersection == null || !Application.isPlaying)
                return;

            foreach (TrafficLightRoad road in intersection.trafficLightRoads)
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
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Viewing active light status in Scene View (Red = Stop, Green = Go)", MessageType.Info);
            }
        }
    }
}
