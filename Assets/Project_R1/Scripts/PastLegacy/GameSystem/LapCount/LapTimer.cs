using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Tracks lap times and total race time during a race session.
    /// Stores completed lap times, calculates the fastest lap,
    /// and maintains delta values relative to the fastest lap.
    /// </summary>
    public class LapTimer : MonoBehaviour
    {
        /// <summary>
        /// Time elapsed in the current lap (in seconds).
        /// </summary>
        public float CurrentLapTime { get; private set; }

        /// <summary>
        /// Total elapsed race time across all laps (in seconds).
        /// </summary>
        public float TotalTime { get; private set; }

        /// <summary>
        /// Completed lap times in seconds (read-only).
        /// </summary>
        public IReadOnlyList<float> LapTimes => lapTimes;

        /// <summary>
        /// Delta values for each lap relative to the fastest lap (read-only).
        /// </summary>
        public IReadOnlyList<float> Deltas => deltas;

        private List<float> lapTimes = new();
        private List<float> deltas = new();

        private bool running;

        /// <summary>
        /// The fastest lap recorded so far.
        /// Initialized as Infinity until a valid lap is completed.
        /// </summary>
        public float FastestLap { get; private set; } = Mathf.Infinity;


        /// <summary>
        /// Starts a new race session.
        /// Resets timers, clears records, and begins tracking time.
        /// </summary>
        public void StartRace()
        {
            lapTimes.Clear();
            deltas.Clear();
            CurrentLapTime = 0f;
            TotalTime = 0f;
            FastestLap = Mathf.Infinity;
            running = true;
        }


        /// <summary>
        /// Updates the current lap and total race time.
        /// Should be called every frame with deltaTime.
        /// </summary>
        /// <param name="deltaTime">Time step since the last update (in seconds).</param>
        public void UpdateTimer(float deltaTime)
        {
            if (!running) return;
            CurrentLapTime += deltaTime;
            TotalTime += deltaTime;
        }


        /// <summary>
        /// Completes the current lap, adds it to records,
        /// updates fastest lap if necessary, and prepares for the next lap.
        /// </summary>
        public void CompleteLap()
        {
            if (!running) return;

            // Add new lap record
            lapTimes.Add(CurrentLapTime);

            // Check if this is the new fastest lap
            if (CurrentLapTime < FastestLap)
            {
                FastestLap = CurrentLapTime;
                RecalculateDeltas(); // Update all deltas
            }
            else
            {
                // Add only this lap's delta
                deltas.Add(CurrentLapTime - FastestLap);
            }

            // Reset current lap timer
            CurrentLapTime = 0f;
        }


        /// <summary>
        /// Stops the race timer. Does not clear recorded lap data.
        /// </summary>
        public void StopRace()
        {
            running = false;
        }


        /// <summary>
        /// Recalculates all deltas relative to the current fastest lap.
        /// Called whenever a new fastest lap is set.
        /// </summary>
        private void RecalculateDeltas()
        {
            deltas.Clear();
            foreach (var lap in lapTimes)
            {
                deltas.Add(lap - FastestLap);
            }
        }
    }
}
