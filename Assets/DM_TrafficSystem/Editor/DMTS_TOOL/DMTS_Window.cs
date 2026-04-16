using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Hosts the traffic system editor pages and forwards scene GUI events to the active page.
    /// </summary>
    public class DMTS_Window : EditorWindow
    {
        [SerializeField] private GlobalSceneGizmoState globalSceneGizmoState = new GlobalSceneGizmoState();

        public Stack<IPage> pageStack = new Stack<IPage>();
        public static DMTS_Window editorWindow;

        private readonly ConnectRoadToolState globalConnectionToolState = new ConnectRoadToolState();

        public static bool SuppressInspectorRoadCurveGizmos =>
            editorWindow != null && editorWindow.ShouldSuppressInspectorRoadCurveGizmos;

        public static bool SuppressInspectorRoadWaypointGizmos =>
            editorWindow != null && editorWindow.ShouldSuppressInspectorRoadWaypointGizmos;

        public static bool SuppressConnectPagePassiveConnectionGizmos =>
            editorWindow != null && editorWindow.ShouldSuppressConnectPagePassiveConnectionGizmos;

        public static bool SuppressViewRoadsPagePreviewGizmos =>
            editorWindow != null && editorWindow.ShouldSuppressViewRoadsPagePreviewGizmos;

        public static bool DrawIntersectionState =>
            editorWindow != null && editorWindow.globalSceneGizmoState != null && editorWindow.globalSceneGizmoState.drawIntersectionState;

        private bool ShouldSuppressInspectorRoadCurveGizmos =>
            globalSceneGizmoState != null
            && globalSceneGizmoState.enabled
            && (globalSceneGizmoState.drawRoadCurves
                || globalSceneGizmoState.drawControlPoints
                || globalSceneGizmoState.drawRoadNames);

        private bool ShouldSuppressInspectorRoadWaypointGizmos =>
            globalSceneGizmoState != null
            && globalSceneGizmoState.enabled
            && (globalSceneGizmoState.drawWaypoints || globalSceneGizmoState.drawLaneChangeLinks);

        private bool ShouldSuppressConnectPagePassiveConnectionGizmos =>
            globalSceneGizmoState != null
            && globalSceneGizmoState.enabled
            && globalSceneGizmoState.drawConnections;

        private bool ShouldSuppressViewRoadsPagePreviewGizmos =>
            globalSceneGizmoState != null
            && globalSceneGizmoState.enabled
            && (globalSceneGizmoState.drawRoadCurves || globalSceneGizmoState.drawRoadNames);

        [MenuItem("Tools/DarkMatter Traffic System Tool", false, 2)]
        /// <summary>
        /// Opens the traffic system window from the Unity Tools menu.
        /// </summary>
        private static void ShowWindowFromMenu()
        {
            ShowWindow();
        }

        /// <summary>
        /// Opens the window and optionally pushes a specific page on top of the navigation stack.
        /// </summary>
        public static DMTS_Window ShowWindow(IPage openPage = null)
        {
            DMTS_Window window = GetWindow<DMTS_Window>();
            window.minSize = new Vector2(320, 240);
            window.titleContent.text = "DarkMatter Traffic System";
            window.Show();

            if (openPage != null)
                window.pageStack.Push(openPage);

            return window;
        }

        /// <summary>
        /// Initializes the root page and hooks the window into scene GUI updates.
        /// </summary>
        private void OnEnable()
        {
            editorWindow = this;
            pageStack.Clear();
            pageStack.Push(new MainPage());
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Resets transient page state and detaches the scene GUI callback when the window closes.
        /// </summary>
        private void OnDisable()
        {
            CreateRoadPage.isActive = false;
            ConnectRoadPage.isActive = false;
            SceneView.duringSceneGui -= OnSceneGUI;

            if (editorWindow == this)
                editorWindow = null;
        }

        /// <summary>
        /// Draws the currently active page.
        /// </summary>
        private void OnGUI()
        {
            if (GameObject.Find("DM_TrafficSystem") == null)
            {
                EditorGUILayout.HelpBox("Traffic System hierarchy is missing in the current scene.", MessageType.Warning);
                if (GUILayout.Button("Create Traffic System Hierarchy", GUILayout.Height(30)))
                {
                    TrafficSystemHierarchyUtility.EnsureSceneHierarchy("Organize Traffic System Hierarchy");
                }
                EditorGUILayout.Space();
            }

            DrawGlobalSceneGizmoPanel();

            if (pageStack.Count > 0)
                pageStack.Peek().OnGUI(this);
        }

        /// <summary>
        /// Forwards scene GUI handling to the active page and tracks whether create-road mode is active.
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (pageStack.Count > 0)
            {
                IPage activePage = pageStack.Peek();
                CreateRoadPage.isActive = activePage is CreateRoadPage;
                ConnectRoadPage.isActive = activePage is ConnectRoadPage;
                DrawGlobalSceneGizmos(sceneView);
                activePage.OnSceneGUI(sceneView, this);
                TrySelectWaypointFromScene();
            }
            else
            {
                CreateRoadPage.isActive = false;
                ConnectRoadPage.isActive = false;
                DrawGlobalSceneGizmos(sceneView);
                TrySelectWaypointFromScene();
            }
        }

        /// <summary>
        /// Draws the foldout UI that controls which shared scene gizmos are enabled.
        /// </summary>
        private void DrawGlobalSceneGizmoPanel()
        {
            EditorGUILayout.BeginVertical("box");
            globalSceneGizmoState.isExpanded = EditorGUILayout.Foldout(
                globalSceneGizmoState.isExpanded,
                "Scene Gizmos",
                true);

            if (globalSceneGizmoState.isExpanded)
            {
                EditorGUI.BeginChangeCheck();

                globalSceneGizmoState.enabled = EditorGUILayout.ToggleLeft(
                    "Enable global scene gizmos",
                    globalSceneGizmoState.enabled);

                using (new EditorGUI.DisabledScope(!globalSceneGizmoState.enabled))
                {
                    globalSceneGizmoState.visibleOnly = EditorGUILayout.ToggleLeft(
                        "Only draw roads visible in the active Scene view",
                        globalSceneGizmoState.visibleOnly);
                    globalSceneGizmoState.drawRoadCurves = EditorGUILayout.ToggleLeft("Road curves", globalSceneGizmoState.drawRoadCurves);
                    globalSceneGizmoState.drawControlPoints = EditorGUILayout.ToggleLeft("Control points", globalSceneGizmoState.drawControlPoints);
                    globalSceneGizmoState.drawRoadNames = EditorGUILayout.ToggleLeft("Road labels", globalSceneGizmoState.drawRoadNames);
                    globalSceneGizmoState.drawWaypoints = EditorGUILayout.ToggleLeft("Generated waypoints", globalSceneGizmoState.drawWaypoints);
                    globalSceneGizmoState.selectWaypointOnClick = EditorGUILayout.ToggleLeft("Select waypoint on click", globalSceneGizmoState.selectWaypointOnClick);
                    globalSceneGizmoState.drawLaneChangeLinks = EditorGUILayout.ToggleLeft("Lane-change links", globalSceneGizmoState.drawLaneChangeLinks);
                    globalSceneGizmoState.drawConnections = EditorGUILayout.ToggleLeft("Road connections", globalSceneGizmoState.drawConnections);
                    globalSceneGizmoState.drawIntersectionState = EditorGUILayout.ToggleLeft("Intersection status (Green/Red)", globalSceneGizmoState.drawIntersectionState);
                    globalSceneGizmoState.drawSpawnPoints = EditorGUILayout.ToggleLeft("Spawn points", globalSceneGizmoState.drawSpawnPoints);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        /// <summary>
        /// Draws the enabled shared road, connection, and runtime intersection gizmos in the Scene view.
        /// </summary>
        private void DrawGlobalSceneGizmos(SceneView sceneView)
        {
            if (globalSceneGizmoState == null
                || !globalSceneGizmoState.enabled
                || !globalSceneGizmoState.DrawsAnyGizmo
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            List<Road> roads = RoadSceneVisibilityUtility.GetRoads(sceneView, globalSceneGizmoState.visibleOnly);

            for (int i = 0; i < roads.Count; i++)
            {
                Road road = roads[i];

                if (globalSceneGizmoState.drawRoadCurves)
                    RoadSceneGizmoDrawer.DrawRoadCurve(road, DMTSPrefs.CurveColor, DMTSPrefs.CurveWidth);

                if (globalSceneGizmoState.drawControlPoints)
                    RoadSceneGizmoDrawer.DrawControlPoints(road);

                if (globalSceneGizmoState.drawRoadNames)
                    RoadSceneGizmoDrawer.DrawRoadLabels(road);

                if (globalSceneGizmoState.drawWaypoints || globalSceneGizmoState.drawLaneChangeLinks)
                {
                    RoadSceneGizmoDrawer.DrawGeneratedWaypointGizmos(
                        road,
                        globalSceneGizmoState.drawWaypoints,
                        globalSceneGizmoState.drawLaneChangeLinks);
                }
            }

            if (globalSceneGizmoState.drawConnections)
            {
                globalConnectionToolState.RefreshLaneTerminalCache();
                List<ConnectRoadToolState.ConnectionRecord> connectionRecords =
                    globalConnectionToolState.BuildConnectionRecords(globalSceneGizmoState.visibleOnly, sceneView);
                RoadSceneGizmoDrawer.DrawRoadConnections(connectionRecords);
            }

            if (globalSceneGizmoState.drawSpawnPoints)
            {
                DrawTrafficManagerSpawnPoints();
            }

            if (globalSceneGizmoState.drawIntersectionState && Application.isPlaying)
            {
                DrawActiveIntersectionGizmos();
            }
        }

        /// <summary>
        /// Draws the bakes spawn points for the scene's Traffic Manager.
        /// </summary>
        private void DrawTrafficManagerSpawnPoints()
        {
            TrafficManager manager = Object.FindAnyObjectByType<TrafficManager>();
            if (manager == null || manager.serializedGrid == null)
                return;

            Handles.color = new Color(0f, 0f, 1f, 0.4f);
            foreach (var cell in manager.serializedGrid)
            {
                if (cell.spawnWaypoints != null)
                {
                    foreach (var sp in cell.spawnWaypoints)
                    {
                        if (sp != null)
                        {
                            Handles.CubeHandleCap(0, sp.transform.position, Quaternion.identity, 1.5f, EventType.Repaint);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Selects a waypoint object from the Scene view when the optional global toggle is enabled.
        /// </summary>
        private void TrySelectWaypointFromScene()
        {
            if (globalSceneGizmoState == null
                || !globalSceneGizmoState.enabled
                || !globalSceneGizmoState.selectWaypointOnClick)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null
                || currentEvent.type != EventType.MouseDown
                || currentEvent.button != 0
                || currentEvent.modifiers != EventModifiers.None
                || GUIUtility.hotControl != 0)
            {
                return;
            }

            AIWaypoint clickedWaypoint = IntersectionSceneUtility.FindWaypointAtMouse(currentEvent.mousePosition);
            if (clickedWaypoint == null)
                return;

            Selection.activeGameObject = clickedWaypoint.gameObject;
            Repaint();
            currentEvent.Use();
        }

        /// <summary>
        /// Draws play-mode stop-state markers for all configured intersections in the scene.
        /// </summary>
        private void DrawActiveIntersectionGizmos()
        {
            PriorityIntersection[] intersections = Object.FindObjectsByType<PriorityIntersection>(FindObjectsInactive.Exclude);
            foreach (PriorityIntersection intersection in intersections)
            {
                if (intersection == null)
                    continue;

                foreach (PriorityStopRoad road in intersection.priorityStopRoads)
                {
                    if (road == null || road.stopPoints == null)
                        continue;

                    DrawIntersectionRoadStatus(road.stopPoints);
                }
            }

            TrafficLightIntersection[] lightIntersections = Object.FindObjectsByType<TrafficLightIntersection>(FindObjectsInactive.Exclude);
            foreach (TrafficLightIntersection intersection in lightIntersections)
            {
                if (intersection == null)
                    continue;

                foreach (TrafficLightRoad road in intersection.trafficLightRoads)
                {
                    if (road == null || road.stopPoints == null)
                        continue;

                    DrawIntersectionRoadStatus(road.stopPoints);
                }
            }
        }

        /// <summary>
        /// Draws stop-point state colors for one intersection road while the game is running.
        /// </summary>
        private static void DrawIntersectionRoadStatus(IEnumerable<AIWaypoint> stopPoints)
        {
            if (stopPoints == null)
                return;

            foreach (AIWaypoint waypoint in stopPoints)
            {
                if (waypoint == null)
                    continue;

                bool isStopping = waypoint.settings.isStopPoint;
                Color fillColor = isStopping ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);
                Color outlineColor = isStopping ? Color.red : Color.green;
                IntersectionSceneUtility.DrawStopPoint(waypoint, fillColor, outlineColor);
            }
        }
    }
}
