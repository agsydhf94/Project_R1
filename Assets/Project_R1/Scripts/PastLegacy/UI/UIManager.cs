using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Threading.Tasks;

namespace R1
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private Dictionary<string, GameObject> openedUIs = new();

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        
        
        public async Task<T> OpenUI<T>(string address, Transform parent = null) where T : MonoBehaviour
        {
            var handle = Addressables.InstantiateAsync(address, parent ?? transform);
            var go = await handle.Task;

            if (!go.TryGetComponent<T>(out var ui))
            {
                Debug.LogError($"[UIManager] Could not find component {typeof(T).Name} on {address}");
                Addressables.ReleaseInstance(go);
                return null;
            }

            openedUIs[address] = go;
            return ui;
        }

        public void CloseUI(string address)
        {
            if (openedUIs.TryGetValue(address, out var go))
            {
                Addressables.ReleaseInstance(go);
                openedUIs.Remove(address);
            }
        }
        
    }
}
