using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Handles scene-view road editing, including point placement, dragging, insertion, and deletion.
    /// </summary>
    public class RoadSceneTool
    {
        private const int SegmentPreviewSamples = 50;

        private Road road;
        private bool initialized;
        private int dragIndex = -1;

        public RoadSceneTool()
        {
        }

        public RoadSceneTool(Road existingRoad)
        {
            road = existingRoad;
            initialized = existingRoad != null;
            FocusRoadSelection();
        }

        public Road Road => road;

        public bool CanGenerateRoad => road != null && road.controlPointsList.Count >= 2;

        public bool HasMissingRoad => initialized && road == null;

        /// <summary>
        /// Draws the active road editing overlays and processes scene input for the current page.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (road == null || road.splineMoveMode == SplineMoveMode.Move2D)
                HandleUtility.AddDefaultControl(controlId);

            if (road != null)
            {
                DrawCurve();
                DrawPoints();
                DrawInsertPreview();
            }

            ProcessInput(controlId, ctx);
            sceneView.Repaint();
        }

        /// <summary>
        /// Creates the road object on the first shift-click so later interactions have a valid target.
        /// </summary>
        private void EnsureInitialized(Vector3 firstClickPosition)
        {
            if (initialized && road != null)
                return;

            GameObject roadObject = new GameObject(GetNextRoadName());
            Undo.RegisterCreatedObjectUndo(roadObject, "Create Road");
            roadObject.transform.position = firstClickPosition;

            road = roadObject.AddComponent<Road>();
            initialized = true;
            CreateRoadPage.isActive = true;

            FocusRoadSelection();
            EditorUtility.SetDirty(road);
        }

        /// <summary>
        /// Draws the editable bezier spline that connects the road control points.
        /// </summary>
        private void DrawCurve()
        {
            var points = road.controlPointsList;
            if (points.Count < 2)
                return;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 startPoint = points[i];
                Vector3 endPoint = points[i + 1];
                SplineMathUtils.GetSegmentHandles(points, i, out Vector3 handleA, out Vector3 handleB);
                Handles.DrawBezier(startPoint, endPoint, handleA, handleB, DMTSPrefs.CurveColor, null, DMTSPrefs.CurveEditWidth);
            }
        }

        /// <summary>
        /// Draws control points, endpoint highlighting, labels, and optional 3D move handles.
        /// </summary>
        private void DrawPoints()
        {
            var points = road.controlPointsList;
            int pointCount = points.Count;

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 controlPoint = points[i];
                bool isDraggedPoint = i == dragIndex;
                bool isSplineEnd = i == pointCount - 1 && pointCount >= 2;

                if (isSplineEnd)
                {
                    Color pointColor = isDraggedPoint
                        ? DMTSPrefs.DraggedControlPointColor
                        : DMTSPrefs.ActiveEndColor;

                    Handles.color = pointColor;
                    Handles.SphereHandleCap(
                        0,
                        controlPoint,
                        Quaternion.identity,
                        DMTSPrefs.ControlPointHandleSize * 2f,
                        EventType.Repaint);

                    if (Camera.current != null)
                    {
                        Handles.color = new Color(pointColor.r, pointColor.g, pointColor.b, 0.4f);
                        Handles.DrawWireDisc(
                            controlPoint,
                            Camera.current.transform.forward,
                            DMTSPrefs.ControlPointHandleSize * 2.5f);
                    }
                }
                else
                {
                    Handles.color = isDraggedPoint
                        ? DMTSPrefs.DraggedControlPointColor
                        : DMTSPrefs.ControlPointColor;
                    Handles.SphereHandleCap(
                        0,
                        controlPoint,
                        Quaternion.identity,
                        DMTSPrefs.ControlPointHandleSize * 2f,
                        EventType.Repaint);
                }

                GUIStyle labelStyle = new GUIStyle
                {
                    normal = { textColor = Color.white },
                    fontStyle = FontStyle.Bold,
                    fontSize = DMTSPrefs.PointLabelFontSize
                };

                Handles.Label(
                    controlPoint + Vector3.up * DMTSPrefs.ControlPointHandleSize * 0.35f,
                    $"[{i}]",
                    labelStyle);

                if (road.splineMoveMode == SplineMoveMode.Move3D)
                    Draw3DMoveHandle(i);
            }
        }

        /// <summary>
        /// Draws the insert preview marker when the user is holding Ctrl near a spline segment.
        /// </summary>
        private void DrawInsertPreview()
        {
            Event currentEvent = Event.current;
            if (!currentEvent.control || road.controlPointsList.Count < 2)
                return;

            FindNearestSegmentScreenSpace(
                currentEvent.mousePosition,
                out int segmentIndex,
                out float segmentT,
                out float screenDistance);

            if (segmentIndex < 0 || screenDistance > DMTSPrefs.InsertScreenThreshold)
                return;

            Vector3 startPoint = road.controlPointsList[segmentIndex];
            Vector3 endPoint = road.controlPointsList[segmentIndex + 1];
            SplineMathUtils.GetSegmentHandles(road.controlPointsList, segmentIndex, out Vector3 handleA, out Vector3 handleB);
            Vector3 previewPoint = SplineMathUtils.EvaluateCubicBezier(startPoint, handleA, handleB, endPoint, segmentT);

            Handles.color = DMTSPrefs.InsertPreviewColor;
            Handles.SphereHandleCap(
                0,
                previewPoint,
                Quaternion.identity,
                DMTSPrefs.ControlPointHandleSize * 2f,
                EventType.Repaint);

            GUIStyle style = new GUIStyle
            {
                normal = { textColor = DMTSPrefs.InsertPreviewColor },
                fontSize = DMTSPrefs.InsertLabelFontSize
            };

            Handles.Label(
                previewPoint + Vector3.up * DMTSPrefs.ControlPointHandleSize * 5f,
                "Ctrl+Click to insert",
                style);
        }

        /// <summary>
        /// Routes scene input to the correct interaction handler based on the current mouse event.
        /// </summary>
        private void ProcessInput(int controlId, DMTS_Window ctx)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                HandleLeftDown(currentEvent, controlId, ctx);
            }
            else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && dragIndex >= 0)
            {
                HandleDrag(currentEvent, ctx);
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && dragIndex >= 0)
            {
                dragIndex = -1;
                GUIUtility.hotControl = 0;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            {
                HandleRightClick(currentEvent, ctx);
            }
        }

        /// <summary>
        /// Handles left-click actions for creating roads, dragging points, inserting points, and extending the spline.
        /// </summary>
        private void HandleLeftDown(Event currentEvent, int controlId, DMTS_Window ctx)
        {
            if (currentEvent.shift && !initialized)
                EnsureInitialized(GetWorldPosition(currentEvent.mousePosition));

            if (!initialized)
                return;

            int pointCount = road.controlPointsList.Count;

            if (currentEvent.control && pointCount >= 2 && TryInsert(currentEvent.mousePosition, ctx))
            {
                currentEvent.Use();
                return;
            }

            int nearestPoint = ScreenNearestPoint(currentEvent.mousePosition, out float nearestDistance);

            if (nearestPoint >= 0
                && pointCount >= 2
                && (nearestPoint == 0 || nearestPoint == pointCount - 1)
                && nearestDistance < DMTSPrefs.EndpointScreenRadius)
            {
                dragIndex = road.splineMoveMode == SplineMoveMode.Move2D ? nearestPoint : -1;
                if (road.splineMoveMode == SplineMoveMode.Move2D)
                    GUIUtility.hotControl = controlId;

                currentEvent.Use();
                ctx.Repaint();
                return;
            }

            if (road.splineMoveMode == SplineMoveMode.Move2D
                && nearestPoint >= 0
                && nearestDistance < DMTSPrefs.PointScreenRadius)
            {
                dragIndex = nearestPoint;
                GUIUtility.hotControl = controlId;
                currentEvent.Use();
                return;
            }

            if (!currentEvent.shift)
                return;

            Vector3 worldPosition = GetWorldPosition(currentEvent.mousePosition);
            Undo.RecordObject(road, "Add Control Point");
            road.AddControlPoint(worldPosition);
            EditorUtility.SetDirty(road);

            currentEvent.Use();
            ctx.Repaint();
        }

        /// <summary>
        /// Moves the currently dragged control point in 2D editing mode.
        /// </summary>
        private void HandleDrag(Event currentEvent, DMTS_Window ctx)
        {
            if (road == null || road.splineMoveMode == SplineMoveMode.Move3D)
                return;

            if (dragIndex < 0 || dragIndex >= road.controlPointsList.Count)
                return;

            Vector3 newPosition = GetWorldPosition(currentEvent.mousePosition);
            Undo.RecordObject(road, "Move Control Point");
            road.controlPointsList[dragIndex] = newPosition;
            EditorUtility.SetDirty(road);
            GUI.changed = true;

            currentEvent.Use();
            ctx.Repaint();
        }

        /// <summary>
        /// Deletes the nearest control point when the user right-clicks close enough to it.
        /// </summary>
        private void HandleRightClick(Event currentEvent, DMTS_Window ctx)
        {
            if (road == null)
                return;

            int nearestPoint = ScreenNearestPoint(currentEvent.mousePosition, out float distance);
            if (nearestPoint < 0 || distance > DMTSPrefs.EndpointScreenRadius)
                return;

            Undo.RecordObject(road, "Delete Control Point");
            road.RemoveControlPoint(nearestPoint);
            EditorUtility.SetDirty(road);

            currentEvent.Use();
            ctx.Repaint();
        }

        /// <summary>
        /// Inserts a new control point on the nearest segment under the mouse cursor.
        /// </summary>
        private bool TryInsert(Vector2 mousePosition, DMTS_Window ctx)
        {
            FindNearestSegmentScreenSpace(mousePosition, out int segmentIndex, out float segmentT, out float screenDistance);

            if (segmentIndex < 0 || screenDistance > DMTSPrefs.InsertScreenThreshold)
                return false;

            Vector3 startPoint = road.controlPointsList[segmentIndex];
            Vector3 endPoint = road.controlPointsList[segmentIndex + 1];
            SplineMathUtils.GetSegmentHandles(road.controlPointsList, segmentIndex, out Vector3 handleA, out Vector3 handleB);
            Vector3 insertPosition = SplineMathUtils.EvaluateCubicBezier(startPoint, handleA, handleB, endPoint, segmentT);

            Undo.RecordObject(road, "Insert Control Point");
            road.InsertControlPoint(segmentIndex + 1, insertPosition);
            EditorUtility.SetDirty(road);
            ctx.Repaint();
            return true;
        }

        /// <summary>
        /// Finds the control point whose screen position is nearest to the mouse cursor.
        /// </summary>
        private int ScreenNearestPoint(Vector2 mousePosition, out float bestDistance)
        {
            bestDistance = float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < road.controlPointsList.Count; i++)
            {
                Vector2 screenPoint = HandleUtility.WorldToGUIPoint(road.controlPointsList[i]);
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
        /// Finds the closest sampled point on the spline in screen space for insert-preview and insertion logic.
        /// </summary>
        private void FindNearestSegmentScreenSpace(
            Vector2 mousePosition,
            out int bestSegment,
            out float bestT,
            out float bestScreenDistance)
        {
            bestSegment = -1;
            bestT = 0f;
            bestScreenDistance = float.MaxValue;

            var points = road.controlPointsList;
            if (points.Count < 2)
                return;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 startPoint = points[i];
                Vector3 endPoint = points[i + 1];
                SplineMathUtils.GetSegmentHandles(points, i, out Vector3 handleA, out Vector3 handleB);

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
        /// Converts a scene-view mouse position into a world position using colliders first and a ground plane fallback.
        /// </summary>
        private static Vector3 GetWorldPosition(Vector2 mousePosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return hit.point;

            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
                return ray.GetPoint(enter);

            return ray.GetPoint(10f);
        }

        /// <summary>
        /// Draws and applies a standard Unity position handle for one control point in 3D mode.
        /// </summary>
        private void Draw3DMoveHandle(int index)
        {
            Vector3 controlPoint = road.controlPointsList[index];
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(controlPoint, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(road, "Move Control Point");
            road.controlPointsList[index] = newPosition;
            EditorUtility.SetDirty(road);
            GUI.changed = true;
        }

        /// <summary>
        /// Selects the current road object in the Unity hierarchy.
        /// </summary>
        private void FocusRoadSelection()
        {
            if (road != null)
                Selection.activeGameObject = road.gameObject;
        }

        /// <summary>
        /// Returns the next available default road name in the scene.
        /// </summary>
        private static string GetNextRoadName()
        {
            Road[] roads = Object.FindObjectsByType<Road>(FindObjectsInactive.Include);
            int nextIndex = 1;

            while (true)
            {
                string candidateName = $"Road_{nextIndex}";
                bool alreadyExists = false;

                for (int i = 0; i < roads.Length; i++)
                {
                    if (roads[i] != null && roads[i].gameObject.name == candidateName)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                    return candidateName;

                nextIndex++;
            }
        }
    }
}
