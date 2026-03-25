using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public class MainPage : IPage
    {
        public void OnGUI(Editor_DMWindow ctx)
        {
            EditorGUILayout.LabelField("Main Menu Page",EditorStyles.boldLabel);
            if(GUILayout.Button("AI Traffic Manager",GUILayout.Height(20)))
            {
                
            }
            if(GUILayout.Button("Road Setup",GUILayout.Height(20)))
            {
                ctx.pageStack.Push((IPage)new RoadSetupPage());
            }
            if(GUILayout.Button("Intersection Setup",GUILayout.Height(20)))
            {
            }
      
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
            
        }
    }
}
