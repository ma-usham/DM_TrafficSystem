using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Lists roads visible to the current scene camera and offers quick edit, frame, delete, and preview actions.
    /// </summary>
    public class ViewRoadsPage : IPage
    {
        private readonly List<Road> _visibleRoads = new List<Road>();
        private readonly HashSet<Road> _roadsWithGizmoDisabled = new HashSet<Road>();

        private Vector2 scrollPos;

        /// <summary>
        /// Draws the list of roads visible to the active scene camera.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("Visible Roads", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            Road[] allRoads = Object.FindObjectsByType<Road>(FindObjectsInactive.Exclude);

            if (allRoads.Length == 0)
            {
                EditorGUILayout.HelpBox("No roads found in the scene.", MessageType.Info);
            }
            else if (_visibleRoads.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No roads are visible in the current Scene view.\nMove the camera to see roads listed here.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"{_visibleRoads.Count} of {allRoads.Length} road(s) visible",
                    EditorStyles.miniLabel);
                EditorGUILayout.Space(4);

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                for (int i = 0; i < _visibleRoads.Count; i++)
                {
                    Road road = _visibleRoads[i];
                    if (road == null) continue;
                    DrawRoadEntry(road, ctx);

                }

                EditorGUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();

            if (Event.current.type == EventType.Repaint)
            {
                RefreshVisibility(SceneView.lastActiveSceneView);
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Back", GUILayout.Width(100)))
                ctx.pageStack.Pop();
        }

        /// <summary>
        /// Refreshes the visible-road cache during scene repaints and draws lightweight road previews.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (Event.current.type == EventType.Repaint)
            {
                bool changed = RefreshVisibility(sceneView);
                if (!DMTS_Window.SuppressViewRoadsPagePreviewGizmos)
                    DrawVisibleRoadGizmos();

                if (changed)
                {
                    ctx.Repaint();
                }
            }
        }

        /// <summary>
        /// Draws one road entry with gizmo toggles and common scene-management actions.
        /// </summary>
        private void DrawRoadEntry(Road road, DMTS_Window ctx)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            bool gizmoEnabled = !_roadsWithGizmoDisabled.Contains(road);

            EditorGUI.BeginChangeCheck();
            gizmoEnabled = EditorGUILayout.Toggle(gizmoEnabled, GUILayout.Width(18));
            if (EditorGUI.EndChangeCheck())
            {
                if (gizmoEnabled)
                    _roadsWithGizmoDisabled.Remove(road);
                else
                    _roadsWithGizmoDisabled.Add(road);
                SceneView.RepaintAll();
            }


            EditorGUILayout.LabelField(road.gameObject.name, EditorStyles.boldLabel);

            if (GUILayout.Button("Edit", GUILayout.Width(40)))
                ctx.pageStack.Push(new CreateRoadPage(road));

            if (GUILayout.Button("View", GUILayout.Width(50)))
            {
                Selection.activeGameObject = road.gameObject;
                Bounds b = RoadSceneVisibilityUtility.ComputeRoadBounds(road);
                SceneView.lastActiveSceneView?.Frame(b, false);
            }

            if (GUILayout.Button("Delete", GUILayout.Width(50)))
            {
                if (EditorUtility.DisplayDialog("Delete Road",
                        $"Are you sure you want to delete '{road.gameObject.name}'?",
                        "Delete", "Cancel"))
                {
                    _roadsWithGizmoDisabled.Remove(road);
                    Undo.DestroyObjectImmediate(road.gameObject);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Points: {road.controlPointsList.Count}    " +
                                       $"Lanes: {road.lanes}    " +
                                       $"Speed: {road.speedLimitForAllLanes}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Rebuilds the cached list of roads whose bounds intersect the current scene camera frustum.
        /// </summary>
        private bool RefreshVisibility(SceneView sceneView)
        {
            List<Road> newVisibleRoads = RoadSceneVisibilityUtility.GetRoads(
                sceneView,
                visibleOnly: true,
                includeInactive: FindObjectsInactive.Include);

            bool changed = !HaveSameRoadOrder(_visibleRoads, newVisibleRoads);

            if (changed)
            {
                _visibleRoads.Clear();
                _visibleRoads.AddRange(newVisibleRoads);
            }

            return changed;
        }

        /// <summary>
        /// Returns whether two road lists contain the same roads in the same order.
        /// </summary>
        private static bool HaveSameRoadOrder(IReadOnlyList<Road> currentRoads, IReadOnlyList<Road> nextRoads)
        {
            if (currentRoads.Count != nextRoads.Count)
                return false;

            for (int i = 0; i < currentRoads.Count; i++)
            {
                if (currentRoads[i] != nextRoads[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Draws simplified bezier previews for roads that are visible and have gizmos enabled.
        /// </summary>
        private void DrawVisibleRoadGizmos()
        {
            foreach (Road road in _visibleRoads)
            {
                if (road == null || road.controlPointsList.Count < 2 || _roadsWithGizmoDisabled.Contains(road)) continue;

                RoadSceneGizmoDrawer.DrawRoadCurve(road, DMTSPrefs.ViewRoadCurveColor, DMTSPrefs.CurveWidth);
                RoadSceneGizmoDrawer.DrawRoadLabels(road);
            }
        }
    }
}
