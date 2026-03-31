using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>

    /// </summary>
    public class ConnectRoadPage : IPage
    {
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Connect Roads",EditorStyles.boldLabel);

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
