using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Immutable-like snapshot of a single race outcome.
    /// Intended for UI presentation, saving to disk, or analytics.
    /// All time values are in seconds.
    /// </summary>
    [System.Serializable]
    public class RaceResults
    {
        /// <summary>
        /// Final classified position (1 = winner). Not the starting grid position.
        /// </summary>
        public int finalPosition;

        /// <summary>
        /// Per-lap times in seconds, ordered chronologically (index 0 = Lap 1).
        /// </summary>
        public float[] lapTimes;

        /// <summary>
        /// Total elapsed time in seconds from race start to finish (including all laps).
        /// </summary>
        public float totalTime;

        /// <summary>
        /// Best (minimum) lap time in seconds during this race.
        /// </summary>
        public float bestLap;

        /// <summary>
        /// True if this session set a new personal/track record (context-dependent).
        /// </summary>
        public bool newRecord;


        /// <summary>
        /// Championship/series points earned from this result (after rules/scoring are applied).
        /// </summary>
        public int pointsEarned;

        /// <summary>
        /// In-game currency rewarded for this result (before taxes/bonuses unless specified).
        /// </summary>
        public int moneyEarned;


        // 式式 Telemetry / Analytics 式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式式

        /// <summary>
        /// Highest speed reached (units depend on project convention, e.g., km/h or m/s).
        /// </summary>
        public float topSpeedReached;

        /// <summary>
        /// Count of successful overtakes recorded during the race.
        /// </summary>
        public int overtakes;

        /// <summary>
        /// Count of collisions detected (wall, car, obstacle). Semantics depend on collision system.
        /// </summary>
        public int collisions;
    }
}
