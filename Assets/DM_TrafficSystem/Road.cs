using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [ExecuteInEditMode]
    public class Road : MonoBehaviour
    {
        public List<Vector3> controlPointsList = new List<Vector3>();
        public SplineMoveMode splineMoveMode = SplineMoveMode.Move2D;
        [Range(1, 8)] public int lanes = 1;
        [Range(1, 15)] public int waypointDistance = 5;
        public float laneWidth = 4f;
        public float speedLimitForAllRoads = 30f;
        [Range(10, 100)] public int curveResolution = 30;
        public DrivingDirection drivingDirection = DrivingDirection.Left;

        public List<List<Transform>> generatedLanes = new List<List<Transform>>();
        public List<AILane> laneObjects = new List<AILane>();

        public void AddControlPoint(Vector3 position)
        {
            controlPointsList.Add(position);
        }

        public void InsertControlPoint(int index, Vector3 position)
        {
            index = Mathf.Clamp(index, 0, controlPointsList.Count);
            controlPointsList.Insert(index, position);
        }

        public void RemoveControlPoint(int index)
        {
            if (index < 0 || index >= controlPointsList.Count) return;
            controlPointsList.RemoveAt(index);
        }

        /// <summary>
        /// Cubic Bezier evaluation: B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
        /// </summary>
        public static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0
                 + 3f * u * u * t * p1
                 + 3f * u * t * t * p2
                 + t * t * t * p3;
        }

        /// <summary>
        /// Computes auto-smooth tangent handles for the Bezier segment between
        /// controlPointsList[segIndex] and controlPointsList[segIndex+1]
        /// using Catmull-Rom to cubic-Bezier conversion.
        /// </summary>
        public void GetSegmentHandles(int segIndex, out Vector3 handleA, out Vector3 handleB)
        {
            int count = controlPointsList.Count;
            Vector3 p0 = controlPointsList[segIndex];
            Vector3 p1 = controlPointsList[segIndex + 1];

            Vector3 tangentA = (segIndex > 0)
                ? (p1 - controlPointsList[segIndex - 1]) * 0.5f
                : (p1 - p0);

            Vector3 tangentB = (segIndex + 2 < count)
                ? (controlPointsList[segIndex + 2] - p0) * 0.5f
                : (p1 - p0);

            handleA = p0 + tangentA / 3f;
            handleB = p1 - tangentB / 3f;
        }



        public List<Vector3> GetCurvePoints()
        {
            var points = new List<Vector3>();
            if (controlPointsList.Count < 2) return points;

            for (int i = 0; i < controlPointsList.Count - 1; i++)
            {
                Vector3 p0 = controlPointsList[i];
                Vector3 p3 = controlPointsList[i + 1];
                GetSegmentHandles(i, out Vector3 p1, out Vector3 p2);

                for (int s = 0; s <= curveResolution; s++)
                {
                    float t = s / (float)curveResolution;
                    points.Add(EvaluateCubicBezier(p0, p1, p2, p3, t));
                }
            }
            return points;
        }

        public void ClearGeneratedWaypoints()
        {
            generatedLanes.Clear();
            foreach (var lane in laneObjects)
            {
                if (lane != null) DestroyImmediate(lane);
            }
            laneObjects.Clear();

            // Fallback for cleanly removing any left-over lane objects
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Lane_"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Snaps a world position to collider geometry below (terrain, road meshes, etc.)
        /// so waypoints sit on the ground instead of following spline height over/under scenery.
        /// </summary>
        private static Vector3 ProjectOntoGround(Vector3 worldPosition)
        {
            const float lift = 500f;
            const float maxDistance = 3000f;
            Vector3 origin = worldPosition + Vector3.up * lift;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point;
            return worldPosition;
        }

        private static Vector3 GetRightSide(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(Vector3.forward, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            return right.normalized;
        }

        private bool LaneTravelsWithSpline(float laneOffset)
        {
            if (Mathf.Abs(laneOffset) < 0.001f)
                return drivingDirection == DrivingDirection.Left;

            return drivingDirection == DrivingDirection.Left
                ? laneOffset < 0f
                : laneOffset > 0f;
        }

        public void GenerateRoadWaypoints()
        {
            if (controlPointsList.Count < 2) return;

            ClearGeneratedWaypoints();

            List<Vector3> densePoints = GetCurvePoints();
            if (densePoints.Count < 2) return;

            // Walk the polyline at waypointDistance intervals
            var centerPositions = new List<Vector3>();
            var tangents = new List<Vector3>();

            centerPositions.Add(densePoints[0]);
            tangents.Add((densePoints[1] - densePoints[0]).normalized);

            float accumulated = 0f;
            for (int i = 1; i < densePoints.Count; i++)
            {
                float segLen = Vector3.Distance(densePoints[i], densePoints[i - 1]);
                accumulated += segLen;

                if (accumulated >= waypointDistance)
                {
                    centerPositions.Add(densePoints[i]);

                    Vector3 forward = (i + 1 < densePoints.Count)
                        ? (densePoints[i + 1] - densePoints[i - 1]).normalized
                        : (densePoints[i] - densePoints[i - 1]).normalized;
                    tangents.Add(forward);

                    accumulated = 0f;
                }
            }

            // Always include the last point
            Vector3 lastPoint = densePoints[densePoints.Count - 1];
            if (Vector3.Distance(centerPositions[centerPositions.Count - 1], lastPoint) > 0.01f)
            {
                centerPositions.Add(lastPoint);
                int c = densePoints.Count;
                tangents.Add((densePoints[c - 1] - densePoints[c - 2]).normalized);
            }

            // Generate waypoints for each lane, offset symmetrically from center
            for (int lane = 0; lane < lanes; lane++)
            {
                float laneOffset = (lane - (lanes - 1) / 2f) * laneWidth;

                var laneGo = new GameObject($"Lane_{lane}");
                laneGo.transform.SetParent(transform);
                laneGo.transform.localPosition = Vector3.zero;
                AILane aiLane = laneGo.AddComponent<AILane>();

                laneObjects.Add(aiLane);

                var lanePositions = new List<Vector3>(centerPositions.Count);
                var laneWaypoints = new List<Transform>();

                for (int w = 0; w < centerPositions.Count; w++)
                {
                    Vector3 right = GetRightSide(tangents[w]);
                    Vector3 pos = ProjectOntoGround(centerPositions[w] + right * laneOffset);
                    lanePositions.Add(pos);
                }

                if (!LaneTravelsWithSpline(laneOffset))
                    lanePositions.Reverse();

                List<AIWaypoint> createdWaypoints = new List<AIWaypoint>();
                for (int w = 0; w < lanePositions.Count; w++)
                {
                    var wpGo = new GameObject($"Waypoint_{w}");
                    AIWaypoint aiWaypoint = wpGo.AddComponent<AIWaypoint>();
                    wpGo.transform.position = lanePositions[w];
                    wpGo.transform.SetParent(laneGo.transform);

                    laneWaypoints.Add(wpGo.transform);
                    createdWaypoints.Add(aiWaypoint);
                    aiLane.waypoints.Add(aiWaypoint);
                }

                // Link previous and next waypoints
                for (int w = 0; w < createdWaypoints.Count; w++)
                {
                    AIWaypoint currentWp = createdWaypoints[w];
                    WaypointSettings settings = currentWp.settings;

                    if (w > 0)
                        settings.previousWaypoint = createdWaypoints[w - 1];

                    if (w < createdWaypoints.Count - 1)
                        settings.nextWaypoint = createdWaypoints[w + 1];

                    currentWp.settings = settings;
                }

                generatedLanes.Add(laneWaypoints);
            }
        }
    }
}
