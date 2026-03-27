using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [ExecuteInEditMode]
    public class SplineRouteCreator : MonoBehaviour
    {
        public List<Transform> controlPointsList = new List<Transform>();

        public SplineMoveMode moveMode = SplineMoveMode.Move2D;
        public DrivingDirection drivingDirection = DrivingDirection.Left;
        [Range(1, 8)] public int lanes = 1;
        [Range(1, 15)] public int waypointDistance = 5;
        public float laneWidth = 4f;
        public float speedLimitForAllRoads = 30f;
        public int spawnedPoints = 0;
        [Range(10, 100)] public int curveResolution = 30;

        public Transform AddControlPoint(Vector3 position)
        {
            Transform point = CreatePointObject(position);
            controlPointsList.Add(point);
            return point;
        }

        public Transform AddControlPointAtStart(Vector3 position)
        {
            Transform point = CreatePointObject(position);
            controlPointsList.Insert(0, point);
            return point;
        }

        public Transform InsertControlPoint(int index, Vector3 position)
        {
            index = Mathf.Clamp(index, 0, controlPointsList.Count);
            Transform point = CreatePointObject(position);
            controlPointsList.Insert(index, point);
            return point;
        }

        public void RemoveControlPoint(int index)
        {
            if (index < 0 || index >= controlPointsList.Count) return;
            Transform point = controlPointsList[index];
            controlPointsList.RemoveAt(index);
            if (point != null)
                DestroyImmediate(point.gameObject);
        }

        public void CleanupNullPoints()
        {
            controlPointsList.RemoveAll(t => t == null);
        }

        private Transform CreatePointObject(Vector3 position)
        {
            spawnedPoints++;
            GameObject go = new GameObject("controlPoint");
            go.transform.position = position;
            go.transform.SetParent(transform);
            return go.transform;
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
            Vector3 p0 = controlPointsList[segIndex].position;
            Vector3 p1 = controlPointsList[segIndex + 1].position;

            Vector3 tangentA = (segIndex > 0)
                ? (p1 - controlPointsList[segIndex - 1].position) * 0.5f
                : (p1 - p0);

            Vector3 tangentB = (segIndex + 2 < count)
                ? (controlPointsList[segIndex + 2].position - p0) * 0.5f
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
                Vector3 p0 = controlPointsList[i].position;
                Vector3 p3 = controlPointsList[i + 1].position;
                GetSegmentHandles(i, out Vector3 p1, out Vector3 p2);

                for (int s = 0; s <= curveResolution; s++)
                {
                    float t = s / (float)curveResolution;
                    points.Add(EvaluateCubicBezier(p0, p1, p2, p3, t));
                }
            }
            return points;
        }
    }
}
