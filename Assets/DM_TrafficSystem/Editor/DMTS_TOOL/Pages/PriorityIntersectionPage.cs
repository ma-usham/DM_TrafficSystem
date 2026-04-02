using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class PriorityIntersectionPage : IPage
    {
        private Vector2 scrollPos;
        private PriorityIntersection targetIntersection;
        private int activeRoadIndex = -1;

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

            // General Settings Block
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

            // Priority Stop Roads Blocks
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

                // Button to remove this road
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

                // Show the Waypoints Array for this specific Priority Road block
                EditorGUILayout.PropertyField(stopPointsProp, new GUIContent("Stop Waypoints"), true);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Add Road Button
            if (GUILayout.Button("Add Road", GUILayout.Height(30)))
            {
                roadsProp.InsertArrayElementAtIndex(roadsProp.arraySize);
                
                // Clear the newly created element so it's fresh instead of duplicating
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

        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (targetIntersection == null) return;

            // Draw all current stop points for visual reference
            DrawAllStopPoints();

            if (activeRoadIndex < 0 || activeRoadIndex >= targetIntersection.priorityStopRoads.Count)
            {
                return;
            }

            // Listen for clicks on waypoints using Raycast (no visible green boxes)
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                AIWaypoint clickedWaypoint = RaycastForWaypoint(e.mousePosition);
                if (clickedWaypoint != null)
                {
                    ToggleStopPoint(clickedWaypoint);
                    e.Use();
                    GUI.changed = true;
                }
            }

            // Force repaint to show color changes immediately
            sceneView.Repaint();
        }

        private void DrawAllStopPoints()
        {
            if (targetIntersection == null) return;

            int roadIdx = 0;
            foreach (var road in targetIntersection.priorityStopRoads)
            {
                if (road == null || road.stopPoints == null) continue;
                foreach (var wp in road.stopPoints)
                {
                    if (wp != null)
                    {
                        // Current editing road is cyan, others are red
                        Handles.color = (activeRoadIndex == roadIdx) ? new Color(0, 1, 1, 0.5f) : new Color(1, 0, 0, 0.5f);
                        // Using a 2D Rectangle Handle instead of a 3D Cube
                        Handles.DrawSolidRectangleWithOutline(
                            new Vector3[] {
                                wp.transform.position + new Vector3(-0.35f, 0, -0.35f),
                                wp.transform.position + new Vector3(0.35f, 0, -0.35f),
                                wp.transform.position + new Vector3(0.35f, 0, 0.35f),
                                wp.transform.position + new Vector3(-0.35f, 0, 0.35f)
                            },
                            Handles.color,
                            (activeRoadIndex == roadIdx) ? Color.cyan : Color.red
                        );
                    }
                }
                roadIdx++;
            }
        }

        private AIWaypoint RaycastForWaypoint(Vector2 mousePosition)
        {
            // First try picking via handles metadata (if they have specialized editors) 
            // but for simple AIWaypoints we rely on a cleaner physics approach or simple distance check
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            
            // Note: This requires the Waypoint to have a Collider or be an Editor object. 
            // If the Waypoint has no collider, we check for proximity to the transform position.
            AIWaypoint bestMatch = null;
            float closestDist = float.MaxValue;
            
            AIWaypoint[] allWaypoints = Object.FindObjectsOfType<AIWaypoint>();
            foreach (var wp in allWaypoints)
            {
                float distToRay = HandleUtility.DistanceToCircle(wp.transform.position, 0.5f);
                if (distToRay < 10 && distToRay < closestDist) // 10 pixel threshold for clicking
                {
                    closestDist = distToRay;
                    bestMatch = wp;
                }
            }

            return bestMatch;
        }

        private void ToggleStopPoint(AIWaypoint wp)
        {
            Undo.RecordObject(targetIntersection, "Toggle Stop Point");
            var road = targetIntersection.priorityStopRoads[activeRoadIndex];

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