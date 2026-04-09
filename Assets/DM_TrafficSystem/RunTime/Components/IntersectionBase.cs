using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Interface allowing road groups to expose their stop points to the intersection manager.
    /// </summary>
    public interface IIntersectionRoad
    {
        List<AIWaypoint> GetStopPoints();
    }

    /// <summary>
    /// Base class for traffic intersections. Handles common stop point management and coroutine lifecycle.
    /// </summary>
    public abstract class IntersectionBase : MonoBehaviour
    {
        [Tooltip("The designated name for this intersection group.")]
        public string intersectionName = "New Intersection";

        protected Coroutine cycleCoroutine;

        /// <summary>
        /// Applies one stop-state value to every stop point in the provided road group.
        /// </summary>
        protected void SetRoadStopStatus(IIntersectionRoad road, bool isStop)
        {
            if (road == null || road.GetStopPoints() == null) return;

            foreach (var wp in road.GetStopPoints())
            {
                if (wp != null)
                {
                    wp.settings.isStopPoint = isStop;
                }
            }
        }

        /// <summary>
        /// Forces every configured road group evaluated in the collection into the stop state.
        /// </summary>
        protected void SetAllRoadsToStop(IEnumerable<IIntersectionRoad> roads)
        {
            if (roads == null) return;

            foreach (var road in roads)
            {
                SetRoadStopStatus(road, true);
            }
        }

        protected virtual void OnDisable()
        {
            StopCycle();
        }

        protected virtual void OnDestroy()
        {
            StopCycle();
        }

        protected void StopCycle()
        {
            if (cycleCoroutine != null)
            {
                StopCoroutine(cycleCoroutine);
                cycleCoroutine = null;
            }
        }
    }
}