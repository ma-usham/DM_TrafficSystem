using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws the connection list and per-connection actions for the connect-road page.
    /// </summary>
    public static class ConnectRoadConnectionsPanel
    {
        /// <summary>
        /// Draws the visible connection list and handles view/delete actions after layout completes.
        /// </summary>
        public static void Draw(
            ConnectRoadToolState toolState,
            IReadOnlyList<ConnectRoadToolState.ConnectionRecord> visibleConnections,
            int totalConnectionCount,
            ref Vector2 scrollPosition)
        {
            EditorGUILayout.LabelField("Road Connections", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            if (visibleConnections == null || visibleConnections.Count == 0)
            {
                EditorGUILayout.LabelField(
                    totalConnectionCount == 0
                        ? "No road connections created."
                        : "No road connections are visible in the current Scene view.");
                EditorGUILayout.EndVertical();
                return;
            }

            bool hasViewAction = false;
            bool hasDeleteAction = false;
            ConnectRoadToolState.ConnectionRecord pendingConnection = default(ConnectRoadToolState.ConnectionRecord);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(220f));
            for (int i = 0; i < visibleConnections.Count; i++)
            {
                ConnectRoadToolState.ConnectionRecord connection = visibleConnections[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(connection.label, EditorStyles.wordWrappedLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("View", GUILayout.Width(70)))
                {
                    pendingConnection = connection;
                    hasViewAction = true;
                }

                if (GUILayout.Button("Delete", GUILayout.Width(70)))
                {
                    pendingConnection = connection;
                    hasDeleteAction = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (hasDeleteAction)
            {
                toolState.DeleteConnection(pendingConnection);
                return;
            }

            if (hasViewAction)
                toolState.ViewConnection(pendingConnection);
        }
    }
}
