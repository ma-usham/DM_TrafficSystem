using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>

    /// </summary>
    public class TrafficManagerPage : IPage
    {
        public void OnGUI(Editor_DMWindow ctx)
        {
            EditorGUILayout.LabelField("Traffic Manager",EditorStyles.boldLabel);
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
            
        }
    }
}
