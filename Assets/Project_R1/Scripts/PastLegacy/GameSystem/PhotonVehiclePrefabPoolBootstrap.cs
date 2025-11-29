#if PHOTON_UNITY_NETWORKING
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace R1
{
    public class PhotonVehiclePrefabPoolBootstrap : MonoBehaviour
    {
        [SerializeField] private VehicleDatabaseSO vehicleDb;

        private void Awake()
        {
            if (vehicleDb == null)
            {
                Debug.LogError("[PrefabPool] VehicleDatabase is not set.");
                return;
            }
            PhotonNetwork.PrefabPool = new DbBackedPrefabPool(vehicleDb.BuildMap());
        }

        private class DbBackedPrefabPool : IPunPrefabPool
        {
            private readonly Dictionary<string, GameObject> dict;
            public DbBackedPrefabPool(Dictionary<string, GameObject> map) => dict = map;

            public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
            {
                if (!dict.TryGetValue(prefabId, out var prefab) || prefab == null)
                {
                    Debug.LogError($"[PrefabPool] Prefab not found for key: {prefabId}");
                    return null;
                }
                return Object.Instantiate(prefab, position, rotation);
            }

            public void Destroy(GameObject go)
            {
                Object.Destroy(go);
            }
        }
    }
}
#endif