using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Stores one editable connection spline between a lane-ending waypoint and a lane-start waypoint.
    /// </summary>
    public class AIWaypointConnection : MonoBehaviour
    {
        public AIWaypoint sourceWaypoint;
        public AIWaypoint targetWaypoint;
        [Min(0f)] public float connectionSpeedLimit = 20f;
        public VehicleType[] connectionVehicleTypes = new[] { VehicleType.Default };

        [Min(1f)] public float waypointSpacing = 2f;
        [Range(4, 100)] public int curveResolution = 30;

        public List<Vector3> controlPointsList = new List<Vector3>();
        public List<AIWaypoint> transitionWaypoints = new List<AIWaypoint>();

        /// <summary>
        /// Keeps the first and last control points locked to the referenced waypoint positions.
        /// </summary>
        public void SyncEndpointControlPoints()
        {
            controlPointsList ??= new List<Vector3>();

            if (sourceWaypoint == null || targetWaypoint == null)
                return;

            Vector3 sourcePosition = sourceWaypoint.transform.position;
            Vector3 targetPosition = targetWaypoint.transform.position;

            if (controlPointsList.Count == 0)
            {
                controlPointsList.Add(sourcePosition);
                controlPointsList.Add(targetPosition);
                return;
            }

            if (controlPointsList.Count == 1)
                controlPointsList.Add(targetPosition);

            controlPointsList[0] = sourcePosition;
            controlPointsList[controlPointsList.Count - 1] = targetPosition;
        }

        /// <summary>
        /// Seeds the spline with two editable middle points aligned to the source and target travel directions.
        /// </summary>
        public void SetDefaultControlPoints(Vector3 sourceForward, Vector3 targetForward)
        {
            controlPointsList ??= new List<Vector3>();
            controlPointsList.Clear();

            if (sourceWaypoint == null || targetWaypoint == null)
                return;

            Vector3 sourcePosition = sourceWaypoint.transform.position;
            Vector3 targetPosition = targetWaypoint.transform.position;
            float connectionDistance = Vector3.Distance(sourcePosition, targetPosition);
            float tangentDistance = Mathf.Clamp(connectionDistance * 0.35f, 2f, 12f);

            if (sourceForward.sqrMagnitude <= Mathf.Epsilon)
                sourceForward = (targetPosition - sourcePosition).normalized;

            if (targetForward.sqrMagnitude <= Mathf.Epsilon)
                targetForward = (targetPosition - sourcePosition).normalized;

            controlPointsList.Add(sourcePosition);
            Vector3 midPoint = Vector3.Lerp(
                sourcePosition + sourceForward.normalized * tangentDistance,
                targetPosition - targetForward.normalized * tangentDistance,
                0.5f
            );
            controlPointsList.Add(midPoint);
            controlPointsList.Add(targetPosition);
        }

        /// <summary>
        /// Inserts a new editable control point while keeping the spline endpoints locked.
        /// </summary>
        public void InsertControlPoint(int index, Vector3 position)
        {
            controlPointsList ??= new List<Vector3>();

            if (controlPointsList.Count < 2)
            {
                SyncEndpointControlPoints();
            }

            index = Mathf.Clamp(index, 1, Mathf.Max(1, controlPointsList.Count - 1));
            controlPointsList.Insert(index, position);
        }

        /// <summary>
        /// Removes an editable control point when it is not one of the fixed spline endpoints.
        /// </summary>
        public void RemoveControlPoint(int index)
        {
            if (controlPointsList == null || index <= 0 || index >= controlPointsList.Count - 1)
                return;

            controlPointsList.RemoveAt(index);
        }
    }
}
