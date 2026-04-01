using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    
    public class Road : MonoBehaviour
    {
#if UNITY_EDITOR
        public List<Vector3> controlPointsList = new List<Vector3>();
        public SplineMoveMode splineMoveMode = SplineMoveMode.Move2D;
        [Range(1, 8)] public int lanes = 1;
        [Range(1, 15)] public int waypointDistance = 5;
        public float laneWidth = 4f;
        public float speedLimitForAllLanes = 30f;
        [Min(1)] public int laneChangeLinkRoadDistance = 1;
        [Range(10, 100)] public int curveResolution = 30;
        public DrivingDirection drivingDirection = DrivingDirection.Left;

        public List<List<Transform>> generatedLanes = new List<List<Transform>>();
        public List<AILane> laneObjects = new List<AILane>();

        #region Control Point Management
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
        #endregion
#endif

    }

}
