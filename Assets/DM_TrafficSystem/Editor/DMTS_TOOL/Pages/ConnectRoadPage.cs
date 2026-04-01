using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Placeholder page for future road-to-road connection tools.
    /// </summary>
    public class ConnectRoadPage : IPage
    {
        /// <summary>
        /// Draws the temporary connect-roads page.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Connect Roads", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }
        }

        /// <summary>
        /// Reserved for future connection scene handles.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
        }
    }
}
