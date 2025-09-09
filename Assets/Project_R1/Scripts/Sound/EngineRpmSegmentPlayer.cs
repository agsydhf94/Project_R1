using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

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

        [Header("Mixer High Boost (ParamEQ)")]
        /// <summary>
        /// Enables or disables driving a high-frequency boost via the AudioMixer.
        /// </summary>
        public bool driveHighBoost = true;

        /// <summary>
        /// Name of the exposed AudioMixer parameter controlling the ParamEQ gain (in dB).
        /// This parameter must be set in the AudioMixer asset.
        /// </summary>
        public string highBoostGainParam = "EngineHighBoost_dB";

        /// <summary>
        /// Curve mapping normalized RPM (0–1) to ParamEQ gain in decibels.
        /// Typically starts at 0 dB at low RPM and increases (e.g., +2 dB) at high RPM.
        /// </summary>
        public AnimationCurve highBoostGainByRpm01 = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 2f)
        );

        /// <summary>
        /// Reference to the AudioMixer extracted from the assigned AudioMixerGroup.
        /// Used internally to set parameter values at runtime.
        /// </summary>
        private AudioMixer engineMixer;  

        [Header("Mixer Routing")]
        /// <summary>
        /// Mixer group (e.g., EngineSound) to route generated engine AudioSources to.
        /// </summary>
        public AudioMixerGroup engineMixerGroup;

        private List<AudioSource> sources = new();
        private float rpmRatioSmoothed, rpmVel;


        /// <summary>
        /// Initializes the engine RPM audio system on start.
        /// Validates that an RPM bank with samples is assigned and creates audio sources for playback.
        /// Disables this component if the bank is missing or empty.
        /// Also sets up the AudioMixer reference if an engine mixer group is provided.
        /// </summary>
        void Start()
        {
            if (rpmBank == null || rpmBank.rpmSamples.Count == 0)
            {
                Debug.LogWarning("[EngineRpmBankPlayer] Bank is missing.");
                enabled = false;
                return;
            }

            if (engineMixerGroup != null) engineMixer = engineMixerGroup.audioMixer;

            CreateSourcesFromBank();
        }


        /// <summary>
        /// Updates the engine sound each frame by calculating the normalized RPM ratio,
        /// smoothing it over time, and blending audio samples accordingly.
        /// Additionally applies a high-frequency boost through the AudioMixer based on the smoothed RPM ratio.
        /// </summary>
        void Update()
        {
            float rpm = GetCurrentRPM();
            float maxRPM = GetMaxRPM();
            float rpmRatio = Mathf.Clamp01(rpm / Mathf.Max(1f, maxRPM));
            rpmRatioSmoothed = Mathf.SmoothDamp(rpmRatioSmoothed, rpmRatio, ref rpmVel, rpmSmoothTime);

            BlendSamples(rpmRatioSmoothed);
            ApplyHighBoost(rpmRatioSmoothed);
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

                if (engineMixerGroup != null)
                    audioSource.outputAudioMixerGroup = engineMixerGroup;

                sources.Add(audioSource);
            }
        }


        /// <summary>
        /// Blends volumes and pitches of the audio sources based on normalized RPM.
        /// Includes safeguards for low-RPM edge cases to prevent artifacts.
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

                // Intentionally reduce index 0 source volume at very low RPM
                if (i == 0)
                {
                    // When RPM is below ~60% of the first step, scale down index 0 contribution
                    float guard = Mathf.InverseLerp(stepRPM * 0.2f, stepRPM * 0.6f, rpm);
                    weight *= Mathf.Clamp01(guard);
                }

                sources[i].volume = weight * masterVolume;

                // Ensure minimum sampleRPM and clamp pitch range
                float sampleRPM = Mathf.Max(stepRPM, i * stepRPM); // avoid division by zero
                float rawOffset = rpm / sampleRPM;
                float pitchOffset = Mathf.Clamp(rawOffset, 0.5f, 2.0f); // prevent excessive pitch
                sources[i].pitch = pitch * pitchOffset;
            }
        }


        /// <summary>
        /// Applies a ParamEQ high-frequency boost derived from the normalized RPM
        /// and clamps the gain to a safe range to prevent excessive amplification.
        /// </summary>
        /// <param name="rpm01">Normalized RPM ratio (0–1).</param>
        void ApplyHighBoost(float rpm01)
        {
            float gainDb = highBoostGainByRpm01.Evaluate(rpm01);

            // Prevent excessive amplification (adjust if necessary)
            gainDb = Mathf.Clamp(gainDb, -1f, 4f);
            engineMixer.SetFloat(highBoostGainParam, gainDb);
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
