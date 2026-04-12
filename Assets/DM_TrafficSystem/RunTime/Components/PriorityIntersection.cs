using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Stores one group of stop points that should stop together in a priority intersection.
    /// </summary>
    [System.Serializable]
    public class PriorityStopRoad : IIntersectionRoad
    {
        public List<AIWaypoint> stopPoints = new List<AIWaypoint>();
        public List<AIWaypoint> GetStopPoints() => stopPoints;
    }

    /// <summary>
    /// Cycles stop states across configured priority-road groups at runtime.
    /// </summary>
    public class PriorityIntersection : IntersectionBase
    {
        [Tooltip("Time all roads wait between switching (yellow/red light duration)")]
        public float waitTime = 2f;
        [Tooltip("Time in seconds each road is green/active")]
        public float activeTime = 5f;

        public List<PriorityStopRoad> priorityStopRoads = new List<PriorityStopRoad>();

        public override IEnumerable<IIntersectionRoad> GetAllRoads() => priorityStopRoads;

        private int currentStopRoadIndex = 0;

        /// <summary>
        /// Initializes all stop points and starts the runtime cycle when road groups exist.
        /// </summary>
        private void Start()
        {
            SetAllRoadsToStop(priorityStopRoads);

            if (priorityStopRoads.Count > 0)
            {
                cycleCoroutine = StartCoroutine(CycleIntersection());
            }
        }

        /// <summary>
        /// Alternates which configured road group may proceed through the intersection.
        /// </summary>
        private IEnumerator CycleIntersection()
        {
            while (true)
            {
                PriorityStopRoad currentRoad = priorityStopRoads[currentStopRoadIndex];

                SetRoadStopStatus(currentRoad, false);
                yield return new WaitForSeconds(activeTime);

                SetRoadStopStatus(currentRoad, true);
                yield return new WaitForSeconds(waitTime);

                currentStopRoadIndex = (currentStopRoadIndex + 1) % priorityStopRoads.Count;
            }
        }
    }
}
