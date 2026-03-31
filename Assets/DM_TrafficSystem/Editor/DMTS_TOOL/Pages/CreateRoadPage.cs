using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class CreateRoadPage : IPage
    {
        private const float MinLaneWidth = 0.1f;
        private const float MinSpeedLimit = 0f;
        private const int SegmentPreviewSamples = 50;

        public static bool isActive;

        private readonly bool editMode;
        private Road routeCreator;
        private bool initialized;
        private int dragIndex = -1;

        public CreateRoadPage()
        {
        }

        public CreateRoadPage(Road existingRoad)
        {
            routeCreator = existingRoad;
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

            EditorGUILayout.HelpBox(
                "\u2022 Shift + Left-click in the scene to place control points at the spline end\n" +
                "\u2022 Ctrl + Left-click near a curve segment to insert a point\n" +
                "\u2022 Right-click a control point to delete it\n" +
                "\u2022 Drag any control point to reposition it in Move2D\n" +
                "\u2022 Use the move handle to reposition points on x, y, z in Move3D",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (routeCreator != null)
            {
                DrawSettings();
                EditorGUILayout.Space(4);
                DrawLaneConfigurations();
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!CanGenerateRoad());
            if (GUILayout.Button("Generate Road", GUILayout.Width(100)))
            {
                RoadBuilder.GenerateRoadWaypoints(routeCreator);
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
            if (routeCreator == null || routeCreator.splineMoveMode == SplineMoveMode.Move2D)
                HandleUtility.AddDefaultControl(controlId);

            if (routeCreator != null)
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
            if (initialized && routeCreator != null)
                return;

            GameObject roadObject = new GameObject(GetNextRoadName());
            Undo.RegisterCreatedObjectUndo(roadObject, "Create Road");
            roadObject.transform.position = firstClickPosition;

            routeCreator = roadObject.AddComponent<Road>();
            initialized = true;
            isActive = true;

            FocusRoadSelection();
            EditorUtility.SetDirty(routeCreator);
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Road Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            SplineMoveMode newMoveMode =
                (SplineMoveMode)EditorGUILayout.EnumPopup("Move Mode", routeCreator.splineMoveMode);
            int newLaneCount = EditorGUILayout.IntSlider("Lanes", routeCreator.lanes, 1, 8);
            float newLaneWidth = Mathf.Max(MinLaneWidth, EditorGUILayout.FloatField("Lane Width", routeCreator.laneWidth));
            int newWaypointDistance = EditorGUILayout.IntSlider("Waypoint Distance", routeCreator.waypointDistance, 1, 15);
            float newSpeedLimit = Mathf.Max(MinSpeedLimit, EditorGUILayout.FloatField("Speed Limit", routeCreator.speedLimitForAllRoads));
            int newCurveResolution = EditorGUILayout.IntSlider("Curve Smoothness", routeCreator.curveResolution, 10, 100);
            DrivingDirection newDrivingDirection =
                (DrivingDirection)EditorGUILayout.EnumPopup("Driving Direction", routeCreator.drivingDirection);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(routeCreator, "Change Road Settings");
                routeCreator.splineMoveMode = newMoveMode;
                routeCreator.lanes = newLaneCount;
                routeCreator.laneWidth = newLaneWidth;
                routeCreator.waypointDistance = newWaypointDistance;
                routeCreator.speedLimitForAllRoads = newSpeedLimit;
                routeCreator.curveResolution = newCurveResolution;
                routeCreator.drivingDirection = newDrivingDirection;
                EditorUtility.SetDirty(routeCreator);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Draw Direction", "End ->", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Points", routeCreator.controlPointsList.Count.ToString());

            EditorGUILayout.EndVertical();
        }

        private void DrawLaneConfigurations()
        {
            if (routeCreator == null || routeCreator.laneObjects == null || routeCreator.laneObjects.Count == 0)
                return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Lane Configurations", EditorStyles.boldLabel);

            for (int i = 0; i < routeCreator.laneObjects.Count; i++)
            {
                AILane lane = routeCreator.laneObjects[i];
                if (lane == null)
                    continue;

                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.LabelField($"Lane {i + 1}", EditorStyles.boldLabel);

                SerializedObject serializedLane = new SerializedObject(lane);
                SerializedProperty waypointsProperty = serializedLane.FindProperty("waypoints");
                SerializedProperty speedLimitProperty = serializedLane.FindProperty("laneSpeedLimit");

                serializedLane.Update();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(speedLimitProperty, new GUIContent("Speed Limit"));
                EditorGUILayout.LabelField("Waypoint Count", waypointsProperty.arraySize.ToString());
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(waypointsProperty, true);
                EditorGUI.EndDisabledGroup();

                if (EditorGUI.EndChangeCheck())
                {
                    serializedLane.ApplyModifiedProperties();
                    SyncLaneWaypointSpeeds(lane);
                    EditorUtility.SetDirty(lane);
                }

                if (GUILayout.Button("Select Lane Object"))
                {
                    Selection.activeGameObject = lane.gameObject;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCurve()
        {
            var points = routeCreator.controlPointsList;
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
            var points = routeCreator.controlPointsList;
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

                if (routeCreator.splineMoveMode == SplineMoveMode.Move3D)
                    Draw3DMoveHandle(i);
            }
        }

        private void DrawInsertPreview()
        {
            Event currentEvent = Event.current;
            if (!currentEvent.control || routeCreator.controlPointsList.Count < 2)
                return;

            FindNearestSegmentScreenSpace(
                currentEvent.mousePosition,
                out int segmentIndex,
                out float segmentT,
                out float screenDistance);

            if (segmentIndex < 0 || screenDistance > DMTSPrefs.InsertScreenThreshold)
                return;

            Vector3 startPoint = routeCreator.controlPointsList[segmentIndex];
            Vector3 endPoint = routeCreator.controlPointsList[segmentIndex + 1];
            SplineMathUtils.GetSegmentHandles(routeCreator.controlPointsList, segmentIndex, out Vector3 handleA, out Vector3 handleB);
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

            int pointCount = routeCreator.controlPointsList.Count;

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
                dragIndex = routeCreator.splineMoveMode == SplineMoveMode.Move2D ? nearestPoint : -1;
                if (routeCreator.splineMoveMode == SplineMoveMode.Move2D)
                    GUIUtility.hotControl = controlId;

                currentEvent.Use();
                ctx.Repaint();
                return;
            }

            if (routeCreator.splineMoveMode == SplineMoveMode.Move2D
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
            Undo.RecordObject(routeCreator, "Add Control Point");
            routeCreator.AddControlPoint(worldPosition);
            EditorUtility.SetDirty(routeCreator);

            currentEvent.Use();
            ctx.Repaint();
        }

        private void HandleDrag(Event currentEvent, DMTS_Window ctx)
        {
            if (routeCreator == null || routeCreator.splineMoveMode == SplineMoveMode.Move3D)
                return;

            if (dragIndex < 0 || dragIndex >= routeCreator.controlPointsList.Count)
                return;

            Vector3 newPosition = GetWorldPosition(currentEvent.mousePosition);
            Undo.RecordObject(routeCreator, "Move Control Point");
            routeCreator.controlPointsList[dragIndex] = newPosition;
            EditorUtility.SetDirty(routeCreator);
            GUI.changed = true;

            currentEvent.Use();
            ctx.Repaint();
        }

        private void HandleRightClick(Event currentEvent, DMTS_Window ctx)
        {
            if (routeCreator == null)
                return;

            int nearestPoint = ScreenNearestPoint(currentEvent.mousePosition, out float distance);
            if (nearestPoint < 0 || distance > DMTSPrefs.EndpointScreenRadius)
                return;

            Undo.RecordObject(routeCreator, "Delete Control Point");
            routeCreator.RemoveControlPoint(nearestPoint);
            EditorUtility.SetDirty(routeCreator);

            currentEvent.Use();
            ctx.Repaint();
        }

        private bool TryInsert(Vector2 mousePosition, DMTS_Window ctx)
        {
            FindNearestSegmentScreenSpace(mousePosition, out int segmentIndex, out float segmentT, out float screenDistance);

            if (segmentIndex < 0 || screenDistance > DMTSPrefs.InsertScreenThreshold)
                return false;

            Vector3 startPoint = routeCreator.controlPointsList[segmentIndex];
            Vector3 endPoint = routeCreator.controlPointsList[segmentIndex + 1];
            SplineMathUtils.GetSegmentHandles(routeCreator.controlPointsList, segmentIndex, out Vector3 handleA, out Vector3 handleB);
            Vector3 insertPosition = SplineMathUtils.EvaluateCubicBezier(startPoint, handleA, handleB, endPoint, segmentT);

            Undo.RecordObject(routeCreator, "Insert Control Point");
            routeCreator.InsertControlPoint(segmentIndex + 1, insertPosition);
            EditorUtility.SetDirty(routeCreator);
            ctx.Repaint();
            return true;
        }

        private int ScreenNearestPoint(Vector2 mousePosition, out float bestDistance)
        {
            bestDistance = float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < routeCreator.controlPointsList.Count; i++)
            {
                Vector2 screenPoint = HandleUtility.WorldToGUIPoint(routeCreator.controlPointsList[i]);
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

            var points = routeCreator.controlPointsList;
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
            Vector3 controlPoint = routeCreator.controlPointsList[index];
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(controlPoint, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(routeCreator, "Move Control Point");
            routeCreator.controlPointsList[index] = newPosition;
            EditorUtility.SetDirty(routeCreator);
            GUI.changed = true;
        }

        private bool CanGenerateRoad()
        {
            return routeCreator != null && routeCreator.controlPointsList.Count >= 2;
        }

        private static void SyncLaneWaypointSpeeds(AILane lane)
        {
            if (lane == null || lane.waypoints == null)
                return;

            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                AIWaypoint waypoint = lane.waypoints[i];
                if (waypoint == null)
                    continue;

                WaypointSettings settings = waypoint.settings;
                settings.speed = lane.laneSpeedLimit;
                waypoint.settings = settings;
                EditorUtility.SetDirty(waypoint);
            }
        }

        private string GetTitle()
        {
            return editMode && routeCreator != null
                ? $"Edit Road - {routeCreator.gameObject.name}"
                : "Create Road";
        }

        private void FocusRoadSelection()
        {
            if (routeCreator != null)
                Selection.activeGameObject = routeCreator.gameObject;
        }

        private bool TryCloseMissingRoad(DMTS_Window ctx)
        {
            if (!initialized || routeCreator != null)
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
