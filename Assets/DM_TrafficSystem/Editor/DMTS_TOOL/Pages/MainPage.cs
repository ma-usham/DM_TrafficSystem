using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Displays the top-level navigation for the traffic system editor window.
    /// </summary>
    public class MainPage : IPage
    {
        /// <summary>
        /// Draws the primary editor menu and pushes the selected workflow page.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
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

        /// <summary>
        /// This menu page does not draw scene handles.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx) { }
    }
}
