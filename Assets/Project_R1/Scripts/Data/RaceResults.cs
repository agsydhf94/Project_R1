using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    [System.Serializable]
    public class RaceResults
    {
        public int finalPosition;         
        public float[] lapTimes;
        public float totalTime;
        public float bestLap;
        public bool newRecord;
        
        public int pointsEarned;
        public int moneyEarned;
        
        // Statistics
        public float topSpeedReached;
        public int overtakes;
        public int collisions;
    }
}
