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
        private const int SegmentPreviewSamples = 50;

        private readonly ConnectRoadToolState toolState;

        public ConnectRoadSceneTool(ConnectRoadToolState toolState)
        {
            this.toolState = toolState;
        }

        /// <summary>
        /// Draws connection gizmos, selection handles, and connection spline editing controls.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView)
        {
            toolState.RefreshLaneTerminalCache();
            List<ConnectRoadToolState.ConnectionRecord> connectionRecords = toolState.BuildConnectionRecords(
                filterBySceneView: true,
                sceneView: sceneView);

            DrawConnectionGizmos(connectionRecords);

            if (toolState.ActiveConnection != null)
            {
                if (!toolState.IsActiveConnectionStillAvailable())
                {
                    toolState.SetMissingActiveConnectionStatus();
                    toolState.RepaintViews();
                    return;
                }

                DrawActiveConnectionEditor(toolState.ActiveConnection);
                return;
            }

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

        /// <summary>
        /// Draws selectable handles for every lane-ending waypoint that can start a connection.
        /// </summary>
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

        /// <summary>
        /// Draws selectable handles for every lane-start waypoint that can receive a connection.
        /// </summary>
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

        /// <summary>
        /// Highlights the currently selected ending waypoint while the user chooses a target lane start.
        /// </summary>
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

        /// <summary>
        /// Draws passive connection previews, emphasizing the currently viewed connection when needed.
        /// </summary>
        private void DrawConnectionGizmos(IReadOnlyList<ConnectRoadToolState.ConnectionRecord> connectionRecords)
        {
            if (connectionRecords == null)
                return;

            bool suppressStandardConnections = DMTS_Window.SuppressConnectPagePassiveConnectionGizmos;

            for (int i = 0; i < connectionRecords.Count; i++)
            {
                ConnectRoadToolState.ConnectionRecord connection = connectionRecords[i];
                if (connection.sourceWaypoint == null || connection.targetWaypoint == null)
                    continue;

                bool isViewedConnection = toolState.IsViewedConnection(connection);
                if (!isViewedConnection && suppressStandardConnections)
                    continue;

                if (connection.connection != null)
                {
                    RoadSceneGizmoDrawer.DrawConnectionCurve(
                        connection.connection,
                        isViewedConnection ? DMTSPrefs.ConnectRoadSelectedWaypointColor : DMTSPrefs.ConnectRoadExistingConnectionColor,
                        isViewedConnection ? DMTSPrefs.ConnectRoadSelectedCurveWidth : DMTSPrefs.ConnectRoadCurveWidth);

                    if (isViewedConnection)
                        RoadSceneGizmoDrawer.DrawConnectionTransitionWaypoints(connection.connection);

                    continue;
                }

                Handles.color = isViewedConnection
                    ? DMTSPrefs.ConnectRoadSelectedWaypointColor
                    : DMTSPrefs.ConnectRoadExistingConnectionColor;
                Handles.DrawDottedLine(
                    connection.sourceWaypoint.transform.position,
                    connection.targetWaypoint.transform.position,
                    ConnectionGizmoScreenSize);
            }
        }

        /// <summary>
        /// Draws the editable connection spline, its generated transition waypoints, and edit handles.
        /// </summary>
        private void DrawActiveConnectionEditor(AIWaypointConnection connection)
        {
            if (connection == null)
                return;

            connection.SyncEndpointControlPoints();

            RoadSceneGizmoDrawer.DrawConnectionCurve(
                connection,
                DMTSPrefs.ConnectRoadSelectedWaypointColor,
                DMTSPrefs.ConnectRoadSelectedCurveWidth);
            RoadSceneGizmoDrawer.DrawConnectionTransitionWaypoints(connection);

            DrawConnectionInsertPreview(connection);
            ProcessActiveConnectionInput(connection);
            DrawConnectionControlPoints(connection);
        }

        /// <summary>
        /// Draws movable interior control points and locked endpoint markers for the active connection.
        /// </summary>
        private void DrawConnectionControlPoints(AIWaypointConnection connection)
        {
            if (connection == null || connection.controlPointsList == null)
                return;

            for (int i = 0; i < connection.controlPointsList.Count; i++)
            {
                Vector3 controlPoint = connection.controlPointsList[i];
                bool isEndpoint = i == 0 || i == connection.controlPointsList.Count - 1;

                if (isEndpoint)
                {
                    Handles.color = DMTSPrefs.ConnectRoadCurveAnchorColor;
                    DrawFilledRectangleCap(
                        0,
                        controlPoint,
                        GetWaypointHandleRotation(),
                        GetWaypointHandleSize(controlPoint) * 0.9f,
                        EventType.Repaint);
                    continue;
                }

                Handles.color = DMTSPrefs.ConnectRoadCurveControlPointColor;
                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.FreeMoveHandle(
                    controlPoint,
                    HandleUtility.GetHandleSize(controlPoint) * 0.08f,
                    Vector3.zero,
                    Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(connection, "Move Connection Control Point");
                    connection.controlPointsList[i] = newPosition;
                    toolState.RebuildActiveConnection("Move Connection Control Point");
                }

                Handles.Label(
                    controlPoint + Vector3.up * HandleUtility.GetHandleSize(controlPoint) * 0.1f,
                    $"[{i}]",
                    EditorStyles.whiteMiniLabel);
            }
        }

        /// <summary>
        /// Draws the preview marker that shows where Ctrl+Click will insert a new control point.
        /// </summary>
        private void DrawConnectionInsertPreview(AIWaypointConnection connection)
        {
            Event currentEvent = Event.current;
            if (!currentEvent.control || connection == null || connection.controlPointsList == null || connection.controlPointsList.Count < 2)
                return;

            FindNearestSegmentScreenSpace(
                connection.controlPointsList,
                currentEvent.mousePosition,
                out int segmentIndex,
                out float segmentT,
                out float screenDistance);

            if (segmentIndex < 0 || screenDistance > DMTSPrefs.InsertScreenThreshold)
                return;

            Vector3 startPoint = connection.controlPointsList[segmentIndex];
            Vector3 endPoint = connection.controlPointsList[segmentIndex + 1];
            SplineMathUtils.GetSegmentHandles(connection.controlPointsList, segmentIndex, out Vector3 handleA, out Vector3 handleB);
            Vector3 previewPoint = SplineMathUtils.EvaluateCubicBezier(startPoint, handleA, handleB, endPoint, segmentT);

            Handles.color = DMTSPrefs.InsertPreviewColor;
            Handles.SphereHandleCap(
                0,
                previewPoint,
                Quaternion.identity,
                HandleUtility.GetHandleSize(previewPoint) * 0.12f,
                EventType.Repaint);

            Handles.Label(
                previewPoint + Vector3.up * HandleUtility.GetHandleSize(previewPoint) * 0.2f,
                "Ctrl+Click to insert",
                EditorStyles.whiteMiniLabel);
        }

        /// <summary>
        /// Routes mouse input for inserting or deleting active connection control points.
        /// </summary>
        private void ProcessActiveConnectionInput(AIWaypointConnection connection)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 0
                && currentEvent.control
                && TryInsertConnectionControlPoint(connection, currentEvent.mousePosition))
            {
                currentEvent.Use();
                GUI.changed = true;
                return;
            }

            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 1
                && TryDeleteConnectionControlPoint(connection, currentEvent.mousePosition))
            {
                currentEvent.Use();
                GUI.changed = true;
            }
        }

        /// <summary>
        /// Inserts a control point on the nearest visible spline segment under the mouse cursor.
        /// </summary>
        private bool TryInsertConnectionControlPoint(AIWaypointConnection connection, Vector2 mousePosition)
        {
            if (connection == null || connection.controlPointsList == null || connection.controlPointsList.Count < 2)
                return false;

            FindNearestSegmentScreenSpace(
                connection.controlPointsList,
                mousePosition,
                out int segmentIndex,
                out float segmentT,
                out float screenDistance);

            if (segmentIndex < 0 || screenDistance > DMTSPrefs.InsertScreenThreshold)
                return false;

            Vector3 startPoint = connection.controlPointsList[segmentIndex];
            Vector3 endPoint = connection.controlPointsList[segmentIndex + 1];
            SplineMathUtils.GetSegmentHandles(connection.controlPointsList, segmentIndex, out Vector3 handleA, out Vector3 handleB);
            Vector3 insertPosition = SplineMathUtils.EvaluateCubicBezier(startPoint, handleA, handleB, endPoint, segmentT);

            Undo.RecordObject(connection, "Insert Connection Control Point");
            connection.InsertControlPoint(segmentIndex + 1, insertPosition);
            toolState.RebuildActiveConnection("Insert Connection Control Point");
            return true;
        }

        /// <summary>
        /// Deletes the nearest interior control point when the cursor is close enough to it.
        /// </summary>
        private bool TryDeleteConnectionControlPoint(AIWaypointConnection connection, Vector2 mousePosition)
        {
            if (connection == null || connection.controlPointsList == null || connection.controlPointsList.Count <= 2)
                return false;

            int nearestPointIndex = FindNearestInteriorPoint(connection.controlPointsList, mousePosition, out float distance);
            if (nearestPointIndex < 0 || distance > DMTSPrefs.EndpointScreenRadius)
                return false;

            Undo.RecordObject(connection, "Delete Connection Control Point");
            connection.RemoveControlPoint(nearestPointIndex);
            toolState.RebuildActiveConnection("Delete Connection Control Point");
            return true;
        }

        /// <summary>
        /// Returns the interior control point closest to the mouse cursor in screen space.
        /// </summary>
        private static int FindNearestInteriorPoint(IReadOnlyList<Vector3> controlPoints, Vector2 mousePosition, out float bestDistance)
        {
            bestDistance = float.MaxValue;
            int bestIndex = -1;

            if (controlPoints == null)
                return bestIndex;

            for (int i = 1; i < controlPoints.Count - 1; i++)
            {
                Vector2 screenPoint = HandleUtility.WorldToGUIPoint(controlPoints[i]);
                float distance = Vector2.Distance(mousePosition, screenPoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Finds the nearest sampled spline segment location to the current mouse position.
        /// </summary>
        private static void FindNearestSegmentScreenSpace(
            IReadOnlyList<Vector3> controlPoints,
            Vector2 mousePosition,
            out int bestSegment,
            out float bestT,
            out float bestScreenDistance)
        {
            bestSegment = -1;
            bestT = 0f;
            bestScreenDistance = float.MaxValue;

            if (controlPoints == null || controlPoints.Count < 2)
                return;

            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                Vector3 startPoint = controlPoints[i];
                Vector3 endPoint = controlPoints[i + 1];
                SplineMathUtils.GetSegmentHandles(controlPoints, i, out Vector3 handleA, out Vector3 handleB);

                for (int sampleIndex = 0; sampleIndex <= SegmentPreviewSamples; sampleIndex++)
                {
                    float t = sampleIndex / (float)SegmentPreviewSamples;
                    Vector3 worldPoint = SplineMathUtils.EvaluateCubicBezier(startPoint, handleA, handleB, endPoint, t);
                    Vector2 screenPoint = HandleUtility.WorldToGUIPoint(worldPoint);
                    float distance = Vector2.Distance(mousePosition, screenPoint);

                    if (distance < bestScreenDistance)
                    {
                        bestScreenDistance = distance;
                        bestSegment = i;
                        bestT = t;
                    }
                }
            }
        }

        /// <summary>
        /// Draws one clickable waypoint handle and returns whether it was pressed this frame.
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
        /// Draws a filled rectangular handle cap used by the connect-road waypoint buttons.
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
        /// Returns the screen-scaled handle size used for connect-road waypoint markers.
        /// </summary>
        private static float GetWaypointHandleSize(Vector3 position)
        {
            return HandleUtility.GetHandleSize(position)
                * DMTSPrefs.WaypointSizeMultiplier
                * DMTSPrefs.ConnectRoadHandleSizeMultiplier;
        }

        /// <summary>
        /// Returns a billboard rotation so rectangular waypoint handles face the active Scene view camera.
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
    }
}
