using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public class VehiclePool
    {
        private VehicleCollection _collection;
        private Transform _container;

        private Dictionary<VehicleType, Queue<AIVehicle>> _pool = new Dictionary<VehicleType, Queue<AIVehicle>>();

        public VehiclePool(VehicleCollection collection, Transform container)
        {
            _collection = collection;
            _container = container;
        }

        public AIVehicle Spawn(AIWaypoint waypoint)
        {
            if (waypoint.settings.vehicleType == null || waypoint.settings.vehicleType.Length == 0)
            {
                Debug.LogWarning("Waypoint has no allowed VehicleTypes assigned.");
                return null;
            }
            VehicleType requiredType = waypoint.settings.vehicleType[Random.Range(0, waypoint.settings.vehicleType.Length)];

            if (!_pool.ContainsKey(requiredType))
            {
                _pool[requiredType] = new Queue<AIVehicle>();
            }

            AIVehicle instance = null;
            Vector3 spawnPos = waypoint.transform.position + new Vector3(0, 1f, 0);
            Quaternion spawnRot = waypoint.transform.rotation;

            if (_pool[requiredType].Count > 0)
            {
                instance = _pool[requiredType].Dequeue();
                instance.transform.position = spawnPos;
                instance.transform.rotation = spawnRot;
                instance.transform.SetParent(_container);
                instance.ResetRuntimeState();
                instance.gameObject.SetActive(true);
            }
            else
            {
                if (_collection != null)
                {
                    AIVehicle prefab = _collection.GetRandomPrefabOfType(requiredType);
                    if (prefab != null)
                    {
                        instance = Object.Instantiate(prefab, spawnPos, spawnRot, _container);
                        instance.ResetRuntimeState();
                        instance.gameObject.SetActive(true);
                    }
                }
            }

            return instance;
        }

        public void Despawn(AIVehicle vehicle)
        {
            if (vehicle == null) return;

            vehicle.ResetRuntimeState();
            vehicle.gameObject.SetActive(false);

            if (!_pool.ContainsKey(vehicle.vehicleType))
            {
                _pool[vehicle.vehicleType] = new Queue<AIVehicle>();
            }

            _pool[vehicle.vehicleType].Enqueue(vehicle);
        }

        internal void Prepopulate(int initialPoolSize)
        {
            if (_collection == null)
            {
                Debug.LogWarning("VehiclePool: Cannot prepopulate because VehicleCollection reference is missing.");
                return;
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                AIVehicle prefab = _collection.GetRandomPrefab();
                if (prefab != null)
                {
                    VehicleType type = prefab.vehicleType;
                    if (!_pool.ContainsKey(type))
                    {
                        _pool[type] = new Queue<AIVehicle>();
                    }
                    AIVehicle instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, _container);
                    instance.gameObject.SetActive(false);
                    _pool[type].Enqueue(instance);
                }

            }
        }
    }
}
