using UnityEngine;
using UnityEngine.SceneManagement;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace R1
{
    /// <summary>
    /// Starts a race for a specific track when clicked.
    /// Populates <see cref="GameLaunchParams"/> (creating it if missing),
    /// clamps/validates values, builds the pre-race grid seed, and loads the race scene.
    /// In multiplayer (Photon) rooms, uses <c>PhotonNetwork.LoadLevel</c>; otherwise uses <c>SceneManager.LoadScene</c>.
    /// </summary>
    public class TrackButton : MonoBehaviour
    {
        [Header("Target Race Scene")]
        /// <summary>
        /// The name of the race scene to load (e.g., "RaceScene").
        /// </summary>
        public string raceSceneName = "RaceScene";

        /// <summary>
        /// Human-readable track name to display in UI and session metadata.
        /// </summary>
        public string trackDisplayName;

        [Header("Vehicles (Addressables Keys)")]
        /// <summary>
        /// Addressables key for the player's vehicle prefab.
        /// </summary>
        public string playerKey = "PlayerCar";

        /// <summary>
        /// Addressables keys for AI vehicle prefabs available for this track.
        /// </summary>
        public string[] aiPoolForThisTrack = new[] { "AI_1", "AI_2", "AI_3" };

        [Header("Defaults (UI overrides allowed)")]
        /// <summary>
        /// Default total lap count for the race (will be clamped to a minimum of 1).
        /// </summary>
        public int defaultTotalLaps = 3;

        /// <summary>
        /// Default number of AI opponents (will be clamped to the size of <see cref="aiPoolForThisTrack"/>).
        /// </summary>
        public int defaultAiCount = 2;

        /// <summary>
        /// Default countdown time before the race starts (seconds, clamped to at least 1).
        /// </summary>
        public float countdownSeconds = 3f;


        /// <summary>
        /// Handles the click action:
        /// ensures a <see cref="GameLaunchParams"/> instance exists,
        /// injects scene/vehicle/rule settings, clamps and validates them,
        /// builds the pre-race grid seed, and loads the race scene.
        /// Uses Photon scene loading if the player is in a Photon room; otherwise loads locally.
        /// </summary>
        public void OnClickStart()
        {
            // Ensure GameLaunchParams exists.
            var paramInstance = GameLaunchParams.Instance;
            if (!paramInstance)
            {
                var go = new GameObject(nameof(GameLaunchParams));
                paramInstance = go.AddComponent<GameLaunchParams>();
            }

            // Inject parameters.
            paramInstance.raceSceneName = raceSceneName;
            paramInstance.trackDisplayName = trackDisplayName;
            paramInstance.playerKey = playerKey;
            paramInstance.aiPool = aiPoolForThisTrack;
            paramInstance.aiCount = Mathf.Clamp(defaultAiCount, 0, paramInstance.aiPool?.Length ?? 0);
            paramInstance.totalLaps = Mathf.Max(1, defaultTotalLaps);
            paramInstance.countdownSeconds = Mathf.Max(1f, countdownSeconds);

            // Validate ranges and log warnings for missing fields.
            paramInstance.ValidateAndClamp(logWarnings: true);

            PreRaceGridSeed.Build
            (
            aiCount: paramInstance.aiCount,
            aiPool: paramInstance.aiPool,
            localUserId: "LOCAL",            
            localName: "Player"
            );

            // Load scene (Photon if in room; otherwise single-player).
#if PHOTON_UNITY_NETWORKING
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.AutomaticallySyncScene = true;
                PhotonNetwork.LoadLevel(paramInstance.raceSceneName);
                return;
            }
#endif
            SceneManager.LoadScene(paramInstance.raceSceneName);
        }
    }
}
