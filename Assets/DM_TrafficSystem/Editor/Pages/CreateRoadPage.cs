using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public class CreateRoadPage : IPage
    {
        public void OnGUI(Editor_DMWindow ctx)
        {
            EditorGUILayout.LabelField("Create Road Page",EditorStyles.boldLabel);
            GUILayout.Space(10);
            if(GUILayout.Button("Back",GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
          
        }
    }
}
