using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class ViewRoadsPage : IPage
    {
        private readonly List<Road> _visibleRoads = new List<Road>(); //list roads that are only visible to camera.

        private readonly HashSet<Road>_roadsWithGizmoDisabled = new HashSet<Road>(); //list roads that have gizmo disabled. ( mandatory: used roadsWithGizmoDisabled to draw gizmo for all roads by default )


        private Vector2 scrollPos;

        public void OnGUI(Editor_DMWindow ctx)
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

            RefreshVisibility(SceneView.lastActiveSceneView);
            SceneView.RepaintAll();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Back", GUILayout.Width(100)))
                ctx.pageStack.Pop();
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
            RefreshVisibility(sceneView);
            DrawVisibleRoadGizmos();
            ctx.Repaint();
        }

        private void DrawRoadEntry(Road road, Editor_DMWindow ctx)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            // HashSet = opt-out: not in set → gizmos on. Checkbox = gizmo enabled.
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

            if (GUILayout.Button("Select", GUILayout.Width(50)))
                Selection.activeGameObject = road.gameObject;

            if (GUILayout.Button("Frame", GUILayout.Width(50)))
            {
                Selection.activeGameObject = road.gameObject;
                Bounds b = ComputeRoadBounds(road);
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
                                       $"Speed: {road.speedLimitForAllRoads}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }


        private void RefreshVisibility(SceneView sceneView)
        {

            _visibleRoads.Clear();
            if (sceneView == null || sceneView.camera == null) return;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(sceneView.camera);
            Road[] allRoads = Object.FindObjectsByType<Road>(FindObjectsInactive.Include);

            foreach (Road road in allRoads)
            {
                if (road == null || road.controlPointsList.Count == 0) continue;

                Bounds bounds = ComputeRoadBounds(road);

                if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                {
                    _visibleRoads.Add(road);
                }
            }
        }

        private static Bounds ComputeRoadBounds(Road     road)
        {
            List<Vector3> pts = road.controlPointsList;
            Vector3 first = pts.Count > 0 ? pts[0] : road.transform.position;
            Bounds bounds = new Bounds(first, Vector3.zero);

            for (int i = 1; i < pts.Count; i++)
            {
                bounds.Encapsulate(pts[i]);
            }

            bounds.Expand(road.laneWidth * road.lanes);
            return bounds;
        }

        private void DrawVisibleRoadGizmos()
        {
            foreach (Road road in _visibleRoads)
            {
                if (road == null || road.controlPointsList.Count < 2 || _roadsWithGizmoDisabled.Contains(road)) continue;

                for (int i = 0; i < road.controlPointsList.Count - 1; i++)
                {
                    Vector3 a = road.controlPointsList[i];
                    Vector3 b = road.controlPointsList[i + 1];

                    road.GetSegmentHandles(i, out Vector3 h1, out Vector3 h2);
                    Handles.DrawBezier(a, b, h1, h2,
                        DMTSPrefs.ViewRoadCurveColor, null, DMTSPrefs.CurveWidth);
                }

                string name = road.gameObject.name;
                List<Vector3> controlPointsList = road.controlPointsList;
                if (controlPointsList.Count > 0)
                {
                    Vector3 firstPoint = controlPointsList[0];
                    Vector3 lastPoint = controlPointsList[controlPointsList.Count - 1];
                    Handles.Label(firstPoint, name, EditorStyles.whiteMiniLabel);
                    Handles.Label(lastPoint, name, EditorStyles.whiteMiniLabel);
                }
            }
        }
    }
}
