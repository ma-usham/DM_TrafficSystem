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

        /// <summary>
        /// Opens the traffic system window from the Unity Tools menu.
        /// </summary>
        [MenuItem("Tools/DarkMatter Traffic System Tool", false, 2)]
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
            }
            else
            {
                CreateRoadPage.isActive = false;
                ConnectRoadPage.isActive = false;
                DrawGlobalSceneGizmos(sceneView);
            }
        }

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
                    globalSceneGizmoState.drawLaneChangeLinks = EditorGUILayout.ToggleLeft("Lane-change links", globalSceneGizmoState.drawLaneChangeLinks);
                    globalSceneGizmoState.drawConnections = EditorGUILayout.ToggleLeft("Road connections", globalSceneGizmoState.drawConnections);
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
        }
    }
}
