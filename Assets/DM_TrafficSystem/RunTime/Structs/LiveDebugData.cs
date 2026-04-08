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
        public bool wantsToOvertake;
        public bool wantsToHonk;
        public bool leftLaneBlocked;
        public bool rightLaneBlocked;
    }
}