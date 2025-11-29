using UnityEngine;
using UnityEngine.Audio;

namespace R1
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Audio Mixer")]
        public AudioMixer mixer;

        // 노출된 파라미터 이름
        [Header("Mixer Parameters")]
        public string engineVolumeParam = "EngineVolume";

        private void Awake()
        {
            // Singleton 패턴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 믹서에 dB 단위 볼륨 설정
        /// </summary>
        public void SetEngineVolume(float volume01)
        {
            float db = Mathf.Log10(Mathf.Clamp(volume01, 0.0001f, 1f)) * 20f;
            mixer.SetFloat(engineVolumeParam, db);
        }

        /// <summary>
        /// 믹서에서 현재 볼륨 (0~1) 조회
        /// </summary>
        public float GetEngineVolume()
        {
            if (mixer.GetFloat(engineVolumeParam, out float db))
            {
                return Mathf.Pow(10f, db / 20f);
            }
            return 1f;
        }
    }
}
