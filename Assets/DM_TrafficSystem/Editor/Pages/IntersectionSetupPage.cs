using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>

    /// </summary>
    public class IntersectionSetupPage : IPage
    {
        public void OnGUI(Editor_DMWindow ctx)
        {
            EditorGUILayout.LabelField("Intersection Setup",EditorStyles.boldLabel);
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
        
        }
    }
}
