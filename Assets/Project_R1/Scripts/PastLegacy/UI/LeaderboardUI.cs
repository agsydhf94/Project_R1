using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Pooled leaderboard UI renderer. Builds or absorbs a pool of <see cref="LeaderboardEntry"/> items,
    /// then fills and toggles them without further instantiation at runtime.
    /// Use <see cref="Set"/> to render a ranked list with optional display-name mapping and time formatting.
    /// </summary>
    [DisallowMultipleComponent]
    public class LeaderboardUI : MonoBehaviour
    {
        [Header("Pool Setup")]
        /// <summary>
        /// Parent transform that will contain all leaderboard entries. Defaults to this object's transform if not set.
        /// </summary>
        [SerializeField] private Transform container;

        /// <summary>
        /// Entry prefab used to grow the pool when needed. 
        /// </summary>
        [SerializeField] private LeaderboardEntry entryPrefab;   

        /// <summary>
        /// Initial number of entry slots to ensure in the pool (upper bound of expected players).
        /// </summary>
        [SerializeField] private int initialCapacity = 16;       
        
        /// <summary>
        /// If true, missing entries are created once during <see cref="Awake"/>.
        /// </summary>
        [SerializeField] private bool buildOnAwake = true;
        
        
        /// <summary>
        /// If true, never instantiate after the first build pass (the pool is locked).
        /// </summary>
        [SerializeField] private bool lockAfterBuild = true;

        private readonly List<LeaderboardEntry> pool = new();
        private int capacity;
        private bool _built;

        /// <summary>
        /// Current number of pooled entries (whether pre-placed in the scene or instantiated).
        /// </summary>
        public int Capacity => capacity;


        /// <summary>
        /// Absorbs any pre-placed entries under the container, optionally builds up to the initial capacity once,
        /// and warns if the resulting capacity is zero in play mode.
        /// </summary>
        void Awake()
        {
            if (!container) container = transform;

            // Absorb pre-placed entries in the scene under container
            var existing = container.GetComponentsInChildren<LeaderboardEntry>(true);
            foreach (var e in existing)
                if (e && !pool.Contains(e)) pool.Add(e);

            // If a prefab is provided and buildOnAwake is true, create the missing amount exactly once
            if (buildOnAwake) BuildIfNeededOnce(initialCapacity);

            capacity = pool.Count;

            if (Application.isPlaying && capacity == 0)
                Debug.LogWarning("[LeaderboardUI] capacity=0. Check prefab/container or increase initialCapacity.");
        }

        /// <summary>
        /// Creates missing entries once to reach the requested capacity. 
        /// Does nothing if already built or if existing/pre-placed entries suffice.
        /// </summary>
        /// <param name="want">Target number of pooled entries desired.</param>
        public void BuildIfNeededOnce(int want)
        {
            if (_built) return;

            // If current pool (including pre-placed ones) satisfies capacity, skip building
            if (pool.Count >= want)
            {
                _built = lockAfterBuild;
                capacity = pool.Count;
                return;
            }

            if (!entryPrefab || !container)
            {
                Debug.LogWarning("[LeaderboardUI] entryPrefab/container not set. Cannot build.");
                _built = lockAfterBuild;
                capacity = pool.Count;
                return;
            }

            int need = want - pool.Count;
            for (int i = 0; i < need; i++)
            {
                var e = Instantiate(entryPrefab, container);
                e.gameObject.SetActive(false);
                pool.Add(e);
            }

            _built = lockAfterBuild;
            capacity = pool.Count;
        }

        
        /// <summary>
        /// Renders the leaderboard using pooled entries only (no new instantiation after the first build).
        /// Entries beyond capacity are ignored. 
        /// Fastest lap is detected to toggle a badge, and the local player row can be highlighted.
        /// </summary>
        /// <param name="records">Ordered race records to display (index 0 is rank 1).</param>
        /// <param name="displayName">Optional mapper from userId to display name; if null or returns empty, falls back to <c>playerId</c> or <c>userId</c>.</param>
        /// <param name="format">Optional formatter for total time (seconds → text); if null, total time is hidden.</param>
        /// <param name="localUserId">User id of the local player for row highlighting.</param>
        public void Set(IReadOnlyList<RaceRecord> records,
                        System.Func<string, string> displayName,
                        System.Func<float, string> format,
                        string localUserId)
        {
            int count = records?.Count ?? 0;

            if (count > capacity)
            {
                Debug.LogWarning($"[LeaderboardUI] requested {count} > capacity {capacity}. Excess entries will be ignored.");
                count = capacity;
            }

            // Determine fastest-lap holder among the first 'count' records
            string fastestUid = null;
            float fastest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var r = records[i];
                if (r == null) continue;
                if (r.bestLap > 0f && r.bestLap < fastest) { fastest = r.bestLap; fastestUid = r.userId; }
            }

            // Render using pooled entries only
            for (int i = 0; i < capacity; i++)
            {
                bool active = (i < count);
                var entry = pool[i];
                if (!entry) continue;

                entry.gameObject.SetActive(active);
                if (!active) continue;

                var rec = records[i];
                bool isLocal = rec.userId == localUserId;
                bool hasFastest = rec.userId == fastestUid;

                string nameToShow = !string.IsNullOrEmpty(rec.playerId) ? rec.playerId : rec.userId;
                if (displayName != null)
                {
                    var mapped = displayName(rec.userId);
                    if (!string.IsNullOrEmpty(mapped)) nameToShow = mapped;
                }

                entry.SetData(
                    rank: i + 1,
                    playerName: nameToShow,
                    totalTime: format != null ? format(rec.bestTotal) : "",
                    isLocalPlayer: isLocal,
                    hasFastestLap: hasFastest
                );
            }
        }
    }
}
