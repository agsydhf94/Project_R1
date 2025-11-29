using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;



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
        private int completedLaps;
        private bool hasFinished;

        [SerializeField] private StartFinishCrossing startFinishCrossing;
        [SerializeField] private LapTimer lapTimer;
        [SerializeField] private LapListHUD lapHUD;
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

        /// <summary>Freezing service to lock/unlock vehicles during countdown/finish.</summary>
        private IFreezeService freezer;

        private IRecordService recordService;

        /// <summary>HUD updater that renders telemetry onto UI.</summary>
        private HudUpdater hud;

        // Runtime state
        /// <summary>All vehicles in the scene (player + AI + late-join).</summary>
        private readonly List<GameObject> vehicles = new();

        /// <summary>Local player's CarController.</summary>
        public CarController localCar;

        /// <summary>Resolved checkpoint transforms.</summary>
        [SerializeField] private TrackWaypoints track;
        public Transform[] waypoints;
        [SerializeField] private TrackCheckpoints trackCP;
        [SerializeField] private Transform rankPivot;
        [SerializeField] private CheckpointPopupUI checkpointPopupUI;
        private SplitService splitService;

        /// <summary>Local race state.</summary>
        public RaceState localState = RaceState.PreRace;

        /// <summary>Running countdown coroutine handle.</summary>
        private Coroutine countdownCo;

        private readonly HashSet<GameObject> _attachedOnce = new();
        private static int _aiSeq = 1;

        [SerializeField] RecordsHUD recordsHUD;
        [SerializeField] private string localDisplayName = "Player";
        public string localUserId;
        private string localUserName;

        [SerializeField] private ResultHUD resultHUD;


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

        void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded_ClearSession;
        }

        /// <summary>
        /// Collects checkpoints, finds preloader, builds service implementations (local or Photon),
        /// wires HUD updater, and subscribes to state-change events.
        /// </summary>
        private void Awake()
        {
            waypoints = track.nodes.ToArray();

            if (!preloader) { preloader = FindObjectOfType<AddressablesPreloader>(); }

            localUserId   = System.Guid.NewGuid().ToString("N");
            localUserName = string.IsNullOrEmpty(localDisplayName) ? "Player" : localDisplayName;

#if PHOTON_UNITY_NETWORKING
            timeSrc = IsMultiplayer ? new PhotonTimeSource() : new LocalTimeSource();
            stateSync = IsMultiplayer ? new PhotonRoomStateSync() : new LocalStateSync();
            recordService = IsMultiplayer ? new RemoteRecordService() : new LocalRecordService();

             if (IsMultiplayer && PhotonNetwork.LocalPlayer != null)
            {
                var me = PhotonNetwork.LocalPlayer;
                if (!string.IsNullOrEmpty(me.UserId))   localUserId   = me.UserId;
                if (!string.IsNullOrEmpty(me.NickName)) localUserName = me.NickName;
                else                                     localUserName = $"P{me.ActorNumber}";
            }
#else
            timeSrc  = new LocalTimeSource();
            stateSync = new LocalStateSync();
            recordService = new LocalRecordService();
#endif
            freezer = new FreezeService();

            // PB 로드 — 반드시 userId 키로
            (recordService as LocalRecordService)?.LoadPersonalBest(localUserId, localUserName);

            // HUD 초기화(이 시점에 확정된 ID로)
            recordsHUD?.Init(DisplayName, localUserId);

            hud = new HudUpdater(
                rpmGauge, rpmText, speedText, gearText, nitroGauge, needle,
                rpmFillAtZero, rpmFillAtMax, rpmFillSmooth,
                needleAngleAtZero, needleAngleAtMax, needleSmooth
            );

            stateSync.OnStateChanged += OnStateChanged;

            Debug.Log($"[RSM] Awake. preloader={(preloader ? "OK" : "NULL")}, checkpoints={waypoints.Length}, mp={IsMultiplayer}");
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
            recordsHUD?.Bind(recordService);

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
            int myGridIndex = Mathf.Clamp(GetMyGridIndex(), 0, gridPoints.Length - 1);
            var player = SpawnPlayer(GetGridPointSafe(myGridIndex));
            if (player == null)
            {
                Debug.LogError("[RSM] Failed to spawn player. Check playerKey, PrefabPool/Addressables keys, and preloader cache.");
                yield break;
            }
            vehicles.Add(player);
            ConfigureIdentityAndGrid(player, myGridIndex, isAI:false, uid: localUserId, displayName: localUserName, isLocal:true);

            localCar = player.GetComponent<CarController>();

            if (localCar == null)
            {
                Debug.LogError("[RSM] CarController missing on player.");
                yield break;
            }
            AttachTrackers(player);
            BindTelemetryAndAudio(player);

            // HUD telemetry hookup
            var adapter = player.GetComponent<CarTelemetryAdapter>();
            if (adapter == null) { adapter = player.AddComponent<CarTelemetryAdapter>(); }
            adapter.Bind(localCar);

            // (4) Spawn AIs
            int aiStartIndex = Mathf.Clamp(GetPlayerCount(), 1, gridPoints.Length - 1);
            var more = SpawnAIs(aiStartIndex);
            foreach (var go in more)
            {
                vehicles.Add(go);
                AttachTrackers(go);
            }

            // Multiplayer reinforcement: auto-discover vehicles in-scene (late joiners)
            StartCoroutine(Co_AutoDiscoverVehicles());

            
            // HUD 슬롯 미리 생성 (고정 랩 수: totalLaps)
            if (lapHUD != null && lapTimer != null)
                lapHUD.Init(lapTimer, totalLaps, recordService);

            // PB 로드 — 반드시 userId + userName 기준
            (recordService as LocalRecordService)?.LoadPersonalBest(localUserId, localUserName);

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

            if (lapTimer != null)
            lapTimer.UpdateTimer(Time.deltaTime);

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

        void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded_ClearSession;
        }

        /// <summary>Unsubscribes from events on teardown.</summary>
        private void OnDestroy()
        {
            stateSync.OnStateChanged -= OnStateChanged;

            recordsHUD?.Unbind();
        }

        private void OnSceneUnloaded_ClearSession(Scene _)
        {
            // 씬 떠날 때 세션 데이터 전부 비우기
            if (splitService != null)
            {
                splitService.ClearAllInMemory();
                splitService = null;
            }

            // 필요하면 다른 런타임 상태도 정리
            // ranking = null; vehicles.Clear(); 등
        }


        private void OnStateChanged(RaceState raceState)
        {
            localState = raceState;

            if (countdownCo != null)
            {
                StopCoroutine(countdownCo);
                countdownCo = null;
            }

            switch (raceState)
            {
                case RaceState.Countdown:
                    // 타이머/HUD는 여기서는 아직 시작하지 않습니다.
                    countdownCo = StartCoroutine(Co_Countdown());
                    // 레이스 시작 전에 혹시 이전 기록이 남아있다면 정리(선택)
                    // lapTimer?.StopRace();
                    
                    break;

                case RaceState.Racing:
                    completedLaps = 0;
                    hasFinished = false;

                    

                    if (countdownText) countdownText.text = string.Empty;
                    freezer.UnfreezeAll(vehicles);

                    // Lap/Record 시작
                    recordService?.StartRun(localUserId, localUserName);
                    splitService?.ResetCurrentLap();
                    foreach (var v in vehicles)
                    {
                        var tr = v.GetComponent<LapCheckpointTracker>();
                        if (tr) tr.OnRaceStart();
                    }
                    startFinishCrossing.OnRaceStart();
                    lapTimer?.StartRace();
                    break;

                case RaceState.Finished:
                    if (countdownText) countdownText.text = "FINISH";
                    freezer.FreezeAll(vehicles);

                    // Lap/Record 종료
                    foreach (var v in vehicles)
                    {
                        var tr = v.GetComponent<LapCheckpointTracker>();
                        if (tr) tr.OnRaceFinish();
                    }
                    lapTimer?.StopRace();
                    recordService?.FinishRun();
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
                            // AI 이름/그리드/태그 설정 (모두에게 동일하게 보이려면 AI는 프리팹에 PhotonView가 있어야 함)
                            ConfigureIdentityAndGrid(goNet, gridIdx, isAI:true, uid:null, displayName:null, isLocal:false);
                            SetupAI(goNet, track); 
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
                    ConfigureIdentityAndGrid(go, gridIdx, isAI:true, uid:null, displayName:null, isLocal:false);
                    SetupAI(go, track); 
                    list.Add(go);
                }
            }
            return list;
        }

        private void SetupAI(GameObject go, TrackWaypoints tw)
        {
            if (!go || tw == null) return;

            // AiInputProvider 확보 + 웨이포인트 바인딩
            var aip = go.GetComponent<AiInputProvider>();
            if (aip == null) aip = go.AddComponent<AiInputProvider>();
            aip.Bind(tw); // ← 이전에 추가했던 메서드

            // AiController에도 입력 소스 주입
            var aic = go.GetComponent<AIController>() ?? go.AddComponent<AIController>();
            aic.Bind(aip);  // ← 핵심 한 줄

            // 기본 주행 파라미터 (필요 시 보정)
            aip.acceleration   = Mathf.Clamp(aip.acceleration <= 0 ? 0.7f : aip.acceleration, 0.1f, 1.0f);
            aip.sterrForce     = (aip.sterrForce <= 0) ? 1.0f : aip.sterrForce;
            aip.distanceOffset = (aip.distanceOffset <= 0) ? 5 : aip.distanceOffset;

            if (go.tag != "AI") go.tag = "AI";
        }

        private void ConfigureIdentityAndGrid(GameObject go, int gridIndex, bool isAI, string uid, string displayName, bool isLocal)
        {
            if (!go) return;

            // GridSlot
            var slot = go.GetComponent<GridSlot>() ?? go.AddComponent<GridSlot>();
            slot.index = gridIndex;

            // PlayerIdentity
            var pid = go.GetComponent<PlayerIdentity>() ?? go.AddComponent<PlayerIdentity>();
            if (isAI)
            {
                // AI 이름 규칙
                string aiUid = string.IsNullOrEmpty(uid) ? $"AI_{_aiSeq}" : uid;
                string aiNm  = string.IsNullOrEmpty(displayName) ? $"AI Player #{_aiSeq}" : displayName;
                pid.SetProfile(aiUid, aiNm, false);
                _aiSeq++;
                if (go.tag != "AI") go.tag = "AI";
            }
            else
            {
                // 사람(로컬/원격)
                string finalUid = string.IsNullOrEmpty(uid) ? (isLocal ? "LOCAL" : go.name) : uid;
                string finalNm  = string.IsNullOrEmpty(displayName) ? go.name : displayName;
                pid.SetProfile(finalUid, finalNm, isLocal);
                if (go.tag != "Player") go.tag = "Player";
            }
        }

        // 룸 내 내 그리드 인덱스(싱글: 0, 멀티: ActorNumber-1 기준)
        private int GetMyGridIndex()
        {
        #if PHOTON_UNITY_NETWORKING
            if (IsMultiplayer && PhotonNetwork.LocalPlayer != null)
                return Mathf.Clamp(PhotonNetwork.LocalPlayer.ActorNumber - 1, 0, gridPoints.Length - 1);
        #endif
            return 0; // 싱글
        }

        // 현재 플레이어 수(싱글=1, 멀티=룸 인원)
        private int GetPlayerCount()
        {
        #if PHOTON_UNITY_NETWORKING
            if (IsMultiplayer) return PhotonNetwork.CurrentRoom?.PlayerCount ?? 1;
        #endif
            return 1;
        }

        private void BindTelemetryAndAudio(GameObject go)
        {
            if (!go) return;

            var car = go.GetComponent<CarController>();
            if (!car) return;

            // 텔레메트리 어댑터 준비/바인딩
            var adapter = go.GetComponent<CarTelemetryAdapter>();
            if (adapter == null)
            {
                adapter = go.AddComponent<CarTelemetryAdapter>();
            }
            adapter.Bind(car); // CarTelemetryAdapter에 만든 Bind/SetSource 메서드 호출

            // 엔진 오디오가 있으면 텔레메트리 주입
            var audio = go.GetComponent<CarEngineAudio>();
            if (audio != null)
            {
                audio.Bind(adapter); // ICarTelemetry로 바인딩
            }
        }

        private string DisplayName(string userId)
        {
#if PHOTON_UNITY_NETWORKING
            if (IsMultiplayer)
            {
                // 빠른 탐색(플레이어 수가 적다면 OK)
                foreach (var p in PhotonNetwork.PlayerList)
                    if (p.UserId == userId)
                        return string.IsNullOrEmpty(p.NickName) ? $"P{p.ActorNumber}" : p.NickName;
            }
#endif
            // 싱글 혹은 매칭 실패 → 로컬이면 내 이름, 아니면 ID 그대로
            return (userId == localUserId) ? localUserName : userId;
        }


        /// <summary>Logs preloaded vehicle keys for verification.</summary>
        private void DumpPreloaderKeys()
        {
            if (preloader == null) return;
            var keys = string.Join(", ", preloader.vehiclePrefabs.Keys);
            Debug.Log($"[RSM] Preloaded vehicle keys: [{keys}]");
        }

        private void StartRace(string userId, string playerName)
        {
            recordService.StartRun(userId, playerName);
        }

        // 예: FinishLineTrigger에서 플레이어가 통과했을 때
        public void OnLapCompleted()
        {
            float curLap = lapTimer.CurrentLapTime;

            // 1) 스플릿 기록
            splitService?.RecordFinish(curLap);

            // 2) 현재 랩 스플릿 묶음 가져오기
            var splits = splitService?.GetCurrentLapSplits();

            // 3) RecordService에 랩 전달 (Lap PB 갱신됨)
            recordService?.CompleteLap(curLap, splits);

            // 4) LapTimer 내부 처리
            lapTimer.CompleteLap();

            // 5) SplitService에서 PB 갱신 (스플릿 포함)
            splitService?.MaybeUpdatePBOnLapFinish(curLap);

            // 6) 델타 HUD 표시
            var deltaAtFinish = splitService?.GetDeltaVsPB(0, curLap) ?? float.NaN;
            checkpointPopupUI?.Show(curLap, deltaAtFinish);

            // 7) 랩 수 체크
            completedLaps++;
            if (completedLaps >= totalLaps)
            {
                FinishRun();
            }

            // 8) 다음 랩 준비
            splitService?.ResetCurrentLap();

            if (splitService != null && splitService.pbLap < float.PositiveInfinity)
            {
                float deltaVsPb = curLap - splitService.pbLap;

                // LapListHUD 같은 UI 쪽으로 전달
                lapHUD?.SetDeltaForLap(completedLaps, deltaVsPb);
            }

            (recordService as LocalRecordService)
                ?.UpdatePersonalBestLap(curLap, localUserId, localUserName);

            Debug.LogError("[RSM] OnLapCompleted CALLED\n" +
                   new System.Diagnostics.StackTrace(1, true));
        }

        private void FinishRun()
        {
            if (hasFinished) return;
            hasFinished = true;

            recordService?.FinishRun();
            stateSync.SetState(RaceState.Finished);

            // 레이스 종료 시 후처리를 한 곳에서 호출
            OnRaceFinished();
        }

        private void OnRaceFinished()
        {
            // 결과 패널 표시 & 데이터 바인딩
            resultHUD.gameObject.SetActive(true);
            resultHUD.Init(recordService);
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


        /*
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
        */


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


        private void AttachTrackers(GameObject go)
        {
            if (!go) return;

            // 실제 체크포인트 개수(트리거 개수 기준)
            int cpCount = trackCP ? trackCP.Count : FindObjectsOfType<CheckpointTrigger>(true).Length;

            // LapTracker
            var lap = go.GetComponent<LapTracker>() ?? go.AddComponent<LapTracker>();
            lap.Init(cpCount);                  // ← waypoints.Length 말고 cpCount
            lap.targetTotalLaps = totalLaps;

            // LapCheckpointTracker
            var tracker = go.GetComponent<LapCheckpointTracker>() ?? go.AddComponent<LapCheckpointTracker>();
            tracker.rsm = this;

            // “모든 차량”에 동일 초기화(그동안 로컬만 하던 게 꼬임의 원인)
            tracker.ResetTracker(cpCount, 3f);

            // 로컬만 스플릿/이벤트 연결
            if (go == localCar?.gameObject)
            {
                if (splitService == null) splitService = new SplitService(cpCount, localUserId);
                else splitService.Reconfigure(cpCount);
                splitService.ResetCurrentLap();

                tracker.OnCheckpointPassed -= OnCheckpointPassed_Local;
                tracker.OnCheckpointPassed += OnCheckpointPassed_Local;

                tracker.OnCheckpointPassed += (idx) =>
                {
                    startFinishCrossing?.OnMidCheckpointPassed(localCar.gameObject, idx);
                };
            }

            // ProgressTracker(부트스트래퍼 주입 필드 유지)
            var prog = go.GetComponent<ProgressTracker>() ?? go.AddComponent<ProgressTracker>();
            prog.checkpoints = waypoints;
            prog.lap = lap;

            if (rankPivot != null)
            {
                prog.pivot = rankPivot;
            }
            else if (waypoints != null && waypoints.Length > 0)
            {
                int quarter = Mathf.FloorToInt(waypoints.Length * 0.25f);
                prog.pivot = waypoints[quarter];
            }
            else
            {
                prog.pivot = null;
            }
            
        }

        private void OnCheckpointPassed_Local(int idx)
        {
            // S/F(0)은 보통 팝업 제외 — 중간 체크포인트에만 표시
            if (idx <= 0) return;

            float cur = lapTimer?.CurrentLapTime ?? 0f;

            // 스플릿 기록/델타 계산
            splitService?.RecordSplit(idx, cur);
            var delta = splitService?.GetDeltaVsPB(idx, cur) ?? float.NaN;

            // 팝업 띄우기
            checkpointPopupUI?.Show(cur, delta);
            startFinishCrossing?.OnMidCheckpointPassed(localCar.gameObject, idx);
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
