using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Local (offline) implementation of <see cref="IRecordService"/> that stores
    /// personal-best values in <see cref="PlayerPrefs"/> and maintains an in-memory
    /// leaderboard for UI updates during the session.
    /// </summary>
    public class LocalRecordService : IRecordService
    {
        /// <summary>
        /// Personal-best record for the current user (loaded/saved via PlayerPrefs).
        /// </summary>
        public RaceRecord PersonalBest { get; private set; }

        /// <summary>
        /// The in-progress run for the local player (populated after <see cref="StartRun"/>).
        /// </summary>
        public RaceRecord CurrentRun { get; private set; }
        
        /// <summary>
        /// The best overall record seen so far in this client session (based on total time).
        /// </summary>
        public RaceRecord OverallBest { get; private set; }

        // <summary>
        /// Current leaderboard snapshot used by the HUD. For local mode this is kept minimal
        /// (e.g., a single “shadow” entry during the run, or the overall best after finish).
        /// </summary>
        public IReadOnlyList<RaceRecord> Leaderboard { get; private set; } = new List<RaceRecord>();

        /// <summary>
        /// Raised whenever the leaderboard should be refreshed in the UI (e.g., after a lap or finish).
        /// </summary>
        public event System.Action<IReadOnlyList<RaceRecord>> OnLeaderboardUpdated;


        /// <summary>
        /// Starts a new local run and loads existing personal best from <see cref="PlayerPrefs"/>.
        /// </summary>
        /// <param name="playerId">Stable unique user ID used as PlayerPrefs key prefix.</param>
        /// <param name="playerName">Display name to show on boards.</param>
        public void StartRun(string playerId, string playerName)
        {
            CurrentRun = new RaceRecord(playerId, playerName);

            // Load existing PB if available.
            LoadPersonalBest(playerId, playerName);
        }


        /// <summary>
        /// Appends a completed lap to <see cref="CurrentRun"/>, updates its best-lap field,
        /// and pushes a lightweight one-entry leaderboard update for immediate HUD feedback.
        /// </summary>
        /// <param name="lapTime">Lap time in seconds.</param>
        /// <param name="currentLapSplits">Optional split times for the lap (kept for future use).</param>
        public void CompleteLap(float lapTime, IReadOnlyList<float> currentLapSplits)
        {
            if (CurrentRun == null) return;

            // Update session state.
            CurrentRun.lapTimes.Add(lapTime);
            CurrentRun.bestLap = Mathf.Min(CurrentRun.bestLap, lapTime);

            // Temporary leaderboard for HUD before the run is finished.
            var shadow = CurrentRun.Clone();
            shadow.bestTotal = float.PositiveInfinity; // 완주 전
            Leaderboard = new List<RaceRecord> { shadow };
            OnLeaderboardUpdated?.Invoke(Leaderboard);
        }


        /// <summary>
        /// Updates personal-best lap if <paramref name="lapTime"/> beats the stored value,
        /// persists it to <see cref="PlayerPrefs"/>, and notifies listeners.
        /// </summary>
        /// <param name="lapTime">Candidate lap time in seconds.</param>
        /// <param name="userId">User ID used as PlayerPrefs key prefix.</param>
        /// <param name="playerName">Display name to assign if a new PB record is created.</param>
        public void UpdatePersonalBestLap(float lapTime, string userId, string playerName)
        {
            if (lapTime <= 0f || float.IsNaN(lapTime) || float.IsInfinity(lapTime))
                return;

            float prevBestLap = PlayerPrefs.GetFloat(userId + "_bestLap", float.PositiveInfinity);

            if (lapTime < prevBestLap)
            {
                PlayerPrefs.SetFloat(userId + "_bestLap", lapTime);
                PlayerPrefs.Save();

                if (PersonalBest == null)
                {
                    PersonalBest = new RaceRecord(userId, playerName)
                    {
                        bestLap = lapTime,
                        bestTotal = float.PositiveInfinity
                    };
                }
                else
                {
                    PersonalBest.bestLap = lapTime;
                }

                OnLeaderboardUpdated?.Invoke(Leaderboard); // prompt HUD refresh
            }
        }


        /// <summary>
        /// Finalizes the current run: computes total time, updates
        /// <see cref="PersonalBest"/> and <see cref="OverallBest"/> when beaten,
        /// persists PB totals, and emits a leaderboard update.
        /// </summary>
        public void FinishRun()
        {
            if (CurrentRun == null || CurrentRun.lapTimes.Count == 0) return;

            float total = 0f;
            foreach (var t in CurrentRun.lapTimes) total += t;

            CurrentRun.bestTotal = total;

            // Update PersonalBest (by total time)
            if (PersonalBest == null || total < PersonalBest.bestTotal)
            {
                PersonalBest = CurrentRun.Clone();
                SavePersonalBest();
            }

            // Update session-wide OverallBest
            if (OverallBest == null || total < OverallBest.bestTotal)
                OverallBest = CurrentRun.Clone();

            Leaderboard = new List<RaceRecord> { OverallBest };
            OnLeaderboardUpdated?.Invoke(Leaderboard);
        }


        /// <summary>
        /// Loads personal-best lap/total for the given user from <see cref="PlayerPrefs"/>.
        /// </summary>
        /// <param name="userId">User ID used as PlayerPrefs key prefix.</param>
        /// <param name="playerName">Display name used to initialize the PB record if found.</param>
        public void LoadPersonalBest(string userId, string playerName)
        {
            float lap = PlayerPrefs.GetFloat(userId + "_bestLap", float.PositiveInfinity);
            float tot = PlayerPrefs.GetFloat(userId + "_bestTotal", float.PositiveInfinity);

            if (lap < float.PositiveInfinity || tot < float.PositiveInfinity)
            {
                PersonalBest = new RaceRecord(userId, playerName)
                {
                    bestLap = lap,
                    bestTotal = tot
                };
            }
        }

        
        /// <summary>
        /// Persists the current <see cref="PersonalBest"/> to <see cref="PlayerPrefs"/>.
        /// </summary>
        private void SavePersonalBest()
        {
            if (PersonalBest == null) return;

            PlayerPrefs.SetFloat(PersonalBest.userId + "_bestLap", PersonalBest.bestLap);
            PlayerPrefs.SetFloat(PersonalBest.userId + "_bestTotal", PersonalBest.bestTotal);
            PlayerPrefs.Save();
        }
        
    }
    
}
