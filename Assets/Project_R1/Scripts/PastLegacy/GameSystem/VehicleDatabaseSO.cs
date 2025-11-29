using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    [CreateAssetMenu(menuName = "Vehicle Database")]
    public class VehicleDatabaseSO : ScriptableObject
    {
        [System.Serializable]
        public class Entry { public string key; public GameObject prefab; }

        public List<Entry> entries = new List<Entry>();

        private Dictionary<string, GameObject> map;
        public GameObject Get(string key)
        {
            if (map == null)
            {
                map = new Dictionary<string, GameObject>();
                foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.key) && e.prefab)
                        map[e.key] = e.prefab;
            }
            return map != null && map.TryGetValue(key, out var go) ? go : null;
        }

        public Dictionary<string, GameObject> BuildMap()
        {
            var d = new Dictionary<string, GameObject>();
            foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.key) && e.prefab)
                    d[e.key] = e.prefab;
            return d;
        }
    }
}
