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
            List<AIVehicle> specificPrefabs = new List<AIVehicle>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i].vehicleType == type)
                {
                    specificPrefabs.Add(prefabs[i]);
                }
            }

            if (specificPrefabs.Count == 0) return null;

            return specificPrefabs[Random.Range(0, specificPrefabs.Count)];
        }

        public AIVehicle GetRandomPrefab()
        {
            if (prefabs.Count == 0) return null;
            return prefabs[Random.Range(0, prefabs.Count)];
        }
    }
}