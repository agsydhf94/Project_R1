using System;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Represents the overall lifecycle state of a race session.
    /// Used by the RaceSessionManager and related UI to control race flow.
    /// </summary>
    public enum RaceState { PreRace, Countdown, Racing, Finished }


    /// <summary>
    /// Represents basic user identification and display information.
    /// Shared between networking, identity, and leaderboard systems.
    /// </summary>
    public struct UserProfile
    {
        /// <summary>
        /// Unique network ID of the user (e.g., Photon user ID or “LOCAL” for offline play).
        /// </summary>
        public string userId;

        /// <summary>
        /// Display name shown in HUDs and leaderboards.
        /// </summary>
        public string displayName;
        
        /// <summary>
        /// Whether this profile belongs to the local player.
        /// </summary>
        public bool   isLocal;
    }

    public interface IIdentitySource
    {
        string LocalUserId { get; }
        string LocalDisplayName { get; }
        string GetDisplayName(string userId);
    }

    /// <summary>
    /// Lightweight struct representing a car’s ranking state during a race.
    /// Used by the <see cref="RankManager"/> to sort participants based on progress,
    /// lap completion, and time-based tiebreakers.
    /// </summary>
    [Serializable]
    public struct RankEntry
    {
        /// <summary>
        /// The GameObject representing this racer (e.g., player or AI car).
        /// </summary>
        public GameObject go;

        /// <summary>
        /// Scalar progress value (e.g., distance along the track or total progress).
        /// Higher value indicates further progress.
        /// </summary>
        public float progress;

        /// <summary>
        /// Whether the racer has completed all laps.
        /// </summary>
        public bool finished;

        /// <summary>
        /// Index of the next checkpoint to reach (mirrors <see cref="LapTracker.nextCheckpoint"/>).
        /// </summary>
        public int nextCheckpoint; // LapTracker.nextCheckpoint

        /// <summary>
        /// Starting grid position assigned to this racer (from <see cref="GridSlot.index"/>).
        /// </summary>
        public int gridSlot;       // GridSlot.index

        /// <summary>
        /// Total race time accumulated so far. Used as a tiebreaker when progress values match.
        /// </summary>
        public float totalTime;    // tie-breaker

        /// <summary>
        /// Best individual lap time achieved by this racer. Also used as a secondary tiebreaker.
        /// </summary>
        public float bestLap;      // tie-breaker
    }

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
    
    public interface IStateSync
    {
        RaceState State { get; }
        double StartTime { get; } // 카운트다운 목표 시간(절대값)
        event Action<RaceState> OnStateChanged;
        event Action<double> OnStartTimeChanged;


        void ScheduleCountdown(double startAtAbsolute);
        void SetState(RaceState s);
    }


    public interface ISpawner
    {
        GameObject SpawnPlayer(string prefabName, Transform slot);
        List<GameObject> SpawnAIs(string[] prefabNames, Transform[] slots, int startIndex);
    }


    public interface IRankingService
    {
        event Action<IReadOnlyList<RankEntry>> OnRankChanged;
        void Tick();

        void AddTarget(GameObject go);
        void RemoveTarget(GameObject go);
    }

        
    public interface IFreezeService
    {
        void FreezeAll(IEnumerable<GameObject> targets);
        void UnfreezeAll(IEnumerable<GameObject> targets);
    }

    /// <summary>
    /// Defines an abstraction for recording and tracking race results across multiple players.
    /// Provides system for starting runs, completing laps, finishing races,
    /// and exposing leaderboards and best records.
    /// </summary>
    public interface IRecordService
    {
        /// <summary>
        /// Called when a new race run begins for a given player.
        /// Initializes internal state for lap tracking and personal records.
        /// </summary>
        /// <param name="playerId">Unique identifier for the player.</param>
        /// <param name="playerName">Display name for the player.</param>
        void StartRun(string playerId, string playerName);


        /// <summary>
        /// Called when a player completes a lap.
        /// Updates lap-based data, including split times for per-lap analysis.
        /// </summary>
        /// <param name="lapTime">Duration (in seconds) of the completed lap.</param>
        /// <param name="currentLapSplits">Read-only list of recorded split times within the lap.</param>
        void CompleteLap(float lapTime, IReadOnlyList<float> currentLapSplits);


        /// <summary>
        /// Called when the player completes the full race.
        /// Finalizes results and triggers leaderboard update events.
        /// </summary>
        void FinishRun();


        /// <summary>
        /// The best record ever achieved by the local player.
        /// May persist across sessions for progress tracking.
        /// </summary>
        public RaceRecord PersonalBest { get; }


        /// <summary>
        /// The record currently being accumulated during an active race session.
        /// </summary>
        RaceRecord CurrentRun { get; }


        /// <summary>
        /// The best overall record among all participants (global fastest).
        /// </summary>
        RaceRecord OverallBest { get; }


        /// <summary>
        /// Ordered list of all race results, typically sorted by total or lap time.
        /// </summary>
        IReadOnlyList<RaceRecord> Leaderboard { get; }


        /// <summary>
        /// Event raised whenever the leaderboard is updated (e.g., after race completion).
        /// </summary>
        event Action<IReadOnlyList<RaceRecord>> OnLeaderboardUpdated;
    }

    /*
    public class Common : MonoBehaviour
    {

    }
    */
}
