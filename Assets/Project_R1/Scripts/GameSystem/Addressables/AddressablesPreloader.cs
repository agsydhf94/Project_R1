using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace R1
{
    /// <summary>
    /// Preloads and caches Addressables assets by label at runtime.
    /// Downloads dependencies, loads resource locations by type, and keeps
    /// loaded assets in dictionaries so game code can retrieve prefabs/SOs
    /// without incurring additional async loads.
    /// </summary>
    public class AddressablesPreloader : MonoBehaviour
    {
        [Header("Labels to Preload")]
        /// <summary>
        /// Addressables labels whose <see cref="GameObject"/> entries (e.g., vehicles)
        /// should be downloaded and loaded into <see cref="vehiclePrefabs"/>.
        /// </summary>
        public List<string> vehicleLabels = new();

        /// <summary>
        /// Addressables labels whose <see cref="ScriptableObject"/> entries (e.g., tuning data)
        /// should be downloaded and loaded into <see cref="tuningAssets"/>.
        /// </summary>
        public List<string> tuningLabels = new();

        [Header("Runtime Cache (Address key → Asset)")]
        /// <summary>
        /// Runtime cache of loaded vehicle prefabs keyed by Addressables primary key.
        /// </summary>
        public readonly Dictionary<string, GameObject> vehiclePrefabs = new();

        /// <summary>
        /// Runtime cache of loaded tuning ScriptableObjects keyed by Addressables primary key.
        /// </summary>
        public readonly Dictionary<string, ScriptableObject> tuningAssets = new();

        /// <summary>
        /// Internal list of Addressables operation handles to release on destroy.
        /// </summary>
        private readonly List<AsyncOperationHandle> _handles = new();

        /// <summary>
        /// Indicates whether <see cref="PreloadAll"/> completed at least once.
        /// Subsequent calls will early-exit to avoid duplicate work.
        /// </summary>
        private bool _loaded;


        /// <summary>
        /// Attempts to get a preloaded vehicle prefab by Addressables key.
        /// </summary>
        /// <param name="key">The Addressables primary key used when loading.</param>
        /// <param name="prefab">The cached <see cref="GameObject"/> if found.</param>
        /// <returns>True if the prefab exists in the cache; otherwise false.</returns>
        public bool TryGetVehicle(string key, out GameObject prefab)
        {
            return vehiclePrefabs.TryGetValue(key, out prefab);
        }

        /// <summary>
        /// Attempts to get a preloaded tuning asset by Addressables key.
        /// </summary>
        /// <param name="key">The Addressables primary key used when loading.</param>
        /// <param name="so">The cached <see cref="ScriptableObject"/> if found.</param>
        /// <returns>True if the asset exists in the cache; otherwise false.</returns>
        public bool TryGetTuning(string key, out ScriptableObject so)
        {
            return tuningAssets.TryGetValue(key, out so);
        }
            

         /// <summary>
        /// Downloads dependencies and loads all Addressables that match the configured labels.
        /// Vehicle labels are loaded as <see cref="GameObject"/>s into <see cref="vehiclePrefabs"/>,
        /// and tuning labels as <see cref="ScriptableObject"/>s into <see cref="tuningAssets"/>.
        /// Subsequent calls are ignored once loading has completed.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerator"/> suitable for <c>StartCoroutine</c>; yields during
        /// dependency downloads, location queries, and asset loads.
        /// </returns>
        public IEnumerator PreloadAll()
        {
            if (_loaded) yield break;

            // Dependencies
            foreach (var label in vehicleLabels)
            {
                var depsHandle = Addressables.DownloadDependenciesAsync(label, true);
                _handles.Add(depsHandle);
                yield return depsHandle;
            }
            foreach (var label in tuningLabels)
            {
                var depsHandle = Addressables.DownloadDependenciesAsync(label, true);
                _handles.Add(depsHandle);
                yield return depsHandle;
            }

            // Vehicles
            foreach (var label in vehicleLabels)
            {
                var locationsHandle = Addressables.LoadResourceLocationsAsync(label, typeof(GameObject));
                _handles.Add(locationsHandle);
                yield return locationsHandle;

                foreach (var location in locationsHandle.Result)
                {
                    var key = location.PrimaryKey;
                    if (vehiclePrefabs.ContainsKey(key)) continue;

                    var loadHandle = Addressables.LoadAssetAsync<GameObject>(location);
                    _handles.Add(loadHandle);
                    yield return loadHandle;

                    if (loadHandle.Status == AsyncOperationStatus.Succeeded && loadHandle.Result)
                        vehiclePrefabs[key] = loadHandle.Result;
                }
            }

            // Tunings
            foreach (var label in tuningLabels)
            {
                var locationsHandle = Addressables.LoadResourceLocationsAsync(label, typeof(ScriptableObject));
                _handles.Add(locationsHandle);
                yield return locationsHandle;

                foreach (var location in locationsHandle.Result)
                {
                    var key = location.PrimaryKey;
                    if (tuningAssets.ContainsKey(key)) continue;

                    var loadHandle = Addressables.LoadAssetAsync<ScriptableObject>(location);
                    _handles.Add(loadHandle);
                    yield return loadHandle;

                    if (loadHandle.Status == AsyncOperationStatus.Succeeded && loadHandle.Result)
                        tuningAssets[key] = loadHandle.Result;
                }
            }

            _loaded = true;
        }


        /// <summary>
        /// Releases all Addressables operation handles and clears runtime caches.
        /// </summary>
        private void OnDestroy()
        {
            // Release Addressables operation handles (assuming the manual, unified strategy)
            foreach (var handle in _handles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            _handles.Clear();

            // Clear runtime caches
            vehiclePrefabs.Clear();
            tuningAssets.Clear();
        }
    }
}
