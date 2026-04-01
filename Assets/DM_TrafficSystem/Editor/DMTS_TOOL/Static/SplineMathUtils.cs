using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Provides spline math helpers shared by the road editor and waypoint builder.
    /// </summary>
    public static class SplineMathUtils
    {
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
        public static void GetSegmentHandles(IReadOnlyList<Vector3> controlPointsList, int segIndex, out Vector3 handleA, out Vector3 handleB)
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

        /// <summary>
        /// Samples every road segment into evenly spaced Bezier points for previews and waypoint generation.
        /// </summary>
        public static List<Vector3> GetCurvePoints(IReadOnlyList<Vector3> controlPointsList, int curveResolution)
        {
            var points = new List<Vector3>();
            if (controlPointsList == null || controlPointsList.Count < 2)
                return points;

            int segmentResolution = Mathf.Max(1, curveResolution);

            for (int i = 0; i < controlPointsList.Count - 1; i++)
            {
                Vector3 p0 = controlPointsList[i];
                Vector3 p3 = controlPointsList[i + 1];
                GetSegmentHandles(controlPointsList, i, out Vector3 p1, out Vector3 p2);

                for (int sampleIndex = 0; sampleIndex <= segmentResolution; sampleIndex++)
                {
                    float t = sampleIndex / (float)segmentResolution;
                    points.Add(EvaluateCubicBezier(p0, p1, p2, p3, t));
                }
            }

            return points;
        }
    }
}
