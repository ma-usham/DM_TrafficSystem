using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Provides navigation to the road creation, connection, and overview workflows.
    /// </summary>
    public class RoadSetupPage : IPage
    {
        /// <summary>
        /// Draws the road tooling menu and pushes the selected page onto the stack.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Road Setup Page", EditorStyles.boldLabel);
            if (GUILayout.Button("Create Road", GUILayout.Height(20)))
                ctx.pageStack.Push(new CreateRoadPage());

            if (GUILayout.Button("Connect Roads", GUILayout.Height(20)))
                ctx.pageStack.Push(new ConnectRoadPage());

            if (GUILayout.Button("View Roads", GUILayout.Height(20)))
                ctx.pageStack.Push(new ViewRoadsPage());

            GUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }
        }

        /// <summary>
        /// This menu page does not draw scene handles.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
        }
    }
}
