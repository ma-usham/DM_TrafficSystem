using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class CreateRoadPage : IPage
    {
        public static bool isActive;
        public static int roadCount = 0;

        private SplineRouteCreator routeCreator;
        private bool initialized;
        private Editor_DMWindow windowCtx;

        private enum DrawEnd { Start, End }
        private DrawEnd activeEnd = DrawEnd.End;

        private int dragIndex = -1;

        private static readonly Color CurveColor = new Color(1f, 0.85f, 0.1f);
        private static readonly Color PointColor = Color.white;
        private static readonly Color ActiveEndColor = Color.green;
        private static readonly Color InactiveEndColor = new Color(1f, 0.55f, 0f);
        private static readonly Color InsertPreviewColor = new Color(0f, 0.85f, 1f, 0.9f);

        private const float PointScreenRadius = 10f;
        private const float EndpointScreenRadius = 16f;
        private const float InsertScreenThreshold = 25f;

        // ───────────────── Initialization ─────────────────

        private void EnsureInitialized()
        {
            if (initialized && routeCreator != null) return;

            GameObject go = new GameObject("Road_Route_"+ (++roadCount));
            routeCreator = go.AddComponent<SplineRouteCreator>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Road Route");
            initialized = true;
            isActive = true;
        }

        // ───────────────── Inspector GUI ─────────────────

        public void OnGUI(Editor_DMWindow ctx)
        {
            windowCtx = ctx;
            EnsureInitialized();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Create Road", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "\u2022 Left-click in the scene to place control points\n" +
                "\u2022 Click a green / orange endpoint to change drawing direction\n" +
                "\u2022 Ctrl + Left-click near a curve segment to insert a point\n" +
                "\u2022 Right-click a control point to delete it\n" +
                "\u2022 Drag any control point to reposition it",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (routeCreator != null)
            {
                DrawSettings();
                EditorGUILayout.Space(4);
                DrawPointList();
            }

            GUILayout.FlexibleSpace();

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

            routeCreator.lanes = EditorGUILayout.IntSlider("Lanes", routeCreator.lanes, 1, 8);
            routeCreator.laneWidth = EditorGUILayout.FloatField("Lane Width", routeCreator.laneWidth);
            routeCreator.drivingDirection =
                (DrivingDirection)EditorGUILayout.EnumPopup("Driving Side", routeCreator.drivingDirection);
            routeCreator.speedLimitForAllRoads =
                EditorGUILayout.FloatField("Speed Limit", routeCreator.speedLimitForAllRoads);
            routeCreator.curveResolution =
                EditorGUILayout.IntSlider("Curve Smoothness", routeCreator.curveResolution, 10, 100);

            EditorGUILayout.Space(2);
            string dir = activeEnd == DrawEnd.End ? "End  \u25BA" : "\u25C4  Start";
            EditorGUILayout.LabelField("Drawing from", dir, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Points", routeCreator.controlPointsList.Count.ToString());

            EditorGUILayout.EndVertical();
        }

        private void DrawPointList()
        {
            if (routeCreator.controlPointsList.Count == 0) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Control Points", EditorStyles.boldLabel);

            for (int i = 0; i < routeCreator.controlPointsList.Count; i++)
            {
                Transform cp = routeCreator.controlPointsList[i];
                if (cp == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}] {cp.name}", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("Sel", GUILayout.Width(36)))
                {
                    Selection.activeGameObject = cp.gameObject;
                    SceneView.lastActiveSceneView?.Frame(
                        new Bounds(cp.position, Vector3.one * 5f), false);
                }
                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    Undo.RecordObject(routeCreator, "Remove Control Point");
                    routeCreator.RemoveControlPoint(i);
                    EditorUtility.SetDirty(routeCreator);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // ───────────────── Scene GUI ─────────────────

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
            if (routeCreator == null) return;
            windowCtx = ctx;
            routeCreator.CleanupNullPoints();

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            DrawCurve();
            DrawPoints();
            DrawInsertPreview();
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
                Handles.DrawBezier(a, b, h1, h2, CurveColor, null, 3f);
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

                bool isEndpoint = (i == 0 || i == count - 1) && count >= 2;
                float handleSize = HandleUtility.GetHandleSize(cp.position);

                if (isEndpoint)
                {
                    bool isActiveEnd = (i == 0 && activeEnd == DrawEnd.Start)
                                    || (i == count - 1 && activeEnd == DrawEnd.End);

                    Handles.color = isActiveEnd ? ActiveEndColor : InactiveEndColor;
                    float sz = handleSize * 0.18f;
                    Handles.SphereHandleCap(0, cp.position, Quaternion.identity,
                        sz * 2f, EventType.Repaint);

                    if (isActiveEnd && Camera.current != null)
                    {
                        Handles.color = new Color(
                            ActiveEndColor.r, ActiveEndColor.g, ActiveEndColor.b, 0.4f);
                        Handles.DrawWireDisc(cp.position,
                            Camera.current.transform.forward, sz * 2.5f);
                    }
                }
                else
                {
                    Handles.color = PointColor;
                    float sz = handleSize * 0.12f;
                    Handles.SphereHandleCap(0, cp.position, Quaternion.identity,
                        sz * 2f, EventType.Repaint);
                }

                var labelStyle = new GUIStyle
                {
                    normal = { textColor = Color.white },
                    fontStyle = FontStyle.Bold,
                    fontSize = 10
                };
                Handles.Label(cp.position + Vector3.up * handleSize * 0.35f,
                    $"[{i}]", labelStyle);
            }
        }

        private void DrawInsertPreview()
        {
            Event e = Event.current;
            if (!e.control || routeCreator.controlPointsList.Count < 2) return;

            FindNearestSegmentScreenSpace(e.mousePosition,
                out int seg, out float t, out float screenDist);

            if (seg < 0 || screenDist > InsertScreenThreshold) return;

            Vector3 p0 = routeCreator.controlPointsList[seg].position;
            Vector3 p3 = routeCreator.controlPointsList[seg + 1].position;
            routeCreator.GetSegmentHandles(seg, out Vector3 p1, out Vector3 p2);
            Vector3 preview = SplineRouteCreator.EvaluateCubicBezier(p0, p1, p2, p3, t);

            float sz = HandleUtility.GetHandleSize(preview) * 0.14f;
            Handles.color = InsertPreviewColor;
            Handles.SphereHandleCap(0, preview, Quaternion.identity,
                sz * 2f, EventType.Repaint);

            var style = new GUIStyle
            {
                normal = { textColor = InsertPreviewColor },
                fontSize = 11
            };
            Handles.Label(preview + Vector3.up * sz * 5f,
                "Ctrl+Click to insert", style);
        }

        // ── Input ──

        private void ProcessInput(int controlId)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
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
            else if (e.type == EventType.MouseDown && e.button == 1
                     && !e.alt && !e.control)
            {
                HandleRightClick(e);
            }
        }

        private void HandleLeftDown(Event e, int controlId)
        {
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

            // Click on endpoint → select drawing direction (+ begin drag)
            if (nearest >= 0 && count >= 2
                && (nearest == 0 || nearest == count - 1)
                && nearDist < EndpointScreenRadius)
            {
                activeEnd = nearest == 0 ? DrawEnd.Start : DrawEnd.End;
                dragIndex = nearest;
                GUIUtility.hotControl = controlId;
                e.Use();
                windowCtx?.Repaint();
                return;
            }

            // Click on mid-point → begin drag
            if (nearest >= 0 && nearDist < PointScreenRadius)
            {
                dragIndex = nearest;
                GUIUtility.hotControl = controlId;
                e.Use();
                return;
            }

            // Click empty space → add new control point
            Vector3 worldPos = GetWorldPosition(e.mousePosition);
            Undo.RecordObject(routeCreator, "Add Control Point");

            Transform newPoint;
            if (activeEnd == DrawEnd.End || count < 2)
                newPoint = routeCreator.AddControlPoint(worldPos);
            else
                newPoint = routeCreator.AddControlPointAtStart(worldPos);

            Undo.RegisterCreatedObjectUndo(newPoint.gameObject, "Add Control Point");
            EditorUtility.SetDirty(routeCreator);
            e.Use();
            windowCtx?.Repaint();
        }

        private void HandleDrag(Event e)
        {
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
            int nearest = ScreenNearestPoint(e.mousePosition, out float dist);
            if (nearest < 0 || dist > EndpointScreenRadius) return;

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

            if (seg < 0 || screenDist > InsertScreenThreshold) return false;

            Vector3 p0 = routeCreator.controlPointsList[seg].position;
            Vector3 p3 = routeCreator.controlPointsList[seg + 1].position;
            routeCreator.GetSegmentHandles(seg, out Vector3 p1, out Vector3 p2);
            Vector3 insertPos = SplineRouteCreator.EvaluateCubicBezier(p0, p1, p2, p3, t);

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
                    Vector3 worldPt = SplineRouteCreator.EvaluateCubicBezier(p0, p1, p2, p3, t);
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
    }
}
