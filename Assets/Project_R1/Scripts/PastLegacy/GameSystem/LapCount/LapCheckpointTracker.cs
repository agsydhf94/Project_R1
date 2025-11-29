using UnityEngine;
using System;

namespace R1
{
    /// <summary>
    /// Tracks checkpoint hits for lap validation using a bitmask approach.
    /// Ensures that all mid checkpoints (1..N-1) are visited in any order between start/finish (0) lines,
    /// applies debounce against repeated hits, enforces a minimum lap time, and notifies the race session
    /// when a valid lap completes.
    /// </summary>
    public class LapCheckpointTracker : MonoBehaviour
    {
        [Header("Refs")]
        /// <summary>
        /// Reference to the race session manager to notify on lap completion.
        /// </summary>
        public RaceSessionManager rsm;

        [Header("Track")]
        /// <summary>
        /// Total number of checkpoints including index 0 (start/finish). Example: 0..(checkpointCount-1).
        /// </summary>
        public int checkpointCount = 0;

        /// <summary>
        /// Minimum seconds required between valid laps to avoid accidental double counts.
        /// </summary>
        public float minLapSeconds = 3f;

        /// <summary>
        /// Debounce window (seconds) for consecutive hits of the same checkpoint index.
        /// </summary>
        public float debounceSeconds = 0.2f;

        // Internal state
        /// <summary>
        /// True while the race is active and hits are considered.
        /// </summary>
        private bool racing = false;

        /// <summary>
        /// Time.time at which the last valid lap was recorded.
        /// </summary>
        private float lastLapAt = -999f;

        /// <summary>
        /// The last checkpoint index that was hit (for debounce).
        /// </summary>
        private int lastHitIdx = -1;

        /// <summary>
        /// Time.time of the last checkpoint hit (for debounce).
        /// </summary>
        private float lastHitTime = -999f;

        // Bitmask method
        /// <summary>
        /// Bitmask of required mid checkpoints (1..N-1), excluding 0.
        /// </summary>
        private int requiredMask = 0;    

        /// <summary>
        /// Bitmask of mid checkpoints visited during the current lap segment.
        /// </summary>
        private int visitedMask = 0;      

        /// <summary>
        /// True if any mid checkpoint (1..N-1) has been visited since the last index 0 hit.
        /// </summary>
        private bool anyVisitedSinceLast0 = false;

        /// <summary>
        /// Event raised when any checkpoint is passed (for split popups, UI, etc.).
        /// </summary>
        public event Action<int> OnCheckpointPassed;

        private StartFinishCrossing _sfc;


        /// <summary>
        /// Resets the tracker to a known state and recomputes masks and timers.
        /// </summary>
        /// <param name="count">Total number of checkpoints including start/finish (index 0).</param>
        /// <param name="minLap">Minimum lap time (seconds) required for a lap to be considered valid.</param>
        public void ResetTracker(int count, float minLap)
        {
            checkpointCount = Mathf.Max(1, count);
            minLapSeconds = Mathf.Max(0f, minLap);

            // Build bitmask requiring all mid checkpoints except 0 (i.e., require 1..N-1).
            requiredMask = (checkpointCount >= 2) ? ((1 << checkpointCount) - 2) : 0;
            visitedMask = 0;
            anyVisitedSinceLast0 = false;

            lastLapAt = Time.time;
            racing = false;
            lastHitIdx = -1;
            lastHitTime = -999f;
        }


        /// <summary>
        /// Marks the beginning of the race; enables hit processing and resets visit status.
        /// </summary>
        public void OnRaceStart()
        {
            lastLapAt = Time.time;
            racing = true;
            visitedMask = 0;
            anyVisitedSinceLast0 = false;
        }


        /// <summary>
        /// Marks the end of the race; disables hit processing.
        /// </summary>
        public void OnRaceFinish() => racing = false;

        private bool IsLocalMe()
        {
            var car = GetComponent<CarController>();
            return rsm && rsm.localCar && car == rsm.localCar;
        }


        public void HitCheckpoint(int idx)
        {
            if (!rsm || !racing || rsm.localState != RaceState.Racing) return;
            if (idx < 0 || idx >= checkpointCount) return;

            // 디바운스
            if (idx == lastHitIdx && Time.time - lastHitTime < debounceSeconds) return;
            lastHitIdx  = idx;
            lastHitTime = Time.time;

            // 스플릿/팝업
            OnCheckpointPassed?.Invoke(idx);

            // 내가 로컬 차량인지 간단 판정
            bool isLocal = false;
            {
                var car = GetComponent<CarController>();
                isLocal = (rsm.localCar && car == rsm.localCar);
            }

            if (idx == 0)
            {
                bool hasMids = (checkpointCount > 1);
                if (hasMids && !anyVisitedSinceLast0)
                    return;

                bool allVisited = (requiredMask == 0) || ((visitedMask & requiredMask) == requiredMask);
                if (!allVisited)
                {
                    anyVisitedSinceLast0 = false;
                    return;
                }

                float elapsed = Time.time - lastLapAt;
                if (elapsed < minLapSeconds)
                    return;

                lastLapAt = Time.time;

                if (isLocal)
                {
                    // 내 차일 때만 RSM 호출 → HUD/기록/스플릿 유지
                    rsm.OnLapCompleted();
                }
                else
                {
                    // AI/원격: 자기 기록만 갱신 (결과 화면용)
                    var lt = GetComponent<LapTracker>();
                    lt?.OnCrossStartFinish();

                    var lapTimer = GetComponent<LapTimer>();
                    lapTimer?.CompleteLap();
                }

                // 다음 랩 준비
                visitedMask = 0;
                anyVisitedSinceLast0 = false;
                return;
            }

            // mid CP(1..N-1) 기록
            visitedMask |= (1 << idx);
            anyVisitedSinceLast0 = true;

            // (선택) StartFinishCrossing에 무장 알림을 주고 싶다면 여기서 호출
            // var sfc = _sfc ??= FindObjectOfType<StartFinishCrossing>(true);
            // if (isLocal) sfc?.OnMidCheckpointPassed(gameObject, idx);
        }
    }
}
