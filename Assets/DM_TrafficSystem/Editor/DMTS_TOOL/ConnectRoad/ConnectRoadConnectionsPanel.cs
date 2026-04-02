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
        private readonly struct ConnectionGroup
        {
            public readonly string roadName;
            public readonly List<ConnectRoadToolState.ConnectionRecord> connections;

            public ConnectionGroup(string roadName)
            {
                this.roadName = roadName;
                connections = new List<ConnectRoadToolState.ConnectionRecord>();
            }
        }

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
            List<ConnectionGroup> groupedConnections = BuildSourceRoadGroups(visibleConnections);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(220f));
            for (int groupIndex = 0; groupIndex < groupedConnections.Count; groupIndex++)
            {
                ConnectionGroup group = groupedConnections[groupIndex];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(group.roadName, EditorStyles.boldLabel);
                EditorGUILayout.Space(2f);

                for (int connectionIndex = 0; connectionIndex < group.connections.Count; connectionIndex++)
                {
                    ConnectRoadToolState.ConnectionRecord connection = group.connections[connectionIndex];

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

        /// <summary>
        /// Groups connection records by their source road name for a shorter UI list.
        /// </summary>
        private static List<ConnectionGroup> BuildSourceRoadGroups(IReadOnlyList<ConnectRoadToolState.ConnectionRecord> visibleConnections)
        {
            var groups = new List<ConnectionGroup>();

            if (visibleConnections == null)
                return groups;

            for (int i = 0; i < visibleConnections.Count; i++)
            {
                ConnectRoadToolState.ConnectionRecord connection = visibleConnections[i];
                string roadName = GetSourceRoadName(connection);
                int groupIndex = FindGroupIndex(groups, roadName);

                if (groupIndex < 0)
                {
                    groups.Add(new ConnectionGroup(roadName));
                    groupIndex = groups.Count - 1;
                }

                groups[groupIndex].connections.Add(connection);
            }

            return groups;
        }

        /// <summary>
        /// Returns the index of the group that matches the provided source road name.
        /// </summary>
        private static int FindGroupIndex(IReadOnlyList<ConnectionGroup> groups, string roadName)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].roadName == roadName)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Returns the source road name shown for one connection record.
        /// </summary>
        private static string GetSourceRoadName(ConnectRoadToolState.ConnectionRecord connection)
        {
            if (connection.sourceWaypoint == null)
                return "Unknown Road";

            Road road = connection.sourceWaypoint.GetComponentInParent<Road>();
            if (road == null || string.IsNullOrWhiteSpace(road.name))
                return "Unknown Road";

            return road.name;
        }
    }
}
