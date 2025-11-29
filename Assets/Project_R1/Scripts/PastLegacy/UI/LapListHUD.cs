using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace R1
{
    /// <summary>
    /// Displays per-lap times and deltas in a scrollable/stacked list and
    /// shows current lap time / total session time at the top.
    /// Can operate with a fixed number of lap slots or grow dynamically.
    /// </summary>
    public class LapListHUD : MonoBehaviour
    {
        [Header("Top Labels")]
        /// <summary>Text element that shows the current lap's running time.</summary>
        [SerializeField] private TextMeshProUGUI currentLapTimeText;

        /// <summary>Text element that shows the total session time.</summary>
        [SerializeField] private TextMeshProUGUI totalTimeText;

        [Header("List")]
        /// <summary>Parent container that receives instantiated <see cref="LapEntry"/> items.</summary>
        [SerializeField] private RectTransform lapListContainer;

        /// <summary>Reusable prefab used to create lap rows in the list.</summary>
        [SerializeField] private LapEntry lapEntryPrefab;   

        [Header("Options")]
        /// <summary>
        /// Total laps to display. 0 means dynamic sizing based on recorded laps;
        /// a value &gt; 0 pre-creates fixed slots.
        /// </summary>
        [SerializeField] private int totalLaps = 0;     

        /// <summary>Whether to visually highlight the fastest lap entry.</summary> 
        [SerializeField] private bool highlightBestLap = true;

        [Header("Delta Colors")]
        /// <summary>Color used when a lap is faster (kept for modes that show faster-than-best deltas).</summary>
        [SerializeField] private Color improvedColor = new(0.3f, 1f, 0.4f, 1f);   // 더 빠름(녹색) - (※이번 모드에서는 사용 빈도 ↓)

        /// <summary>Color used when a lap is slower than the fastest lap (usually red).</summary>
        [SerializeField] private Color worseColor = new(1f, 0.4f, 0.4f, 1f);   

        /// <summary>Neutral color used for best/equal laps or when no baseline exists.</summary>
        [SerializeField] private Color neutralColor = new(0.8f, 0.8f, 0.8f, 1f); 

        /// <summary>Source of lap and session timing.</summary>
        private LapTimer lapTimer;

        /// <summary>Optional record service (kept for future PB/records UI).</summary>
        private IRecordService recordService;

        /// <summary>Current list of instantiated lap entries.</summary>
        private readonly List<LapEntry> entries = new();

        /// <summary>Last observed number of recorded laps (for change detection).</summary>
        private int lastCount = -1;

        /// <summary>Index of the currently fastest lap; -1 if none.</summary>
        private int bestIndex = -1;

        /// <summary>Cached last fastest-lap value to detect changes.</summary>
        private float lastFastestLap = float.PositiveInfinity;


        /// <summary>
        /// Initializes the HUD with a <see cref="LapTimer"/>, an optional fixed number of laps,
        /// and an optional record service.
        /// </summary>
        /// <param name="timer">LapTimer that provides current/total times and lap history.</param>
        /// <param name="fixedTotalLaps">
        /// If &gt; 0, pre-creates that many slots; if 0, the list grows with recorded laps.
        /// </param>
        /// <param name="recordSvc">Optional record service for future PB/record UI features.</param>
        public void Init(LapTimer timer, int fixedTotalLaps, IRecordService recordSvc = null)
        {
            lapTimer = timer;
            totalLaps = fixedTotalLaps;
            recordService = recordSvc;

            for (int i = lapListContainer.childCount - 1; i >= 0; i--)
            {
                var t = lapListContainer.GetChild(i);
                if (lapEntryPrefab == null || t.gameObject != lapEntryPrefab.gameObject)
                    Destroy(t.gameObject);
            }
            entries.Clear();
            lastCount = -1;
            bestIndex = -1;
            lastFastestLap = float.PositiveInfinity;

            if (totalLaps > 0)
            {
                EnsureItemCount(totalLaps);
                for (int i = 0; i < entries.Count; i++)
                    entries[i].SetPlaceholder(i);
                lastCount = 0;
            }
        }


        /// <summary>
        /// Updates the top labels (current lap/total time) and synchronizes the list
        /// with recorded laps: ensures item count, recomputes fastest-lap index when needed,
        /// and sets per-lap data and deltas (vs session fastest).
        /// </summary>
        void Update()
        {
            if (lapTimer == null) return;

            // Top timer labels
            currentLapTimeText.text = $"{Format(lapTimer.CurrentLapTime)}";
            totalTimeText.text = $"{Format(lapTimer.TotalTime)}";

            var laps = lapTimer.LapTimes;
            if (laps == null) return;

            // Adjust slot count
            int want = (totalLaps > 0) ? totalLaps : laps.Count;
            if (entries.Count < want) EnsureItemCount(want);

            // Conditions for refreshing best-lap index/deltas:
            //    1) Number of laps has changed
            //    2) FastestLap value has changed
            bool countChanged = (lastCount != laps.Count);
            bool fastestChanged = !Mathf.Approximately(lastFastestLap, lapTimer.FastestLap);

            if (countChanged || fastestChanged)
            {
                RecomputeBestIndex();        // 최소값 인덱스 재계산
                lastCount = laps.Count;
                lastFastestLap = lapTimer.FastestLap;
            }

            //  Current mode: per-lap delta = (this lap time − current session FastestLap)
            //     - FastestLap entry: delta = 0.000, colored neutral (or highlighted background)
            //     - Others: delta > 0 -> red
            for (int i = 0; i < entries.Count; i++)
            {
                if (i < laps.Count)
                {
                    bool hi = highlightBestLap && i == bestIndex;
                    entries[i].SetData(i, laps[i], hi);

                    float fastest = lapTimer.FastestLap;
                    if (float.IsInfinity(fastest) || fastest <= 0f)
                    {
                        // No valid FastestLap yet -> leave delta empty
                        entries[i].SetDelta(0f, neutralColor, showSign: false);
                    }
                    else
                    {
                        float delta = laps[i] - fastest;
                        if (Mathf.Abs(delta) < 1e-4f)
                        {
                            entries[i].SetDelta(0f, neutralColor, showSign: false);
                        }
                        else
                        {
                            entries[i].SetDelta(delta, worseColor, showSign: true);
                        }
                    }
                }
                else
                {
                    entries[i].SetPlaceholder(i);
                }
            }
        }

        /// <summary>
        /// Sets a delta value explicitly for a lap entry (utility for external control).
        /// </summary>
        /// <param name="lapIndex">Zero-based lap index to update.</param>
        /// <param name="delta">
        /// Delta to display in seconds; negative means faster, positive slower.
        /// NaN/Infinity clears the delta.
        /// </param>
        public void SetDeltaForLap(int lapIndex, float delta)
        {
            if (lapIndex < 0 || lapIndex >= entries.Count) return;

            var entry = entries[lapIndex];
            if (float.IsNaN(delta) || float.IsInfinity(delta))
            {
                entry.SetDelta(0f, Color.clear, false);
                return;
            }

            var color = (delta <= 0f) ? Color.green : Color.red;
            entry.SetDelta(delta, color, showSign: true);
        }


        /// <summary>
        /// Ensures the list has exactly <paramref name="count"/> items by instantiating
        /// or destroying <see cref="LapEntry"/> rows under <see cref="lapListContainer"/>.
        /// </summary>
        /// <param name="count">Desired number of visible row items.</param>
        void EnsureItemCount(int count)
        {
            while (entries.Count < count)
            {
                var e = Instantiate(lapEntryPrefab, lapListContainer);
                if (!e.gameObject.activeSelf) e.gameObject.SetActive(true);
                entries.Add(e);
            }
            while (entries.Count > count)
            {
                var last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (last) Destroy(last.gameObject);
            }
        }


        /// <summary>
        /// Recomputes the index of the fastest lap (minimum lap time) and caches it in <see cref="bestIndex"/>.
        /// </summary>
        void RecomputeBestIndex()
        {
            bestIndex = -1;
            float best = float.MaxValue;
            var laps = lapTimer.LapTimes;
            for (int i = 0; i < laps.Count; i++)
            {
                if (laps[i] < best)
                {
                    best = laps[i];
                    bestIndex = i;
                }
            }
        }


        /// <summary>
        /// Formats a duration in seconds to "m:ss.mmm".
        /// </summary>
        /// <param name="t">Time in seconds.</param>
        string Format(float t)
        {
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);
            int c = Mathf.FloorToInt((t - Mathf.Floor(t)) * 1000f);
            return $"{m:0}:{s:00}.{c:000}";
        }
    }
}
