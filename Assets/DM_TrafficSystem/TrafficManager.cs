using UnityEngine;
using UnityEngine.Serialization;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Stores scene-wide traffic configuration values used by editor tools and future runtime systems.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        [FormerlySerializedAs("VehicleCount")]
        [Min(0)]
        public int vehicleCount = 5;
    }
}
