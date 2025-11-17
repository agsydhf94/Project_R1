using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class Common : MonoBehaviour
    {
        public enum RaceState
        {
            PreRace,    
            Countdown,  
            Racing,     
            Finished    
        }

        public enum DifficultyLevel
        {
            Easy,       
            Medium,     
            Hard        
        }
    }
}
