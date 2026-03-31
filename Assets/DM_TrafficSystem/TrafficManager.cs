using UnityEngine;
using UnityEngine.Serialization;

namespace Darkmatter.TrafficSystem
{
    public class TrafficManager : MonoBehaviour
    {
        [FormerlySerializedAs("VehicleCount")]
        [Min(0)]
        public int vehicleCount = 5;
    }
}
