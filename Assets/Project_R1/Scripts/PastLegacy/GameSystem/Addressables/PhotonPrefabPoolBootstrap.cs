#if PHOTON_UNITY_NETWORKING
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Wires PhotonNetwork.PrefabPool to the AddressablesPreloader cache so that PUN
    /// instantiation uses preloaded Addressables prefabs. Keeps Photon/Preloader wiring
    /// separate from gameplay logic.
    /// </summary>
    public class PhotonPrefabPoolBootstrap : MonoBehaviour
    {
        /// <summary>
        /// Reference to the Addressables preloader that holds the prefab cache.
        /// If not assigned, the component tries to find one at runtime.
        /// </summary>
        [SerializeField] private AddressablesPreloader preloader;

        /// <summary>
        /// If true, waits until the preloader finishes and then sets the PrefabPool.
        /// If false, sets the PrefabPool immediately.
        /// </summary>
        [SerializeField] private bool waitForPreload = true;


        /// <summary>
        /// Initializes the wiring: resolves the preloader, optionally waits for preload
        /// completion, and sets PhotonNetwork.PrefabPool once.
        /// </summary>
        private void Awake()
        {
            if (preloader == null)
            {
                preloader = FindObjectOfType<AddressablesPreloader>();
            }
            if (preloader == null)
            {
                Debug.LogError("[PrefabPool] AddressablesPreloader is missing in scene.");
                enabled = false;
                return;
            }

            if (waitForPreload)
            {
                // A) Use event if available
                preloader.Loaded += SetPoolOnce;

                // If already loaded, set immediately
                if (preloader.IsLoaded)
                {
                    SetPoolOnce();
                }
            }
            else
            {
                // B) Set immediately regardless of preload status
                //    If preload is done by the time Instantiate is called, it is fine
                SetPoolOnce();
            }
        }


        /// <summary>
        /// Unsubscribes from the preloader event when this component is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (preloader != null)
            {
                preloader.Loaded -= SetPoolOnce;
            }
        }


        /// <summary>
        /// Sets PhotonNetwork.PrefabPool to a pool backed by the preloaded prefabs.
        /// No-ops if it is already assigned to our Addressables pool.
        /// </summary>
        private void SetPoolOnce()
        {
            if (PhotonNetwork.PrefabPool is AddressablesPrefabPool) return;
            PhotonNetwork.PrefabPool = new AddressablesPrefabPool(preloader.vehiclePrefabs);
        }

        /// <summary>
        /// Minimal PUN prefab pool implementation that returns preloaded prefabs by key
        /// and destroys instances on return.
        /// </summary>
        private class AddressablesPrefabPool : IPunPrefabPool
        {
            private readonly Dictionary<string, GameObject> dict;

            /// <summary>
            /// Creates a pool backed by the given prefab cache (Addressables preloaded map).
            /// </summary>
            /// <param name="cache">Dictionary mapping Addressables keys to prefabs.</param>
            public AddressablesPrefabPool(Dictionary<string, GameObject> cache)
            {
                dict = cache;
            }


            /// <summary>
            /// Instantiates a preloaded prefab by key at the given position/rotation.
            /// </summary>
            /// <param name="prefabId">Addressables key (Photon prefabId).</param>
            /// <param name="position">Spawn position.</param>
            /// <param name="rotation">Spawn rotation.</param>
            /// <returns>Instantiated GameObject, or null if not found.</returns>
            public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
            {
                if (!dict.TryGetValue(prefabId, out var prefab) || prefab == null)
                {
                    Debug.LogError($"[PrefabPool] Not preloaded or missing key: {prefabId}");
                    return null;
                }
                return Object.Instantiate(prefab, position, rotation);
            }


            /// <summary>
            /// Destroys the given instance (no pooling of instances is performed).
            /// </summary>
            /// <param name="go">Instance to destroy.</param>
            public void Destroy(GameObject go)
            {
                Object.Destroy(go);
            }
        }
    }
}
#endif