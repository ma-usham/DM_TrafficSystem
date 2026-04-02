using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Stores connect-road page state plus connection discovery and mutation helpers.
    /// </summary>
    public class ConnectRoadToolState
    {
        private readonly List<LaneTerminal> laneStarts = new List<LaneTerminal>();
        private readonly List<LaneTerminal> laneEnds = new List<LaneTerminal>();

        private AIWaypoint selectedEndingWaypoint;
        private string selectedEndingConnectionName;
        private AIWaypointConnection activeConnection;
        private AIWaypoint viewedConnectionSourceWaypoint;
        private AIWaypoint viewedConnectionTargetWaypoint;
        private string statusMessage = "Click an ending waypoint in the Scene view to begin connecting lanes.";

        public IReadOnlyList<LaneTerminal> LaneStarts => laneStarts;
        public IReadOnlyList<LaneTerminal> LaneEnds => laneEnds;
        public AIWaypoint SelectedEndingWaypoint => selectedEndingWaypoint;
        public string SelectedEndingConnectionName => selectedEndingConnectionName;
        public AIWaypointConnection ActiveConnection => activeConnection;
        public string StatusMessage => statusMessage;

        /// <summary>
        /// Rebuilds the cached lane-start and lane-end waypoint lists from the scene.
        /// </summary>
        public void RefreshLaneTerminalCache()
        {
            laneStarts.Clear();
            laneEnds.Clear();

            Road[] roads = Object.FindObjectsByType<Road>(FindObjectsInactive.Exclude);
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
        /// Builds the current connection records, including editable spline connections and legacy direct links.
        /// </summary>
        public List<ConnectionRecord> BuildConnectionRecords(bool filterBySceneView, SceneView sceneView = null)
        {
            var connectionRecords = new List<ConnectionRecord>();
            var registeredPairs = new HashSet<ulong>();

            AIWaypointConnection[] connectionObjects = Object.FindObjectsByType<AIWaypointConnection>(FindObjectsInactive.Exclude);
            for (int i = 0; i < connectionObjects.Length; i++)
            {
                AIWaypointConnection connection = connectionObjects[i];
                if (connection == null
                    || connection.sourceWaypoint == null
                    || connection.targetWaypoint == null
                    || !TryGetLaneEndTerminal(connection.sourceWaypoint, out LaneTerminal laneEnd)
                    || !TryGetLaneStartTerminal(connection.targetWaypoint, out LaneTerminal laneStart))
                {
                    continue;
                }

                if (filterBySceneView && !IsConnectionVisible(connection, sceneView))
                    continue;

                registeredPairs.Add(GetConnectionKey(connection.sourceWaypoint, connection.targetWaypoint));
                connectionRecords.Add(new ConnectionRecord(
                    laneEnd.connectionName,
                    laneStart.connectionName,
                    connection.sourceWaypoint,
                    connection.targetWaypoint,
                    connection));
            }

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

                    ulong connectionKey = GetConnectionKey(endWaypoint, targetWaypoint);
                    if (!registeredPairs.Add(connectionKey))
                        continue;

                    if (filterBySceneView
                        && !RoadSceneVisibilityUtility.IsSegmentVisible(
                            sceneView,
                            endWaypoint.transform.position,
                            targetWaypoint.transform.position,
                            1.5f))
                    {
                        continue;
                    }

                    connectionRecords.Add(new ConnectionRecord(
                        laneEnd.connectionName,
                        laneStart.connectionName,
                        endWaypoint,
                        targetWaypoint,
                        null));
                }
            }

            return connectionRecords;
        }

        /// <summary>
        /// Returns whether the selected ending waypoint is still present in the current cache.
        /// </summary>
        public bool IsSelectedEndingStillAvailable()
        {
            return IsWaypointStillAvailable(selectedEndingWaypoint, laneEnds);
        }

        /// <summary>
        /// Returns whether the actively edited connection is still valid.
        /// </summary>
        public bool IsActiveConnectionStillAvailable()
        {
            return activeConnection != null
                && activeConnection.sourceWaypoint != null
                && activeConnection.targetWaypoint != null;
        }

        /// <summary>
        /// Returns whether the provided connection is the currently viewed one.
        /// </summary>
        public bool IsViewedConnection(ConnectionRecord connection)
        {
            return viewedConnectionSourceWaypoint == connection.sourceWaypoint
                && viewedConnectionTargetWaypoint == connection.targetWaypoint;
        }

        /// <summary>
        /// Selects one lane ending waypoint as the source for the next connection.
        /// </summary>
        public void SelectEndingWaypoint(LaneTerminal laneEnd)
        {
            activeConnection = null;
            selectedEndingWaypoint = laneEnd.waypoint;
            selectedEndingConnectionName = laneEnd.connectionName;
            statusMessage = $"Selected end waypoint on {laneEnd.connectionName}. Click a lane start waypoint to create a curved connection.";
            RepaintViews();
        }

        /// <summary>
        /// Frames and highlights one connection in the Scene view, entering curve-edit mode when possible.
        /// </summary>
        public void ViewConnection(ConnectionRecord connection)
        {
            activeConnection = connection.connection;
            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;
            viewedConnectionSourceWaypoint = connection.sourceWaypoint;
            viewedConnectionTargetWaypoint = connection.targetWaypoint;

            statusMessage = connection.connection != null
                ? $"Editing {connection.label}. Drag control points to shape the turn. Connection waypoint spacing and curve resolution come from this AIWaypointConnection."
                : $"Viewing {connection.label}";

            SceneView sceneView = GetActiveSceneView();
            if (sceneView != null)
            {
                Bounds connectionBounds = ComputeConnectionBounds(connection);
                sceneView.Frame(connectionBounds, false);
            }

            RepaintViews();
        }

        /// <summary>
        /// Creates a curved road connection from the selected lane ending waypoint to the provided lane start.
        /// </summary>
        public void CreateLaneConnection(LaneTerminal laneStart)
        {
            if (selectedEndingWaypoint == null || laneStart.waypoint == null)
                return;

            if (selectedEndingWaypoint == laneStart.waypoint)
            {
                statusMessage = "Cannot connect a waypoint to itself. Choose a different lane start.";
                return;
            }

            AIWaypointConnection existingConnection = FindExistingConnection(selectedEndingWaypoint, laneStart.waypoint);
            if (existingConnection != null)
            {
                activeConnection = existingConnection;
                selectedEndingWaypoint = null;
                selectedEndingConnectionName = null;
                viewedConnectionSourceWaypoint = existingConnection.sourceWaypoint;
                viewedConnectionTargetWaypoint = existingConnection.targetWaypoint;
                statusMessage = $"Connection already exists: {GetLaneTerminalName(existingConnection.sourceWaypoint)} --> {laneStart.connectionName}. Editing the existing curve with its saved waypoint spacing and curve resolution.";
                RepaintViews();
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Road Connection");

            AIWaypointConnection connection = WaypointConnectionBuilder.CreateConnection(selectedEndingWaypoint, laneStart.waypoint);

            if (connection != null)
            {
                activeConnection = connection;
                viewedConnectionSourceWaypoint = selectedEndingWaypoint;
                viewedConnectionTargetWaypoint = laneStart.waypoint;
                statusMessage = $"{selectedEndingConnectionName} --> {laneStart.connectionName}. Connection waypoints were generated using this AIWaypointConnection's spacing and curve resolution.";
            }

            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;

            Undo.CollapseUndoOperations(undoGroup);
            RepaintViews();
        }

        /// <summary>
        /// Regenerates the currently active connection after the user edits its spline.
        /// </summary>
        public void RebuildActiveConnection(string undoLabel)
        {
            if (activeConnection == null)
                return;

            WaypointConnectionBuilder.RegenerateConnection(activeConnection, undoLabel);
            viewedConnectionSourceWaypoint = activeConnection.sourceWaypoint;
            viewedConnectionTargetWaypoint = activeConnection.targetWaypoint;
            statusMessage = $"Editing {GetConnectionLabel(activeConnection.sourceWaypoint, activeConnection.targetWaypoint)}. Connection waypoints regenerate from this AIWaypointConnection's spacing and curve resolution.";
            RepaintViews();
        }

        /// <summary>
        /// Finalizes the current connection edit and returns the tool to selection mode.
        /// </summary>
        public void ApplyActiveConnection()
        {
            if (activeConnection == null)
                return;

            WaypointConnectionBuilder.RegenerateConnection(activeConnection, "Apply Road Connection");

            string appliedLabel = GetConnectionLabel(activeConnection.sourceWaypoint, activeConnection.targetWaypoint);
            activeConnection = null;
            viewedConnectionSourceWaypoint = null;
            viewedConnectionTargetWaypoint = null;
            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;

            statusMessage = $"Applied {appliedLabel}. Choose another ending waypoint or connection.";
            RepaintViews();
        }

        /// <summary>
        /// Deletes the actively edited connection and clears the edit state.
        /// </summary>
        public void DeleteActiveConnection()
        {
            if (activeConnection == null)
                return;

            string deletedLabel = GetConnectionLabel(activeConnection.sourceWaypoint, activeConnection.targetWaypoint);
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Road Connection");

            WaypointConnectionBuilder.DeleteConnection(activeConnection, "Delete Road Connection");

            activeConnection = null;
            viewedConnectionSourceWaypoint = null;
            viewedConnectionTargetWaypoint = null;
            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;

            statusMessage = $"Deleted {deletedLabel}";
            Undo.CollapseUndoOperations(undoGroup);
            RepaintViews();
        }

        /// <summary>
        /// Deletes the selected road connection from the scene.
        /// </summary>
        public void DeleteConnection(ConnectionRecord connection)
        {
            if (connection.sourceWaypoint == null || connection.targetWaypoint == null)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Road Connection");

            if (connection.connection != null)
            {
                WaypointConnectionBuilder.DeleteConnection(connection.connection, "Delete Road Connection");
            }
            else
            {
                bool removedNext = RemoveWaypointLink(connection.sourceWaypoint, connection.targetWaypoint, useNextWaypoint: true);
                bool removedPrevious = RemoveWaypointLink(connection.targetWaypoint, connection.sourceWaypoint, useNextWaypoint: false);

                if (!removedNext && !removedPrevious)
                    statusMessage = $"Connection was already missing: {connection.label}";
            }

            if (IsViewedConnection(connection))
            {
                activeConnection = null;
                viewedConnectionSourceWaypoint = null;
                viewedConnectionTargetWaypoint = null;
            }

            statusMessage = $"Deleted {connection.label}";
            Undo.CollapseUndoOperations(undoGroup);
            RepaintViews();
        }

        /// <summary>
        /// Clears the current source selection and active connection.
        /// </summary>
        public void ClearSelection(bool resetStatus)
        {
            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;
            activeConnection = null;
            viewedConnectionSourceWaypoint = null;
            viewedConnectionTargetWaypoint = null;

            if (resetStatus)
                UpdateStatusForCurrentSelection();
        }

        /// <summary>
        /// Updates the page status text so it matches the current selection state and scene cache.
        /// </summary>
        public void UpdateStatusForCurrentSelection()
        {
            if (laneEnds.Count == 0)
            {
                statusMessage = "No generated lane ending waypoints were found. Generate roads before connecting them.";
                return;
            }

            if (activeConnection != null)
            {
                statusMessage = "Editing a curved connection. Drag control points, Ctrl+Click the curve to insert, or right-click a middle point to delete. Connection waypoint spacing and curve resolution come from the active AIWaypointConnection.";
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

            statusMessage = "Click a lane beginning waypoint in the Scene view to create the curved connection.";
        }

        /// <summary>
        /// Sets the empty-scene message used when no lane endings exist.
        /// </summary>
        public void SetNoLaneEndsStatus()
        {
            ClearSelection(resetStatus: false);
            statusMessage = "No generated lane ending waypoints were found. Generate roads before connecting them.";
        }

        /// <summary>
        /// Sets the status used when the previously selected ending waypoint has disappeared.
        /// </summary>
        public void SetMissingSelectedEndingStatus()
        {
            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;
            statusMessage = "The selected ending waypoint is no longer valid. Choose an ending waypoint again.";
        }

        /// <summary>
        /// Sets the status used when the actively edited connection has disappeared.
        /// </summary>
        public void SetMissingActiveConnectionStatus()
        {
            activeConnection = null;
            viewedConnectionSourceWaypoint = null;
            viewedConnectionTargetWaypoint = null;
            statusMessage = "The selected connection is no longer valid. Pick a connection again.";
        }

        /// <summary>
        /// Sets the status used when no lane starts are currently available to connect.
        /// </summary>
        public void SetNoLaneStartsStatus()
        {
            statusMessage = "No lane beginning waypoints are available to connect.";
        }

        /// <summary>
        /// Repaints the editor window and Scene view after state changes.
        /// </summary>
        public void RepaintViews()
        {
            DMTS_Window.editorWindow?.Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Returns the cached lane-start terminal record for the provided waypoint when one exists.
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
        /// Returns the cached lane-end terminal record for the provided waypoint when one exists.
        /// </summary>
        private bool TryGetLaneEndTerminal(AIWaypoint waypoint, out LaneTerminal terminal)
        {
            for (int i = 0; i < laneEnds.Count; i++)
            {
                if (laneEnds[i].waypoint == waypoint)
                {
                    terminal = laneEnds[i];
                    return true;
                }
            }

            terminal = default(LaneTerminal);
            return false;
        }

        /// <summary>
        /// Returns the editable connection object that already links the provided source and target waypoints.
        /// </summary>
        private AIWaypointConnection FindExistingConnection(AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
        {
            AIWaypointConnection[] connections = Object.FindObjectsByType<AIWaypointConnection>(FindObjectsInactive.Exclude);
            for (int i = 0; i < connections.Length; i++)
            {
                AIWaypointConnection connection = connections[i];
                if (connection != null
                    && connection.sourceWaypoint == sourceWaypoint
                    && connection.targetWaypoint == targetWaypoint)
                {
                    return connection;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns whether one connection's curve or straight segment is visible in the current Scene view.
        /// </summary>
        private bool IsConnectionVisible(AIWaypointConnection connection, SceneView sceneView)
        {
            List<Vector3> curvePoints = RoadSceneGizmoDrawer.GetConnectionCurvePoints(connection);
            if (curvePoints.Count == 0)
            {
                return RoadSceneVisibilityUtility.IsSegmentVisible(
                    sceneView,
                    connection.sourceWaypoint.transform.position,
                    connection.targetWaypoint.transform.position,
                    1.5f);
            }

            Bounds bounds = new Bounds(curvePoints[0], Vector3.zero);
            for (int i = 1; i < curvePoints.Count; i++)
            {
                bounds.Encapsulate(curvePoints[i]);
            }

            bounds.Expand(2f);
            return RoadSceneVisibilityUtility.IsBoundsVisible(sceneView, bounds);
        }

        /// <summary>
        /// Computes framing bounds for the provided connection record.
        /// </summary>
        private static Bounds ComputeConnectionBounds(ConnectionRecord connection)
        {
            if (connection.connection != null)
            {
                List<Vector3> curvePoints = RoadSceneGizmoDrawer.GetConnectionCurvePoints(connection.connection);
                if (curvePoints.Count > 0)
                {
                    Bounds bounds = new Bounds(curvePoints[0], Vector3.zero);
                    for (int i = 1; i < curvePoints.Count; i++)
                    {
                        bounds.Encapsulate(curvePoints[i]);
                    }

                    bounds.Expand(4f);
                    return bounds;
                }
            }

            Bounds fallbackBounds = new Bounds(connection.sourceWaypoint.transform.position, Vector3.zero);
            fallbackBounds.Encapsulate(connection.targetWaypoint.transform.position);
            fallbackBounds.Expand(4f);
            return fallbackBounds;
        }

        /// <summary>
        /// Removes one waypoint link from the chosen previous or next array on the owner waypoint.
        /// </summary>
        private bool RemoveWaypointLink(AIWaypoint ownerWaypoint, AIWaypoint linkedWaypoint, bool useNextWaypoint)
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
        /// Returns a deduplicated waypoint array with the requested waypoint removed.
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
        /// Returns whether two waypoint link arrays contain the same references in the same order.
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
        /// Returns whether one waypoint still exists inside the provided cached terminal list.
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
        /// Returns the most descriptive lane-terminal label available for the provided waypoint.
        /// </summary>
        private string GetLaneTerminalName(AIWaypoint waypoint)
        {
            if (TryGetLaneEndTerminal(waypoint, out LaneTerminal laneEnd))
                return laneEnd.connectionName;

            if (TryGetLaneStartTerminal(waypoint, out LaneTerminal laneStart))
                return laneStart.connectionName;

            return waypoint != null ? waypoint.name : "waypoint";
        }

        /// <summary>
        /// Builds the display label used for one source-to-target road connection.
        /// </summary>
        private string GetConnectionLabel(AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
        {
            return $"{GetLaneTerminalName(sourceWaypoint)} --> {GetLaneTerminalName(targetWaypoint)}";
        }

        /// <summary>
        /// Builds a unique key for one source-to-target waypoint pair.
        /// </summary>
        private static ulong GetConnectionKey(AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
        {
            uint sourceId = sourceWaypoint != null ? unchecked((uint)RuntimeHelpers.GetHashCode(sourceWaypoint)) : 0u;
            uint targetId = targetWaypoint != null ? unchecked((uint)RuntimeHelpers.GetHashCode(targetWaypoint)) : 0u;
            return ((ulong)sourceId << 32) | targetId;
        }

        /// <summary>
        /// Returns the best currently active Scene view for framing and visibility queries.
        /// </summary>
        private static SceneView GetActiveSceneView()
        {
            return SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
        }

        /// <summary>
        /// Stores the waypoint metadata needed for one selectable lane terminal.
        /// </summary>
        public struct LaneTerminal
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
        public struct ConnectionRecord
        {
            public readonly string label;
            public readonly AIWaypoint sourceWaypoint;
            public readonly AIWaypoint targetWaypoint;
            public readonly AIWaypointConnection connection;

            public ConnectionRecord(
                string sourceConnectionName,
                string targetConnectionName,
                AIWaypoint sourceWaypoint,
                AIWaypoint targetWaypoint,
                AIWaypointConnection connection)
            {
                label = $"{sourceConnectionName} --> {targetConnectionName}";
                this.sourceWaypoint = sourceWaypoint;
                this.targetWaypoint = targetWaypoint;
                this.connection = connection;
            }
        }
    }
}
