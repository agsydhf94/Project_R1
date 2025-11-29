using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class LeaderboardEntryPool : MonoBehaviour
    {
        /*
        [SerializeField] private Transform container;
        [SerializeField] private LeaderboardEntry entryPrefab;
        [SerializeField] private int capacity = 16;

        [Tooltip("Start()에서 한 번만 필요한 수까지 채우고 이후에는 절대 Instantiate하지 않습니다.")]
        [SerializeField] private bool buildOnStart = true;

        private readonly List<LeaderboardEntry> _entries = new();
        private bool _locked;

        public IReadOnlyList<LeaderboardEntry> Entries => _entries;
        public int Capacity => _entries.Count;
        public bool IsLocked => _locked;

        void Awake()
        {
            if (!container) container = transform;

            // 컨테이너에 이미 있는 엔트리 흡수
            var existing = container.GetComponentsInChildren<LeaderboardEntry>(true);
            foreach (var e in existing)
                if (!_entries.Contains(e)) _entries.Add(e);
        }

        void Start()
        {
            if (!buildOnStart) { _locked = true; return; }
            BuildIfNeededOnce();
        }

        public void BuildIfNeededOnce()
        {
            if (_locked) return;
            if (!container || !entryPrefab) { _locked = true; return; }

            int need = Mathf.Max(0, capacity - _entries.Count);
            for (int i = 0; i < need; i++)
            {
                var e = Instantiate(entryPrefab, container);
                e.gameObject.SetActive(false);
                _entries.Add(e);
            }
            _locked = true; // 이후 절대 Instantiate 금지
        }

        // 플레이 중 자식 수 변동 감시(생성/이동 등)
        void OnTransformChildrenChanged()
        {
            if (!Application.isPlaying || !_locked || !container) return;

            if (container.childCount != _entries.Count)
            {
                Debug.LogError($"[LeaderboardEntryPool] 런타임에 자식 수가 변했습니다. locked={_locked} entries={_entries.Count} children={container.childCount}");
                // 필요 시 초과분 비활성화 등 방어로직 추가 가능
            }
        }
        */
    }
}
