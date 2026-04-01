using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Provides shared Scene view visibility and bounds queries for road editor tools.
    /// </summary>
    public static class RoadSceneVisibilityUtility
    {
        /// <summary>
        /// Returns all roads, optionally filtered to the current Scene view frustum.
        /// </summary>
        public static List<Road> GetRoads(
            SceneView sceneView,
            bool visibleOnly,
            FindObjectsInactive includeInactive = FindObjectsInactive.Exclude)
        {
            Road[] allRoads = Object.FindObjectsByType<Road>(includeInactive);
            var roads = new List<Road>(allRoads.Length);

            for (int i = 0; i < allRoads.Length; i++)
            {
                Road road = allRoads[i];
                if (road == null || road.controlPointsList == null || road.controlPointsList.Count == 0)
                    continue;

                if (visibleOnly && !IsRoadVisible(sceneView, road))
                    continue;

                roads.Add(road);
            }

            return roads;
        }

        /// <summary>
        /// Returns whether a road's bounds intersect the active Scene view frustum.
        /// </summary>
        public static bool IsRoadVisible(SceneView sceneView, Road road)
        {
            if (road == null || road.controlPointsList == null || road.controlPointsList.Count == 0)
                return false;

            return IsBoundsVisible(sceneView, ComputeRoadBounds(road));
        }

        /// <summary>
        /// Returns whether a bounds volume intersects the active Scene view frustum.
        /// </summary>
        public static bool IsBoundsVisible(SceneView sceneView, Bounds bounds)
        {
            if (!TryGetFrustumPlanes(sceneView, out Plane[] frustumPlanes))
                return true;

            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        /// <summary>
        /// Returns whether the segment joining two points intersects the active Scene view frustum.
        /// </summary>
        public static bool IsSegmentVisible(SceneView sceneView, Vector3 start, Vector3 end, float padding = 0f)
        {
            Bounds bounds = new Bounds(start, Vector3.zero);
            bounds.Encapsulate(end);

            if (padding > 0f)
                bounds.Expand(padding);

            return IsBoundsVisible(sceneView, bounds);
        }

        /// <summary>
        /// Computes a padded road bounds volume used for framing and frustum tests.
        /// </summary>
        public static Bounds ComputeRoadBounds(Road road)
        {
            List<Vector3> points = road.controlPointsList;
            Vector3 firstPoint = points.Count > 0 ? points[0] : road.transform.position;
            Bounds bounds = new Bounds(firstPoint, Vector3.zero);

            for (int i = 1; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            bounds.Expand(Mathf.Max(road.laneWidth * road.lanes, 1f));
            return bounds;
        }

        private static bool TryGetFrustumPlanes(SceneView sceneView, out Plane[] frustumPlanes)
        {
            SceneView targetSceneView = sceneView ?? SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
            if (targetSceneView == null || targetSceneView.camera == null)
            {
                frustumPlanes = null;
                return false;
            }

            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(targetSceneView.camera);
            return true;
        }
    }
}
