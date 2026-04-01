using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Hosts the connect-road workflow and forwards work to dedicated UI and scene helpers.
    /// </summary>
    public class ConnectRoadPage : IPage
    {
        private readonly ConnectRoadToolState toolState = new ConnectRoadToolState();
        private readonly ConnectRoadSceneTool sceneTool;
        private Vector2 connectionScrollPosition;

        public static bool isActive;

        public ConnectRoadPage()
        {
            sceneTool = new ConnectRoadSceneTool(toolState);
        }

        /// <summary>
        /// Draws the connect-road workflow, selection state, and visible connections list.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            toolState.RefreshLaneTerminalCache();
            List<ConnectRoadToolState.ConnectionRecord> allConnectionRecords = toolState.BuildConnectionRecords(filterBySceneView: false);
            List<ConnectRoadToolState.ConnectionRecord> visibleConnectionRecords = toolState.BuildConnectionRecords(filterBySceneView: true);

            EditorGUILayout.LabelField("Connect Roads", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            string instructions = toolState.SelectedEndingWaypoint == null
                ? "Scene step 1: click any lane ending waypoint."
                : "Scene step 2: click a lane beginning waypoint to create the connection.";
            EditorGUILayout.HelpBox(instructions, MessageType.Info);

            EditorGUILayout.LabelField("Status", toolState.StatusMessage, EditorStyles.wordWrappedLabel);

            if (toolState.SelectedEndingWaypoint != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Selected End", toolState.SelectedEndingConnectionName);
                EditorGUILayout.ObjectField("Waypoint", toolState.SelectedEndingWaypoint, typeof(AIWaypoint), true);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(toolState.SelectedEndingWaypoint == null);
            if (GUILayout.Button("Clear Selection", GUILayout.Width(120)))
            {
                toolState.ClearSelection(resetStatus: true);
                toolState.RepaintViews();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Refresh Scene Data", GUILayout.Width(120)))
            {
                toolState.RefreshLaneTerminalCache();
                toolState.UpdateStatusForCurrentSelection();
                toolState.RepaintViews();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            ConnectRoadConnectionsPanel.Draw(
                toolState,
                visibleConnectionRecords,
                allConnectionRecords.Count,
                ref connectionScrollPosition);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                toolState.ClearSelection(resetStatus: true);
                ctx.pageStack.Pop();
                toolState.RepaintViews();
            }
        }

        /// <summary>
        /// Forwards Scene view handling to the shared connect-road scene tool.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            sceneTool.OnSceneGUI(sceneView);
        }
    }
}
