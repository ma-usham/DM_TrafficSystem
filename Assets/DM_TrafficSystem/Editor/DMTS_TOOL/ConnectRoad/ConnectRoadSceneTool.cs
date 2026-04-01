using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Handles Scene view drawing and input for the connect-road workflow.
    /// </summary>
    public class ConnectRoadSceneTool
    {
        private const float ConnectionGizmoScreenSize = 5f;
        private const float HighlightedConnectionLineWidth = 4f;

        private readonly ConnectRoadToolState toolState;

        public ConnectRoadSceneTool(ConnectRoadToolState toolState)
        {
            this.toolState = toolState;
        }

        /// <summary>
        /// Draws connection gizmos and terminal selection handles for the active connect-road page.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView)
        {
            toolState.RefreshLaneTerminalCache();
            List<ConnectRoadToolState.ConnectionRecord> connectionRecords = toolState.BuildConnectionRecords(
                filterBySceneView: true,
                sceneView: sceneView);

            DrawConnectionGizmos(connectionRecords);

            if (toolState.LaneEnds.Count == 0)
            {
                toolState.SetNoLaneEndsStatus();
                toolState.RepaintViews();
                return;
            }

            if (toolState.SelectedEndingWaypoint == null)
            {
                DrawLaneEndSelectionHandles();
                return;
            }

            if (!toolState.IsSelectedEndingStillAvailable())
            {
                toolState.SetMissingSelectedEndingStatus();
                toolState.RepaintViews();
                return;
            }

            if (toolState.LaneStarts.Count == 0)
            {
                toolState.SetNoLaneStartsStatus();
                DrawSelectedEndingWaypoint();
                toolState.RepaintViews();
                return;
            }

            DrawSelectedEndingWaypoint();
            DrawLaneStartSelectionHandles();
        }

        private void DrawLaneEndSelectionHandles()
        {
            IReadOnlyList<ConnectRoadToolState.LaneTerminal> laneEnds = toolState.LaneEnds;
            for (int i = 0; i < laneEnds.Count; i++)
            {
                ConnectRoadToolState.LaneTerminal laneEnd = laneEnds[i];
                if (!TryDrawWaypointButton(laneEnd.waypoint, DMTSPrefs.ConnectRoadEndWaypointColor))
                    continue;

                toolState.SelectEndingWaypoint(laneEnd);
                GUI.changed = true;
                break;
            }
        }

        private void DrawLaneStartSelectionHandles()
        {
            IReadOnlyList<ConnectRoadToolState.LaneTerminal> laneStarts = toolState.LaneStarts;
            for (int i = 0; i < laneStarts.Count; i++)
            {
                ConnectRoadToolState.LaneTerminal laneStart = laneStarts[i];
                AIWaypoint startWaypoint = laneStart.waypoint;
                if (startWaypoint == null || startWaypoint == toolState.SelectedEndingWaypoint)
                    continue;

                if (!TryDrawWaypointButton(startWaypoint, DMTSPrefs.ConnectRoadAvailableWaypointColor))
                    continue;

                toolState.CreateLaneConnection(laneStart);
                GUI.changed = true;
                break;
            }
        }

        private void DrawSelectedEndingWaypoint()
        {
            if (toolState.SelectedEndingWaypoint == null)
                return;

            Handles.color = DMTSPrefs.ConnectRoadSelectedWaypointColor;
            DrawFilledRectangleCap(
                0,
                toolState.SelectedEndingWaypoint.transform.position,
                GetWaypointHandleRotation(),
                GetWaypointHandleSize(toolState.SelectedEndingWaypoint.transform.position) * 1.15f,
                EventType.Repaint);
        }

        private void DrawConnectionGizmos(IReadOnlyList<ConnectRoadToolState.ConnectionRecord> connectionRecords)
        {
            if (connectionRecords == null)
                return;

            bool suppressStandardConnections = DMTS_Window.SuppressConnectPagePassiveConnectionGizmos;
            Handles.color = DMTSPrefs.ConnectRoadExistingConnectionColor;

            for (int i = 0; i < connectionRecords.Count; i++)
            {
                ConnectRoadToolState.ConnectionRecord connection = connectionRecords[i];
                if (connection.sourceWaypoint == null || connection.targetWaypoint == null)
                    continue;

                Vector3 start = connection.sourceWaypoint.transform.position;
                Vector3 end = connection.targetWaypoint.transform.position;

                if (toolState.IsViewedConnection(connection))
                {
                    Handles.color = DMTSPrefs.ConnectRoadSelectedWaypointColor;
                    Handles.DrawAAPolyLine(HighlightedConnectionLineWidth, start, end);
                    Handles.color = DMTSPrefs.ConnectRoadExistingConnectionColor;

                    if (suppressStandardConnections)
                        continue;
                }

                if (suppressStandardConnections)
                    continue;

                Handles.DrawDottedLine(start, end, ConnectionGizmoScreenSize);
            }
        }

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

        private static float GetWaypointHandleSize(Vector3 position)
        {
            return HandleUtility.GetHandleSize(position)
                * DMTSPrefs.WaypointSizeMultiplier
                * DMTSPrefs.ConnectRoadHandleSizeMultiplier;
        }

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
    }
}
