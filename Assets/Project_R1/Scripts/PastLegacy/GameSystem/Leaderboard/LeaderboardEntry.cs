using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace R1
{
    /// <summary>
    /// UI row component for a leaderboard. Renders rank, player name,
    /// optional total time text, a local-player highlight background, and an optional fastest-lap badge.
    /// </summary>
    public class LeaderboardEntry : MonoBehaviour
    {
        /// <summary>Text element displaying the numeric rank (1-based).</summary>
        [SerializeField] private TextMeshProUGUI rankText;

        /// <summary>Text element displaying the player's display name.</summary>
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI totalTimeText;      // Added (may be left empty/hidden if not used).

        /// <summary>Background image used to highlight the local player's row.</summary>
        [SerializeField] private Image highlightBackground;
        [SerializeField] private GameObject fastestLapBadge;         // Optional: badge shown when this entry holds the fastest lap.


        /// <summary>
        /// Legacy API: set basic row data (rank and name) with optional local highlight.
        /// </summary>
        /// <param name="rank">Rank to display (1-based).</param>
        /// <param name="playerName">Display name to show.</param>
        /// <param name="isLocalPlayer">Whether to highlight this row as the local player.</param>
        public void SetData(int rank, string playerName, bool isLocalPlayer = false)
        {
            SetData(rank, playerName, null, isLocalPlayer, false);
        }

        /// <summary>
        /// Extended API: set row data including optional total time and fastest-lap badge.
        /// </summary>
        /// <param name="rank">Rank to display (1-based).</param>
        /// <param name="playerName">Display name to show.</param>
        /// <param name="totalTime">
        /// Optional formatted total time (e.g., "03:41.257"). If null or empty, the field is hidden.
        /// </param>
        /// <param name="isLocalPlayer">Whether to highlight this row as the local player.</param>
        /// <param name="hasFastestLap">Whether to show the fastest-lap badge for this entry.</param>
        public void SetData(int rank, string playerName, string totalTime,
                            bool isLocalPlayer, bool hasFastestLap)
        {
            if (rankText) rankText.text = rank.ToString();
            if (nameText) nameText.text = playerName;

            if (totalTimeText)
            {
                bool has = !string.IsNullOrEmpty(totalTime);
                totalTimeText.gameObject.SetActive(has);
                if (has) totalTimeText.text = totalTime;
            }

            if (highlightBackground) highlightBackground.enabled = isLocalPlayer;
            if (fastestLapBadge) fastestLapBadge.SetActive(hasFastestLap);
        }
    }
}
