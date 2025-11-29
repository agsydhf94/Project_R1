using System;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Tracks lap progress across checkpoint indices (0 = Start/Finish, 1..N-1 = intermediates),
    /// validates ordered checkpoint passing per lap, counts laps, and raises completion/finish events.
    /// </summary>
    public class LapTracker : MonoBehaviour
    {
        /// <summary>
        /// Total number of checkpoints in the track (0..N-1, where 0 is Start/Finish).
        /// </summary>
        public int totalCheckpoints;

        /// <summary>
        /// Current lap number (starts at 0 before crossing Start/Finish for the first time).
        /// </summary>
        public int currentLap { get; private set; } = 0;

        /// <summary>
        /// True when the target number of laps has been completed.
        /// </summary>
        public bool hasFinished { get; private set; } = false;

        /// <summary>
        /// Next required checkpoint index to validate correct order.
        /// </summary>
        public int nextCheckpoint { get; private set; } = 0;

        /// <summary>
        /// Target total laps to finish (0 means unlimited).
        /// </summary>
        public int targetTotalLaps = 3;

        /// <summary>
        /// Per-lap record of which checkpoints (1..N-1) were visited.
        /// </summary>
        private bool[] visitedThisLap;

        /// <summary>
        /// Raised when a lap is completed; passes the new currentLap value.
        /// </summary>
        public event Action<int> onLapCompleted; 

        /// <summary>
        /// Raised when the race is finished (after reaching targetTotalLaps).
        /// </summary>
        public event Action onFinished;         


        /// <summary>
        /// Initializes the tracker with a given number of checkpoints.
        /// Sets the next checkpoint to 1 (if any) right after the start line.
        /// </summary>
        /// <param name="checkpointsCount">The total number of checkpoints (including 0 = Start/Finish).</param>
        public void Init(int checkpointsCount)
        {
            totalCheckpoints = Mathf.Max(1, checkpointsCount);
            visitedThisLap = new bool[totalCheckpoints];
            ResetLapFlags();

            // Immediately after start, the next target is CP1
            nextCheckpoint = (totalCheckpoints > 1) ? 1 : 0;
        }


        /// <summary>
        /// Clears the per-lap visited flags while preserving array size.
        /// </summary>
        private void ResetLapFlags()
        {
            if (visitedThisLap == null || visitedThisLap.Length != totalCheckpoints)
                visitedThisLap = new bool[totalCheckpoints];
            else
                Array.Clear(visitedThisLap, 0, visitedThisLap.Length);
        }

        /// <summary>
        /// Handles passing an intermediate checkpoint (1..N-1).
        /// Updates the next required checkpoint and records visit when the order is correct.
        /// </summary>
        /// <param name="index">Checkpoint index that was hit (must be in 1..N-1).</param>
        public void OnHitCheckpoint(int index)
        {
            if (hasFinished) return;
            if (index <= 0 || index >= totalCheckpoints) return;

            // Ignore if this is not the next required checkpoint
            if (index != nextCheckpoint) return; 

            visitedThisLap[index] = true;
            nextCheckpoint = (index + 1);
            if (nextCheckpoint >= totalCheckpoints)
                nextCheckpoint = 0;
        }

        /// <summary>
        /// Handles crossing the Start/Finish line (index = 0).
        /// Starts the first lap, validates completion for subsequent laps,
        /// increments the lap count, and fires completion/finish events as needed.
        /// </summary>
        public void OnCrossStartFinish()
        {
            if (hasFinished) return;

            // First crossing starts lap 1 (no validation yet)
            if (currentLap == 0)
            {
                currentLap = 1;
                ResetLapFlags();
                nextCheckpoint = (totalCheckpoints > 1) ? 1 : 0;
                Debug.Log($"[LapTracker] First lap started (Lap {currentLap})");
                return;
            }

            // From lap 2 onward, require correct order and all mids visited
            if (nextCheckpoint != 0) return;

            // Ensure all intermediate checkpoints were visited this lap
            bool ok = true;
            for (int i = 1; i < totalCheckpoints; i++)
            {
                if (!visitedThisLap[i]) { ok = false; break; }
            }
            if (!ok) return;

            // Lap completion
            currentLap++;
            Debug.Log($"[LapTracker] Lap {currentLap} completed!");
            onLapCompleted?.Invoke(currentLap);

            ResetLapFlags();
            nextCheckpoint = (totalCheckpoints > 1) ? 1 : 0;
            
            // Finish if targetTotalLaps reached
            if (targetTotalLaps > 0 && currentLap >= targetTotalLaps)
            {
                hasFinished = true;
                onFinished?.Invoke();
                Debug.Log("[LapTracker] Race Finished!");
            }
        }
    }
}
