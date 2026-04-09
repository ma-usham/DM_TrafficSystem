using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [CreateAssetMenu(fileName = "VehicleCollection", menuName = "DM Traffic System/Vehicle Collection")]
    public class VehicleCollection : ScriptableObject
    {
        public List<AIVehicle> prefabs = new List<AIVehicle>();

        public AIVehicle GetRandomPrefabOfType(VehicleType type)
        {
            AIVehicle selectedPrefab = null;
            int matchCount = 0;

            for (int i = 0; i < prefabs.Count; i++)
            {
                AIVehicle prefab = prefabs[i];
                if (prefab != null && prefab.vehicleType == type)
                {
                    matchCount++;
                    if (Random.Range(0, matchCount) == 0)
                    {
                        selectedPrefab = prefab;
                    }
                }
            }

            return selectedPrefab;
        }

        public AIVehicle GetRandomPrefab()
        {
            if (prefabs.Count == 0) return null;
            return prefabs[Random.Range(0, prefabs.Count)];
        }
    }
}
