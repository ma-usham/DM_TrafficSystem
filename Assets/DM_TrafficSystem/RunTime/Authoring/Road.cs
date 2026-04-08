using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Stores the editable spline definition and generated lane data for a road segment.
    /// </summary>
    public class Road : MonoBehaviour
    {
#if UNITY_EDITOR
        public List<Vector3> controlPointsList = new List<Vector3>();
        public SplineMoveMode splineMoveMode = SplineMoveMode.Move2D;
        [Range(1, 8)] public int lanes = 1;
        [Range(1, 15)] public int waypointDistance = 4;
        public float laneWidth = 4f;
        public float speedLimitForAllLanes = 30f;
        [FormerlySerializedAs("laneChangeLinkRoadDistance")]
        [Min(1)] public int laneChangeLinkOffset = 1;
        [Min(1f)] public float laneChangeMaxTurnAngle = 5f;
        [Range(10, 100)] public int curveResolution = 30;
        public DrivingDirection drivingDirection = DrivingDirection.Left;

        public List<List<Transform>> generatedLanes = new List<List<Transform>>();
        public List<AILane> laneObjects = new List<AILane>();

        #region Control Point Management
        /// <summary>
        /// Appends a new control point to the end of the road spline.
        /// </summary>
        public void AddControlPoint(Vector3 position)
        {
            controlPointsList.Add(position);
        }

        /// <summary>
        /// Inserts a control point into the spline while clamping the requested index to a valid range.
        /// </summary>
        public void InsertControlPoint(int index, Vector3 position)
        {
            index = Mathf.Clamp(index, 0, controlPointsList.Count);
            controlPointsList.Insert(index, position);
        }

        /// <summary>
        /// Removes the control point at the given index when it exists.
        /// </summary>
        public void RemoveControlPoint(int index)
        {
            if (index < 0 || index >= controlPointsList.Count)
                return;

            controlPointsList.RemoveAt(index);
        }
        #endregion
#endif

    }
}
