using UnityEngine;

namespace R1  
{
    /// <summary>
    /// Singleton container for race launch parameters.
    /// Stores scene, track, vehicle, AI, and rule settings used when starting a race.
    /// Ensures settings are validated both in editor and at runtime.
    /// </summary>
    public class GameLaunchParams : MonoBehaviour
    {   
        /// <summary>
        /// Singleton instance of <see cref="GameLaunchParams"/>.
        /// Only one instance is allowed to persist across scenes.
        /// </summary>
        public static GameLaunchParams Instance { get; private set; }

        [Header("Race Scene / Track")]
        /// <summary>
        /// The name of the race scene to be loaded.
        /// </summary>
        public string raceSceneName;

        /// <summary>
        /// Human-readable name of the track for UI display.
        /// </summary>
        public string trackDisplayName;

        [Header("Vehicle Keys (Addressables keys)")]
        /// <summary>
        /// Addressables key of the selected player vehicle prefab.
        /// </summary>
        public string playerKey;

        [Header("AI Selection")]
        /// <summary>
        /// Pool of available AI vehicle keys (Addressables).
        /// </summary>
        public string[] aiPool;

        /// <summary>
        /// Number of AI racers to spawn from the pool.
        /// </summary>
        public int aiCount;

        [Header("Rules")]
        /// <summary>
        /// Total number of laps required to finish the race.
        /// </summary>
        public int totalLaps;

        /// <summary>
        /// Countdown duration in seconds before the race starts.
        /// </summary>
        public float countdownSeconds = 3f;


        private void OnValidate()
        {
            ValidateAndClamp();
        }

        // ▶ 메뉴 버튼에서도 호출할 수 있게 public 으로
        public void ValidateAndClamp(bool logWarnings = true)
        {
            if (logWarnings)
            {
                if (string.IsNullOrEmpty(raceSceneName))
                    Debug.LogWarning("[GameLaunchParams] raceSceneName is empty.");
                if (string.IsNullOrEmpty(playerKey))
                    Debug.LogWarning("[GameLaunchParams] playerKey is empty.");
            }

            if (totalLaps < 1) totalLaps = 1;

            if (aiPool == null) aiPool = System.Array.Empty<string>();
            aiCount = Mathf.Clamp(aiCount, 0, aiPool.Length);

            if (countdownSeconds < 1f) countdownSeconds = 1f;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameLaunchParams] Duplicate in scene '{gameObject.scene.name}'. Destroying this.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 실행 시점 추적용(문제 있을 때 원인 찾기 쉬움)
            Debug.Log($"[GameLaunchParams] Awake in '{gameObject.scene.name}' | race='{raceSceneName}', playerKey='{playerKey}'");

            // 런타임 보정
            ValidateAndClamp(logWarnings: true);
        }


        /// <summary>
        /// Returns the selected AI vehicle keys up to the configured AI count.
        /// </summary>
        /// <returns>Array of AI vehicle keys to be used for spawning.</returns>
        public string[] GetSelectedAiKeys()
        {
            int n = Mathf.Clamp(aiCount, 0, (aiPool != null ? aiPool.Length : 0));
            var arr = new string[n];
            for (int i = 0; i < n; i++) arr[i] = aiPool[i];
            return arr;
        }
    }
}
