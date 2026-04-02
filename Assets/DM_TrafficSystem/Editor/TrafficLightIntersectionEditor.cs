using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(TrafficLightIntersection))]
    public class TrafficLightIntersectionEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            if (!DMTS_Window.DrawIntersectionState) return;

            TrafficLightIntersection intersection = (TrafficLightIntersection)target;
            if (intersection == null || !Application.isPlaying) return;

            foreach (var road in intersection.trafficLightRoads)
            {
                if (road == null || road.stopPoints == null) continue;

                foreach (var wp in road.stopPoints)
                {
                    if (wp == null) continue;

                    bool isStopping = wp.settings.isStopPoint;
                    Color boxColor = isStopping ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);
                    Color outlineColor = isStopping ? Color.red : Color.green;

                    Handles.color = boxColor;
                    Vector3 pos = wp.transform.position;
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
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Viewing active light status in Scene View (Red = Stop, Green = Go)", MessageType.Info);
            }
        }
    }
}