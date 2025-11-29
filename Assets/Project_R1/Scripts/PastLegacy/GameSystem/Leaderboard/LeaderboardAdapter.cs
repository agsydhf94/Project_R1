using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace R1
{
    public class LeaderboardAdapter : MonoBehaviour
    {
        private static LeaderboardAdapter _live; // 단 하나만 허용

        [Header("Refs")]
        [SerializeField] private LeaderboardUI leaderboardUI;
        [SerializeField] private RankManager rankManager;

        [Header("Local Player")]
        [SerializeField] private string localUserId = "LOCAL";

        public event Action<IReadOnlyList<RaceRecord>> OnLeaderboardUpdated;

        void Awake()
        {
            if (_live != null && _live != this)
            {
                Debug.LogWarning($"[LB-Adapter] Duplicate adapter '{name}' -> destroying this one.");
                Destroy(gameObject);
                return;
            }
            _live = this;

            if (!leaderboardUI) leaderboardUI = FindObjectOfType<LeaderboardUI>(true);
            if (!rankManager)   rankManager   = FindObjectOfType<RankManager>(true);
        }

        void OnDestroy()
        {
            if (_live == this) _live = null;
        }

        void OnEnable()
        {
            if (rankManager != null)
                rankManager.OnRanksUpdated += OnRanksUpdated;
        }

        void OnDisable()
        {
            if (rankManager != null)
                rankManager.OnRanksUpdated -= OnRanksUpdated;
        }

        private void OnRanksUpdated(IReadOnlyList<ProgressTracker> cars,
                                    IReadOnlyDictionary<ProgressTracker, int> ranks)
        {
            if (!leaderboardUI || leaderboardUI.Capacity <= 0) return; // UI가 아직 풀을 못 만들었으면 무시
            UpdateLeaderboard(cars, ranks);
        }

        private void UpdateLeaderboard(IReadOnlyList<ProgressTracker> cars,
                                       IReadOnlyDictionary<ProgressTracker, int> ranks)
        {
            if (!leaderboardUI || cars == null || ranks == null) return;

            var recMap = new Dictionary<string, RaceRecord>(cars.Count);

            foreach (var prog in cars)
            {
                if (!prog) continue;

                var id  = prog.GetComponent<PlayerIdentity>();
                var lap = prog.GetComponent<LapTimer>();

                string uid   = prog.gameObject.GetInstanceID().ToString(); // 고정 UID
                string pname = id ? (string.IsNullOrEmpty(id.DisplayName) ? prog.name : id.DisplayName) : prog.name;

                var rec = new RaceRecord(uid, pname)
                {
                    bestTotal = (lap != null) ? lap.TotalTime   : prog.dp,
                    bestLap   = (lap != null) ? lap.FastestLap  : float.PositiveInfinity,
                    lapTimes  = (lap != null && lap.LapTimes != null) ? new List<float>(lap.LapTimes) : null
                };

                recMap[uid] = rec;
            }

            var records = new List<RaceRecord>(recMap.Values);

            // 랭크 맵 기준 정렬
            records.Sort((a, b) =>
            {
                var pa = FindByUid(cars, a.userId);
                var pb = FindByUid(cars, b.userId);
                int ra = (pa != null && ranks.ContainsKey(pa)) ? ranks[pa] : int.MaxValue;
                int rb = (pb != null && ranks.ContainsKey(pb)) ? ranks[pb] : int.MaxValue;
                return ra.CompareTo(rb);
            });

            leaderboardUI.Set(records, _ => null, FormatTime, localUserId);
            OnLeaderboardUpdated?.Invoke(records);
        }

        private ProgressTracker FindByUid(IReadOnlyList<ProgressTracker> list, string uid)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p && p.gameObject.GetInstanceID().ToString() == uid)
                    return p;
            }
            return null;
        }

        private string FormatTime(float t)
        {
            if (t <= 0f || float.IsInfinity(t)) return "";
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);
            int c = Mathf.FloorToInt((t - Mathf.Floor(t)) * 1000f);
            return $"{m:0}:{s:00}.{c:000}";
        }
    }
}
