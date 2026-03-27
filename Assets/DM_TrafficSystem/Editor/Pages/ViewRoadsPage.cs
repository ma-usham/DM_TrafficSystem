using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class ViewRoadsPage : IPage
    {
        private readonly List<SplineRouteCreator> visibleRoads = new List<SplineRouteCreator>();
        private Vector2 scrollPos;

        public void OnGUI(Editor_DMWindow ctx)
        {
            EditorGUILayout.LabelField("Visible Roads", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            SplineRouteCreator[] allRoads = Object.FindObjectsByType<SplineRouteCreator>(FindObjectsSortMode.None);

            if (allRoads.Length == 0)
            {
                EditorGUILayout.HelpBox("No roads found in the scene.", MessageType.Info);
            }
            else if (visibleRoads.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No roads are visible in the current Scene view.\nMove the camera to see roads listed here.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"{visibleRoads.Count} of {allRoads.Length} road(s) visible",
                    EditorStyles.miniLabel);
                EditorGUILayout.Space(4);

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                for (int i = 0; i < visibleRoads.Count; i++)
                {
                    SplineRouteCreator road = visibleRoads[i];
                    if (road == null) continue;
                    DrawRoadEntry(road);
                }

                EditorGUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();


            RefreshVisibility(SceneView.lastActiveSceneView);
            DrawVisibleRoadGizmos();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Back", GUILayout.Width(100)))
                ctx.pageStack.Pop();
        }

        private void DrawRoadEntry(SplineRouteCreator road)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(road.gameObject.name, EditorStyles.boldLabel);

            if (GUILayout.Button("Select", GUILayout.Width(50)))
                Selection.activeGameObject = road.gameObject;

            if (GUILayout.Button("Frame", GUILayout.Width(50)))
            {
                Selection.activeGameObject = road.gameObject;
                Bounds b = ComputeRoadBounds(road);
                SceneView.lastActiveSceneView?.Frame(b, false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Points: {road.controlPointsList.Count}    " +
                                       $"Lanes: {road.lanes}    " +
                                       $"Speed: {road.speedLimitForAllRoads}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx)
        {
            RefreshVisibility(sceneView);
            DrawVisibleRoadGizmos();
            ctx.Repaint();
        }

        private void RefreshVisibility(SceneView sceneView)
        {
            visibleRoads.Clear();
            if (sceneView == null || sceneView.camera == null) return;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(sceneView.camera);
            SplineRouteCreator[] allRoads = Object.FindObjectsByType<SplineRouteCreator>(FindObjectsSortMode.None);

            foreach (SplineRouteCreator road in allRoads)
            {
                if (road == null || road.controlPointsList.Count == 0) continue;

                Bounds bounds = ComputeRoadBounds(road);

                if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                    visibleRoads.Add(road);
            }
        }

        private static Bounds ComputeRoadBounds(SplineRouteCreator road)
        {
            List<Transform> pts = road.controlPointsList;
            Vector3 first = pts[0] != null ? pts[0].position : road.transform.position;
            Bounds bounds = new Bounds(first, Vector3.zero);

            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i] != null)
                    bounds.Encapsulate(pts[i].position);
            }

            bounds.Expand(road.laneWidth * road.lanes);
            return bounds;
        }


        private void DrawVisibleRoadGizmos()
        {
            foreach (SplineRouteCreator road in visibleRoads)
            {
                if (road == null || road.controlPointsList.Count < 2) continue;

                for (int i = 0; i < road.controlPointsList.Count - 1; i++)
                {
                    Transform a = road.controlPointsList[i];
                    Transform b = road.controlPointsList[i + 1];
                    if (a == null || b == null) continue;

                    road.GetSegmentHandles(i, out Vector3 h1, out Vector3 h2);
                    Handles.DrawBezier(a.position, b.position, h1, h2,
                        Color.cyan, null, 2.5f);
                }
            }
        }
    }
}
