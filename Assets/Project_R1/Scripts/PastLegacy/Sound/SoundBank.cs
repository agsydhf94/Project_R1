using UnityEngine;

namespace R1
{
    [CreateAssetMenu(menuName = "Audio/SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Core")]
        public AudioClip idle;
        public AudioClip low_on;
        public AudioClip low_off;
        public AudioClip med_on;
        public AudioClip med_off;
        public AudioClip high_on;
        public AudioClip high_off;
        public AudioClip maxRPM;   

        [Header("Extras")]
        public AudioClip startup;  

        // 편의 검사기
        public bool HasAny() =>
            idle || low_on || low_off || med_on || med_off || high_on || high_off || maxRPM || startup;
    }
}
