using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class CreateRoadPage : IPage
    {
        public static bool isActive;
        public static int roadCount = 0;

        private SplineRoadCreator routeCreator;
        private bool initialized;
        private bool editMode;
        private Editor_DMWindow windowCtx;

        private int dragIndex = -1;

        

        public CreateRoadPage() { }

        public CreateRoadPage(SplineRoadCreator existingRoad)
        {
            routeCreator = existingRoad;
            initialized = true;
            editMode = true;
            isActive = true;
            Selection.activeGameObject = existingRoad.gameObject;
        }

        // ───────────────── Initialization ─────────────────

        private void EnsureInitialized(Vector3 firstClickPosition)
        {

            if (initialized && routeCreator != null) return;

            GameObject go = new GameObject("Road_Route_"+ (++roadCount));
            go.transform.position = firstClickPosition;
            routeCreator = go.AddComponent<SplineRoadCreator>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Road Route");
            initialized = true;
            isActive = true;
        }

        // ───────────────── Inspector GUI ─────────────────

        public void OnGUI(Editor_DMWindow ctx)
        {
            windowCtx = ctx;

            if (initialized && routeCreator == null)
            {
                isActive = false;
                ctx.pageStack.Pop();
                return;
            }
            if(routeCreator) Selection.activeGameObject = routeCreator.gameObject;

            EditorGUILayout.Space(4);
            string title = (editMode && routeCreator != null)
                ? $"Edit Road — {routeCreator.gameObject.name}"
                : "Create Road";
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "\u2022 Shift + Left-click in the scene to place control points at the spline end\n" +
                "\u2022 Ctrl + Left-click near a curve segment to insert a point\n" +
                "\u2022 Right-click a control point to delete it\n" +
                "\u2022 Drag any control point to reposition it in Move2D\n" +
                "\u2022 Use the move handle to reposition points on x,y,z in Move3D",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (routeCreator != null)
            {
                DrawSettings();
                EditorGUILayout.Space(4);
                //DrawPointList();
            }

            GUILayout.FlexibleSpace();

            bool canGenerate = routeCreator != null && routeCreator.controlPointsList.Count >= 2;
            EditorGUI.BeginDisabledGroup(!canGenerate);
            if (GUILayout.Button("Generate Road", GUILayout.Width(100)))
            {
                Undo.RegisterFullObjectHierarchyUndo(routeCreator.gameObject, "Generate Road Waypoints");
                routeCreator.GenerateRoadWaypoints();
                EditorUtility.SetDirty(routeCreator);
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                isActive = false;
                ctx.pageStack.Pop();
            }
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Road Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            SplineMoveMode newMoveMode =
                (SplineMoveMode)EditorGUILayout.EnumPopup("Move Mode", routeCreator.splineMoveMode);
            int newLaneCount = EditorGUILayout.IntSlider("Lanes", routeCreator.lanes, 1, 8);
            float newLaneWidth = EditorGUILayout.FloatField("Lane Width", routeCreator.laneWidth);
            int newWaypointDistance = EditorGUILayout.IntSlider("Waypoint Distance", routeCreator.waypointDistance, 1, 15);
            float newSpeedLimit = EditorGUILayout.FloatField("Speed Limit", routeCreator.speedLimitForAllRoads);
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
            EditorGUILayout.LabelField("Extends from", "End  \u25BA", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Points", routeCreator.controlPointsList.Count.ToString());

            EditorGUILayout.EndVertical();
        }

        // ───────────────── Scene GUI ─────────────────

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
            windowCtx = ctx;

            if (initialized && routeCreator == null)
            {
                isActive = false;
                ctx.pageStack.Pop();
                return;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (routeCreator == null || routeCreator.splineMoveMode == SplineMoveMode.Move2D)
                HandleUtility.AddDefaultControl(controlId);

            if (routeCreator != null)
            {
                routeCreator.CleanupNullPoints();
                DrawCurve();
                DrawPoints();
                DrawInsertPreview();
            }

            ProcessInput(controlId);
            sceneView.Repaint();
        }

        // ── Drawing ──

        private void DrawCurve()
        {
            var pts = routeCreator.controlPointsList;
            if (pts.Count < 2) return;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (pts[i] == null || pts[i + 1] == null) continue;
                Vector3 a = pts[i].position;
                Vector3 b = pts[i + 1].position;
                routeCreator.GetSegmentHandles(i, out Vector3 h1, out Vector3 h2);
                Handles.DrawBezier(a, b, h1, h2, DMTSPrefs.CurveColor, null, DMTSPrefs.CurveEditWidth);
            }
        }

        private void DrawPoints()
        {
            var pts = routeCreator.controlPointsList;
            int count = pts.Count;

            for (int i = 0; i < count; i++)
            {
                Transform cp = pts[i];
                if (cp == null) continue;

                bool isDraggedPoint = i == dragIndex;
                bool isSplineEnd = i == count - 1 && count >= 2;

                if (isSplineEnd)
                {
                    Color pointColor = isDraggedPoint
                        ? DMTSPrefs.DraggedControlPointColor
                        : DMTSPrefs.ActiveEndColor;
                    Handles.color = pointColor;
                    Handles.SphereHandleCap(0, cp.position, Quaternion.identity,
                        DMTSPrefs.ControlPointHandleSize * 2f, EventType.Repaint);

                    if (Camera.current != null)
                    {
                        Color ac = pointColor;
                        Handles.color = new Color(ac.r, ac.g, ac.b, 0.4f);
                        Handles.DrawWireDisc(cp.position,
                            Camera.current.transform.forward, DMTSPrefs.ControlPointHandleSize * 2.5f);
                    }
                }
                else
                {
                    Handles.color = isDraggedPoint
                        ? DMTSPrefs.DraggedControlPointColor
                        : DMTSPrefs.ControlPointColor;
                    Handles.SphereHandleCap(0, cp.position, Quaternion.identity,
                        DMTSPrefs.ControlPointHandleSize * 2f, EventType.Repaint);
                }

                var labelStyle = new GUIStyle
                {
                    normal = { textColor = Color.white },
                    fontStyle = FontStyle.Bold,
                    fontSize = DMTSPrefs.PointLabelFontSize
                };
                Handles.Label(cp.position + Vector3.up * DMTSPrefs.ControlPointHandleSize * 0.35f,
                    $"[{i}]", labelStyle);

                if (routeCreator.splineMoveMode == SplineMoveMode.Move3D)
                    Draw3DMoveHandle(cp);
            }
        }

        private void DrawInsertPreview()
        {
            Event e = Event.current;
            if (!e.control || routeCreator.controlPointsList.Count < 2) return;

            FindNearestSegmentScreenSpace(e.mousePosition,
                out int seg, out float t, out float screenDist);

            if (seg < 0 || screenDist > DMTSPrefs.InsertScreenThreshold) return;

            Vector3 p0 = routeCreator.controlPointsList[seg].position;
            Vector3 p3 = routeCreator.controlPointsList[seg + 1].position;
            routeCreator.GetSegmentHandles(seg, out Vector3 p1, out Vector3 p2);
            Vector3 preview = SplineRoadCreator.EvaluateCubicBezier(p0, p1, p2, p3, t);

            Handles.color = DMTSPrefs.InsertPreviewColor;
            Handles.SphereHandleCap(0, preview, Quaternion.identity,
                DMTSPrefs.ControlPointHandleSize * 2f, EventType.Repaint);

            var style = new GUIStyle
            {
                normal = { textColor = DMTSPrefs.InsertPreviewColor },
                fontSize = DMTSPrefs.InsertLabelFontSize
            };
            Handles.Label(preview + Vector3.up * DMTSPrefs.ControlPointHandleSize * 5f,
                "Ctrl+Click to insert", style);
        }

        // ── Input ──

        private void ProcessInput(int controlId)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                HandleLeftDown(e, controlId);
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && dragIndex >= 0)
            {
                HandleDrag(e);
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && dragIndex >= 0)
            {
                dragIndex = -1;
                GUIUtility.hotControl = 0;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 1) 
            {
                HandleRightClick(e);
            }
        }

        private void HandleLeftDown(Event e, int controlId)
        {
            if(e.shift && !initialized )EnsureInitialized(GetWorldPosition(e.mousePosition)); //create road when first clicked on the scene
            if(!initialized) return; //if not initialized, ignore other clicks
            var pts = routeCreator.controlPointsList;
            int count = pts.Count;

            // Ctrl+Click → insert between existing points
            if (e.control && count >= 2)
            {
                if (TryInsert(e.mousePosition))
                {
                    e.Use();
                    return;
                }
            }

            int nearest = ScreenNearestPoint(e.mousePosition, out float nearDist);

            // Click on endpoint → begin drag
            if (nearest >= 0 && count >= 2
                && (nearest == 0 || nearest == count - 1)
                && nearDist < DMTSPrefs.EndpointScreenRadius)
            {
                dragIndex = routeCreator.splineMoveMode == SplineMoveMode.Move2D ? nearest : -1;
                if (routeCreator.splineMoveMode == SplineMoveMode.Move2D)
                    GUIUtility.hotControl = controlId;
                e.Use();
                windowCtx?.Repaint();
                return;
            }

            // Click on mid-point → begin drag
            if (routeCreator.splineMoveMode == SplineMoveMode.Move2D
                && nearest >= 0
                && nearDist < DMTSPrefs.PointScreenRadius)
            {
                dragIndex = nearest;
                GUIUtility.hotControl = controlId;
                e.Use();
                return;
            }

            // Shift+Click empty space → add new control point
            if (!e.shift) return;

            Vector3 worldPos = GetWorldPosition(e.mousePosition);
            Undo.RecordObject(routeCreator, "Add Control Point");

            Transform newPoint = routeCreator.AddControlPoint(worldPos);
            Undo.RegisterCreatedObjectUndo(newPoint.gameObject, "Add Control Point");
            EditorUtility.SetDirty(routeCreator);
            e.Use();
            windowCtx?.Repaint();
        }

        private void HandleDrag(Event e)
        {
            if (routeCreator == null) return;
            if (routeCreator.splineMoveMode == SplineMoveMode.Move3D) return;
            if (dragIndex < 0 || dragIndex >= routeCreator.controlPointsList.Count)
                return;

            Vector3 newPos = GetWorldPosition(e.mousePosition);
            Undo.RecordObject(routeCreator.controlPointsList[dragIndex],
                "Move Control Point");
            routeCreator.controlPointsList[dragIndex].position = newPos;
            GUI.changed = true;
            e.Use();
        }

        private void HandleRightClick(Event e)
        {
            if (routeCreator == null) return;
            int nearest = ScreenNearestPoint(e.mousePosition, out float dist);
            if (nearest < 0 || dist > DMTSPrefs.EndpointScreenRadius) return;

            Transform point = routeCreator.controlPointsList[nearest];
            Undo.RecordObject(routeCreator, "Delete Control Point");
            routeCreator.controlPointsList.RemoveAt(nearest);
            if (point != null)
                Undo.DestroyObjectImmediate(point.gameObject);

            EditorUtility.SetDirty(routeCreator);
            e.Use();
            windowCtx?.Repaint();
        }

        private bool TryInsert(Vector2 mousePos)
        {
            FindNearestSegmentScreenSpace(mousePos,
                out int seg, out float t, out float screenDist);

            if (seg < 0 || screenDist > DMTSPrefs.InsertScreenThreshold) return false;

            Vector3 p0 = routeCreator.controlPointsList[seg].position;
            Vector3 p3 = routeCreator.controlPointsList[seg + 1].position;
            routeCreator.GetSegmentHandles(seg, out Vector3 p1, out Vector3 p2);
            Vector3 insertPos = SplineRoadCreator.EvaluateCubicBezier(p0, p1, p2, p3, t);

            Undo.RecordObject(routeCreator, "Insert Control Point");
            Transform newPoint = routeCreator.InsertControlPoint(seg + 1, insertPos);
            Undo.RegisterCreatedObjectUndo(newPoint.gameObject, "Insert Control Point");
            EditorUtility.SetDirty(routeCreator);
            windowCtx?.Repaint();
            return true;
        }

        // ───────────────── Helpers ─────────────────

        private int ScreenNearestPoint(Vector2 mousePos, out float bestDist)
        {
            bestDist = float.MaxValue;
            int best = -1;

            for (int i = 0; i < routeCreator.controlPointsList.Count; i++)
            {
                Transform cp = routeCreator.controlPointsList[i];
                if (cp == null) continue;

                Vector2 sp = HandleUtility.WorldToGUIPoint(cp.position);
                float d = Vector2.Distance(mousePos, sp);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        private void FindNearestSegmentScreenSpace(Vector2 mousePos,
            out int bestSeg, out float bestT, out float bestScreenDist)
        {
            bestSeg = -1;
            bestT = 0f;
            bestScreenDist = float.MaxValue;

            var pts = routeCreator.controlPointsList;
            if (pts.Count < 2) return;

            const int samples = 50;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (pts[i] == null || pts[i + 1] == null) continue;
                Vector3 p0 = pts[i].position;
                Vector3 p3 = pts[i + 1].position;
                routeCreator.GetSegmentHandles(i, out Vector3 p1, out Vector3 p2);

                for (int s = 0; s <= samples; s++)
                {
                    float t = s / (float)samples;
                    Vector3 worldPt = SplineRoadCreator.EvaluateCubicBezier(p0, p1, p2, p3, t);
                    Vector2 screenPt = HandleUtility.WorldToGUIPoint(worldPt);
                    float d = Vector2.Distance(mousePos, screenPt);

                    if (d < bestScreenDist)
                    {
                        bestScreenDist = d;
                        bestSeg = i;
                        bestT = t;
                    }
                }
            }
        }

        private static Vector3 GetWorldPosition(Vector2 mousePos)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return hit.point;

            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            
            return ray.GetPoint(10f);
        }

        private void Draw3DMoveHandle(Transform controlPoint)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(controlPoint.position, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(controlPoint, "Move Control Point");
            controlPoint.position = newPosition;
            EditorUtility.SetDirty(controlPoint);
            EditorUtility.SetDirty(routeCreator);
            GUI.changed = true;
            windowCtx?.Repaint();
        }

    }
}
