using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Tracks per-checkpoint split times for the current lap, loads/saves personal-best (PB)
    /// lap and split data, computes deltas versus PB, and exposes read-only accessors for UI/logic.
    /// </summary>
    public class SplitService
    {
        private readonly string userKey;

        /// <summary>
        /// Split times (seconds) recorded for the current lap. Index corresponds to checkpoint index.
        /// </summary>
        private List<float> currentLap = new();

        /// <summary>
        /// Persisted personal-best split times (seconds). Index corresponds to checkpoint index.
        /// </summary>
        private List<float> pbSplits = new();

        /// <summary>
        /// Persisted personal-best lap time in seconds (Infinity if not set).
        /// </summary>
        public float pbLap = float.PositiveInfinity;

        const string KEY_PB_LAP = "_PB_LAP";
        const string KEY_PB_SPLIT = "_PB_SPLITS";

        [System.Serializable] class FWrap { public float[] arr; }


        /// <summary>
        /// Creates a new split service bound to a specific user key and loads any persisted PB data.
        /// Also resets the current-lap split buffer.
        /// </summary>
        /// <param name="checkpointCount">Number of checkpoints in the track (not strictly required at construction).</param>
        /// <param name="userKey">User- or profile-specific key for PlayerPrefs storage.</param>
        public SplitService(int checkpointCount, string userKey)
        {
            this.userKey = userKey;
            LoadPB();
            ResetCurrentLap();
        }


        /// <summary>
        /// Clears the current-lap split buffer.
        /// </summary>
        public void ResetCurrentLap() => currentLap = new List<float>();

        /// <summary>
        /// Ensures the list has at least <paramref name="size"/> elements, padding with NaN.
        /// </summary>
        private void Ensure(List<float> l, int size)
        {
            while (l.Count < size)
            {
                l.Add(float.NaN);
            }
        }


        /// <summary>
        /// Records a split time for a checkpoint index.
        /// </summary>
        /// <param name="idx">Checkpoint index to record.</param>
        /// <param name="t">Elapsed time (seconds) at this checkpoint.</param>
        public void RecordSplit(int idx, float t)
        {
            Ensure(currentLap, idx + 1); currentLap[idx] = t;
        }


        /// <summary>
        /// Records the final lap time (stored at split index 0 by convention).
        /// </summary>
        /// <param name="lap">Lap time in seconds.</param>
        public void RecordFinish(float lap)
        {
            Ensure(currentLap, 1); currentLap[0] = lap;
        }


        /// <summary>
        /// Compares the given lap time to the stored PB. If it beats the PB,
        /// updates <see cref="pbLap"/> and <see cref="pbSplits"/> and persists them.
        /// </summary>
        /// <param name="lap">Lap time to evaluate in seconds.</param>
        public void MaybeUpdatePBOnLapFinish(float lap)
        {
            if (lap <= 0f || float.IsNaN(lap) || float.IsInfinity(lap)) return;
            if (lap < pbLap)
            {
                pbLap = lap;
                pbSplits = new List<float>(currentLap);
                SavePB();
            }
        }


        /// <summary>
        /// Returns the time delta versus PB for a given split:
        /// positive = slower than PB, negative = faster than PB.
        /// Returns NaN if PB/split is unavailable or invalid.
        /// </summary>
        /// <param name="idx">Checkpoint index to compare.</param>
        /// <param name="cur">Current split time (seconds).</param>
        public float GetDeltaVsPB(int idx, float cur)
        {
            if (pbSplits == null || pbSplits.Count == 0) return float.NaN;
            if (idx < 0 || idx >= pbSplits.Count) return float.NaN;
            float pb = pbSplits[idx];
            if (pb <= 0f || float.IsNaN(pb) || float.IsInfinity(pb)) return float.NaN;
            return cur - pb; // + slower / - faster
        }


        /// <summary>
        /// Returns a read-only view of the current-lap split buffer.
        /// </summary>
        public IReadOnlyList<float> GetCurrentLapSplits() => currentLap;


        /// <summary>
        /// Loads PB lap and split data from PlayerPrefs using the bound user key.
        /// </summary>
        private void LoadPB()
        {
            pbLap = PlayerPrefs.GetFloat(userKey + KEY_PB_LAP, float.PositiveInfinity);
            string js = PlayerPrefs.GetString(userKey + KEY_PB_SPLIT, "");
            pbSplits = new();
            if (!string.IsNullOrEmpty(js))
            {
                try { var w = JsonUtility.FromJson<FWrap>(js); if (w?.arr != null) pbSplits = new List<float>(w.arr); }
                catch { }
            }
        }

        /// <summary>
        /// Persists PB lap and splits to PlayerPrefs using the bound user key.
        /// </summary>
        private void SavePB()
        {
            PlayerPrefs.SetFloat(userKey + KEY_PB_LAP, pbLap);
            var w = new FWrap { arr = pbSplits.ToArray() };
            PlayerPrefs.SetString(userKey + KEY_PB_SPLIT, JsonUtility.ToJson(w));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Reinitializes in-memory state for a new track configuration.
        /// </summary>
        /// <param name="checkpointCount">Number of checkpoints for the new configuration (reserved for future use).</param>
        public void Reconfigure(int checkpointCount)
        {
            ResetCurrentLap(); // reflect cpCount if needed in the future
        }


        /// <summary>
        /// Clears all in-memory data (does not touch persisted PBs).
        /// </summary>
        public void ClearAllInMemory()
        {
            currentLap.Clear();
            pbSplits.Clear();
        }
    }
}
