using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Configures traffic-light intersections and lets the user pick stop points directly in the Scene view.
    /// </summary>
    public class TrafficLightIntersectionPage : IPage
    {
        private Vector2 scrollPos;
        private TrafficLightIntersection targetIntersection;
        private int activeRoadIndex = -1;

        /// <summary>
        /// Draws the traffic-light intersection editor workflow and serialized light-group settings.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Traffic Light Intersection Setup", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(10);
            targetIntersection = (TrafficLightIntersection)EditorGUILayout.ObjectField("Intersection Object", targetIntersection, typeof(TrafficLightIntersection), true);

            if (targetIntersection == null && Selection.activeGameObject != null)
                targetIntersection = Selection.activeGameObject.GetComponent<TrafficLightIntersection>();

            if (targetIntersection == null)
            {
                EditorGUILayout.HelpBox("Select or assign a TrafficLightIntersection object in the scene to edit its values.", MessageType.Info);
                return;
            }

            SerializedObject so = new SerializedObject(targetIntersection);
            so.Update();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Cycle Times", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("intersectionName"), new GUIContent("Name"));
            if (EditorGUI.EndChangeCheck())
            {
                targetIntersection.gameObject.name = so.FindProperty("intersectionName").stringValue;
            }

            EditorGUILayout.PropertyField(so.FindProperty("greenTime"), new GUIContent("Green (Seconds)"));
            EditorGUILayout.PropertyField(so.FindProperty("yellowTime"), new GUIContent("Yellow (Seconds)"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Emission Colors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("redEmissionColor"), new GUIContent("Red Color"));
            EditorGUILayout.PropertyField(so.FindProperty("yellowEmissionColor"), new GUIContent("Yellow Color"));
            EditorGUILayout.PropertyField(so.FindProperty("greenEmissionColor"), new GUIContent("Green Color"));
            EditorGUILayout.PropertyField(so.FindProperty("offColor"), new GUIContent("Off Color"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Traffic Light Roads", EditorStyles.boldLabel);
            SerializedProperty roadsProp = so.FindProperty("trafficLightRoads");

            for (int i = 0; i < roadsProp.arraySize; i++)
            {
                Color defaultColor = GUI.backgroundColor;
                if (activeRoadIndex == i) GUI.backgroundColor = Color.cyan;

                EditorGUILayout.BeginVertical("helpbox");
                GUI.backgroundColor = defaultColor;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Road Group {i}", EditorStyles.boldLabel);
                
                if (activeRoadIndex == i)
                {
                    if (GUILayout.Button("Stop Editing", GUILayout.Width(100))) activeRoadIndex = -1;
                }
                else
                {
                    if (GUILayout.Button("Select Road", GUILayout.Width(100))) activeRoadIndex = i;
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    roadsProp.DeleteArrayElementAtIndex(i);
                    if (activeRoadIndex == i) activeRoadIndex = -1;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                SerializedProperty roadProp = roadsProp.GetArrayElementAtIndex(i);
                SerializedProperty stopPointsProp = roadProp.FindPropertyRelative("stopPoints");
                SerializedProperty visualsProp = roadProp.FindPropertyRelative("visuals");
                
                EditorGUILayout.PropertyField(stopPointsProp, new GUIContent("Stop Waypoints"), true);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(visualsProp, new GUIContent("Light Renderers"), true);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("Add Light Road Group", GUILayout.Height(30)))
            {
                roadsProp.InsertArrayElementAtIndex(roadsProp.arraySize);
                SerializedProperty newElement = roadsProp.GetArrayElementAtIndex(roadsProp.arraySize - 1);
                newElement.FindPropertyRelative("stopPoints").ClearArray();
                newElement.FindPropertyRelative("visuals").ClearArray();
                activeRoadIndex = roadsProp.arraySize - 1;
            }

            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }

            if (so.ApplyModifiedProperties())
            {
                if (targetIntersection.gameObject.name != targetIntersection.intersectionName)
                {
                    targetIntersection.gameObject.name = targetIntersection.intersectionName;
                }
            }
        }

        /// <summary>
        /// Draws current stop points and lets the user toggle them for the selected traffic-light road group.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (targetIntersection == null)
                return;

            DrawAllStopPoints();

            if (activeRoadIndex < 0 || activeRoadIndex >= targetIntersection.trafficLightRoads.Count)
                return;

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                AIWaypoint clickedWaypoint = FindWaypointAtMouse(currentEvent.mousePosition);
                if (clickedWaypoint != null)
                {
                    ToggleStopPoint(clickedWaypoint);
                    currentEvent.Use();
                    GUI.changed = true;
                }
            }
            sceneView.Repaint();
        }

        /// <summary>
        /// Draws colored markers for every stop point across every configured traffic-light road group.
        /// </summary>
        private void DrawAllStopPoints()
        {
            int roadIdx = 0;
            foreach (TrafficLightRoad road in targetIntersection.trafficLightRoads)
            {
                if (road == null || road.stopPoints == null)
                    continue;

                Color fillColor = activeRoadIndex == roadIdx ? new Color(0f, 1f, 1f, 0.5f) : new Color(1f, 0.6f, 0f, 0.5f);
                IntersectionSceneUtility.DrawStopPoints(road.stopPoints, fillColor, Color.black);

                roadIdx++;
            }
        }

        /// <summary>
        /// Returns the waypoint closest to the user's current Scene view click.
        /// </summary>
        private static AIWaypoint FindWaypointAtMouse(Vector2 mousePosition)
        {
            return IntersectionSceneUtility.FindWaypointAtMouse(mousePosition);
        }

        /// <summary>
        /// Adds or removes one waypoint from the currently active traffic-light road group.
        /// </summary>
        private void ToggleStopPoint(AIWaypoint wp)
        {
            Undo.RecordObject(targetIntersection, "Toggle Stop Point");
            TrafficLightRoad road = targetIntersection.trafficLightRoads[activeRoadIndex];
            if (road.stopPoints.Contains(wp))
                road.stopPoints.Remove(wp);
            else
                road.stopPoints.Add(wp);

            EditorUtility.SetDirty(targetIntersection);
        }
    }
}
