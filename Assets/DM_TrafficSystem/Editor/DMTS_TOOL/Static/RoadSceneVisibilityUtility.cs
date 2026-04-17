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

        /// <summary>
        /// Returns the active Scene view frustum planes when a usable Scene view camera exists.
        /// </summary>
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

    /// <summary>
    /// Ensures traffic-system scene objects live under a consistent root hierarchy.
    /// </summary>
    public static class TrafficSystemHierarchyUtility
    {
        private const string SystemRootName = "DM_TrafficSystem";
        private const string RoadNetworkRootName = "RoadNetwork";
        private const string RoadsRootName = "Roads";
        private const string ConnectionsRootName = "Connections";
        private const string LegacyConnectionsRootName = "WaypointConnections";
        private const string IntersectionsRootName = "Intersections";
        private const string PriorityIntersectionsRootName = "Priority Intersections";
        private const string TrafficLightIntersectionsRootName = "Traffic Light Intersections";

        /// <summary>
        /// Creates the traffic-system hierarchy when needed and reparents known scene objects into it.
        /// </summary>
        public static void EnsureSceneHierarchy(string undoLabel)
        {
            Transform systemRoot = GetOrCreateSystemRoot(undoLabel);
            Transform roadNetworkRoot = GetOrCreateChild(systemRoot, RoadNetworkRootName, undoLabel);
            Transform roadsRoot = GetOrCreateChild(roadNetworkRoot, RoadsRootName, undoLabel);
            Transform connectionsRoot = GetOrCreateConnectionsRoot(roadNetworkRoot, undoLabel);
            Transform intersectionsRoot = GetOrCreateChild(roadNetworkRoot, IntersectionsRootName, undoLabel);
            Transform priorityIntersectionsRoot = GetOrCreateChild(intersectionsRoot, PriorityIntersectionsRootName, undoLabel);
            Transform trafficLightIntersectionsRoot = GetOrCreateChild(intersectionsRoot, TrafficLightIntersectionsRootName, undoLabel);

            ParentObjects(Object.FindObjectsByType<Road>(FindObjectsInactive.Include), roadsRoot, undoLabel);
            ParentConnections(Object.FindObjectsByType<AIWaypointConnection>(FindObjectsInactive.Include), undoLabel);
            CleanupEmptyConnectionGroups(connectionsRoot, undoLabel);
            ParentObjects(Object.FindObjectsByType<PriorityIntersection>(FindObjectsInactive.Include), priorityIntersectionsRoot, undoLabel);
            ParentObjects(Object.FindObjectsByType<TrafficLightIntersection>(FindObjectsInactive.Include), trafficLightIntersectionsRoot, undoLabel);
            ParentObjects(Object.FindObjectsByType<TrafficManager>(FindObjectsInactive.Include), systemRoot, undoLabel);

            EnforceChildOrder(roadNetworkRoot, RoadsRootName, ConnectionsRootName, IntersectionsRootName);
        }

        /// <summary>
        /// Parents one road object under the shared Roads container.
        /// </summary>
        public static void ParentRoad(GameObject roadObject, string undoLabel)
        {
            if (roadObject == null)
                return;

            SetParentIfNeeded(roadObject.transform, GetOrCreateChild(GetOrCreateRoadNetworkRoot(undoLabel), RoadsRootName, undoLabel), undoLabel);
        }

        /// <summary>
        /// Parents one connection object under the shared Connections container.
        /// </summary>
        public static void ParentConnection(GameObject connectionObject, string undoLabel)
        {
            if (connectionObject == null)
                return;

            AIWaypointConnection connection = connectionObject.GetComponent<AIWaypointConnection>();
            ParentConnection(connectionObject, connection != null ? connection.sourceWaypoint : null, undoLabel);
        }

        /// <summary>
        /// Parents one connection object under the source road folder inside the shared Connections container.
        /// </summary>
        public static void ParentConnection(GameObject connectionObject, AIWaypoint sourceWaypoint, string undoLabel)
        {
            if (connectionObject == null)
                return;

            AIWaypointConnection connection = connectionObject.GetComponent<AIWaypointConnection>();
            if (connection != null)
                WaypointConnectionBuilder.SyncConnectionObjectName(connection, undoLabel);

            Transform previousParent = connectionObject.transform.parent;
            Transform targetParent = GetOrCreateConnectionGroupRoot(sourceWaypoint, undoLabel);
            SetParentIfNeeded(connectionObject.transform, targetParent, undoLabel);
            CleanupEmptyConnectionGroup(previousParent, undoLabel);
        }

        /// <summary>
        /// Deletes an empty per-road connection folder when it no longer contains any connections.
        /// </summary>
        public static void CleanupEmptyConnectionGroup(Transform candidateGroup, string undoLabel)
        {
            if (!IsEmptyConnectionGroup(candidateGroup))
                return;

            //Undo.DestroyObjectImmediate(candidateGroup.gameObject);
            Object.DestroyImmediate(candidateGroup.gameObject);
        }

        /// <summary>
        /// Parents one intersection object under the Priority Intersections container.
        /// </summary>
        public static void ParentPriorityIntersection(GameObject intersectionObject, string undoLabel)
        {
            if (intersectionObject == null)
                return;

            Transform intersectionsRoot = GetOrCreateChild(GetOrCreateRoadNetworkRoot(undoLabel), IntersectionsRootName, undoLabel);
            Transform priorityRoot = GetOrCreateChild(intersectionsRoot, PriorityIntersectionsRootName, undoLabel);
            SetParentIfNeeded(intersectionObject.transform, priorityRoot, undoLabel);
        }

        /// <summary>
        /// Parents one intersection object under the Traffic Light Intersections container.
        /// </summary>
        public static void ParentTrafficLightIntersection(GameObject intersectionObject, string undoLabel)
        {
            if (intersectionObject == null)
                return;

            Transform intersectionsRoot = GetOrCreateChild(GetOrCreateRoadNetworkRoot(undoLabel), IntersectionsRootName, undoLabel);
            Transform trafficLightRoot = GetOrCreateChild(intersectionsRoot, TrafficLightIntersectionsRootName, undoLabel);
            SetParentIfNeeded(intersectionObject.transform, trafficLightRoot, undoLabel);
        }

        /// <summary>
        /// Parents the traffic manager under the traffic-system root.
        /// </summary>
        public static void ParentTrafficManager(GameObject trafficManagerObject, string undoLabel)
        {
            if (trafficManagerObject == null)
                return;

            SetParentIfNeeded(trafficManagerObject.transform, GetOrCreateSystemRoot(undoLabel), undoLabel);
        }

        /// <summary>
        /// Returns the shared road-network root, creating it together with its parent root if needed.
        /// </summary>
        public static Transform GetOrCreateRoadNetworkRoot(string undoLabel)
        {
            return GetOrCreateChild(GetOrCreateSystemRoot(undoLabel), RoadNetworkRootName, undoLabel);
        }

        /// <summary>
        /// Returns the shared connections root, migrating the legacy waypoint-connections object when present.
        /// </summary>
        public static Transform GetOrCreateConnectionsRoot(string undoLabel)
        {
            return GetOrCreateConnectionsRoot(GetOrCreateRoadNetworkRoot(undoLabel), undoLabel);
        }

        /// <summary>
        /// Returns the top-level traffic-system root object, creating it when necessary.
        /// </summary>
        private static Transform GetOrCreateSystemRoot(string undoLabel)
        {
            GameObject existingRoot = GameObject.Find(SystemRootName);
            if (existingRoot != null)
                return existingRoot.transform;

            GameObject rootObject = new GameObject(SystemRootName);
            //Undo.RegisterCreatedObjectUndo(rootObject, undoLabel);
            return rootObject.transform;
        }

        /// <summary>
        /// Returns the requested child object under the provided parent, creating it when it does not exist yet.
        /// </summary>
        private static Transform GetOrCreateChild(Transform parent, string childName, string undoLabel)
        {
            if (parent != null)
            {
                Transform existingChild = parent.Find(childName);
                if (existingChild != null)
                    return existingChild;
            }

            GameObject childObject = new GameObject(childName);
            //Undo.RegisterCreatedObjectUndo(childObject, undoLabel);

            if (parent != null)
               // Undo.SetTransformParent(childObject.transform, parent, undoLabel);
                childObject.transform.SetParent(parent);

            return childObject.transform;
        }

        /// <summary>
        /// Returns the connections root, handling migration from the previous top-level waypoint-connections root.
        /// </summary>
        private static Transform GetOrCreateConnectionsRoot(Transform roadNetworkRoot, string undoLabel)
        {
            Transform connectionsRoot = roadNetworkRoot != null ? roadNetworkRoot.Find(ConnectionsRootName) : null;
            GameObject legacyRootObject = GameObject.Find(LegacyConnectionsRootName);

            if (connectionsRoot == null && legacyRootObject != null)
            {
                //Undo.RecordObject(legacyRootObject, undoLabel);
                legacyRootObject.name = ConnectionsRootName;
                connectionsRoot = legacyRootObject.transform;
            }

            if (connectionsRoot == null)
                connectionsRoot = GetOrCreateChild(roadNetworkRoot, ConnectionsRootName, undoLabel);

            SetParentIfNeeded(connectionsRoot, roadNetworkRoot, undoLabel);

            if (legacyRootObject != null && legacyRootObject.transform != connectionsRoot)
            {
                MoveChildren(legacyRootObject.transform, connectionsRoot, undoLabel);

                if (legacyRootObject.transform.childCount == 0)
                    //Undo.DestroyObjectImmediate(legacyRootObject);
                    Object.DestroyImmediate(legacyRootObject);
            }

            return connectionsRoot;
        }

        /// <summary>
        /// Reparents the provided components' game objects under one shared target parent.
        /// </summary>
        private static void ParentObjects<T>(T[] components, Transform targetParent, string undoLabel) where T : Component
        {
            if (components == null || targetParent == null)
                return;

            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                    continue;

                SetParentIfNeeded(component.transform, targetParent, undoLabel);
            }
        }

        /// <summary>
        /// Reparents all connections under folders named after their source road.
        /// </summary>
        private static void ParentConnections(AIWaypointConnection[] connections, string undoLabel)
        {
            if (connections == null)
                return;

            for (int i = 0; i < connections.Length; i++)
            {
                AIWaypointConnection connection = connections[i];
                if (connection == null)
                    continue;

                ParentConnection(connection.gameObject, connection.sourceWaypoint, undoLabel);
            }
        }

        /// <summary>
        /// Reparents a transform only when it is not already under the requested parent.
        /// </summary>
        private static void SetParentIfNeeded(Transform child, Transform parent, string undoLabel)
        {
            if (child == null || parent == null || child.parent == parent)
                return;

            //Undo.SetTransformParent(child, parent, undoLabel);
            child.SetParent(parent);
        }

        /// <summary>
        /// Moves all direct children from one transform to another.
        /// </summary>
        private static void MoveChildren(Transform source, Transform target, string undoLabel)
        {
            if (source == null || target == null)
                return;

            while (source.childCount > 0)
            {
                //Undo.SetTransformParent(source.GetChild(0), target, undoLabel);
                source.GetChild(0).SetParent(target);
            }
        }

        /// <summary>
        /// Returns the folder used to store connections for one source road.
        /// </summary>
        private static Transform GetOrCreateConnectionGroupRoot(AIWaypoint sourceWaypoint, string undoLabel)
        {
            Transform connectionsRoot = GetOrCreateConnectionsRoot(GetOrCreateRoadNetworkRoot(undoLabel), undoLabel);
            return GetOrCreateChild(connectionsRoot, GetConnectionSourceRoadName(sourceWaypoint), undoLabel);
        }

        /// <summary>
        /// Returns the source road name used for one connection folder.
        /// </summary>
        private static string GetConnectionSourceRoadName(AIWaypoint sourceWaypoint)
        {
            if (sourceWaypoint == null)
                return "Unknown Road";

            Road road = sourceWaypoint.GetComponentInParent<Road>();
            if (road == null || string.IsNullOrWhiteSpace(road.name))
                return "Unknown Road";

            return SanitizeHierarchyName(road.name);
        }

        /// <summary>
        /// Removes path separator characters so the road name can be used safely in Transform.Find lookups.
        /// </summary>
        private static string SanitizeHierarchyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown Road";

            return value.Replace('/', '_').Replace('\\', '_');
        }

        /// <summary>
        /// Removes empty per-road folders that no longer contain any connection objects.
        /// </summary>
        private static void CleanupEmptyConnectionGroups(Transform connectionsRoot, string undoLabel)
        {
            if (connectionsRoot == null)
                return;

            for (int i = connectionsRoot.childCount - 1; i >= 0; i--)
            {
                CleanupEmptyConnectionGroup(connectionsRoot.GetChild(i), undoLabel);
            }
        }

        /// <summary>
        /// Returns whether the provided transform is an empty connection-group folder.
        /// </summary>
        private static bool IsEmptyConnectionGroup(Transform candidateGroup)
        {
            if (candidateGroup == null || candidateGroup.parent == null || candidateGroup.parent.name != ConnectionsRootName)
                return false;

            if (candidateGroup.childCount > 0 || candidateGroup.TryGetComponent<AIWaypointConnection>(out _))
                return false;

            Component[] components = candidateGroup.GetComponents<Component>();
            return components.Length == 1;
        }

        /// <summary>
        /// Applies a stable sibling order to the named children under one parent.
        /// </summary>
        private static void EnforceChildOrder(Transform parent, params string[] childNames)
        {
            if (parent == null || childNames == null)
                return;

            for (int i = 0; i < childNames.Length; i++)
            {
                Transform child = parent.Find(childNames[i]);
                if (child != null)
                    child.SetSiblingIndex(i);
            }
        }
    }
}
