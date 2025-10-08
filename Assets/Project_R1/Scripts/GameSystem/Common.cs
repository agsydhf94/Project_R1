using System;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{   
    /// <summary>
    /// Stores per-player race performance data, including lap times,
    /// fastest lap, and total time. Each instance corresponds to one user.
    /// </summary>
    [Serializable]
    public class RaceRecord
    {
        /// <summary>
        /// Permanent user identifier (unique key, e.g. network ID).
        /// </summary>
        public string userId;

        /// <summary>
        /// Display name shown in leaderboard UI (can differ from userId).
        /// </summary>        
        public string playerId;   

        /// <summary>
        /// List of all lap times (seconds) recorded during the race session.
        /// </summary>  
        public List<float> lapTimes = new();
        
        /// <summary>
        /// Fastest lap time achieved by this player (seconds).
        /// </summary>
        public float bestLap = Mathf.Infinity;

        /// <summary>
        /// Sum of all laps (total race completion time).
        /// </summary>
        public float bestTotal = Mathf.Infinity;

        
        /// <summary>
        /// Creates a new <see cref="RaceRecord"/> with a given user ID and optional display name.
        /// </summary>
        /// <param name="uid">Unique user identifier.</param>
        /// <param name="name">Optional player display name.</param>
        public RaceRecord(string uid, string name = null)
        {
            userId = uid;
            playerId = string.IsNullOrEmpty(name) ? uid : name;
        }

        
        /// <summary>
        /// Creates a deep copy of this record including all lap times.
        /// </summary>
        /// <returns>A cloned instance with independent lists and values.</returns>
        public RaceRecord Clone() => new RaceRecord(userId, playerId)
        {
            lapTimes = new List<float>(lapTimes),
            bestLap = bestLap,
            bestTotal = bestTotal
        };
    }
    

    /// <summary>
    /// Lightweight structure representing the best lap information across all players.
    /// Used for leaderboard comparisons.
    /// </summary>
    public struct FastestLapInfo
    {
        /// <summary>Unique identifier of the player who set the fastest lap.</summary>
        public string userId;

        /// <summary>Lap time (in seconds) of the fastest lap.</summary>
        public float time;
    }


    /// <summary>
    /// Abstracts a time source that provides a monotonically increasing clock value.
    /// Useful for testing or injecting simulated timers.
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>Current time value in seconds (monotonic).</summary>
        double Now { get; }
    }
}
