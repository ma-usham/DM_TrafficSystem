using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public class PriorityStopRoad
    {
        public List<AIWaypoint> stopPoints = new List<AIWaypoint>();
    }

    public class PriorityIntersection : MonoBehaviour
    {
        public string intersectionName = "New Intersection";
        [Tooltip("Time all roads wait between switching (yellow/red light duration)")]
        public float waitTime = 2f;
        [Tooltip("Time in seconds each road is green/active")]
        public float activeTime = 5f;

        public List<PriorityStopRoad> priorityStopRoads = new List<PriorityStopRoad>();

        private int currentStopRoadIndex = 0;

        private void Start()
        {
            // Initialize: Set all stop points to true (red) initially
            SetAllRoadsToStop();

            if (priorityStopRoads.Count > 0)
            {
                StartCoroutine(CycleIntersection());
            }
        }

        private IEnumerator CycleIntersection()
        {
            while (true)
            {
                PriorityStopRoad currentRoad = priorityStopRoads[currentStopRoadIndex];

                // Set current road's stop points to false to let cars pass
                SetRoadStopStatus(currentRoad, false);

                // Wait for the active duration
                yield return new WaitForSeconds(activeTime);

                // Set current road's stop points to true (red/stop)
                SetRoadStopStatus(currentRoad, true);

                // Short delay to let cars clear out the intersection
                yield return new WaitForSeconds(waitTime);

                // Move to next road
                currentStopRoadIndex = (currentStopRoadIndex + 1) % priorityStopRoads.Count;
            }
        }

        private void SetRoadStopStatus(PriorityStopRoad road, bool isStop)
        {
            foreach (var wp in road.stopPoints)
            {
                if (wp != null)
                {
                    wp.settings.isStopPoint = isStop;
                }
            }
        }

        private void SetAllRoadsToStop()
        {
            foreach (var road in priorityStopRoads)
            {
                SetRoadStopStatus(road, true);
            }
        }
    }
}
