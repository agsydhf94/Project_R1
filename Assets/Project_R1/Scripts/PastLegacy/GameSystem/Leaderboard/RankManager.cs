using System;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class RankManager : MonoBehaviour
    {
        public event Action<IReadOnlyList<ProgressTracker>, IReadOnlyDictionary<ProgressTracker, int>> OnRanksUpdated;

        private readonly Dictionary<string, ProgressTracker> _byKey = new();
        private readonly List<ProgressTracker> _cars = new();
        private readonly Dictionary<ProgressTracker, int> _rankMap = new();

        // 마지막으로 브로드캐스트했던 “순서 지문”을 기억
        private string _lastOrderFingerprint = "";

        public IReadOnlyList<ProgressTracker> Cars => _cars;
        public IReadOnlyDictionary<ProgressTracker, int> Ranks => _rankMap;

        private static string KeyOf(ProgressTracker p)
            => p ? p.gameObject.GetInstanceID().ToString() : "";

        public void Register(ProgressTracker p)
        {
            if (!p) return;
            _byKey[KeyOf(p)] = p;
        }

        public void Unregister(ProgressTracker p)
        {
            if (!p) return;
            string key = KeyOf(p);
            if (_byKey.TryGetValue(key, out var cur) && cur == p)
                _byKey.Remove(key);
        }

        private void Update()
        {
            UpdateRanks();

            // 현재 순서를 지문(fingerprint)으로 만들어 비교
            string fp = BuildOrderFingerprint(_cars);
            if (!string.Equals(fp, _lastOrderFingerprint))
            {
                _lastOrderFingerprint = fp;
                OnRanksUpdated?.Invoke(_cars, _rankMap);   // ← “순서가 바뀐 경우에만” 갱신
            }
        }

        private void UpdateRanks()
        {
            _cars.Clear();
            _rankMap.Clear();

            var seen = new HashSet<GameObject>();
            foreach (var kv in _byKey)
            {
                var p = kv.Value;
                if (!p) continue;
                if (!seen.Add(p.gameObject)) continue; // 동일 GO 중복 제거
                _cars.Add(p);
            }

            // dp 큰 순
            _cars.Sort((a, b) => b.dp.CompareTo(a.dp));

            for (int i = 0; i < _cars.Count; i++)
                _rankMap[_cars[i]] = i + 1;
        }

        // 참가자 “현재 순서”를 문자열로 고정(InstanceID 나열)
        private static string BuildOrderFingerprint(List<ProgressTracker> list)
        {
            if (list == null || list.Count == 0) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(list.Count * 12);
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append('|');
                var p = list[i];
                sb.Append(p ? p.gameObject.GetInstanceID() : 0);
            }
            return sb.ToString();
        }
    }
}
