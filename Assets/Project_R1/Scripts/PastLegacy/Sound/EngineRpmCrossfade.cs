using UnityEngine;

namespace R1
{
    public class EngineRpmCrossfade : MonoBehaviour
    {
        [Header("Bank / Sources")]
        public SoundBank bank;
        public CarController carController;
        public RealisticGearbox gearbox;

        [Header("Tuning")]
        public float maxRPM = 8000f;        // 최대 RPM
        public float shiftRPM = 6000f;      // 시프팅 RPM
        [Range(0f, 1f)] public float masterVolume = 0.9f;
        public float rpmSmoothTime = 0.08f;
        public float throttleSmoothTime = 0.06f;

        [Header("Pitch Settings")]
        [Range(0.8f, 1.2f)] public float globalPitch = 1.0f;
        public float gearPitchFactor = 0.15f;  // 기어별 Pitch 보정 강도
        public float shiftPitchDrop = 0.15f;   // 변속 시 Pitch 순간 하락량
        public float shiftRecoveryTime = 0.25f; // 변속 후 Pitch 회복 속도

        [Header("3D / Rolloff")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 5f, maxDistance = 200f;

        [Header("Startup")]
        public float layersFadeInTime = 0.35f;
        public float startupDuckDb = 6f;

        // 내부 런타임
        private AudioSource s_idle, s_lowOn, s_lowOff, s_medOn, s_medOff, s_highOn, s_highOff, s_max, s_startup;
        private float rpmRatioSmoothed, rpmVel;
        private float throttleSmoothed, throttleVel;
        private bool started;

        // 변속 연출
        private int lastGear = 1;
        private float shiftPitchOffset = 0f;
        private float shiftVel = 0f;

        void Awake()
        {
            if (!bank || !bank.HasAny())
            {
                Debug.LogWarning("[EngineRpmCrossfade] Bank가 비어있습니다.");
                enabled = false;
                return;
            }
            RebuildFromBank();
        }

        void Start()
        {
            if (s_startup && bank.startup)
            {
                s_startup.Play();
                StartCoroutine(FadeInLayersAfterStartup());
            }
            else
            {
                SetAllLayerVolumes(0f);
                SetAllPlay(true);
                StartCoroutine(FadeAllLayersTo(1f, layersFadeInTime));
            }
            started = true;
        }

        void Update()
        {
            if (!started) return;

            // RPM & 스로틀 스무딩
            float rpm = GetRPM();
            float rpm01 = Mathf.Clamp01(rpm / Mathf.Max(1f, GetMaxRPM()));
            rpmRatioSmoothed = Mathf.SmoothDamp(rpmRatioSmoothed, rpm01, ref rpmVel, rpmSmoothTime);

            float throttle = GetThrottle01();
            throttleSmoothed = Mathf.SmoothDamp(throttleSmoothed, throttle, ref throttleVel, throttleSmoothTime);

            float r = rpmRatioSmoothed;

            // ----------------------------
            // RPM 크로스페이드 경계
            // ----------------------------
            float b0 = 0.18f; // idle → low
            float b1 = 0.42f; // low → mid
            float b2 = 0.72f; // mid → high
            float targetMaxStartRPM = 7400f;
            float b3 = Mathf.Clamp01(targetMaxStartRPM / GetMaxRPM());

            float wIdle = 0f, wLow = 0f, wMed = 0f, wHigh = 0f, wMax = 0f;

            if (r < b0)
            {
                float t = Mathf.InverseLerp(0f, b0, r);
                wIdle = 1f - t;
                wLow = t;
            }
            else if (r < b1)
            {
                float t = Mathf.InverseLerp(b0, b1, r);
                wLow = 1f - t;
                wMed = t;
            }
            else if (r < b2)
            {
                float t = Mathf.InverseLerp(b1, b2, r);
                wMed = 1f - t;
                wHigh = t;
            }
            else if (r < b3)
            {
                wHigh = 1f;
                wMax = 0f;
            }
            else
            {
                float t = Mathf.InverseLerp(b3, 1f, r);
                wHigh = 1f - t;
                wMax = t;
            }

            // ----------------------------
            // Pitch 계산 (RPM + 기어비 + 변속 효과)
            // ----------------------------
            float pitch = GetPitchFromCurve(r) * GetGearPitchMultiplier();

            // 기어 변경 시 Pitch 순간 하락 연출
            int currentGear = GetCurrentGear();
            if (currentGear != lastGear)
            {
                shiftPitchOffset = -shiftPitchDrop;
                lastGear = currentGear;
            }

            // 변속 후 Pitch 서서히 회복
            shiftPitchOffset = Mathf.SmoothDamp(shiftPitchOffset, 0f, ref shiftVel, shiftRecoveryTime);
            pitch += shiftPitchOffset;

            // 스로틀 on/off 게인
            float onGain = throttleSmoothed;
            float offGain = 1f - throttleSmoothed;

            // Idle
            SetVol(s_idle, masterVolume * wIdle);
            SetPitch(s_idle, pitch * globalPitch);

            // Low
            SetVol(s_lowOn, masterVolume * wLow * onGain);
            SetVol(s_lowOff, masterVolume * wLow * offGain);
            SetPitch(s_lowOn, pitch * globalPitch);
            SetPitch(s_lowOff, pitch * globalPitch);

            // Mid
            SetVol(s_medOn, masterVolume * wMed * onGain);
            SetVol(s_medOff, masterVolume * wMed * offGain);
            SetPitch(s_medOn, pitch * globalPitch);
            SetPitch(s_medOff, pitch * globalPitch);

            // High
            SetVol(s_highOn, masterVolume * wHigh * onGain);
            SetVol(s_highOff, masterVolume * wHigh * offGain);
            SetPitch(s_highOn, pitch * globalPitch);
            SetPitch(s_highOff, pitch * globalPitch);

            // Max - 7400RPM 이상부터 서서히 재생
            SetVol(s_max, masterVolume * wMax);
            SetPitch(s_max, pitch * globalPitch);
        }

        // ----------------------------
        // RPM 비율 기반 Pitch 커브
        // ----------------------------
        float GetPitchFromCurve(float r)
        {
            if (r < 0.3f)
                return Mathf.Lerp(1.0f, 1.05f, r / 0.3f);
            if (r < 0.7f)
                return Mathf.Lerp(1.05f, 1.3f, (r - 0.3f) / 0.4f);
            if (r < 0.9f)
                return Mathf.Lerp(1.3f, 1.5f, (r - 0.7f) / 0.2f);
            return 1.5f;
        }

        // ----------------------------
        // 기어 기반 Pitch 보정
        // ----------------------------
        float GetGearPitchMultiplier()
        {
            int gear = GetCurrentGear();
            return 1f / Mathf.Pow(1.2f, gear - 1); 
        }

        int GetCurrentGear()
        {
            if (carController) return carController.gearNum;
            return 1;
        }


        // ----------------------------
        // 오디오 유틸
        // ----------------------------
        [ContextMenu("Rebuild From Bank")]
        public void RebuildFromBank()
        {
            DestroyAllCreatedSources();

            s_idle = CreateLoop(bank.idle);
            s_lowOn = CreateLoop(bank.low_on);
            s_lowOff = CreateLoop(bank.low_off);
            s_medOn = CreateLoop(bank.med_on);
            s_medOff = CreateLoop(bank.med_off);
            s_highOn = CreateLoop(bank.high_on);
            s_highOff = CreateLoop(bank.high_off);
            s_max = CreateLoop(bank.maxRPM);
            s_startup = CreateOneShot(bank.startup);

            SetAllLayerVolumes(0f);
            SetAllPlay(true);
        }

        void DestroyAllCreatedSources()
        {
            foreach (var s in GetComponents<AudioSource>())
                DestroyImmediate(s);
        }

        AudioSource CreateLoop(AudioClip clip)
        {
            if (!clip) return null;
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = spatialBlend;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.time = Random.Range(0f, clip.length);
            return s;
        }

        AudioSource CreateOneShot(AudioClip clip)
        {
            if (!clip) return null;
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = false;
            s.playOnAwake = false;
            s.spatialBlend = spatialBlend;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            return s;
        }

        void SetAllPlay(bool play)
        {
            if (s_idle) s_idle.Play();
            if (s_lowOn) s_lowOn.Play();
            if (s_lowOff) s_lowOff.Play();
            if (s_medOn) s_medOn.Play();
            if (s_medOff) s_medOff.Play();
            if (s_highOn) s_highOn.Play();
            if (s_highOff) s_highOff.Play();
            if (s_max) s_max.Play();
        }

        void SetAllLayerVolumes(float v)
        {
            SetVol(s_idle, v);
            SetVol(s_lowOn, v);
            SetVol(s_lowOff, v);
            SetVol(s_medOn, v);
            SetVol(s_medOff, v);
            SetVol(s_highOn, v);
            SetVol(s_highOff, v);
            SetVol(s_max, v);
        }

        System.Collections.IEnumerator FadeInLayersAfterStartup()
        {
            float duck = Mathf.Pow(10f, -startupDuckDb / 20f);
            SetAllLayerVolumes(0f);
            SetAllPlay(true);

            float t = 0f;
            float dur = Mathf.Max(0.05f, bank.startup.length * 0.6f);
            while (t < dur)
            {
                t += Time.deltaTime;
                yield return null;
            }
            yield return FadeAllLayersTo(1f, layersFadeInTime);
        }

        System.Collections.IEnumerator FadeAllLayersTo(float target, float time)
        {
            float t = 0f;
            float[] start = {
                GetVol(s_idle), GetVol(s_lowOn), GetVol(s_lowOff),
                GetVol(s_medOn), GetVol(s_medOff), GetVol(s_highOn),
                GetVol(s_highOff), GetVol(s_max)
            };
            while (t < time)
            {
                t += Time.deltaTime;
                float k = t / time;
                if (s_idle) s_idle.volume = Mathf.Lerp(start[0], target, k);
                if (s_lowOn) s_lowOn.volume = Mathf.Lerp(start[1], target, k);
                if (s_lowOff) s_lowOff.volume = Mathf.Lerp(start[2], target, k);
                if (s_medOn) s_medOn.volume = Mathf.Lerp(start[3], target, k);
                if (s_medOff) s_medOff.volume = Mathf.Lerp(start[4], target, k);
                if (s_highOn) s_highOn.volume = Mathf.Lerp(start[5], target, k);
                if (s_highOff) s_highOff.volume = Mathf.Lerp(start[6], target, k);
                if (s_max) s_max.volume = Mathf.Lerp(start[7], target, k);
                yield return null;
            }
        }

        static void SetVol(AudioSource s, float v) { if (s) s.volume = Mathf.Clamp01(v); }
        static void SetPitch(AudioSource s, float p) { if (s) s.pitch = Mathf.Max(0.01f, p); }
        static float GetVol(AudioSource s) => s ? s.volume : 0f;

        float GetRPM()
        {
            if (gearbox) return Mathf.Max(0f, gearbox.GetCurrentRPM());
            if (carController) return Mathf.Max(0f, carController.currentEngineRPM);
            return 0f;
        }

        float GetMaxRPM()
        {
            if (gearbox && gearbox.maxRpm > 0f) return gearbox.maxRpm;
            if (carController && carController.maxRPM > 0f) return carController.maxRPM;
            return maxRPM;
        }

        float GetThrottle01()
        {
            if (carController && carController.TryGetComponent<InputManager>(out var input))
                return Mathf.Clamp01(input.vertical);

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                return 1f;

            return 0f;
        }
    }
}
