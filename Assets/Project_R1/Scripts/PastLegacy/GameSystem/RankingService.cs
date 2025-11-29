// 04_Ranking.cs
// �ܼ� ���൵ ��� ���� ���� (�̱�/��Ƽ ����)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class RankingService
    {
        /*
        private readonly List<GameObject> _targets;
        public event Action<IReadOnlyList<RankEntry>> OnRankChanged;
        private readonly List<RankEntry> _buffer = new();

        // 카운트다운/출발 직후엔 그리드 그대로(해제는 RSM이 제어)
        private bool _gridLock = true;
        public void SetGridLock(bool on) => _gridLock = on;

        public RankingService(List<GameObject> targets) { _targets = targets; }

        public void AddTarget(GameObject go) { if (go && !_targets.Contains(go)) _targets.Add(go); }
        public void RemoveTarget(GameObject go) { if (go) _targets.Remove(go); }

        public void Tick()
        {
            _buffer.Clear();
            foreach (var go in _targets)
            {
                if (!go) continue;

                var pt  = go.GetComponent<ProgressTracker>();
                var lap = go.GetComponent<LapTracker>();

                _buffer.Add(new RankEntry
                {
                    go       = go,
                    // progress 필드(레거시)는 유지만
                    progress = pt ? pt.progress : 0f,
                    finished = lap && lap.hasFinished
                });
            }

            if (_gridLock)
                _buffer.Sort((a, b) => GridIndexOf(a.go).CompareTo(GridIndexOf(b.go)));
            else
                _buffer.Sort(CompareByDelta);

            OnRankChanged?.Invoke(_buffer);
        }

        private static int GridIndexOf(GameObject go) =>
            go ? (go.GetComponent<GridSlot>()?.index ?? int.MaxValue) : int.MaxValue;

        private static int CompareByDelta(RankEntry a, RankEntry b)
        {
            // 0) 완주자 우선
            int c = b.finished.CompareTo(a.finished);
            if (c != 0) return c;

            // 1) Δ거리(출발 이후 더 간 거리) 내림차순
            var pta = a.go.GetComponent<ProgressTracker>();
            var ptb = b.go.GetComponent<ProgressTracker>();
            float da = pta ? pta.DeltaMetersSinceStart() : 0f;
            float db = ptb ? ptb.DeltaMetersSinceStart() : 0f;

            c = db.CompareTo(da);
            if (c != 0) return c;

            // 2) 타이브레이커: 그리드 인덱스(작은 수가 앞)
            return GridIndexOf(a.go).CompareTo(GridIndexOf(b.go));
        }
        */
    }
}
