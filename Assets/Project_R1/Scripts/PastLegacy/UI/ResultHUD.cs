using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace R1
{
    public class ResultHUD : MonoBehaviour
    {
        [SerializeField] private RectTransform leaderboardContainer;
        [SerializeField] private TextMeshProUGUI entryPrefab; // 비활성 템플릿

        private IRecordService recordService;
        private readonly List<TextMeshProUGUI> _entries = new();

        public void Init(IRecordService service)
        {
            recordService = service;
            recordService.OnLeaderboardUpdated += RefreshLeaderboard;

            // 초기 갱신
            if (recordService.Leaderboard != null)
                RefreshLeaderboard(recordService.Leaderboard);
        }

        private void OnDestroy()
        {
            if (recordService != null)
                recordService.OnLeaderboardUpdated -= RefreshLeaderboard;
        }

        private void RefreshLeaderboard(IReadOnlyList<RaceRecord> records)
        {
            // 기존 엔트리 정리
            foreach (var e in _entries) Destroy(e.gameObject);
            _entries.Clear();

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                var item = Instantiate(entryPrefab, leaderboardContainer);
                item.gameObject.SetActive(true);
                item.text = $"{i+1}. {r.userId} - BestLap {Format(r.bestLap)} / Total {Format(r.bestTotal)}";
                _entries.Add(item);
            }
        }

        private string Format(float t)
        {
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);
            int c = Mathf.FloorToInt((t - Mathf.Floor(t)) * 100f);
            return $"{m:00}:{s:00}.{c:00}";
        }
    }
}
