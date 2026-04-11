using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public struct LiveDebugData
    {
        public float currentSpeed;
        public float localMaxSpeed;
        public float engineMaxSpeed;
        public bool isChangingLanes;
        public bool wantsToChangeLane;
        public bool leftLaneBlocked;
        public bool rightLaneBlocked;
        public float impatienceTimer;
        public float frustrationTime;
        public float laneChangeCooldownTimer;
    }
}