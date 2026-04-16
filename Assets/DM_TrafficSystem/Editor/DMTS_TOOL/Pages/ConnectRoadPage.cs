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

            string instructions = toolState.ActiveConnection != null
                ? "Scene edit: drag the curve points, Ctrl+Click the curve to insert, or right-click a middle point to delete."
                : toolState.SelectedEndingWaypoint == null
                    ? "Scene step 1: click any lane ending waypoint."
                    : "Scene step 2: click a lane beginning waypoint to create the curved connection.";
            EditorGUILayout.HelpBox(instructions, MessageType.Info);

            EditorGUILayout.LabelField("Status", toolState.StatusMessage, EditorStyles.wordWrappedLabel);

            if (toolState.SelectedEndingWaypoint != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Selected End", toolState.SelectedEndingConnectionName);
                EditorGUILayout.ObjectField("Waypoint", toolState.SelectedEndingWaypoint, typeof(AIWaypoint), true);
            }

            if (toolState.ActiveConnection != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Active Connection", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("Curve", toolState.ActiveConnection, typeof(AIWaypointConnection), true);
                EditorGUILayout.ObjectField("Source", toolState.ActiveConnection.sourceWaypoint, typeof(AIWaypoint), true);
                EditorGUILayout.ObjectField("Target", toolState.ActiveConnection.targetWaypoint, typeof(AIWaypoint), true);

                SerializedObject serializedConnection = new SerializedObject(toolState.ActiveConnection);
                SerializedProperty speedLimitProperty = serializedConnection.FindProperty("connectionSpeedLimit");
                SerializedProperty vehicleTypesProperty = serializedConnection.FindProperty("connectionVehicleTypes");

                serializedConnection.Update();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(speedLimitProperty, new GUIContent("Speed Limit"));
                EditorGUILayout.PropertyField(vehicleTypesProperty, new GUIContent("Vehicle Types"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedConnection.ApplyModifiedProperties();
                    EditorUtility.SetDirty(toolState.ActiveConnection);
                    toolState.RebuildActiveConnection("Update Connection Settings");
                }
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(toolState.SelectedEndingWaypoint == null && toolState.ActiveConnection == null);
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

            EditorGUI.BeginDisabledGroup(toolState.ActiveConnection == null);
            if (GUILayout.Button("Delete", GUILayout.Width(90)))
            {
                toolState.DeleteActiveConnection();
            }

            if (GUILayout.Button("Apply", GUILayout.Width(90)))
            {
                toolState.ApplyActiveConnection();
            }
            EditorGUI.EndDisabledGroup();
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
