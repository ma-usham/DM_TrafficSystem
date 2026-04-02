using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Placeholder page for future intersection authoring tools.
    /// </summary>
    public class IntersectionSetupPage : IPage
    {
        private Vector2 priorityScroll = Vector2.zero;
        private Vector2 trafficLightScroll = Vector2.zero;

        /// <summary>
        /// Draws the temporary intersection setup page.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Intersection Setup", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Create Priority Intersection", GUILayout.Height(30)))
            {
                GameObject newIntersection = new GameObject("New Priority Intersection");
                newIntersection.AddComponent<PriorityIntersection>();
                Selection.activeGameObject = newIntersection;
                ctx.pageStack.Push(new PriorityIntersectionPage());
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Create Traffic Light Intersection", GUILayout.Height(30)))
            {
                GameObject newIntersection = new GameObject("New Traffic Light Intersection");
                newIntersection.AddComponent<TrafficLightIntersection>();
                Selection.activeGameObject = newIntersection;
                ctx.pageStack.Push(new TrafficLightIntersectionPage());
            }
            
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Existing Intersections", EditorStyles.boldLabel);
            
            // Priority Intersections Block
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Priority Intersections", EditorStyles.miniBoldLabel);
            
            PriorityIntersection[] priorityIntersections = Object.FindObjectsOfType<PriorityIntersection>();
            
            if (priorityIntersections.Length == 0)
            {
                EditorGUILayout.LabelField("No Priority Intersections found.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                priorityScroll = EditorGUILayout.BeginScrollView(priorityScroll, GUILayout.MaxHeight(150));
                foreach (var intersection in priorityIntersections)
                {
                    EditorGUILayout.BeginHorizontal();
                    string displayName = string.IsNullOrEmpty(intersection.intersectionName) ? intersection.gameObject.name : intersection.intersectionName;
                    EditorGUILayout.LabelField(displayName, GUILayout.Width(150));
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Edit", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = intersection.gameObject;
                        ctx.pageStack.Push(new PriorityIntersectionPage());
                    }
                    if (GUILayout.Button("View", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = intersection.gameObject;
                        EditorGUIUtility.PingObject(intersection.gameObject);
                        if (SceneView.lastActiveSceneView != null)
                        {
                            SceneView.lastActiveSceneView.FrameSelected();
                        }
                    }
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Intersection", $"Are you sure you want to delete {displayName}?", "Yes", "No"))
                        {
                            Object.DestroyImmediate(intersection.gameObject);
                            GUIUtility.ExitGUI(); // Prevent layout errors during GUI loops
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Traffic Light Intersections Block
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Traffic Light Intersections", EditorStyles.miniBoldLabel);
            
            TrafficLightIntersection[] trafficLightIntersections = Object.FindObjectsOfType<TrafficLightIntersection>();
            
            if (trafficLightIntersections.Length == 0)
            {
                EditorGUILayout.LabelField("No Traffic Light Intersections found.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                trafficLightScroll = EditorGUILayout.BeginScrollView(trafficLightScroll, GUILayout.MaxHeight(150));
                foreach (var intersection in trafficLightIntersections)
                {
                    EditorGUILayout.BeginHorizontal();
                    string displayName = string.IsNullOrEmpty(intersection.intersectionName) ? intersection.gameObject.name : intersection.intersectionName;
                    EditorGUILayout.LabelField(displayName, GUILayout.Width(150));
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Edit", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = intersection.gameObject;
                        ctx.pageStack.Push(new TrafficLightIntersectionPage());
                    }
                    if (GUILayout.Button("View", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = intersection.gameObject;
                        EditorGUIUtility.PingObject(intersection.gameObject);
                        if (SceneView.lastActiveSceneView != null)
                        {
                            SceneView.lastActiveSceneView.FrameSelected();
                        }
                    }
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Intersection", $"Are you sure you want to delete {displayName}?", "Yes", "No"))
                        {
                            Object.DestroyImmediate(intersection.gameObject);
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }
        }

        /// <summary>
        /// Reserved for future intersection scene handles.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
        }
    }
}
