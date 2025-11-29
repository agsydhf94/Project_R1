using UnityEngine;
using UnityEngine.Audio;

namespace R1
{
    public class GameSoundManager : MonoBehaviour
    {
        public static GameSoundManager Instance { get; private set; }

        public AudioMixer masterMixer; // optional
        public float masterVolume = 1f;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = value;
            AudioListener.volume = value;
        }

        // 예시 확장용
        public void PlaySFX(AudioClip clip, Vector3 position)
        {
            AudioSource.PlayClipAtPoint(clip, position);
        }
    }
}
