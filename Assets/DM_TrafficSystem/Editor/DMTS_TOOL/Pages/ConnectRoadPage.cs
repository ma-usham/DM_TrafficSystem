using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Provides a scene-driven workflow for connecting lane end waypoints to lane start waypoints.
    /// </summary>
    public class ConnectRoadPage : IPage
    {
        private const float ConnectionGizmoScreenSize = 5f;
        private const float HighlightedConnectionLineWidth = 4f;

        private readonly List<LaneTerminal> laneStarts = new List<LaneTerminal>();
        private readonly List<LaneTerminal> laneEnds = new List<LaneTerminal>();

        private Vector2 connectionScrollPosition;
        private AIWaypoint selectedEndingWaypoint;
        private string selectedEndingConnectionName;
        private AIWaypoint viewedConnectionSourceWaypoint;
        private AIWaypoint viewedConnectionTargetWaypoint;
        private string statusMessage = "Click an ending waypoint in the Scene view to begin connecting lanes.";

        public static bool isActive;

        /// <summary>
        /// Draws the lane-connection instructions, selection state, and existing connection list.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            RefreshLaneTerminalCache();
            List<ConnectionRecord> allConnectionRecords = BuildConnectionRecords(filterBySceneView: false);
            List<ConnectionRecord> connectionRecords = BuildConnectionRecords(filterBySceneView: true);

            EditorGUILayout.LabelField("Connect Roads", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            string instructions = selectedEndingWaypoint == null
                ? "Scene step 1: click any lane ending waypoint."
                : "Scene step 2: click a lane beginning waypoint to create the connection.";
            EditorGUILayout.HelpBox(instructions, MessageType.Info);

            EditorGUILayout.LabelField("Status", statusMessage, EditorStyles.wordWrappedLabel);

            if (selectedEndingWaypoint != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Selected End", selectedEndingConnectionName);
                EditorGUILayout.ObjectField("Waypoint", selectedEndingWaypoint, typeof(AIWaypoint), true);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(selectedEndingWaypoint == null);
            if (GUILayout.Button("Clear Selection", GUILayout.Width(120)))
            {
                ClearSelection(resetStatus: true);
                DMTS_Window.editorWindow?.Repaint();
                SceneView.RepaintAll();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Refresh Scene Data", GUILayout.Width(120)))
            {
                RefreshLaneTerminalCache();
                UpdateStatusForCurrentSelection();
                DMTS_Window.editorWindow?.Repaint();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Road Connections", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            if (connectionRecords.Count == 0)
            {
                EditorGUILayout.LabelField(
                    allConnectionRecords.Count == 0
                        ? "No road connections created."
                        : "No road connections are visible in the current Scene view.");
            }
            else
            {
                connectionScrollPosition = EditorGUILayout.BeginScrollView(connectionScrollPosition, GUILayout.MaxHeight(220f));
                for (int i = 0; i < connectionRecords.Count; i++)
                {
                    ConnectionRecord connection = connectionRecords[i];

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField(connection.label, EditorStyles.wordWrappedLabel);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("View", GUILayout.Width(70)))
                    {
                        ViewConnection(connection);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                        return;
                    }

                    if (GUILayout.Button("Delete", GUILayout.Width(70)))
                    {
                        DeleteConnection(connection);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                        return;
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ClearSelection(resetStatus: true);
                ctx.pageStack.Pop();
                DMTS_Window.editorWindow?.Repaint();
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Draws only connectable lane terminals in the scene and creates links from the selected pair.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            RefreshLaneTerminalCache();
            List<ConnectionRecord> connectionRecords = BuildConnectionRecords(filterBySceneView: true);

            DrawConnectionGizmos(connectionRecords);

            if (laneEnds.Count == 0)
            {
                ClearSelection(resetStatus: false);
                statusMessage = "No generated lane ending waypoints were found. Generate roads before connecting them.";
                DMTS_Window.editorWindow?.Repaint();
                return;
            }

            if (selectedEndingWaypoint == null)
            {
                DrawLaneEndSelectionHandles();
                return;
            }

            if (!IsWaypointStillAvailable(selectedEndingWaypoint, laneEnds))
            {
                ClearSelection(resetStatus: false);
                statusMessage = "The selected ending waypoint is no longer valid. Choose an ending waypoint again.";
                DMTS_Window.editorWindow?.Repaint();
                return;
            }

            if (laneStarts.Count == 0)
            {
                statusMessage = "No lane beginning waypoints are available to connect.";
                DrawSelectedEndingWaypoint();
                DMTS_Window.editorWindow?.Repaint();
                return;
            }

            DrawSelectedEndingWaypoint();
            DrawLaneStartSelectionHandles();
        }

        /// <summary>
        /// Frames the selected connection in the scene and highlights its gizmo.
        /// </summary>
        private void ViewConnection(ConnectionRecord connection)
        {
            viewedConnectionSourceWaypoint = connection.sourceWaypoint;
            viewedConnectionTargetWaypoint = connection.targetWaypoint;
            statusMessage = $"Viewing {connection.label}";

            SceneView sceneView = SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sceneView != null && connection.sourceWaypoint != null && connection.targetWaypoint != null)
            {
                Bounds connectionBounds = new Bounds(connection.sourceWaypoint.transform.position, Vector3.zero);
                connectionBounds.Encapsulate(connection.targetWaypoint.transform.position);
                connectionBounds.Expand(4f);
                sceneView.Frame(connectionBounds, false);
            }

            DMTS_Window.editorWindow?.Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Deletes the selected road connection from both participating waypoints.
        /// </summary>
        private void DeleteConnection(ConnectionRecord connection)
        {
            if (connection.sourceWaypoint == null || connection.targetWaypoint == null)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Road Connection");

            bool removedNext = RemoveWaypointLink(connection.sourceWaypoint, connection.targetWaypoint, useNextWaypoint: true);
            bool removedPrevious = RemoveWaypointLink(connection.targetWaypoint, connection.sourceWaypoint, useNextWaypoint: false);

            if (removedNext || removedPrevious)
            {
                statusMessage = $"Deleted {connection.label}";
            }
            else
            {
                statusMessage = $"Connection was already missing: {connection.label}";
            }

            if (viewedConnectionSourceWaypoint == connection.sourceWaypoint
                && viewedConnectionTargetWaypoint == connection.targetWaypoint)
            {
                viewedConnectionSourceWaypoint = null;
                viewedConnectionTargetWaypoint = null;
            }

            Undo.CollapseUndoOperations(undoGroup);
            DMTS_Window.editorWindow?.Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Draws clickable red rectangle handles for every lane ending waypoint in the scene.
        /// </summary>
        private void DrawLaneEndSelectionHandles()
        {
            for (int i = 0; i < laneEnds.Count; i++)
            {
                LaneTerminal laneEnd = laneEnds[i];
                if (!TryDrawWaypointButton(laneEnd.waypoint, DMTSPrefs.ConnectRoadEndWaypointColor))
                    continue;

                selectedEndingWaypoint = laneEnd.waypoint;
                selectedEndingConnectionName = laneEnd.connectionName;
                statusMessage = $"Selected end waypoint on {laneEnd.connectionName}. Click a lane start waypoint to connect.";
                GUI.changed = true;
                DMTS_Window.editorWindow?.Repaint();
                SceneView.RepaintAll();
                break;
            }
        }

        /// <summary>
        /// Draws clickable green rectangle handles for every lane start waypoint once an ending waypoint has been chosen.
        /// </summary>
        private void DrawLaneStartSelectionHandles()
        {
            for (int i = 0; i < laneStarts.Count; i++)
            {
                LaneTerminal laneStart = laneStarts[i];
                AIWaypoint startWaypoint = laneStart.waypoint;
                if (startWaypoint == null || startWaypoint == selectedEndingWaypoint)
                    continue;

                if (!TryDrawWaypointButton(startWaypoint, DMTSPrefs.ConnectRoadAvailableWaypointColor))
                    continue;

                CreateLaneConnection(selectedEndingWaypoint, startWaypoint, laneStart.connectionName);
                GUI.changed = true;
                DMTS_Window.editorWindow?.Repaint();
                SceneView.RepaintAll();
                break;
            }
        }

        /// <summary>
        /// Highlights the currently selected ending waypoint with a green rectangle.
        /// </summary>
        private void DrawSelectedEndingWaypoint()
        {
            if (selectedEndingWaypoint == null)
                return;

            Handles.color = DMTSPrefs.ConnectRoadSelectedWaypointColor;
            DrawFilledRectangleCap(
                0,
                selectedEndingWaypoint.transform.position,
                GetWaypointHandleRotation(),
                GetWaypointHandleSize(selectedEndingWaypoint.transform.position) * 1.15f,
                EventType.Repaint);
        }

        /// <summary>
        /// Draws gizmos for every existing road connection and highlights the viewed connection when applicable.
        /// </summary>
        private void DrawConnectionGizmos(IReadOnlyList<ConnectionRecord> connectionRecords)
        {
            if (connectionRecords == null)
                return;

            Handles.color = DMTSPrefs.ConnectRoadExistingConnectionColor;

            for (int i = 0; i < connectionRecords.Count; i++)
            {
                ConnectionRecord connection = connectionRecords[i];
                if (connection.sourceWaypoint == null || connection.targetWaypoint == null)
                    continue;

                Vector3 start = connection.sourceWaypoint.transform.position;
                Vector3 end = connection.targetWaypoint.transform.position;

                if (viewedConnectionSourceWaypoint == connection.sourceWaypoint
                    && viewedConnectionTargetWaypoint == connection.targetWaypoint)
                {
                    Handles.color = DMTSPrefs.ConnectRoadSelectedWaypointColor;
                    Handles.DrawAAPolyLine(HighlightedConnectionLineWidth, start, end);
                    Handles.color = DMTSPrefs.ConnectRoadExistingConnectionColor;
                    continue;
                }

                Handles.DrawDottedLine(start, end, ConnectionGizmoScreenSize);
            }
        }

        /// <summary>
        /// Creates a bidirectional road connection between the selected lane end and the chosen lane start.
        /// </summary>
        private void CreateLaneConnection(AIWaypoint endingWaypoint, AIWaypoint startingWaypoint, string destinationConnectionName)
        {
            if (endingWaypoint == null || startingWaypoint == null)
                return;

            if (endingWaypoint == startingWaypoint)
            {
                statusMessage = "Cannot connect a waypoint to itself. Choose a different lane start.";
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Road Lanes");

            bool updatedNext = AppendWaypointLink(endingWaypoint, startingWaypoint, useNextWaypoint: true);
            bool updatedPrevious = AppendWaypointLink(startingWaypoint, endingWaypoint, useNextWaypoint: false);

            if (updatedNext || updatedPrevious)
            {
                statusMessage = $"{selectedEndingConnectionName} --> {destinationConnectionName}";
            }
            else
            {
                statusMessage = $"Connection already exists: {selectedEndingConnectionName} --> {destinationConnectionName}";
            }

            Undo.CollapseUndoOperations(undoGroup);
            ClearSelection(resetStatus: false);
        }

        /// <summary>
        /// Appends a waypoint link while removing null entries and avoiding duplicates.
        /// </summary>
        private static bool AppendWaypointLink(AIWaypoint ownerWaypoint, AIWaypoint linkedWaypoint, bool useNextWaypoint)
        {
            if (ownerWaypoint == null || linkedWaypoint == null)
                return false;

            WaypointSettings settings = ownerWaypoint.settings;
            AIWaypoint[] currentLinks = useNextWaypoint ? settings.nextWaypoint : settings.previousWaypoint;
            AIWaypoint[] updatedLinks = BuildUniqueWaypointLinkArray(currentLinks, linkedWaypoint);

            if (WaypointArraysEqual(currentLinks, updatedLinks))
                return false;

            Undo.RecordObject(ownerWaypoint, "Connect Road Lanes");

            if (useNextWaypoint)
            {
                settings.nextWaypoint = updatedLinks;
            }
            else
            {
                settings.previousWaypoint = updatedLinks;
            }

            ownerWaypoint.settings = settings;
            EditorUtility.SetDirty(ownerWaypoint);
            return true;
        }

        /// <summary>
        /// Removes one waypoint link while compacting null entries and preserving the remaining order.
        /// </summary>
        private static bool RemoveWaypointLink(AIWaypoint ownerWaypoint, AIWaypoint linkedWaypoint, bool useNextWaypoint)
        {
            if (ownerWaypoint == null || linkedWaypoint == null)
                return false;

            WaypointSettings settings = ownerWaypoint.settings;
            AIWaypoint[] currentLinks = useNextWaypoint ? settings.nextWaypoint : settings.previousWaypoint;
            AIWaypoint[] updatedLinks = BuildFilteredWaypointLinkArray(currentLinks, linkedWaypoint);

            if (WaypointArraysEqual(currentLinks, updatedLinks))
                return false;

            Undo.RecordObject(ownerWaypoint, "Delete Road Connection");

            if (useNextWaypoint)
            {
                settings.nextWaypoint = updatedLinks;
            }
            else
            {
                settings.previousWaypoint = updatedLinks;
            }

            ownerWaypoint.settings = settings;
            EditorUtility.SetDirty(ownerWaypoint);
            return true;
        }

        /// <summary>
        /// Builds a compact waypoint array that preserves order and contains each waypoint at most once.
        /// </summary>
        private static AIWaypoint[] BuildUniqueWaypointLinkArray(IReadOnlyList<AIWaypoint> existingLinks, AIWaypoint candidate)
        {
            var uniqueLinks = new List<AIWaypoint>();

            if (existingLinks != null)
            {
                for (int i = 0; i < existingLinks.Count; i++)
                {
                    AIWaypoint existingLink = existingLinks[i];
                    if (existingLink != null && !uniqueLinks.Contains(existingLink))
                        uniqueLinks.Add(existingLink);
                }
            }

            if (candidate != null && !uniqueLinks.Contains(candidate))
                uniqueLinks.Add(candidate);

            return uniqueLinks.Count > 0
                ? uniqueLinks.ToArray()
                : System.Array.Empty<AIWaypoint>();
        }

        /// <summary>
        /// Builds a compact waypoint array while removing null entries and the specific candidate.
        /// </summary>
        private static AIWaypoint[] BuildFilteredWaypointLinkArray(IReadOnlyList<AIWaypoint> existingLinks, AIWaypoint candidateToRemove)
        {
            var remainingLinks = new List<AIWaypoint>();

            if (existingLinks != null)
            {
                for (int i = 0; i < existingLinks.Count; i++)
                {
                    AIWaypoint existingLink = existingLinks[i];
                    if (existingLink == null || existingLink == candidateToRemove || remainingLinks.Contains(existingLink))
                        continue;

                    remainingLinks.Add(existingLink);
                }
            }

            return remainingLinks.Count > 0
                ? remainingLinks.ToArray()
                : System.Array.Empty<AIWaypoint>();
        }

        /// <summary>
        /// Returns whether two waypoint arrays contain the same references in the same order.
        /// </summary>
        private static bool WaypointArraysEqual(IReadOnlyList<AIWaypoint> first, IReadOnlyList<AIWaypoint> second)
        {
            int firstCount = first != null ? first.Count : 0;
            int secondCount = second != null ? second.Count : 0;
            if (firstCount != secondCount)
                return false;

            for (int i = 0; i < firstCount; i++)
            {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether the waypoint still exists inside the current lane-terminal cache.
        /// </summary>
        private static bool IsWaypointStillAvailable(AIWaypoint waypoint, IReadOnlyList<LaneTerminal> terminals)
        {
            if (waypoint == null || terminals == null)
                return false;

            for (int i = 0; i < terminals.Count; i++)
            {
                if (terminals[i].waypoint == waypoint)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the start-terminal metadata for the given waypoint when it is a connectable lane start.
        /// </summary>
        private bool TryGetLaneStartTerminal(AIWaypoint waypoint, out LaneTerminal terminal)
        {
            for (int i = 0; i < laneStarts.Count; i++)
            {
                if (laneStarts[i].waypoint == waypoint)
                {
                    terminal = laneStarts[i];
                    return true;
                }
            }

            terminal = default(LaneTerminal);
            return false;
        }

        /// <summary>
        /// Builds the human-readable connection list shown in the page UI.
        /// </summary>
        private List<ConnectionRecord> BuildConnectionRecords(bool filterBySceneView)
        {
            var connectionRecords = new List<ConnectionRecord>();

            for (int i = 0; i < laneEnds.Count; i++)
            {
                LaneTerminal laneEnd = laneEnds[i];
                AIWaypoint endWaypoint = laneEnd.waypoint;
                if (endWaypoint == null || endWaypoint.settings.nextWaypoint == null)
                    continue;

                for (int connectionIndex = 0; connectionIndex < endWaypoint.settings.nextWaypoint.Length; connectionIndex++)
                {
                    AIWaypoint targetWaypoint = endWaypoint.settings.nextWaypoint[connectionIndex];
                    if (targetWaypoint == null || !TryGetLaneStartTerminal(targetWaypoint, out LaneTerminal laneStart))
                        continue;

                    if (filterBySceneView
                        && !IsConnectionVisibleInSceneView(
                            endWaypoint.transform.position,
                            targetWaypoint.transform.position))
                    {
                        continue;
                    }

                    connectionRecords.Add(new ConnectionRecord(
                        laneEnd.connectionName,
                        laneStart.connectionName,
                        endWaypoint,
                        targetWaypoint));
                }
            }

            return connectionRecords;
        }

        /// <summary>
        /// Returns whether a connection segment is visible inside the active Scene view camera.
        /// </summary>
        private static bool IsConnectionVisibleInSceneView(Vector3 sourcePosition, Vector3 targetPosition)
        {
            SceneView sceneView = SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sceneView == null || sceneView.camera == null)
                return true;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(sceneView.camera);
            Bounds connectionBounds = new Bounds(sourcePosition, Vector3.zero);
            connectionBounds.Encapsulate(targetPosition);
            connectionBounds.Expand(1.5f);

            return GeometryUtility.TestPlanesAABB(frustumPlanes, connectionBounds);
        }

        /// <summary>
        /// Rebuilds the cached lane-start and lane-end waypoint lists from the scene.
        /// </summary>
        private void RefreshLaneTerminalCache()
        {
            laneStarts.Clear();
            laneEnds.Clear();

            Road[] roads = Object.FindObjectsOfType<Road>();
            for (int roadIndex = 0; roadIndex < roads.Length; roadIndex++)
            {
                Road road = roads[roadIndex];
                if (road == null || road.laneObjects == null)
                    continue;

                for (int laneIndex = 0; laneIndex < road.laneObjects.Count; laneIndex++)
                {
                    AILane lane = road.laneObjects[laneIndex];
                    if (lane == null || lane.waypoints == null || lane.waypoints.Count == 0)
                        continue;

                    AIWaypoint startWaypoint = lane.waypoints[0];
                    if (startWaypoint != null)
                        laneStarts.Add(new LaneTerminal(road, laneIndex, startWaypoint));

                    AIWaypoint endWaypoint = lane.waypoints[lane.waypoints.Count - 1];
                    if (endWaypoint != null)
                        laneEnds.Add(new LaneTerminal(road, laneIndex, endWaypoint));
                }
            }
        }

        /// <summary>
        /// Clears the current scene selection state.
        /// </summary>
        private void ClearSelection(bool resetStatus)
        {
            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;

            if (resetStatus)
                UpdateStatusForCurrentSelection();
        }

        /// <summary>
        /// Updates the page status text so it matches the current selection state and scene cache.
        /// </summary>
        private void UpdateStatusForCurrentSelection()
        {
            if (laneEnds.Count == 0)
            {
                statusMessage = "No generated lane ending waypoints were found. Generate roads before connecting them.";
                return;
            }

            if (selectedEndingWaypoint == null)
            {
                statusMessage = "Click an ending waypoint in the Scene view to begin connecting lanes.";
                return;
            }

            if (laneStarts.Count == 0)
            {
                statusMessage = "No lane beginning waypoints are available to connect.";
                return;
            }

            statusMessage = "Click a lane beginning waypoint in the Scene view to create the connection.";
        }

        /// <summary>
        /// Draws an interactive rectangle handle for one connectable waypoint.
        /// </summary>
        private static bool TryDrawWaypointButton(AIWaypoint waypoint, Color color)
        {
            if (waypoint == null)
                return false;

            Vector3 position = waypoint.transform.position;
            float handleSize = GetWaypointHandleSize(position);
            Quaternion handleRotation = GetWaypointHandleRotation();

            Handles.color = color;
            return Handles.Button(
                position,
                handleRotation,
                handleSize,
                handleSize,
                DrawFilledRectangleCap);
        }

        /// <summary>
        /// Draws a small filled rectangle and provides hit testing for connect-road waypoint buttons.
        /// </summary>
        private static void DrawFilledRectangleCap(int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                    HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(position, size * 0.5f));
                    break;

                case EventType.Repaint:
                    float halfSize = size * 0.5f;
                    Vector3 right = rotation * Vector3.right * halfSize;
                    Vector3 up = rotation * Vector3.up * halfSize;
                    Vector3[] vertices =
                    {
                        position - right - up,
                        position - right + up,
                        position + right + up,
                        position + right - up
                    };

                    Handles.DrawSolidRectangleWithOutline(vertices, Handles.color, Handles.color);
                    break;
            }
        }

        /// <summary>
        /// Returns the screen-consistent size used for scene waypoint rectangles.
        /// </summary>
        private static float GetWaypointHandleSize(Vector3 position)
        {
            return HandleUtility.GetHandleSize(position)
                * DMTSPrefs.WaypointSizeMultiplier
                * DMTSPrefs.ConnectRoadHandleSizeMultiplier;
        }

        /// <summary>
        /// Returns a camera-facing rotation so rectangle handles stay readable in the scene.
        /// </summary>
        private static Quaternion GetWaypointHandleRotation()
        {
            SceneView sceneView = SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Transform cameraTransform = sceneView.camera.transform;
                return Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
            }

            return Quaternion.identity;
        }

        /// <summary>
        /// Stores the waypoint metadata needed for one selectable lane terminal.
        /// </summary>
        private struct LaneTerminal
        {
            public readonly AIWaypoint waypoint;
            public readonly string connectionName;

            public LaneTerminal(Road road, int laneIndex, AIWaypoint waypoint)
            {
                this.waypoint = waypoint;
                string roadName = road != null && !string.IsNullOrEmpty(road.name) ? road.name : "road";
                roadName = roadName.Replace(' ', '_');
                connectionName = $"{roadName}_lane_{laneIndex}";
            }
        }

        /// <summary>
        /// Stores one existing road connection for UI actions and scene gizmos.
        /// </summary>
        private struct ConnectionRecord
        {
            public readonly string sourceConnectionName;
            public readonly string targetConnectionName;
            public readonly string label;
            public readonly AIWaypoint sourceWaypoint;
            public readonly AIWaypoint targetWaypoint;

            public ConnectionRecord(string sourceConnectionName, string targetConnectionName, AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
            {
                this.sourceConnectionName = sourceConnectionName;
                this.targetConnectionName = targetConnectionName;
                label = $"{sourceConnectionName} --> {targetConnectionName}";
                this.sourceWaypoint = sourceWaypoint;
                this.targetWaypoint = targetWaypoint;
            }
        }
    }
}
