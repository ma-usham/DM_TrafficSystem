using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws the road inspector plus scene gizmos for spline editing and generated waypoints.
    /// </summary>
    [CustomEditor(typeof(Road))]
    public class RoadEditor : UnityEditor.Editor
    {
        private Road road => (Road)target;

        /// <summary>
        /// Draws the default road inspector and a shortcut into the custom traffic system window.
        /// </summary>
        public override void OnInspectorGUI()
        {
            RoadSettingsPanel.DrawHelpBox();
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Traffic System Window", GUILayout.Height(24)))
            {
                DMTS_Window.ShowWindow(new CreateRoadPage(road));
            }
            //SceneView.RepaintAll();
        }

        /// <summary>
        /// Draws the spline and control points in the scene when the dedicated create-road page is not active.
        /// </summary>
        private void OnSceneGUI()
        {
            if (CreateRoadPage.isActive || ConnectRoadPage.isActive || DMTS_Window.SuppressInspectorRoadCurveGizmos)
                return;

            RoadSceneGizmoDrawer.DrawRoadCurve(road, DMTSPrefs.CurveColor, DMTSPrefs.CurveWidth);
            RoadSceneGizmoDrawer.DrawControlPoints(road);
        }

#region Gizmos
        [DrawGizmo(GizmoType.Selected)]
        /// <summary>
        /// Draws generated waypoint lines, arrow heads, and lane-change links for the selected road.
        /// </summary>
        private static void DrawGeneratedWaypointGizmos(Road road, GizmoType gizmoType)
        {
            if (ConnectRoadPage.isActive || DMTS_Window.SuppressInspectorRoadWaypointGizmos)
                return;

            RoadSceneGizmoDrawer.DrawGeneratedWaypointGizmos(road, drawWaypoints: true, drawLaneChangeLinks: true);
        }

#endregion
    }
}
