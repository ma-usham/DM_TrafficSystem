using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>

    /// </summary>
    public class IntersectionSetupPage : IPage
    {
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Intersection Setup",EditorStyles.boldLabel);

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
