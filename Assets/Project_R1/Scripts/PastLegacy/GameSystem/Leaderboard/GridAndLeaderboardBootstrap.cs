using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace R1
{
    public class GridAndLeaderboardBootstrap : MonoBehaviour
    {
        /*
        [Header("Enable")]
        [Tooltip("레이스 씬 로드 직후 강제 적용")]
        public bool enabledAtStart = true;

        [Header("Timing")]
        [Tooltip("프리팹 스폰/컴포넌트 부착을 잠깐 기다리는 시간(초)")]
        public float applyDelaySeconds = 0.05f;
        [Tooltip("참가자/그리드 탐색 최대 대기 시간(초)")]
        public float discoveryTimeoutSeconds = 2f;

        [Header("UI")]
        [Tooltip("초기 리더보드 스냅샷을 그리드 순서로 즉시 렌더")]
        public bool forceInitialLeaderboard = true;

        private bool _applied;

        void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!enabledAtStart) return;

            // GameLaunchParams에 지정한 raceSceneName과 일치할 때만 동작(비어있으면 모든 씬에서 허용)
            var glp = GameLaunchParams.Instance;
            if (glp != null && !string.IsNullOrEmpty(glp.raceSceneName))
            {
                if (!string.Equals(scene.name, glp.raceSceneName, System.StringComparison.Ordinal)) return;
            }

            StartCoroutine(Co_ApplyOnce());
        }

        private IEnumerator Co_ApplyOnce()
        {
            if (_applied) yield break; // 중복 방지
            _applied = true;

            if (applyDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(applyDelaySeconds);

            // 참가자/그리드 탐색
            float stopAt = Time.realtimeSinceStartup + Mathf.Max(0.1f, discoveryTimeoutSeconds);
            List<PlayerIdentity> actors = null;
            Transform[] grid = null;

            while (Time.realtimeSinceStartup < stopAt)
            {
                actors = new List<PlayerIdentity>(FindObjectsOfType<PlayerIdentity>(true));
                if (actors.Count > 0)
                {
                    grid = TryGetGridPoints();
                    break;
                }
                yield return null;
            }
            if (actors == null || actors.Count == 0) yield break;

            // 1) GridSlot 인덱스 강제/보정
            AssignGridIndices(actors, grid);

            // 2) 리더보드 초기 스냅샷
            if (forceInitialLeaderboard)
                RenderInitialLeaderboard(actors);
        }

        private Transform[] TryGetGridPoints()
        {
            var rsm = FindObjectOfType<RaceSessionManager>();
            if (rsm != null && rsm.gridPoints != null && rsm.gridPoints.Length > 0)
                return rsm.gridPoints;
            return null; // 없어도 인덱스는 보정 가능
        }

        private void AssignGridIndices(List<PlayerIdentity> actors, Transform[] gridPoints)
        {
            // 기본: 기존 GridSlot.index가 있으면 존중, 없으면 가장 가까운 그리드 슬롯으로 보정
            foreach (var id in actors)
            {
                if (!id) continue;
                var gs = id.GetComponent<GridSlot>() ?? id.gameObject.AddComponent<GridSlot>();

                bool needsAssign = (gs.index == int.MaxValue || gs.index < 0);
                if (needsAssign)
                    gs.index = ComputeNearestSlot(id.transform.position, gridPoints);
            }

            // 중복 인덱스가 있으면 위치 순으로 유일하게 재배정
            actors.Sort((a, b) =>
            {
                int ia = a ? (a.GetComponent<GridSlot>()?.index ?? int.MaxValue) : int.MaxValue;
                int ib = b ? (b.GetComponent<GridSlot>()?.index ?? int.MaxValue) : int.MaxValue;
                return ia.CompareTo(ib);
            });

            var used = new HashSet<int>();
            foreach (var id in actors)
            {
                var gs = id.GetComponent<GridSlot>();
                if (!gs) continue;

                int idx = Mathf.Max(0, gs.index);
                while (used.Contains(idx)) idx++;
                gs.index = idx;
                used.Add(idx);
            }
        }

        private int ComputeNearestSlot(Vector3 pos, Transform[] gridPoints)
        {
            if (gridPoints == null || gridPoints.Length == 0) return int.MaxValue;
            int best = -1; float bestD2 = float.PositiveInfinity;
            for (int i = 0; i < gridPoints.Length; i++)
            {
                var t = gridPoints[i]; if (!t) continue;
                float d2 = (t.position - pos).sqrMagnitude;
                if (d2 < bestD2) { bestD2 = d2; best = i; }
            }
            return best < 0 ? int.MaxValue : best;
        }

        private void RenderInitialLeaderboard(List<PlayerIdentity> actors)
        {
            var ui = FindObjectOfType<LeaderboardUI>(true);
            if (!ui) return;

            // GridSlot.index 기준으로 정렬
            actors.Sort((a, b) =>
            {
                int ia = a ? (a.GetComponent<GridSlot>()?.index ?? int.MaxValue) : int.MaxValue;
                int ib = b ? (b.GetComponent<GridSlot>()?.index ?? int.MaxValue) : int.MaxValue;
                return ia.CompareTo(ib);
            });

            // RaceRecord로 변환(시간값은 비워둠)
            var list = new List<RaceRecord>(actors.Count);
            foreach (var id in actors)
            {
                if (!id) continue;
                string uid = string.IsNullOrEmpty(id.PlayerId) ? id.gameObject.name : id.PlayerId;
                string nm  = string.IsNullOrEmpty(id.DisplayName) ? id.gameObject.name : id.DisplayName;

                list.Add(new RaceRecord(uid, nm)
                {
                    bestTotal = Mathf.Infinity,
                    bestLap   = Mathf.Infinity
                });
            }

            // 로컬 유저 id 추출(하이라이트용)
            string localUid = "LOCAL";
            var me = actors.Find(p => p && p.IsLocal);
            if (me != null && !string.IsNullOrEmpty(me.PlayerId)) localUid = me.PlayerId;

            ui.Set(
                list,
                displayName: s => s, // 표시명 그대로 사용
                format:      _ => "",// 시간 비움
                localUserId: localUid
            );
        }
        */
    }
}
