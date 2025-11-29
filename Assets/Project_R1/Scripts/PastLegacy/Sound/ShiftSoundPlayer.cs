using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace R1
{
    /// <summary>
    /// Handles playback of gear shift sounds and mixer parameter automation,
    /// including gear clicks, hiss, turbo BOV, and engine sound modulation.
    /// </summary>
    public class ShiftSoundPlayer : MonoBehaviour
    {
        [Header("References")]
        /// <summary>
        /// Reference to the car controller that provides gear change events.
        /// </summary>
        public CarController car;

        /// <summary>
        /// Reference to the engine RPM crossfade component for collecting engine sources.
        /// </summary>
        public EngineRpmCrossfade engineRpmCrossfade;

        /// <summary>
        /// If true, automatically collects engine AudioSources from engineRpmCrossfade.
        /// </summary>
        public bool autoCollectEngineSources = true;

        /// <summary>
        /// AudioSources that play engine sound layers.
        /// </summary>
        public AudioSource[] engineSources;

        /// <summary>
        /// Main audio mixer used to control engine and shift automation.
        /// </summary>
        public AudioMixer mixer;

        /// <summary>
        /// Mixer group for engine sound layers.
        /// </summary>
        public AudioMixerGroup engineMixerGroup;


        /// <summary>
        /// Mixer group for gear click sound.
        /// </summary>
        public AudioMixerGroup clickMixerGroup;


        /// <summary>
        /// Mixer group for hiss/solenoid sound (optional).
        /// </summary>
        public AudioMixerGroup hissMixerGroup;


        /// <summary>
        /// Mixer group for turbo BOV sound (optional).
        /// </summary>
        public AudioMixerGroup bovMixerGroup;

        [Header("Shift One-shots")]
        /// <summary>
        /// AudioSource for gear click/clunk sounds.
        /// </summary>
        public AudioSource srcGearClick;  // click/clunk

        /// <summary>
        /// AudioSource for hiss/solenoid sounds (optional).
        /// </summary>
        public AudioSource srcGearHiss;   // hiss/solenoid (optional)

        /// <summary>
        /// AudioSource for turbo blow-off valve sounds (optional).
        /// </summary>
        public AudioSource srcBOV;        // turbo BOV (optional)



        // ───────────────────────────────────────────────────────────
        // /// <summary>
        /// Serializable container for per-direction shift parameters such as timing and filter settings.
        /// </summary>
        // ───────────────────────────────────────────────────────────
        [System.Serializable]
        public class ShiftParams
        {
            [Header("Timings (seconds)")]
            /// <summary>Duration of engine duck (seconds).</summary>
            public float cutTime = 0.07f;

            /// <summary>Duration of pitch glide (seconds).</summary>
            public float glideTime = 0.10f;

            /// <summary>Duration of bandpass sweep (seconds).</summary>
            public float bpTime = 0.18f;

            [Header("Amounts")]

            /// <summary>Duck level in decibels (applied to engine bus).</summary>
            [Range(-24f, 0f)] public float duck_dB = -8f;

            /// <summary>Pitch glide in semitones (negative = down, positive = up).</summary>
            [Range(-2f, 2f)] public float pitchGlideSemi = -0.5f;

            /// <summary>Bandpass filter start frequency (Hz).</summary>
            public float bpStartHz = 600f;

            /// <summary>Bandpass filter end frequency (Hz).</summary>
            public float bpEndHz = 1600f;

            /// <summary>Bandpass filter Q factor.</summary>
            public float bpQ = 1.2f;


            [Header("Parallel Distortion Send (dB)")]

            /// <summary>Send level to parallel distortion bus (dB).</summary>
            public float distSend_dB = -6f;

            /// <summary>Duration of parallel distortion send (seconds).</summary>
            public float distSendDur = 0.12f;
        }

        [Header("Per-direction params")]
        /// <summary>Parameters for upshift automation.</summary>
        public ShiftParams upshift = new ShiftParams
        {
            cutTime = 0.07f,
            glideTime = 0.10f,
            bpTime = 0.18f,
            duck_dB = -8f,
            pitchGlideSemi = -0.4f,
            bpStartHz = 600f,
            bpEndHz = 1600f,
            bpQ = 1.2f,
            distSend_dB = -6f,
            distSendDur = 0.12f
        };

        /// <summary>Parameters for downshift automation.</summary>
        public ShiftParams downshift = new ShiftParams
        {
            cutTime = 0.06f,
            glideTime = 0.10f,
            bpTime = 0.16f,
            duck_dB = -6f,
            pitchGlideSemi = +0.45f,
            bpStartHz = 700f,
            bpEndHz = 1400f,
            bpQ = 1.1f,
            distSend_dB = -8f,
            distSendDur = 0.10f
        };

        [Header("Mixer Param Names")]
        /// <summary>Mixer exposed parameter for ducking engine volume.</summary>
        public string pDuck = "EngineDuck_dB";

        /// <summary>Mixer exposed parameter for bandpass cutoff frequency.</summary>
        public string pBpCutoff = "EngineBP_Cutoff";

        /// <summary>Mixer exposed parameter for bandpass Q factor.</summary>
        public string pBpQ = "EngineBP_Q";

        /// <summary>Mixer exposed parameter for pitch glide in semitones.</summary>
        public string pPitchSemi = "EnginePitchSemitones";


        [Header("Parallel Distortion Send Param")]
        /// <summary>Mixer exposed parameter for distortion send level (dB).</summary>
        public string pDistSend = "EngineDist_Send_dB";

        /// <summary>List of currently running coroutines for shift effects.</summary>
        private readonly List<Coroutine> running = new();

        /// <summary>Stores the previous gear number to determine shift direction.</summary>
        private int _prevGear = -999;

        private void Awake()
        {
            if (autoCollectEngineSources && (engineSources == null || engineSources.Length == 0))
                TryCollectEngineSources();
            AssignMixerGroups();
        }

        private void OnEnable()
        {
            if (car != null)
            {
                car.OnGearChanged += HandleGearChanged;
                _prevGear = car.gearNum;
            }
        }

        private void OnDisable()
        {
            if (car != null) car.OnGearChanged -= HandleGearChanged;
            StopAllRunning();
        }


        /// <summary>
        /// Attempts to automatically collect engine sound sources from engineRpmCrossfade.
        /// </summary>
        private void TryCollectEngineSources()
        {
            if (engineRpmCrossfade == null) return;
            var go = engineRpmCrossfade is Component c ? c.gameObject : null;
            if (go == null) return;
            engineSources = go.GetComponentsInChildren<AudioSource>(includeInactive: true);
        }


        /// <summary>
        /// Assigns AudioMixerGroups to the configured AudioSources.
        /// </summary>
        private void AssignMixerGroups()
        {
            if (engineMixerGroup && engineSources != null)
                foreach (var s in engineSources) if (s) s.outputAudioMixerGroup = engineMixerGroup;
            if (clickMixerGroup && srcGearClick) srcGearClick.outputAudioMixerGroup = clickMixerGroup;
            if (hissMixerGroup && srcGearHiss) srcGearHiss.outputAudioMixerGroup = hissMixerGroup;
            if (bovMixerGroup && srcBOV) srcBOV.outputAudioMixerGroup = bovMixerGroup;
        }


        /// <summary>
        /// Handles gear change events and triggers appropriate sound and mixer automation.
        /// </summary>
        /// <param name="newGear">The new gear number after the shift.</param>
        private void HandleGearChanged(int newGear)
        {
            bool isUpshift = true;
            if (_prevGear != -999) isUpshift = newGear > _prevGear;
            _prevGear = newGear;

            PlayIfAvailable(srcGearClick, 0.03f);
            PlayIfAvailable(srcGearHiss, 0.02f); 

            var p = isUpshift ? upshift : downshift;

            StopAllRunning();
            running.Add(StartCoroutine(CoEngineDuck(p.cutTime, p.duck_dB)));
            running.Add(StartCoroutine(CoBandpassSweep(p.bpTime, p.bpStartHz, p.bpEndHz, p.bpQ)));
            running.Add(StartCoroutine(CoPitchGlide(p.glideTime, p.pitchGlideSemi)));
            running.Add(StartCoroutine(CoDistSendBurst(p.distSend_dB, p.distSendDur)));

            if (isUpshift && car != null && !car.nitrusFlag)
                PlayIfAvailable(srcBOV, 0.02f);
        }


        /// <summary>
        /// Stops all currently running shift effect coroutines.
        /// </summary>
        private void StopAllRunning()
        {
            foreach (var co in running) if (co != null) StopCoroutine(co);
            running.Clear();
        }

        // ────────────── Co-routines ──────────────
        /// <summary>
        /// Coroutine to apply temporary ducking to the engine mixer group.
        /// </summary>
        IEnumerator CoEngineDuck(float dur, float targetDb)
        {
            if (mixer == null || string.IsNullOrEmpty(pDuck)) yield break;
            mixer.GetFloat(pDuck, out float baseDb);
            mixer.SetFloat(pDuck, targetDb);
            float t = 0f; while (t < dur) { t += Time.deltaTime; yield return null; }
            const float rel = 0.08f;
            float rt = 0f;
            while (rt < rel)
            {
                rt += Time.deltaTime; float k = Mathf.Clamp01(rt / rel);
                mixer.SetFloat(pDuck, Mathf.Lerp(targetDb, baseDb, k));
                yield return null;
            }
            mixer.SetFloat(pDuck, baseDb);
        }


        /// <summary>
        /// Coroutine to apply a bandpass sweep automation to the mixer.
        /// </summary>
        IEnumerator CoBandpassSweep(float dur, float startHz, float endHz, float q)
        {
            if (mixer == null || string.IsNullOrEmpty(pBpCutoff) || string.IsNullOrEmpty(pBpQ)) yield break;
            mixer.SetFloat(pBpQ, q);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                mixer.SetFloat(pBpCutoff, Mathf.Lerp(startHz, endHz, k));
                yield return null;
            }
            mixer.SetFloat(pBpCutoff, 20000f);
        }


        /// <summary>
        /// Coroutine to apply temporary pitch glide using mixer or AudioSource pitch.
        /// </summary>
        IEnumerator CoPitchGlide(float dur, float semi)
        {
            bool useMixerPitch = mixer && !string.IsNullOrEmpty(pPitchSemi);
            if (useMixerPitch)
            {
                mixer.GetFloat(pPitchSemi, out float baseSemi);
                float down = dur * 0.4f, up = dur - down, t = 0f;
                while (t < down) { t += Time.deltaTime; float k = t / down; mixer.SetFloat(pPitchSemi, baseSemi + Mathf.Lerp(0f, semi, k)); yield return null; }
                t = 0f;
                while (t < up) { t += Time.deltaTime; float k = t / up; mixer.SetFloat(pPitchSemi, baseSemi + Mathf.Lerp(semi, 0f, k)); yield return null; }
                mixer.SetFloat(pPitchSemi, baseSemi);
            }
            else
            {
                if (engineSources == null || engineSources.Length == 0) yield break;
                float PitchMul(float s) => Mathf.Pow(2f, s / 12f);
                float[] basePitch = new float[engineSources.Length];
                for (int i = 0; i < engineSources.Length; i++) if (engineSources[i]) basePitch[i] = engineSources[i].pitch;
                float down = dur * 0.4f, up = dur - down, t = 0f;
                while (t < down)
                {
                    t += Time.deltaTime; float k = t / down; float pm = PitchMul(Mathf.Lerp(0f, semi, k));
                    for (int i = 0; i < engineSources.Length; i++) if (engineSources[i]) engineSources[i].pitch = basePitch[i] * pm;
                    yield return null;
                }
                t = 0f;
                while (t < up)
                {
                    t += Time.deltaTime; float k = t / up; float pm = PitchMul(Mathf.Lerp(semi, 0f, k));
                    for (int i = 0; i < engineSources.Length; i++) if (engineSources[i]) engineSources[i].pitch = basePitch[i] * pm;
                    yield return null;
                }
                for (int i = 0; i < engineSources.Length; i++) if (engineSources[i]) engineSources[i].pitch = basePitch[i];
            }
        }


        /// <summary>
        /// Coroutine to apply a burst of distortion send during a shift.
        /// </summary>

        IEnumerator CoDistSendBurst(float sendDb, float dur)
        {
            if (mixer == null || string.IsNullOrEmpty(pDistSend)) yield break;

            mixer.SetFloat(pDistSend, sendDb);

            float t = 0f;
            while (t < dur) { t += Time.deltaTime; yield return null; }

            const float rel = 0.085f;
            float rt = 0f;
            while (rt < rel)
            {
                rt += Time.deltaTime; float k = rt / rel;
                mixer.SetFloat(pDistSend, Mathf.Lerp(sendDb, -80f, k));
                yield return null;
            }
            mixer.SetFloat(pDistSend, -80f);
        }

        // ────────────── Utils ──────────────
        /// <summary>
        /// Utility to play a one-shot AudioSource if available.
        /// </summary>
        /// <param name="src">AudioSource to play.</param>
        /// <param name="randomPitchJitter">Optional random pitch variation factor.</param>
        private static void PlayIfAvailable(AudioSource src, float randomPitchJitter = 0f)
        {
            if (!src || !src.clip) return;
            if (randomPitchJitter > 0f)
            {
                float j = Random.Range(-randomPitchJitter, randomPitchJitter);
                src.pitch = Mathf.Clamp(src.pitch * (1f + j), 0.5f, 2f);
            }
            src.Play();
        }
    }
}
