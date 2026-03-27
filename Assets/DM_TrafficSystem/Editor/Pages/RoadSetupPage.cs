using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class RoadSetupPage : IPage
    {
        public void OnGUI(Editor_DMWindow ctx)
        {
            EditorGUILayout.LabelField("Road Setup Page",EditorStyles.boldLabel);
            if(GUILayout.Button("Create Road",GUILayout.Height(20)))
            {
                ctx.pageStack.Push((IPage)new CreateRoadPage());
            }
            if(GUILayout.Button("Connect Roads",GUILayout.Height(20)))
            {
                ctx.pageStack.Push((IPage)new ConnectRoadPage());
            }
            if(GUILayout.Button("View Roads",GUILayout.Height(20)))
            {
                ctx.pageStack.Push((IPage)new ViewRoadsPage());
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {

        }
    }
}
