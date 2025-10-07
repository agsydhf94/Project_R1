using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Displays race records on the HUD. Subscribes to an IRecordService to render
    /// fastest lap (time and driver) and the player's personal best total.
    /// Manages a small pool for leaderboard rows (optional).
    /// </summary>
    public class RecordsHUD : MonoBehaviour
    {
        [Header("Leaderboard")]
        /// <summary>Parent container for dynamically created leaderboard entries.</summary>
        [SerializeField] private RectTransform listContainer;

        /// <summary>Inactive template prefab for a leaderboard entry.</summary>   
        [SerializeField] private LeaderboardEntry entryPrefab;  

        [Header("Texts")]
        /// <summary>Text for showing the overall fastest lap time in the session.</summary>
        [SerializeField] private TextMeshProUGUI fastestLapTimeText;

        /// <summary>Text for showing who set the fastest lap.</summary>
        [SerializeField] private TextMeshProUGUI fastestLapByText;

        /// <summary>Text for showing the local player's personal best total (or fallback).</summary>
        [SerializeField] private TextMeshProUGUI personalBestTotalText;

        /// <summary>Reusable pool of entry UI items (optional, not strictly required).</summary>
        private readonly List<LeaderboardEntry> pool = new();

        
        /// <summary>Records provider injected at runtime (bind/unbind handled safely).</summary>
        private IRecordService recordService;

        /// <summary>Resolver to map userId → display name.</summary>
        private Func<string, string> displayName;

        /// <summary>Local player's user id for highlighting purposes.</summary>
        private string localUserId;

        /// <summary>Whether the HUD is currently subscribed to the record service event.</summary>
        private bool subscribed;


        /// <summary>
        /// Initializes name resolution and local user id.
        /// </summary>
        /// <param name="displayNameResolver">Function that maps a userId to a display name; if null, identity is used.</param>
        /// <param name="localUserId">Local player's user id for UI hints.</param>
        public void Init(Func<string, string> displayNameResolver, string localUserId)
        {
            this.displayName = displayNameResolver ?? (id => id);
            this.localUserId = localUserId;
        }


        /// <summary>
        /// Binds to an <see cref="IRecordService"/> and subscribes to leaderboard updates.
        /// Performs an initial render immediately.
        /// </summary>
        /// <param name="service">Records service to subscribe to; if null, unbinds.</param>
        public void Bind(IRecordService service)
        {
            Unbind();
            recordService = service;
            if (recordService == null) return;

            recordService.OnLeaderboardUpdated += OnLeaderboardUpdated;
            subscribed = true;

            OnLeaderboardUpdated(recordService.Leaderboard);
        }


        /// <summary>
        /// Unsubscribes from the current record service and clears the reference.
        /// Safe to call multiple times.
        /// </summary>
        public void Unbind()
        {
            if (subscribed && recordService != null)
                recordService.OnLeaderboardUpdated -= OnLeaderboardUpdated;
            recordService = null;
            subscribed = false;
        }


        /// <summary>
        /// Lifecycle cleanup; ensures events are unsubscribed.
        /// </summary>
        void OnDestroy() => Unbind();


        /// <summary>
        /// Renders the fastest lap (time and driver) and personal best from the given leaderboard.
        /// Optionally updates leaderboard entries (commented block kept as reference).
        /// </summary>
        /// <param name="list">Ordered leaderboard records (best-first typical, but not required).</param>
        private void OnLeaderboardUpdated(IReadOnlyList<RaceRecord> list)
        {
            // 1) Leaderboard
            /*
            int count = list?.Count ?? 0;

            for (int i = 0; i < pool.Count; i++)
            {
                bool active = i < count;
                pool[i].gameObject.SetActive(active);
                if (!active) continue;

                var r = list[i];
                string shownName = string.IsNullOrEmpty(r.playerId) ? r.userId : r.playerId;
                bool isLocal = (r.userId == localUserId);

                pool[i].SetData(i + 1, displayName(shownName), isLocal);
            }
            */

            // 2) Fastest Lap
            string fastestUid = "";
            float best = float.PositiveInfinity;
            if (list != null)
            {
                foreach (var r in list)
                {
                    if (r != null && r.bestLap > 0f && r.bestLap < best)
                    {
                        best = r.bestLap;
                        fastestUid = string.IsNullOrEmpty(r.playerId) ? r.userId : r.playerId;
                    }
                }
            }

            if (fastestLapTimeText)
                fastestLapTimeText.text = (best < float.PositiveInfinity) ? Format(best) : "--:--.---";
            if (fastestLapByText)
                fastestLapByText.text = string.IsNullOrEmpty(fastestUid) ? "" : displayName(fastestUid);

            // 3) Personal Best
            if (recordService != null && personalBestTotalText)
            {
                var myPB = recordService.PersonalBest;
                if (myPB != null && myPB.bestLap > 0f
                    && !float.IsInfinity(myPB.bestLap)
                    && !float.IsNaN(myPB.bestLap))
                {
                    personalBestTotalText.text = Format(myPB.bestLap);
                }
                else
                {
                    personalBestTotalText.text = "--:--.---";
                }
            }
        }


        /// <summary>
        /// Formats seconds into m:ss.mmm string (e.g., 1:23.456).
        /// </summary>
        /// <param name="t">Time in seconds.</param>
        /// <returns>Formatted time string.</returns>

        private string Format(float t)
        {
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);
            int ms = Mathf.FloorToInt((t - Mathf.Floor(t)) * 1000f);
            return $"{m:00}:{s:00}.{ms:000}";
        }
    }
}
