using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class MainPage : IPage
    {
        public void OnGUI(Editor_DMWindow ctx)
        {
            EditorGUILayout.LabelField("DarkMatter Traffic System", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            if (GUILayout.Button("AI Traffic Manager", GUILayout.Height(28)))
                ctx.pageStack.Push(new TrafficManagerPage());

            if (GUILayout.Button("Road Setup", GUILayout.Height(28)))
                ctx.pageStack.Push(new RoadSetupPage());

            if (GUILayout.Button("Intersection Setup", GUILayout.Height(28)))
                ctx.pageStack.Push(new IntersectionSetupPage());
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx) { }
    }
}
