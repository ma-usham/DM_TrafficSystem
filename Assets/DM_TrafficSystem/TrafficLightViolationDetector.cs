using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public class TrafficLightViolationDetector : MonoBehaviour
    {
        public TrafficLightIntersection targetIntersection;
        public int targetRoadIndex = -1;

        private void OnTriggerEnter(Collider other)
        {
            if (targetIntersection == null || targetRoadIndex < 0 || targetRoadIndex >= targetIntersection.trafficLightRoads.Count)
            {
                Debug.LogWarning("TrafficLightViolationDetector: No valid target intersection or road index assigned.");
                return;
            }
            
            if (other.CompareTag("Player"))
            {
                TrafficLightRoad road = targetIntersection.trafficLightRoads[targetRoadIndex];
                if (road.currentLightState == TrafficLightState.Red)
                {
                    Debug.Log("TrafficLightViolationDetector: Vehicle violated red light at intersection.");
                }
            }
        }
    }
}