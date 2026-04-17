using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Configures priority intersections and lets the user pick stop points directly in the Scene view.
    /// </summary>
    public class PriorityIntersectionPage : IPage
    {
        private Vector2 scrollPos;
        private PriorityIntersection targetIntersection;
        private int activeRoadIndex = -1;

        /// <summary>
        /// Draws the priority-intersection editor workflow and serialized road-group settings.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Priority Intersection Setup", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(10);

            // Let user pick or create a target
            targetIntersection = (PriorityIntersection)EditorGUILayout.ObjectField(
                "Intersection Object", 
                targetIntersection, 
                typeof(PriorityIntersection), 
                true
            );

            // Automatically try to get the selected one
            if (targetIntersection == null && Selection.activeGameObject != null)
            {
                targetIntersection = Selection.activeGameObject.GetComponent<PriorityIntersection>();
            }

            if (targetIntersection == null)
            {
                EditorGUILayout.HelpBox("Select or assign a PriorityIntersection object in the scene to edit its values.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Select a Road to highlight it, then CLICK on Waypoints in the Scene View to add/remove them as Stop Points.", MessageType.Info);

            SerializedObject so = new SerializedObject(targetIntersection);
            so.Update();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("intersectionName"), new GUIContent("Intersection Name"));
            if (EditorGUI.EndChangeCheck())
            {
                targetIntersection.gameObject.name = so.FindProperty("intersectionName").stringValue;
            }

            EditorGUILayout.PropertyField(so.FindProperty("activeTime"), new GUIContent("Active Time (Green)"));
            EditorGUILayout.PropertyField(so.FindProperty("waitTime"), new GUIContent("Wait Time (Yellow/Red)"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Priority Stop Roads", EditorStyles.boldLabel);

            SerializedProperty roadsProp = so.FindProperty("priorityStopRoads");

            for (int i = 0; i < roadsProp.arraySize; i++)
            {
                Color defaultColor = GUI.backgroundColor;
                if (activeRoadIndex == i) GUI.backgroundColor = Color.cyan;

                EditorGUILayout.BeginVertical("helpbox");
                GUI.backgroundColor = defaultColor;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Priority Stop Road {i}", EditorStyles.boldLabel);
                
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

                EditorGUILayout.PropertyField(stopPointsProp, new GUIContent("Stop Waypoints"), true);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("Add Road", GUILayout.Height(30)))
            {
                roadsProp.InsertArrayElementAtIndex(roadsProp.arraySize);
                
                SerializedProperty newElement = roadsProp.GetArrayElementAtIndex(roadsProp.arraySize - 1);
                SerializedProperty stopPointsProp = newElement.FindPropertyRelative("stopPoints");
                stopPointsProp.ClearArray();
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
        /// Draws current stop points and lets the user toggle them for the selected road group.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (targetIntersection == null)
                return;

            DrawAllStopPoints();

            if (activeRoadIndex < 0 || activeRoadIndex >= targetIntersection.priorityStopRoads.Count)
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
        /// Draws colored markers for every stop point across every configured priority road.
        /// </summary>
        private void DrawAllStopPoints()
        {
            if (targetIntersection == null)
                return;

            int roadIdx = 0;
            foreach (PriorityStopRoad road in targetIntersection.priorityStopRoads)
            {
                if (road == null || road.stopPoints == null)
                    continue;

                Color fillColor = activeRoadIndex == roadIdx ? new Color(0f, 1f, 1f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
                Color outlineColor = activeRoadIndex == roadIdx ? Color.cyan : Color.red;
                IntersectionSceneUtility.DrawStopPoints(road.stopPoints, fillColor, outlineColor);

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
        /// Adds or removes one waypoint from the currently active priority road group.
        /// </summary>
        private void ToggleStopPoint(AIWaypoint wp)
        {
            //Undo.RecordObject(targetIntersection, "Toggle Stop Point");
            PriorityStopRoad road = targetIntersection.priorityStopRoads[activeRoadIndex];

            if (road.stopPoints.Contains(wp))
            {
                road.stopPoints.Remove(wp);
            }
            else
            {
                road.stopPoints.Add(wp);
            }
            EditorUtility.SetDirty(targetIntersection);
        }
    }
}
