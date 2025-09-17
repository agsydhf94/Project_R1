using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace R1
{
    /// <summary>
    /// Orchestrates a race session for single-player and Photon multiplayer:
    /// preloads assets, spawns player and AI, runs countdown/state sync,
    /// hooks up HUD via telemetry, tracks progress/ranking, and freezes/unfreezes vehicles.
    /// </summary>
    public class RaceSessionManager : MonoBehaviour
#if PHOTON_UNITY_NETWORKING
, IPunObservable
#endif
    {

        [Header("Preloader Ref")]
        /// <summary>Reference to the scene's Addressables preloader.</summary>
        public AddressablesPreloader preloader;

        /// <summary>Addressables keys for the player and AI vehicles.</summary>
        [Header("Keys (Addressables name)")]
        public string playerKey = "PlayerCar";
        public string[] aiKeys;

        /// <summary>Grid slots where cars will spawn.</summary>
        [Header("Grid Points")]
        public Transform[] gridPoints;

        /// <summary>Root transform whose children are ordered checkpoints (0 = Start/Finish).</summary>
        [Header("Checkpoints (0 = Start/Finish)")]
        public Transform checkpointsRoot;

        /// <summary>Race rules: number of laps and countdown duration in seconds.</summary>
        [Header("Rules")]
        public int totalLaps = 3;
        public float countdownSeconds = 3f;

        /// <summary>HUD references for countdown text, speed/RPM, position, gear and gauges.</summary>
        [Header("HUD")]
        public TMP_Text countdownText;
        public TMP_Text speedText;
        public TMP_Text rpmText;
        public TMP_Text positionText;
        public TMP_Text gearText;
        public Image rpmGauge;
        public Image nitroGauge;
        public Transform needle;
        [Range(0f, 1f)] public float rpmFillAtZero = 0.10f;
        [Range(0f, 1f)] public float rpmFillAtMax = 0.90f;
        public float rpmFillSmooth = 0.06f;
        public float needleAngleAtZero = 143.5f;
        public float needleAngleAtMax = -143.5f;
        public float needleSmooth = 0.06f;

        // Services
        /// <summary>Abstract time source (local vs Photon network time).</summary>
        private ITimeSource timeSrc;

        /// <summary>Abstract race-state synchronizer (local vs Photon room properties).</summary>
        private IStateSync stateSync;

        /// <summary>Ranking service that computes order/positions.</summary>
        private IRankingService ranking;

        /// <summary>Freezing service to lock/unlock vehicles during countdown/finish.</summary>
        private IFreezeService freezer;

        /// <summary>HUD updater that renders telemetry onto UI.</summary>
        private HudUpdater hud;

        // Runtime state
        /// <summary>All vehicles in the scene (player + AI + late-join).</summary>
        private readonly List<GameObject> vehicles = new();

        /// <summary>Local player's CarController.</summary>
        private CarController localCar;

        /// <summary>Resolved checkpoint transforms.</summary>
        private Transform[] checkpoints;

        /// <summary>Local race state.</summary>
        private RaceState localState = RaceState.PreRace;

        /// <summary>Running countdown coroutine handle.</summary>
        private Coroutine countdownCo;


        /// <summary>Returns true when in a Photon room.</summary>
        private bool IsMultiplayer
        {
            get
            {
#if PHOTON_UNITY_NETWORKING
                return PhotonNetwork.InRoom;
#else
                return false;
#endif
            }
        }


        /// <summary>
        /// Collects checkpoints, finds preloader, builds service implementations (local or Photon),
        /// wires HUD updater, and subscribes to state-change events.
        /// </summary>
        private void Awake()
        {
            checkpoints = CollectChildren(checkpointsRoot);
            if (checkpoints.Length == 0)
            {
                Debug.LogWarning("[RaceSessionManager] No checkpoints found under checkpointsRoot.");
            }

            if (!preloader) { preloader = FindObjectOfType<AddressablesPreloader>(); }

#if PHOTON_UNITY_NETWORKING
            timeSrc = IsMultiplayer ? new PhotonTimeSource() : new LocalTimeSource();
            stateSync = IsMultiplayer ? new PhotonRoomStateSync() : new LocalStateSync();
#else
            timeSrc  = new LocalTimeSource();
            stateSync = new LocalStateSync();
#endif
            freezer = new FreezeService();

            hud = new HudUpdater(
                rpmGauge, rpmText, speedText, gearText, nitroGauge, needle,
                rpmFillAtZero, rpmFillAtMax, rpmFillSmooth,
                needleAngleAtZero, needleAngleAtMax, needleSmooth
            );

            stateSync.OnStateChanged += OnStateChanged;

            Debug.Log($"[RSM] Awake. preloader={(preloader ? "OK" : "NULL")}, checkpoints={checkpoints.Length}, mp={IsMultiplayer}");
        }


        /// <summary>
        /// Session bootstrap coroutine:
        /// applies menu parameters, ensures preloader ready, spawns player/AIs (local or Photon),
        /// attaches trackers/ranking, schedules countdown, and starts ranking tick.
        /// </summary>
        private IEnumerator Start()
        {
#if PHOTON_UNITY_NETWORKING
            if (IsMultiplayer)
            {
                PhotonNetwork.AutomaticallySyncScene = true;
            }
#endif

            // (0) Apply menu parameters
            var paramInstance = GameLaunchParams.Instance;
            if (paramInstance != null)
            {
                paramInstance.ValidateAndClamp(false);
                playerKey = paramInstance.playerKey;
                aiKeys = paramInstance.GetSelectedAiKeys();
                totalLaps = Mathf.Max(1, paramInstance.totalLaps);
                countdownSeconds = Mathf.Max(0.5f, paramInstance.countdownSeconds);
            }

#if PHOTON_UNITY_NETWORKING
            // (0-1) Apply room custom properties (Hashtable; use ContainsKey and casting)
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
            {
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                if (props != null)
                {
                    if (props.ContainsKey("laps"))
                    {
                        object obj = props["laps"];
                        int lapsVal = (obj is int i) ? i : (obj is byte b ? b : (obj is string s && int.TryParse(s, out var pv) ? pv : totalLaps));
                        totalLaps = Mathf.Max(1, lapsVal);
                    }
                    if (props.ContainsKey("aiCount") && paramInstance != null)
                    {
                        object obj = props["aiCount"];
                        int aiVal = (obj is int i) ? i : (obj is byte b ? b : (obj is string s && int.TryParse(s, out var pv) ? pv : paramInstance.aiCount));
                        paramInstance.aiCount = aiVal;
                        aiKeys = paramInstance.GetSelectedAiKeys();
                    }
                }
            }
#endif

            // (1) Ensure preloader is ready; if not started, start it here
            if (preloader == null) { preloader = FindObjectOfType<AddressablesPreloader>(); }
            if (preloader == null)
            {
                Debug.LogError("[RSM] AddressablesPreloader not found in scene.");
                yield break;
            }
            if (!preloader.IsLoaded)
            {
                Debug.Log("[RSM] Preloader not loaded. Starting preload now...");
                yield return StartCoroutine(preloader.PreloadAll()); 
            }
            DumpPreloaderKeys();

            // (2) Guard required references
            if (gridPoints == null || gridPoints.Length == 0)
            {
                Debug.LogError("[RSM] gridPoints is empty."); yield break;
            }
            if (checkpointsRoot == null)
            {
                Debug.LogError("[RSM] checkpointsRoot is not set."); yield break;
            }

            Debug.Log($"[RSM] Params: playerKey='{playerKey}', aiKeys[{(aiKeys?.Length ?? 0)}], laps={totalLaps}, countdown={countdownSeconds}, mp={IsMultiplayer}");

            // (3) Spawn player
            var player = SpawnPlayer(GetGridPointSafe(0));
            if (player == null)
            {
                Debug.LogError("[RSM] Failed to spawn player. Check playerKey, PrefabPool/Addressables keys, and preloader cache.");
                yield break;
            }
            vehicles.Add(player);

            localCar = player.GetComponent<CarController>();
            if (localCar == null)
            {
                Debug.LogError("[RSM] CarController missing on player.");
                yield break;
            }

            AttachTrackers(player);

            // HUD telemetry hookup
            var adapter = player.GetComponent<CarTelemetryAdapter>();
            if (adapter == null) { adapter = player.AddComponent<CarTelemetryAdapter>(); }
            adapter.Bind(localCar);

            // (4) Spawn AIs
            var more = SpawnAIs(1);
            foreach (var go in more)
            {
                vehicles.Add(go);
                AttachTrackers(go);
            }

            // Multiplayer reinforcement: auto-discover vehicles in-scene (late joiners)
            StartCoroutine(Co_AutoDiscoverVehicles());

            // (5) Ranking
            ranking = new RankingService(vehicles);
            ranking.OnRankChanged += OnRankChanged;

            // (6) Start countdown
            if (IsMultiplayer)
            {
#if PHOTON_UNITY_NETWORKING
                if (PhotonNetwork.IsMasterClient)
                {
                    stateSync.ScheduleCountdown(timeSrc.Now + countdownSeconds);
                }
#endif
            }
            else
            {
                stateSync.ScheduleCountdown(timeSrc.Now + countdownSeconds);
            }

            // (7) Ranking tick loop
            StartCoroutine(Co_RankingTick());
            yield break;
        }


        /// <summary>
        /// During the race, updates HUD each frame from telemetry (adapter if present, otherwise controller).
        /// </summary>
        private void Update()
        {
            if (!localCar || localState != RaceState.Racing)
            {
                return;
            }

            var telemetry = localCar.GetComponent<CarTelemetryAdapter>();
            if (telemetry != null)
            {
                hud.UpdateFrom(telemetry, localCar.maxRPM);
            }
            else
            {
                hud.UpdateFrom(localCar);
            }
        }


        /// <summary>Unsubscribes from events on teardown.</summary>
        private void OnDestroy()
        {
            stateSync.OnStateChanged -= OnStateChanged;
            if (ranking != null)
            {
                ranking.OnRankChanged -= OnRankChanged;
            }
        }


        /// <summary>
        /// Handles transitions between PreRace/Countdown/Racing/Finished,
        /// starting/stopping countdown coroutine and freezing/unfreezing vehicles.
        /// </summary>
        private void OnStateChanged(RaceState raceState)
        {
            localState = raceState;

            if (countdownCo != null)
            {
                StopCoroutine(countdownCo);
            }

            switch (raceState)
            {
                case RaceState.Countdown:
                    countdownCo = StartCoroutine(Co_Countdown());
                    break;

                case RaceState.Racing:
                    if (countdownText)
                    {
                        countdownText.text = string.Empty;
                    }
                    freezer.UnfreezeAll(vehicles);
                    break;

                case RaceState.Finished:
                    if (countdownText)
                    {
                        countdownText.text = "FINISH";
                    }
                    freezer.FreezeAll(vehicles);
                    break;
            }
        }


        /// <summary>
        /// Spawns the local player (Photon or local instantiate) at the given grid slot.
        /// </summary>
        private GameObject SpawnPlayer(Transform slot)
        {
#if PHOTON_UNITY_NETWORKING
            if (IsMultiplayer)
            {
                var goNet = PhotonNetwork.Instantiate(playerKey, slot.position, slot.rotation);

                // If prefab doesn't carry the tag, enforce it.
                if (goNet != null && goNet.tag != "Player") goNet.tag = "Player";
                return goNet;
            }
#endif
            if (!preloader || !preloader.vehiclePrefabs.TryGetValue(playerKey, out var prefab) || !prefab)
            {
                Debug.LogError($"[RaceSessionManager] Player prefab not found for key: {playerKey}");
                return null;
            }
            var go = Instantiate(prefab, slot.position, slot.rotation);
            if (go.tag != "Player") go.tag = "Player";
            return go;
        }


        /// <summary>
        /// Spawns AI vehicles (Photon by master client or local instantiate) starting from a grid index.
        /// </summary>
        private List<GameObject> SpawnAIs(int startIndex)
        {
            var list = new List<GameObject>();
            if (aiKeys == null || aiKeys.Length == 0)
            {
                return list;
            }

            for (int i = 0; i < aiKeys.Length; i++)
            {
                int gridIdx = startIndex + i;
                if (gridPoints == null || gridPoints.Length == 0 || gridIdx >= gridPoints.Length)
                {
                    Debug.LogWarning("[RaceSessionManager] Not enough grid points for all AIs.");
                    break;
                }

                var gp = gridPoints[gridIdx];

#if PHOTON_UNITY_NETWORKING
                if (IsMultiplayer)
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        var goNet = PhotonNetwork.Instantiate(aiKeys[i], gp.position, gp.rotation);
                        if (goNet != null)
                        {
                            goNet.tag = "AI";
                            list.Add(goNet);
                        }
                        else
                        {
                            Debug.LogError($"[RaceSessionManager] Failed to instantiate AI (key: {aiKeys[i]}).");
                        }
                    }
                }
                else
#endif
                {
                    if (!preloader || !preloader.vehiclePrefabs.TryGetValue(aiKeys[i], out var prefab) || !prefab)
                    {
                        Debug.LogError($"[RaceSessionManager] AI prefab not found for key: {aiKeys[i]}");
                        continue;
                    }
                    var go = Instantiate(prefab, gp.position, gp.rotation);
                    go.tag = "AI";
                    list.Add(go);
                }
            }
            return list;
        }


        /// <summary>Logs preloaded vehicle keys for verification.</summary>
        private void DumpPreloaderKeys()
        {
            if (preloader == null) return;
            var keys = string.Join(", ", preloader.vehiclePrefabs.Keys);
            Debug.Log($"[RSM] Preloaded vehicle keys: [{keys}]");
        }


        // ------------------------
        // Coroutines
        // ------------------------


        /// <summary>
        /// Drives the countdown UI and freezes vehicles until the race starts.
        /// Transitions to the Racing state when the scheduled start time is reached.
        /// </summary>
        private IEnumerator Co_Countdown()
        {
            while (localState == RaceState.Countdown)
            {
                double t = stateSync.StartTime - timeSrc.Now;
                if (t <= 0)
                {
                    stateSync.SetState(RaceState.Racing);
                    yield break;
                }

                if (countdownText)
                {
                    // Show remaining seconds; display "GO!" in the last second.
                    countdownText.text = (t > 1.0) ? Mathf.CeilToInt((float)t).ToString() : "GO!";
                }

                freezer.FreezeAll(vehicles);
                yield return null;
            }
        }


        /// <summary>
        /// Periodic ranking update loop. Ends the race when all participants have finished.
        /// </summary>
        private IEnumerator Co_RankingTick()
        {
            var wait = new WaitForSeconds(0.2f);
            while (true)
            {
                if (localState == RaceState.Racing && ranking != null)
                {
                    ranking.Tick();
                }

                if (AllFinished())
                {
                    stateSync.SetState(RaceState.Finished);
                }

                yield return wait;
            }
        }


        /// <summary>
        /// In multiplayer, automatically discovers other participants / late join vehicles
        /// for a short time and attaches trackers.
        /// </summary>
        private IEnumerator Co_AutoDiscoverVehicles(float seconds = 5f)
        {
            float stopAt = Time.time + seconds;
            var wait = new WaitForSeconds(0.5f);

            while (Time.time < stopAt)
            {
                var found = FindObjectsOfType<CarController>();
                foreach (var cc in found)
                {
                    var go = cc.gameObject;
                    if (!vehicles.Contains(go))
                    {
                        vehicles.Add(go);
                        AttachTrackers(go);
                    }
                }
                yield return wait;
            }
        }

        // ------------------------
        // Ranking & Helpers
        // ------------------------


        /// <summary>
        /// Updates the position HUD when ranking changes.
        /// </summary>
        private void OnRankChanged(IReadOnlyList<RankEntry> list)
        {
            if (!localCar || positionText == null)
            {
                return;
            }

            int rank = 1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].go == localCar.gameObject)
                {
                    rank = i + 1;
                    break;
                }
            }
            positionText.text = $"{rank}/{list.Count}";
        }


        /// <summary>
        /// Returns true when all tracked vehicles have finished (requires at least one vehicle).
        /// </summary>
        private bool AllFinished()
        {
            foreach (var go in vehicles)
            {
                var lap = go ? go.GetComponent<LapTracker>() : null;
                if (lap && !lap.hasFinished)
                {
                    return false;
                }
            }
            return vehicles.Count > 0; // Require at least one vehicle before finishing
        }


        /// <summary>
        /// Ensures lap/progress trackers exist and configures them for this race.
        /// </summary>
        private void AttachTrackers(GameObject go)
        {
            if (!go)
            {
                return;
            }

            var lap = go.GetComponent<LapTracker>() ?? go.AddComponent<LapTracker>();
            lap.Init(checkpoints.Length);
            lap.targetTotalLaps = totalLaps;

            var prog = go.GetComponent<ProgressTracker>() ?? go.AddComponent<ProgressTracker>();
            prog.checkpoints = checkpoints;
            prog.lap = lap;
        }


        /// <summary>
        /// Collects direct children of a root transform into an array.
        /// </summary>
        private Transform[] CollectChildren(Transform root)
        {
            if (!root)
            {
                return System.Array.Empty<Transform>();
            }

            var tempList = new List<Transform>();
            foreach (Transform t in root)
            {
                tempList.Add(t);
            }
            return tempList.ToArray();
        }


        /// <summary>
        /// Returns a valid grid slot transform; falls back to a temporary origin object if missing.
        /// </summary>
        private Transform GetGridPointSafe(int index)
        {
            if (gridPoints != null && gridPoints.Length > 0)
            {
                return gridPoints[Mathf.Clamp(index, 0, gridPoints.Length - 1)];
            }

            var go = new GameObject("SpawnFallback");
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            return go.transform;
        }

#if PHOTON_UNITY_NETWORKING

        /// <summary>
        /// Photon serialization hook (reserved for future state/time correction data).
        /// </summary>
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            // Add race state/time correction serialization if needed.
        }
#endif
    }
}
