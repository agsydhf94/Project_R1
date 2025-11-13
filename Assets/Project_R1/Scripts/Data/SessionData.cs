using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    [System.Serializable]
    public class SessionData
    {
        public TrackData selectedTrack;
        public CarData selectedCar;
        public int lapCount;
        public DifficultyLevel difficulty;
        
        public CarData[] opponentCars;
        public int opponentCount = 11;
        
        public RaceResults results;
        
        public void Reset()
        {
            selectedTrack = null;
            selectedCar = null;
            results = null;
        }
    }
}
