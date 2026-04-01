using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Placeholder page for future intersection authoring tools.
    /// </summary>
    public class IntersectionSetupPage : IPage
    {
        /// <summary>
        /// Draws the temporary intersection setup page.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Intersection Setup", EditorStyles.boldLabel);

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
