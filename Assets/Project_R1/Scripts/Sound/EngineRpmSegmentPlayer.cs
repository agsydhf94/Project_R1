using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Handles playback of engine RPM samples using a bank of audio clips.
    /// Smoothly blends multiple audio sources to simulate continuous RPM-based engine sound.
    /// </summary>
    public class EngineRpmSegmentPlayer : MonoBehaviour
    {
        [Header("Bank / Sources")]
        /// <summary>
        /// The RPM sound bank containing audio clips for each RPM step.
        /// </summary>
        public EngineRpmBankSO rpmBank;

        /// <summary>
        /// Reference to the car controller providing engine RPM values.
        /// </summary>
        public CarController carController;

        [Header("Tuning")]
        /// <summary>
        /// Master volume multiplier applied to all sources.
        /// </summary>
        public float masterVolume = 0.9f;

        /// <summary>
        /// Smoothing time applied to RPM ratio changes.
        /// </summary>
        public float rpmSmoothTime = 0.08f;

        /// <summary>
        /// Base pitch multiplier for all sources.
        /// </summary>
        public float pitch = 1.0f;

        /// <summary>
        /// Spatial blend value (0 = 2D, 1 = 3D).
        /// </summary>
        public float spatialBlend = 1f;

        /// <summary>
        /// Minimum distance for 3D audio rolloff.
        /// </summary>
        public float minDistance = 5f;

        /// <summary>
        /// Maximum distance for 3D audio rolloff.
        /// </summary>
        public float maxDistance = 200f;

        private List<AudioSource> sources = new();
        private float rpmRatioSmoothed, rpmVel;


        /// <summary>
        /// Initializes the engine RPM audio system on start.
        /// Validates that an RPM bank with samples is assigned and creates audio sources for playback.
        /// Disables this component if the bank is missing or empty.
        /// </summary>
        void Start()
        {
            if (rpmBank == null || rpmBank.rpmSamples.Count == 0)
            {
                Debug.LogWarning("[EngineRpmBankPlayer] Bank is missing.");
                enabled = false;
                return;
            }

            CreateSourcesFromBank();
        }


        /// <summary>
        /// Updates the engine sound each frame by calculating the normalized RPM ratio,
        /// smoothing it over time, and blending audio samples accordingly.
        /// </summary>
        void Update()
        {
            float rpm = GetCurrentRPM();
            float maxRPM = GetMaxRPM();
            float rpmRatio = Mathf.Clamp01(rpm / Mathf.Max(1f, maxRPM));
            rpmRatioSmoothed = Mathf.SmoothDamp(rpmRatioSmoothed, rpmRatio, ref rpmVel, rpmSmoothTime);

            BlendSamples(rpmRatioSmoothed);
        }


        /// <summary>
        /// Creates looping audio sources for each clip in the RPM bank.
        /// </summary>
        void CreateSourcesFromBank()
        {
            foreach (var clip in rpmBank.rpmSamples)
            {
                var audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
                audioSource.volume = 0f;
                audioSource.spatialBlend = spatialBlend;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                audioSource.minDistance = minDistance;
                audioSource.maxDistance = maxDistance;
                audioSource.dopplerLevel = 0f;
                audioSource.time = Random.Range(0f, clip.length);
                audioSource.Play();
                sources.Add(audioSource);
            }
        }


        /// <summary>
        /// Blends volumes and pitches of the audio sources based on normalized RPM.
        /// </summary>
        /// <param name="rpm_01Normalized">The current RPM as a normalized 0–1 value.</param>
        void BlendSamples(float rpm_01Normalized)
        {
            int total = sources.Count;
            if (total < 2) return;

            float maxRPM = GetMaxRPM();
            float stepRPM = rpmBank.stepRpm;
            float rpm = rpm_01Normalized * maxRPM;

            float exactIndex = rpm / stepRPM;
            int baseIndex = Mathf.FloorToInt(exactIndex);
            float t = exactIndex - baseIndex;

            const int blendRange = 2;
            for (int i = 0; i < total; i++)
            {
                int delta = Mathf.Abs(i - baseIndex);
                float weight = 0f;

                if (delta <= blendRange)
                {
                    float fade = 1f - (delta / (float)(blendRange + 1));
                    weight = Mathf.SmoothStep(0f, 1f, fade);
                }

                sources[i].volume = weight * masterVolume;

                float sampleRPM = i * stepRPM;
                float pitchOffset = rpm / Mathf.Max(1f, sampleRPM);
                sources[i].pitch = pitch * pitchOffset;
            }
        }


        /// <summary>
        /// Gets the current RPM value from the car controller.
        /// </summary>
        /// <returns>Current engine RPM, or 0 if unavailable.</returns>
        float GetCurrentRPM()
        {
            if (carController)
            {
                return Mathf.Max(0f, carController.currentEngineRPM);
            }
            return 0f;
        }


        /// <summary>
        /// Gets the maximum RPM supported, either from the car controller or the bank definition.
        /// </summary>
        /// <returns>The maximum RPM value.</returns>
        float GetMaxRPM()
        {
            if (carController && carController.maxRPM > 0f)
            {
                return carController.maxRPM;
            }
            return rpmBank.stepRpm * (rpmBank.rpmSamples.Count - 1);
        }

    }
}
