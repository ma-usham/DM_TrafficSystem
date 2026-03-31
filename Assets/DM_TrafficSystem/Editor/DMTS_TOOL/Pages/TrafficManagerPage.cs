using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class TrafficManagerPage : IPage
    {
        private TrafficManager trafficManager;
        private UnityEditor.Editor editor;
        private Vector2 scrollPos;

        private TrafficManager FindTrafficManager()
        {
            if (trafficManager != null)
                return trafficManager;

            trafficManager = Object.FindAnyObjectByType<TrafficManager>();
            return trafficManager;
        }

        private void CreateTrafficManager()
        {
            GameObject go = new GameObject("TrafficManager");
            trafficManager = go.AddComponent<TrafficManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create TrafficManager");
            Selection.activeGameObject = go;
        }

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

        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
        }
    }
}
