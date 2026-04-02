using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Provides editor controls for locating, creating, and configuring the scene traffic manager.
    /// </summary>
    public class TrafficManagerPage : IPage
    {
        private TrafficManager trafficManager;
        private UnityEditor.Editor editor;
        private Vector2 scrollPos;

        /// <summary>
        /// Finds the scene traffic manager and caches the result for repeated UI draws.
        /// </summary>
        private TrafficManager FindTrafficManager()
        {
            if (trafficManager != null)
                return trafficManager;

            trafficManager = Object.FindAnyObjectByType<TrafficManager>();
            return trafficManager;
        }

        /// <summary>
        /// Creates a new traffic manager object and selects it in the hierarchy.
        /// </summary>
        private void CreateTrafficManager()
        {
            GameObject go = new GameObject("TrafficManager");
            trafficManager = go.AddComponent<TrafficManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create TrafficManager");
            TrafficSystemHierarchyUtility.ParentTrafficManager(go, "Create TrafficManager");
            Selection.activeGameObject = go;
        }

        /// <summary>
        /// Draws traffic manager creation and configuration controls.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("AI Traffic Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            TrafficManager manager = FindTrafficManager();

            if (manager == null)
            {
                EditorGUILayout.HelpBox(
                    "No TrafficManager found in the scene. Create one to configure traffic settings.",
                    MessageType.Info);

                EditorGUILayout.Space(4);

                if (GUILayout.Button("Create Traffic Manager", GUILayout.Height(30)))
                {
                    CreateTrafficManager();
                    manager = this.trafficManager;
                }
            }

            if (manager != null)
            {
                EditorGUILayout.Space(4);

                if (GUILayout.Button("Select in Hierarchy", GUILayout.Height(22)))
                    Selection.activeGameObject = manager.gameObject;

                EditorGUILayout.Space(6);

                if (editor == null || editor.target != manager)
                {
                    if (editor != null)
                        Object.DestroyImmediate(editor);
                    editor = UnityEditor.Editor.CreateEditor(manager);
                }

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                editor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }
        }

        /// <summary>
        /// This page currently has no scene interaction.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
        }
    }
}
