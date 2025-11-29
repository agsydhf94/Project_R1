using UnityEngine;
using UnityEngine.Audio;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace R1
{
    public class CarEngineAudio : MonoBehaviour
    {
        [Header("Telemetry source (bind at runtime)")]
        public MonoBehaviour telemetryProvider; // must implement ICarTelemetry
        private ICarTelemetry telemetry;

        [Header("Audio Source & Mixer")]
        public AudioSource engineSource;
        public AudioMixerGroup mixerPlayer;   // Engines/Player
        public AudioMixerGroup mixerRemote;   // Engines/Remote

        [Header("Tuning")]
        public float rpmAtMinPitch = 800f;
        public float rpmAtMaxPitch = 7000f;
        public float minPitch = 0.8f;
        public float maxPitch = 2.2f;
        public float minVol  = 0.15f;
        public float maxVol  = 1.0f;

        [Header("Per-Source filter for remotes (optional)")]
        public AudioLowPassFilter perSourceLPF;       // 붙여두면 원격차에만 켜짐
        public float lpfMin = 500f;
        public float lpfMax = 22000f;

#if PHOTON_UNITY_NETWORKING
        private PhotonView pv;
#endif

        public void Bind(ICarTelemetry t)
        {
            telemetry = t;
        }

        private void Reset()
        {
            engineSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (!engineSource) engineSource = GetComponent<AudioSource>();
#if PHOTON_UNITY_NETWORKING
            pv = GetComponent<PhotonView>();
#endif
            // 텔레메트리 자동 연결(선택)
            if (telemetry == null && telemetryProvider is ICarTelemetry t)
            {
                telemetry = t;
            }
            else if (telemetry == null)
            {
                // 같은 오브젝트에서 찾기 (편의)
                telemetry = GetComponent<ICarTelemetry>();
            }

            // 로컬/원격 라우팅
            bool isLocal =
#if PHOTON_UNITY_NETWORKING
                (pv == null || !PhotonNetwork.InRoom) || pv.IsMine;
#else
                true;
#endif
            engineSource.outputAudioMixerGroup = isLocal ? mixerPlayer : mixerRemote;

            // 원격차에만 Per-Source LPF 활성화
            if (perSourceLPF) perSourceLPF.enabled = !isLocal;

            // 3D 기본값
            engineSource.spatialBlend = 1f;
            engineSource.rolloffMode  = AudioRolloffMode.Logarithmic;
            engineSource.dopplerLevel = 0.2f;
        }

        private void Update()
        {
            if (telemetry == null || engineSource == null) return;

            float rpm = Mathf.Max(telemetry.EngineRpm, 0f);
            float t   = Mathf.InverseLerp(rpmAtMinPitch, rpmAtMaxPitch, rpm);

            // 피치/볼륨 맵핑
            engineSource.pitch  = Mathf.Lerp(minPitch, maxPitch, t);
            engineSource.volume = Mathf.Lerp(minVol,  maxVol,  t);

            // 원격차 전용 Per-Source LPF로 “원거리/혼탁감” 약간 주기
            if (perSourceLPF && perSourceLPF.enabled)
            {
                // 거리 기반으로 컷오프를 살짝 낮춤
                float d = Vector3.Distance(Camera.main.transform.position, transform.position);
                float dt = Mathf.InverseLerp(5f, 80f, d); // 5m~80m
                perSourceLPF.cutoffFrequency = Mathf.Lerp(lpfMax, lpfMin, dt);
            }
        }
    }
}
