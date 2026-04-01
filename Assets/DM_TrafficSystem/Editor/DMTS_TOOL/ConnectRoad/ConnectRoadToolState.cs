using System.Collections.Generic;
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
        private AIWaypoint viewedConnectionSourceWaypoint;
        private AIWaypoint viewedConnectionTargetWaypoint;
        private string statusMessage = "Click an ending waypoint in the Scene view to begin connecting lanes.";

        public IReadOnlyList<LaneTerminal> LaneStarts => laneStarts;
        public IReadOnlyList<LaneTerminal> LaneEnds => laneEnds;
        public AIWaypoint SelectedEndingWaypoint => selectedEndingWaypoint;
        public string SelectedEndingConnectionName => selectedEndingConnectionName;
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
        /// Builds the current connection records, optionally filtering them to the active Scene view camera.
        /// </summary>
        public List<ConnectionRecord> BuildConnectionRecords(bool filterBySceneView, SceneView sceneView = null)
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
                        targetWaypoint));
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
            selectedEndingWaypoint = laneEnd.waypoint;
            selectedEndingConnectionName = laneEnd.connectionName;
            statusMessage = $"Selected end waypoint on {laneEnd.connectionName}. Click a lane start waypoint to connect.";
            RepaintViews();
        }

        /// <summary>
        /// Frames and highlights one connection in the Scene view.
        /// </summary>
        public void ViewConnection(ConnectionRecord connection)
        {
            viewedConnectionSourceWaypoint = connection.sourceWaypoint;
            viewedConnectionTargetWaypoint = connection.targetWaypoint;
            statusMessage = $"Viewing {connection.label}";

            SceneView sceneView = GetActiveSceneView();
            if (sceneView != null && connection.sourceWaypoint != null && connection.targetWaypoint != null)
            {
                Bounds connectionBounds = new Bounds(connection.sourceWaypoint.transform.position, Vector3.zero);
                connectionBounds.Encapsulate(connection.targetWaypoint.transform.position);
                connectionBounds.Expand(4f);
                sceneView.Frame(connectionBounds, false);
            }

            RepaintViews();
        }

        /// <summary>
        /// Creates a bidirectional road connection from the selected ending waypoint to the provided lane start.
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

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Road Lanes");

            bool updatedNext = AppendWaypointLink(selectedEndingWaypoint, laneStart.waypoint, useNextWaypoint: true);
            bool updatedPrevious = AppendWaypointLink(laneStart.waypoint, selectedEndingWaypoint, useNextWaypoint: false);

            if (updatedNext || updatedPrevious)
            {
                statusMessage = $"{selectedEndingConnectionName} --> {laneStart.connectionName}";
            }
            else
            {
                statusMessage = $"Connection already exists: {selectedEndingConnectionName} --> {laneStart.connectionName}";
            }

            Undo.CollapseUndoOperations(undoGroup);
            ClearSelection(resetStatus: false);
            RepaintViews();
        }

        /// <summary>
        /// Deletes the selected road connection from both participating waypoints.
        /// </summary>
        public void DeleteConnection(ConnectionRecord connection)
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

            if (IsViewedConnection(connection))
            {
                viewedConnectionSourceWaypoint = null;
                viewedConnectionTargetWaypoint = null;
            }

            Undo.CollapseUndoOperations(undoGroup);
            RepaintViews();
        }

        /// <summary>
        /// Clears the current source selection while optionally restoring the default status text.
        /// </summary>
        public void ClearSelection(bool resetStatus)
        {
            selectedEndingWaypoint = null;
            selectedEndingConnectionName = null;

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
            ClearSelection(resetStatus: false);
            statusMessage = "The selected ending waypoint is no longer valid. Choose an ending waypoint again.";
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

            public ConnectionRecord(string sourceConnectionName, string targetConnectionName, AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
            {
                label = $"{sourceConnectionName} --> {targetConnectionName}";
                this.sourceWaypoint = sourceWaypoint;
                this.targetWaypoint = targetWaypoint;
            }
        }
    }
}
