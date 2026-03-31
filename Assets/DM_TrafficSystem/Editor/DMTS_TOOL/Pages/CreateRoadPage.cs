using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class CreateRoadPage : IPage
    {

        private const int SegmentPreviewSamples = 50;

        private RoadEditor roadEditor;
        private AILaneEditor laneEditor;

        public static bool isActive;

        private readonly bool editMode;
        private Road road;
        private bool initialized;
        private int dragIndex = -1;

        public CreateRoadPage()
        {
            InitializeEditors();
        }
        private void InitializeEditors()
        {
            if (roadEditor == null) roadEditor = ScriptableObject.CreateInstance<RoadEditor>();
            if (laneEditor == null) laneEditor = ScriptableObject.CreateInstance<AILaneEditor>();
        }

        public CreateRoadPage(Road existingRoad)
        {
            InitializeEditors();
            road = existingRoad;
            initialized = existingRoad != null;
            editMode = existingRoad != null;
            isActive = existingRoad != null;
            FocusRoadSelection();
        }

        public void OnGUI(DMTS_Window ctx)
        {
            if (TryCloseMissingRoad(ctx))
                return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(GetTitle(), EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            roadEditor.DrawRoadHelpBox();

            EditorGUILayout.Space(6);

            if (road != null)
            {
                roadEditor.DrawRoadSettings(road);
                EditorGUILayout.Space(4);
                DrawLaneConfigurations();
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!CanGenerateRoad());
            if (GUILayout.Button("Generate Road", GUILayout.Width(100)))
            {
                RoadBuilder.GenerateRoadWaypoints(road);
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ClosePage(ctx);
            }
        }

        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (TryCloseMissingRoad(ctx))
                return;

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

        private void EnsureInitialized(Vector3 firstClickPosition)
        {
            if (initialized && road != null)
                return;

            GameObject roadObject = new GameObject(GetNextRoadName());
            Undo.RegisterCreatedObjectUndo(roadObject, "Create Road");
            roadObject.transform.position = firstClickPosition;

            road = roadObject.AddComponent<Road>();
            initialized = true;
            isActive = true;

            FocusRoadSelection();
            EditorUtility.SetDirty(road);
        }

        private void DrawLaneConfigurations()
        {
            if (road == null || road.laneObjects == null || road.laneObjects.Count == 0)
                return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Lane Configurations", EditorStyles.boldLabel);

            for (int i = 0; i < road.laneObjects.Count; i++)
            {
                AILane lane = road.laneObjects[i];
                if (lane == null)
                    continue;

                laneEditor.DrawLaneSettings(lane);
                EditorGUILayout.Space(4);
            }
            EditorGUILayout.EndVertical();
        }

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

        private bool CanGenerateRoad()
        {
            return road != null && road.controlPointsList.Count >= 2;
        }

        // private static void SyncLaneWaypointSpeeds(AILane lane)
        // {
        //     if (lane == null || lane.waypoints == null)
        //         return;

        //     for (int i = 0; i < lane.waypoints.Count; i++)
        //     {
        //         AIWaypoint waypoint = lane.waypoints[i];
        //         if (waypoint == null)
        //             continue;

        //         WaypointSettings settings = waypoint.settings;
        //         settings.speed = lane.laneSpeedLimit;
        //         waypoint.settings = settings;
        //         EditorUtility.SetDirty(waypoint);
        //     }
        // }

        private string GetTitle()
        {
            return editMode && road != null
                ? $"Edit Road - {road.gameObject.name}"
                : "Create Road";
        }

        private void FocusRoadSelection()
        {
            if (road != null)
                Selection.activeGameObject = road.gameObject;
        }

        private bool TryCloseMissingRoad(DMTS_Window ctx)
        {
            if (!initialized || road != null)
                return false;

            ClosePage(ctx);
            return true;
        }

        private void ClosePage(DMTS_Window ctx)
        {
            isActive = false;
            if (ctx.pageStack.Count > 0)
                ctx.pageStack.Pop();
        }

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
