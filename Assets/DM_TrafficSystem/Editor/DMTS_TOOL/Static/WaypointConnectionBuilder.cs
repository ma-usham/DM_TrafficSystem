using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Creates, regenerates, and deletes editable curved waypoint connections between roads.
    /// </summary>
    public static class WaypointConnectionBuilder
    {
        private const float MinWaypointSeparation = 0.1f;

        private static readonly AIWaypoint[] EmptyWaypointLinks = System.Array.Empty<AIWaypoint>();

        /// <summary>
        /// Returns the compact terminal name used in connection object names and editor labels.
        /// </summary>
        public static string BuildConnectionTerminalName(Road road, int laneIndex)
        {
            return $"{BuildRoadToken(road)}_{BuildLaneToken(laneIndex)}";
        }

        /// <summary>
        /// Returns the compact terminal name for the provided waypoint's road and lane.
        /// </summary>
        public static string BuildConnectionTerminalName(AIWaypoint waypoint)
        {
            if (!TryGetWaypointLaneInfo(waypoint, out Road road, out int laneIndex))
                return waypoint != null ? waypoint.name : "Waypoint";

            return BuildConnectionTerminalName(road, laneIndex);
        }

        /// <summary>
        /// Renames one connection object so it matches the current source and target road/lane endpoints.
        /// </summary>
        public static void SyncConnectionObjectName(AIWaypointConnection connection, string undoLabel)
        {
            if (connection == null)
                return;

            string expectedName = BuildConnectionObjectName(connection.sourceWaypoint, connection.targetWaypoint);
            if (connection.gameObject.name == expectedName)
                return;

           // Undo.RecordObject(connection.gameObject, undoLabel);
            connection.gameObject.name = expectedName;
            EditorUtility.SetDirty(connection.gameObject);
        }

        /// <summary>
        /// Creates a new connection object, seeds a default spline, and generates its transition waypoints.
        /// </summary>
        public static AIWaypointConnection CreateConnection(AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
        {
            if (sourceWaypoint == null || targetWaypoint == null || sourceWaypoint == targetWaypoint)
                return null;

            GameObject connectionObject = new GameObject(BuildConnectionObjectName(sourceWaypoint, targetWaypoint));
           // Undo.RegisterCreatedObjectUndo(connectionObject, "Create Road Connection");

           // AIWaypointConnection connection = Undo.AddComponent<AIWaypointConnection>(connectionObject);
            AIWaypointConnection connection = connectionObject.AddComponent<AIWaypointConnection>();
            connection.sourceWaypoint = sourceWaypoint;
            connection.targetWaypoint = targetWaypoint;
            TrafficSystemHierarchyUtility.ParentConnection(connectionObject, sourceWaypoint, "Create Road Connection");
            connectionObject.transform.position = sourceWaypoint.transform.position;
            connection.SetDefaultControlPoints(
                GetSourceForward(sourceWaypoint, targetWaypoint),
                GetTargetForward(targetWaypoint, sourceWaypoint));
            connection.SyncEndpointControlPoints();

            RegenerateConnection(connection, "Create Road Connection");
            EditorUtility.SetDirty(connection);

            return connection;
        }

        /// <summary>
        /// Rebuilds one connection's generated transition waypoints and reconnects the waypoint graph.
        /// </summary>
        public static void RegenerateConnection(AIWaypointConnection connection, string undoLabel)
        {
            if (connection == null)
                return;

            SyncConnectionObjectName(connection, undoLabel);
            TrafficSystemHierarchyUtility.ParentConnection(connection.gameObject, undoLabel);
            connection.SyncEndpointControlPoints();

            RemoveConnectionLinks(connection, undoLabel);
            ClearGeneratedWaypoints(connection, undoLabel);

            if (!HasValidEndpoints(connection))
            {
                EditorUtility.SetDirty(connection);
                return;
            }

            List<Vector3> sampledPositions = BuildTransitionWaypointPositions(connection);
            CreateGeneratedWaypoints(connection, sampledPositions, undoLabel);
            LinkConnectionWaypoints(connection, undoLabel);
            connection.SyncEndpointControlPoints();
            EditorUtility.SetDirty(connection);
        }

        /// <summary>
        /// Deletes a connection object and removes all of its graph links and generated waypoints.
        /// </summary>
        public static void DeleteConnection(AIWaypointConnection connection, string undoLabel)
        {
            if (connection == null)
                return;

            Transform connectionGroup = connection.transform.parent;
            RemoveConnectionLinks(connection, undoLabel);
            ClearGeneratedWaypoints(connection, undoLabel);
            //Undo.DestroyObjectImmediate(connection.gameObject);
            Object.DestroyImmediate(connection.gameObject);
            TrafficSystemHierarchyUtility.CleanupEmptyConnectionGroup(connectionGroup, undoLabel);
        }

        /// <summary>
        /// Deletes curved connections that touch any provided waypoint and removes stale waypoint references to them.
        /// </summary>
        public static void RemoveConnectionsForWaypoints(IEnumerable<AIWaypoint> waypoints, string undoLabel)
        {
            HashSet<AIWaypoint> affectedWaypoints = BuildWaypointSet(waypoints);
            if (affectedWaypoints.Count == 0)
                return;

            AIWaypointConnection[] connectionObjects = Object.FindObjectsByType<AIWaypointConnection>(FindObjectsInactive.Exclude);
            for (int i = 0; i < connectionObjects.Length; i++)
            {
                AIWaypointConnection connection = connectionObjects[i];
                if (connection == null)
                    continue;

                if (affectedWaypoints.Contains(connection.sourceWaypoint) || affectedWaypoints.Contains(connection.targetWaypoint))
                    DeleteConnection(connection, undoLabel);
            }

            RemoveWaypointReferences(affectedWaypoints, undoLabel);
        }

        /// <summary>
        /// Returns whether the connection still references both required endpoint waypoints.
        /// </summary>
        private static bool HasValidEndpoints(AIWaypointConnection connection)
        {
            return connection != null
                && connection.sourceWaypoint != null
                && connection.targetWaypoint != null;
        }

        /// <summary>
        /// Builds a unique waypoint set while filtering out null entries.
        /// </summary>
        private static HashSet<AIWaypoint> BuildWaypointSet(IEnumerable<AIWaypoint> waypoints)
        {
            var uniqueWaypoints = new HashSet<AIWaypoint>();
            if (waypoints == null)
                return uniqueWaypoints;

            foreach (AIWaypoint waypoint in waypoints)
            {
                if (waypoint != null)
                    uniqueWaypoints.Add(waypoint);
            }

            return uniqueWaypoints;
        }

        /// <summary>
        /// Removes all graph links that were created for the provided connection.
        /// </summary>
        private static void RemoveConnectionLinks(AIWaypointConnection connection, string undoLabel)
        {
            if (connection == null)
                return;

            AIWaypoint entryWaypoint = GetEntryWaypoint(connection);
            AIWaypoint exitWaypoint = GetExitWaypoint(connection);

            RemoveWaypointLink(connection.sourceWaypoint, entryWaypoint, useNextWaypoint: true, undoLabel);
            RemoveWaypointLink(connection.targetWaypoint, exitWaypoint, useNextWaypoint: false, undoLabel);

            // Remove any legacy direct endpoint-to-endpoint link when the connection is converted from a straight link.
            RemoveWaypointLink(connection.sourceWaypoint, connection.targetWaypoint, useNextWaypoint: true, undoLabel);
            RemoveWaypointLink(connection.targetWaypoint, connection.sourceWaypoint, useNextWaypoint: false, undoLabel);
        }

        /// <summary>
        /// Removes stale waypoint references to any waypoint that is about to be deleted.
        /// </summary>
        private static void RemoveWaypointReferences(HashSet<AIWaypoint> affectedWaypoints, string undoLabel)
        {
            if (affectedWaypoints == null || affectedWaypoints.Count == 0)
                return;

            AIWaypoint[] allWaypoints = Object.FindObjectsByType<AIWaypoint>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allWaypoints.Length; i++)
            {
                AIWaypoint waypoint = allWaypoints[i];
                if (waypoint == null)
                    continue;

                WaypointSettings settings = waypoint.settings;
                AIWaypoint[] previousWaypoint = FilterWaypointLinks(settings.previousWaypoint, affectedWaypoints);
                AIWaypoint[] nextWaypoint = FilterWaypointLinks(settings.nextWaypoint, affectedWaypoints);
                AIWaypoint[] laneChangePoints = FilterWaypointLinks(settings.laneChangePoints, affectedWaypoints);

                if (WaypointArraysEqual(settings.previousWaypoint, previousWaypoint)
                    && WaypointArraysEqual(settings.nextWaypoint, nextWaypoint)
                    && WaypointArraysEqual(settings.laneChangePoints, laneChangePoints))
                {
                    continue;
                }

                //Undo.RecordObject(waypoint, undoLabel);
                settings.previousWaypoint = previousWaypoint;
                settings.nextWaypoint = nextWaypoint;
                settings.laneChangePoints = laneChangePoints;
                waypoint.settings = settings;
                EditorUtility.SetDirty(waypoint);
            }
        }

        /// <summary>
        /// Deletes all generated transition waypoints that belong to one connection object.
        /// </summary>
        private static void ClearGeneratedWaypoints(AIWaypointConnection connection, string undoLabel)
        {
            if (connection == null)
                return;

            //Undo.RecordObject(connection, undoLabel);

            if (connection.transitionWaypoints != null)
            {
                for (int i = connection.transitionWaypoints.Count - 1; i >= 0; i--)
                {
                    AIWaypoint transitionWaypoint = connection.transitionWaypoints[i];
                    if (transitionWaypoint != null)
                    {
                        // Undo.DestroyObjectImmediate(transitionWaypoint.gameObject);
                        Object.DestroyImmediate(transitionWaypoint.gameObject);
                    }
                       
                }
            }

            for (int i = connection.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = connection.transform.GetChild(i);
                if (child != null && child.TryGetComponent<AIWaypoint>(out _))
                {
                    // Undo.DestroyObjectImmediate(child.gameObject);
                     Object.DestroyImmediate(child.gameObject);
                }
            }

            connection.transitionWaypoints ??= new List<AIWaypoint>();
            connection.transitionWaypoints.Clear();
            EditorUtility.SetDirty(connection);
        }

        /// <summary>
        /// Samples the connection spline into evenly spaced transition waypoint positions.
        /// </summary>
        private static List<Vector3> BuildTransitionWaypointPositions(AIWaypointConnection connection)
        {
            var positions = new List<Vector3>();

            if (!HasValidEndpoints(connection))
                return positions;

            List<Vector3> densePoints = SplineMathUtils.GetCurvePoints(
                connection.controlPointsList,
                Mathf.Max(4, connection.curveResolution));

            if (densePoints.Count < 2)
                return positions;

            float spacing = Mathf.Max(1f, connection.waypointSpacing);
            Vector3 sourcePosition = connection.sourceWaypoint.transform.position;
            Vector3 targetPosition = connection.targetWaypoint.transform.position;
            float distanceSinceLastWaypoint = 0f;
            Vector3 segmentStart = densePoints[0];

            for (int i = 1; i < densePoints.Count; i++)
            {
                Vector3 segmentEnd = densePoints[i];
                Vector3 segmentVector = segmentEnd - segmentStart;
                float segmentLength = segmentVector.magnitude;

                if (segmentLength <= Mathf.Epsilon)
                {
                    segmentStart = segmentEnd;
                    continue;
                }

                Vector3 segmentDirection = segmentVector / segmentLength;
                Vector3 sampleStart = segmentStart;
                float remainingSegmentLength = segmentLength;

                while (distanceSinceLastWaypoint + remainingSegmentLength >= spacing)
                {
                    float distanceToNextWaypoint = spacing - distanceSinceLastWaypoint;
                    Vector3 candidatePosition = sampleStart + segmentDirection * distanceToNextWaypoint;

                    if (!IsNearEndpoint(candidatePosition, sourcePosition, targetPosition))
                        positions.Add(candidatePosition);

                    sampleStart = candidatePosition;
                    remainingSegmentLength -= distanceToNextWaypoint;
                    distanceSinceLastWaypoint = 0f;
                }

                distanceSinceLastWaypoint += remainingSegmentLength;
                segmentStart = segmentEnd;
            }

            if (positions.Count == 0)
            {
                Vector3 midpoint = densePoints[densePoints.Count / 2];
                if (IsNearEndpoint(midpoint, sourcePosition, targetPosition))
                    midpoint = Vector3.Lerp(sourcePosition, targetPosition, 0.5f);

                positions.Add(midpoint);
            }

            return positions;
        }

        /// <summary>
        /// Creates generated transition waypoint objects at the provided sampled positions.
        /// </summary>
        private static void CreateGeneratedWaypoints(
            AIWaypointConnection connection,
            IReadOnlyList<Vector3> positions,
            string undoLabel)
        {
            if (connection == null)
                return;

            //Undo.RecordObject(connection, undoLabel);
            connection.transitionWaypoints ??= new List<AIWaypoint>();
            connection.transitionWaypoints.Clear();

            float speedLimit = GetConnectionSpeed(connection);
            VehicleType[] vehicleTypes = GetConnectionVehicleTypes(connection);
            float laneWidth = connection.sourceWaypoint != null ? connection.sourceWaypoint.settings.LaneWidth : 4f;

            for (int i = 0; i < positions.Count; i++)
            {
                GameObject waypointObject = new GameObject($"ConnectionWaypoint_{i}");
                //Undo.RegisterCreatedObjectUndo(waypointObject, undoLabel);
                waypointObject.transform.SetParent(connection.transform);
                waypointObject.transform.position = positions[i];

                // Align Waypoint Transform Z axis to face the next waypoint, or keep the last one's direction
                if (i < positions.Count - 1)
                {
                    Vector3 direction = (positions[i + 1] - positions[i]).normalized;
                    if (direction != Vector3.zero)
                    {
                        waypointObject.transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
                else if (i > 0)
                {
                    // If it's the last waypoint, use the direction from the previous point
                    Vector3 direction = (positions[i] - positions[i - 1]).normalized;
                    if (direction != Vector3.zero)
                    {
                        waypointObject.transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
                else if (connection.targetWaypoint != null)
                {
                    // Only 1 waypoint in the connection, point it at the final target
                    Vector3 direction = (connection.targetWaypoint.transform.position - positions[i]).normalized;
                    if (direction != Vector3.zero)
                    {
                        waypointObject.transform.rotation = Quaternion.LookRotation(direction);
                    }
                }

              //  AIWaypoint waypoint = Undo.AddComponent<AIWaypoint>(waypointObject);
                AIWaypoint waypoint = waypointObject.AddComponent<AIWaypoint>();
                WaypointSettings settings = waypoint.settings;
                settings.speed = speedLimit;
                settings.vehicleType = vehicleTypes;
                settings.LaneWidth = laneWidth;
                settings.previousWaypoint = EmptyWaypointLinks;
                settings.nextWaypoint = EmptyWaypointLinks;
                settings.laneChangePoints = EmptyWaypointLinks;
                waypoint.settings = settings;

                connection.transitionWaypoints.Add(waypoint);
                EditorUtility.SetDirty(waypoint);
            }

            EditorUtility.SetDirty(connection);
        }

        /// <summary>
        /// Rebuilds previous and next links across the generated connection waypoint chain.
        /// </summary>
        private static void LinkConnectionWaypoints(AIWaypointConnection connection, string undoLabel)
        {
            if (!HasValidEndpoints(connection))
                return;

            List<AIWaypoint> generatedWaypoints = connection.transitionWaypoints ?? new List<AIWaypoint>();
            int generatedCount = generatedWaypoints.Count;

            if (generatedCount == 0)
            {
                AppendWaypointLink(connection.sourceWaypoint, connection.targetWaypoint, useNextWaypoint: true, undoLabel);
                AppendWaypointLink(connection.targetWaypoint, connection.sourceWaypoint, useNextWaypoint: false, undoLabel);
                return;
            }

            for (int i = 0; i < generatedCount; i++)
            {
                AIWaypoint waypoint = generatedWaypoints[i];
                if (waypoint == null)
                    continue;

                AIWaypoint previousWaypoint = i > 0 ? generatedWaypoints[i - 1] : connection.sourceWaypoint;
                AIWaypoint nextWaypoint = i < generatedCount - 1 ? generatedWaypoints[i + 1] : connection.targetWaypoint;

               // Undo.RecordObject(waypoint, undoLabel);
                WaypointSettings settings = waypoint.settings;
                settings.previousWaypoint = previousWaypoint != null ? new[] { previousWaypoint } : EmptyWaypointLinks;
                settings.nextWaypoint = nextWaypoint != null ? new[] { nextWaypoint } : EmptyWaypointLinks;
                settings.laneChangePoints = EmptyWaypointLinks;
                waypoint.settings = settings;
                EditorUtility.SetDirty(waypoint);
            }

            AppendWaypointLink(connection.sourceWaypoint, generatedWaypoints[0], useNextWaypoint: true, undoLabel);
            AppendWaypointLink(connection.targetWaypoint, generatedWaypoints[generatedCount - 1], useNextWaypoint: false, undoLabel);
        }

        /// <summary>
        /// Adds one waypoint reference to the chosen previous or next array when it is not already present.
        /// </summary>
        private static bool AppendWaypointLink(
            AIWaypoint ownerWaypoint,
            AIWaypoint linkedWaypoint,
            bool useNextWaypoint,
            string undoLabel)
        {
            if (ownerWaypoint == null || linkedWaypoint == null)
                return false;

            WaypointSettings settings = ownerWaypoint.settings;
            AIWaypoint[] currentLinks = useNextWaypoint ? settings.nextWaypoint : settings.previousWaypoint;
            AIWaypoint[] updatedLinks = BuildUniqueWaypointLinkArray(currentLinks, linkedWaypoint);

            if (WaypointArraysEqual(currentLinks, updatedLinks))
                return false;

            //Undo.RecordObject(ownerWaypoint, undoLabel);

            if (useNextWaypoint)
            {
                settings.nextWaypoint = updatedLinks;
            }
            else
            {
                settings.previousWaypoint = updatedLinks;
            }

            ownerWaypoint.settings = settings;
            EditorUtility.SetDirty(ownerWaypoint);
            return true;
        }

        /// <summary>
        /// Removes one waypoint reference from the chosen previous or next array when present.
        /// </summary>
        private static bool RemoveWaypointLink(
            AIWaypoint ownerWaypoint,
            AIWaypoint linkedWaypoint,
            bool useNextWaypoint,
            string undoLabel)
        {
            if (ownerWaypoint == null || linkedWaypoint == null)
                return false;

            WaypointSettings settings = ownerWaypoint.settings;
            AIWaypoint[] currentLinks = useNextWaypoint ? settings.nextWaypoint : settings.previousWaypoint;
            AIWaypoint[] updatedLinks = BuildFilteredWaypointLinkArray(currentLinks, linkedWaypoint);

            if (WaypointArraysEqual(currentLinks, updatedLinks))
                return false;

            //Undo.RecordObject(ownerWaypoint, undoLabel);

            if (useNextWaypoint)
            {
                settings.nextWaypoint = updatedLinks;
            }
            else
            {
                settings.previousWaypoint = updatedLinks;
            }

            ownerWaypoint.settings = settings;
            EditorUtility.SetDirty(ownerWaypoint);
            return true;
        }

        /// <summary>
        /// Returns the first waypoint reached after leaving the connection source endpoint.
        /// </summary>
        private static AIWaypoint GetEntryWaypoint(AIWaypointConnection connection)
        {
            if (connection == null)
                return null;

            return connection.transitionWaypoints != null && connection.transitionWaypoints.Count > 0
                ? connection.transitionWaypoints[0]
                : connection.targetWaypoint;
        }

        /// <summary>
        /// Returns the last generated waypoint before the connection target endpoint.
        /// </summary>
        private static AIWaypoint GetExitWaypoint(AIWaypointConnection connection)
        {
            if (connection == null)
                return null;

            return connection.transitionWaypoints != null && connection.transitionWaypoints.Count > 0
                ? connection.transitionWaypoints[connection.transitionWaypoints.Count - 1]
                : connection.sourceWaypoint;
        }

        /// <summary>
        /// Returns a deduplicated waypoint link array that includes the provided candidate.
        /// </summary>
        private static AIWaypoint[] BuildUniqueWaypointLinkArray(IReadOnlyList<AIWaypoint> existingLinks, AIWaypoint candidate)
        {
            var uniqueLinks = new List<AIWaypoint>();

            if (existingLinks != null)
            {
                for (int i = 0; i < existingLinks.Count; i++)
                {
                    AIWaypoint existingLink = existingLinks[i];
                    if (existingLink != null && !uniqueLinks.Contains(existingLink))
                        uniqueLinks.Add(existingLink);
                }
            }

            if (candidate != null && !uniqueLinks.Contains(candidate))
                uniqueLinks.Add(candidate);

            return uniqueLinks.Count > 0
                ? uniqueLinks.ToArray()
                : EmptyWaypointLinks;
        }

        /// <summary>
        /// Returns a deduplicated waypoint link array with the provided candidate removed.
        /// </summary>
        private static AIWaypoint[] BuildFilteredWaypointLinkArray(IReadOnlyList<AIWaypoint> existingLinks, AIWaypoint candidateToRemove)
        {
            var remainingLinks = new List<AIWaypoint>();

            if (existingLinks != null)
            {
                for (int i = 0; i < existingLinks.Count; i++)
                {
                    AIWaypoint existingLink = existingLinks[i];
                    if (existingLink == null || existingLink == candidateToRemove || remainingLinks.Contains(existingLink))
                        continue;

                    remainingLinks.Add(existingLink);
                }
            }

            return remainingLinks.Count > 0
                ? remainingLinks.ToArray()
                : EmptyWaypointLinks;
        }

        /// <summary>
        /// Returns a deduplicated waypoint link array with all blocked waypoints removed.
        /// </summary>
        private static AIWaypoint[] FilterWaypointLinks(IReadOnlyList<AIWaypoint> existingLinks, HashSet<AIWaypoint> blockedWaypoints)
        {
            var remainingLinks = new List<AIWaypoint>();

            if (existingLinks != null)
            {
                for (int i = 0; i < existingLinks.Count; i++)
                {
                    AIWaypoint existingLink = existingLinks[i];
                    if (existingLink == null
                        || (blockedWaypoints != null && blockedWaypoints.Contains(existingLink))
                        || remainingLinks.Contains(existingLink))
                    {
                        continue;
                    }

                    remainingLinks.Add(existingLink);
                }
            }

            return remainingLinks.Count > 0
                ? remainingLinks.ToArray()
                : EmptyWaypointLinks;
        }

        /// <summary>
        /// Returns whether two waypoint link arrays contain the same references in the same order.
        /// </summary>
        private static bool WaypointArraysEqual(IReadOnlyList<AIWaypoint> first, IReadOnlyList<AIWaypoint> second)
        {
            int firstCount = first != null ? first.Count : 0;
            int secondCount = second != null ? second.Count : 0;
            if (firstCount != secondCount)
                return false;

            for (int i = 0; i < firstCount; i++)
            {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether one sampled point is too close to either endpoint to create a transition waypoint.
        /// </summary>
        private static bool IsNearEndpoint(Vector3 position, Vector3 sourcePosition, Vector3 targetPosition)
        {
            return Vector3.Distance(position, sourcePosition) <= MinWaypointSeparation
                || Vector3.Distance(position, targetPosition) <= MinWaypointSeparation;
        }

        /// <summary>
        /// Returns the speed limit that should be applied to generated transition waypoints.
        /// </summary>
        private static float GetConnectionSpeed(AIWaypointConnection connection)
        {
            if (connection == null)
                return 10f;

            if (connection.connectionSpeedLimit > 0f)
                return connection.connectionSpeedLimit;

            float sourceSpeed = connection.sourceWaypoint != null ? connection.sourceWaypoint.settings.speed : 10f;
            float targetSpeed = connection.targetWaypoint != null ? connection.targetWaypoint.settings.speed : sourceSpeed;
            return Mathf.Min(sourceSpeed, targetSpeed);
        }

        /// <summary>
        /// Returns the vehicle filter that should be applied to generated transition waypoints.
        /// </summary>
        private static VehicleType[] GetConnectionVehicleTypes(AIWaypointConnection connection)
        {
            if (connection == null)
                return new[] { VehicleType.Default };

            if (connection.connectionVehicleTypes != null && connection.connectionVehicleTypes.Length > 0)
                return (VehicleType[])connection.connectionVehicleTypes.Clone();

            VehicleType[] sourceVehicleTypes = connection.sourceWaypoint != null
                ? connection.sourceWaypoint.settings.vehicleType
                : null;
            VehicleType[] targetVehicleTypes = connection.targetWaypoint != null
                ? connection.targetWaypoint.settings.vehicleType
                : null;

            if (sourceVehicleTypes != null && sourceVehicleTypes.Length > 0)
                return (VehicleType[])sourceVehicleTypes.Clone();

            if (targetVehicleTypes != null && targetVehicleTypes.Length > 0)
                return (VehicleType[])targetVehicleTypes.Clone();

            return new[] { VehicleType.Default };
        }

        /// <summary>
        /// Returns the preferred forward direction to seed the source side of a new connection spline.
        /// </summary>
        private static Vector3 GetSourceForward(AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
        {
            if (sourceWaypoint != null
                && sourceWaypoint.settings.previousWaypoint != null
                && sourceWaypoint.settings.previousWaypoint.Length > 0
                && sourceWaypoint.settings.previousWaypoint[0] != null)
            {
                return (sourceWaypoint.transform.position - sourceWaypoint.settings.previousWaypoint[0].transform.position).normalized;
            }

            return targetWaypoint != null
                ? (targetWaypoint.transform.position - sourceWaypoint.transform.position).normalized
                : Vector3.forward;
        }

        /// <summary>
        /// Returns the preferred forward direction to seed the target side of a new connection spline.
        /// </summary>
        private static Vector3 GetTargetForward(AIWaypoint targetWaypoint, AIWaypoint sourceWaypoint)
        {
            if (targetWaypoint != null
                && targetWaypoint.settings.nextWaypoint != null
                && targetWaypoint.settings.nextWaypoint.Length > 0
                && targetWaypoint.settings.nextWaypoint[0] != null)
            {
                return (targetWaypoint.settings.nextWaypoint[0].transform.position - targetWaypoint.transform.position).normalized;
            }

            return sourceWaypoint != null
                ? (targetWaypoint.transform.position - sourceWaypoint.transform.position).normalized
                : Vector3.forward;
        }

        /// <summary>
        /// Builds the default scene object name for a connection between two lane terminals.
        /// </summary>
        private static string BuildConnectionObjectName(AIWaypoint sourceWaypoint, AIWaypoint targetWaypoint)
        {
            string sourceName = BuildConnectionTerminalName(sourceWaypoint);
            string targetName = BuildConnectionTerminalName(targetWaypoint);
            return $"{sourceName}_To_{targetName}_Connection";
        }

        /// <summary>
        /// Returns whether the waypoint can be resolved back to a generated road lane.
        /// </summary>
        private static bool TryGetWaypointLaneInfo(AIWaypoint waypoint, out Road road, out int laneIndex)
        {
            road = waypoint != null ? waypoint.GetComponentInParent<Road>() : null;
            laneIndex = -1;

            if (waypoint == null)
                return false;

            AILane lane = waypoint.GetComponentInParent<AILane>();
            if (lane == null)
                return road != null;

            if (road != null && road.laneObjects != null)
            {
                for (int i = 0; i < road.laneObjects.Count; i++)
                {
                    if (road.laneObjects[i] == lane)
                    {
                        laneIndex = i;
                        return true;
                    }
                }
            }

            laneIndex = ExtractTrailingNumber(lane.name);
            return road != null || laneIndex >= 0;
        }

        /// <summary>
        /// Converts a road name such as Road_1 into the compact R1 token used in connection names.
        /// </summary>
        private static string BuildRoadToken(Road road)
        {
            string roadName = road != null ? road.name : string.Empty;
            if (string.IsNullOrWhiteSpace(roadName))
                return "R?";

            string compactRoadName = roadName.Replace(" ", string.Empty).Replace("_", string.Empty);
            if (compactRoadName.StartsWith("Road", System.StringComparison.OrdinalIgnoreCase))
            {
                string suffix = compactRoadName.Substring(4);
                return !string.IsNullOrEmpty(suffix) ? $"R{suffix}" : "R?";
            }

            return compactRoadName;
        }

        /// <summary>
        /// Returns the compact lane token used in connection names.
        /// </summary>
        private static string BuildLaneToken(int laneIndex)
        {
            return laneIndex >= 0 ? $"L{laneIndex}" : "L?";
        }

        /// <summary>
        /// Extracts the last numeric run from an object name, returning -1 when none exists.
        /// </summary>
        private static int ExtractTrailingNumber(string value)
        {
            if (string.IsNullOrEmpty(value))
                return -1;

            int endIndex = value.Length - 1;
            while (endIndex >= 0 && !char.IsDigit(value[endIndex]))
                endIndex--;

            if (endIndex < 0)
                return -1;

            int startIndex = endIndex;
            while (startIndex >= 0 && char.IsDigit(value[startIndex]))
                startIndex--;

            string numericPortion = value.Substring(startIndex + 1, endIndex - startIndex);
            return int.TryParse(numericPortion, out int parsedNumber) ? parsedNumber : -1;
        }
    }
}
