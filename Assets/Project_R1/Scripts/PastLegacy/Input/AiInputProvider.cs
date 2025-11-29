using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Provides AI driving input using waypoint navigation.
    /// Calculates steering and acceleration based on the closest waypoint,
    /// applying distance offset and configurable steering force for smoother driving.
    /// </summary>
    public class AiInputProvider : MonoBehaviour
    {   
        /// <summary>
        /// Forward acceleration value applied to the vehicle (0..1).
        /// </summary>
        public float vertical;

        /// <summary>
        /// Steering input value (-1..1) determined by waypoint direction.
        /// </summary>
        public float horizontal;

        /// <summary>
        /// Index of the closest waypoint currently targeted.
        /// </summary>
        public int currentNode;

        [Header("AI settings")]
        /// <summary>
        /// Default acceleration value used by the AI.
        /// </summary>
        [Range(0, 1)] public float acceleration = 0.5f;

        /// <summary>
        /// Number of waypoints ahead to look when selecting the target waypoint.
        /// </summary>
        public int distanceOffset = 5;

        /// <summary>
        /// Steering force multiplier applied to horizontal input.
        /// </summary>
        public float sterrForce = 1f;


        /// <summary>
        /// Reference to the waypoint path (TrackWaypoints).
        /// </summary>
        private TrackWaypoints waypoints;

        /// <summary>
        /// Transform of the currently selected target waypoint.
        /// </summary>
        private Transform currentWaypoint;

        /// <summary>
        /// Cached list of waypoint transforms from TrackWaypoints.
        /// </summary>
        private List<Transform> nodes = new();

        [SerializeField] private CurvatureSpeedLimiter limiter;

        [Header("Speed Planning / Braking")]
        [Tooltip("직선에서의 최고 속도(km/h)")] 
        public float maxSpeedStraight = 180f;

        [Tooltip("급코너에서의 목표 속도(km/h)")] 
        public float maxSpeedCorner   = 60f;

        [Tooltip("이 각도(alpha) 이상이면 급코너로 간주(0~1; 0=직진, 1=약 40°+ )")]
        [Range(0f, 1f)] public float cornerAlphaForMax = 0.7f;

        [Tooltip("속도 P게인 (목표-현재) -> 가감속 명령")]
        public float speedPGain = 0.02f;

        [Tooltip("코너까지 남은 거리 비례 감속 가중치")]
        public float brakingDistanceMin = 10f;
        public float brakingDistanceMax = 60f;

        // 내부 캐시
        private Rigidbody _rb;
        private float _lastAlpha; // 직전 프레임의 곡률(부드럽게)
        private float _alphaSmooth = 0.15f; // 0=즉각, 1=매우 느림


        [SerializeField] float stanleyK = 1.2f;     // 횡오차 이득(0.5~2.0)
        [SerializeField] float maxSteerRad = 0.6f;  // 조향 한계 라디안(≈34°)
        [SerializeField] float steerLerp = 0.18f;   // 스무딩
        [SerializeField] bool loop = true;



        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Initializes waypoint references on start by finding a TrackWaypoints
        /// object in the scene tagged as "Path".
        /// </summary>
        private void Start()
        {
            var path = GameObject.FindGameObjectWithTag("Path");
            if (path != null)
            {
                waypoints = path.GetComponent<TrackWaypoints>();
                if (waypoints != null) nodes = waypoints.nodes;
            }
            currentWaypoint = transform;
        }


        /// <summary>
        /// Updates AI driving input every physics frame.
        /// </summary>
        private void FixedUpdate()
        {
            AIDrive();
        }

        public void Bind(TrackWaypoints tw)
        {
            waypoints = tw;
            nodes = (tw != null) ? tw.nodes : new List<Transform>();
        }


        private void AIDrive()
        {
            CalculateDistanceOfWaypoints(); // currentWaypoint, currentNode 갱신
            Steering();                     // horizontal 계산

            // --- 기존 속도 타깃/PGain 로직 그대로 유지 ---
            float alpha = 0f;
            if (currentWaypoint != null)
            {
                Vector3 toWpLocal = transform.InverseTransformPoint(currentWaypoint.position).normalized;
                alpha = Mathf.Clamp01(Mathf.Abs(toWpLocal.x));
            }
            _lastAlpha = Mathf.Lerp(_lastAlpha, alpha, 1f - _alphaSmooth);

            float distToWp = (currentWaypoint != null)
                ? Vector3.Distance(transform.position, currentWaypoint.position)
                : brakingDistanceMax;

            float distT = Mathf.InverseLerp(brakingDistanceMax, brakingDistanceMin, distToWp);
            float baseTarget = Mathf.Lerp(maxSpeedStraight, maxSpeedCorner, Mathf.InverseLerp(0f, cornerAlphaForMax, _lastAlpha));
            float targetKph = Mathf.Lerp(baseTarget, maxSpeedCorner, distT);

            float currentKph = (_rb != null) ? _rb.velocity.magnitude * 3.6f : 0f;

            float spdErr = targetKph - currentKph;
            float accelCmd = acceleration + speedPGain * spdErr;
            accelCmd -= _lastAlpha * 0.2f;

            // === 1) 당신 P제어 → throttle/brake로 분리 ===
            float pThrottle = Mathf.Clamp01(Mathf.Max(0f,  accelCmd));  // +면 가속
            float pBrake    = Mathf.Clamp01(Mathf.Max(0f, -accelCmd));  // -면 제동

            // === 2) 곡률 제한기(Limiter)와 결합 ===
            // Limiter가 더 강하게 요구하는 쪽을 우선시 (max 사용)
            float lThrottle = (limiter != null) ? Mathf.Clamp01(limiter.desiredAccel) : 0f;
            float lBrake    = (limiter != null) ? Mathf.Clamp01(limiter.desiredBrake) : 0f;

            float throttle = Mathf.Max(pThrottle, lThrottle);
            float brake    = Mathf.Max(pBrake,    lBrake);

            // 출발 킥(정지 마찰 탈출)
            if (_rb != null && _rb.velocity.sqrMagnitude < 0.25f)
                throttle = Mathf.Max(throttle, 0.25f);

            // [C] vertical만 있고, 브레이크는 별도 필드인 경우
            vertical = throttle;                 // +가속

            // (디버그) 지금 값이 제대로 나오나 확인
            //Debug.Log($"AI: vTar={targetKph:F1} kph, vCur={currentKph:F1} kph, th={throttle:F2}, br={brake:F2}, lim(acc={lThrottle:F2}, brk={lBrake:F2})");
        }


        private void CalculateDistanceOfWaypoints()
        {
            if (nodes == null || nodes.Count == 0) return;

            Vector3 pos = transform.position;
            float nearest = Mathf.Infinity;
            int best = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                float d = (nodes[i].position - pos).sqrMagnitude; // 빠르게
                if (d < nearest)
                {
                    nearest = d;
                    best = i;
                }
            }

            int idx = (best + Mathf.Max(0, distanceOffset)) % nodes.Count;
            currentWaypoint = nodes[idx];
            currentNode = best;
        }


        private void Steering()
        {
            if (currentWaypoint == null || waypoints == null || nodes.Count < 2) return;

            // --- 1) 현재 세그먼트 구하기: currentNode -> next ---
            int n = nodes.Count;
            int i = Mathf.Clamp(currentNode, 0, n - 1);
            int j = loop ? (i + 1) % n : Mathf.Min(i + 1, n - 1);

            Vector3 a = nodes[i].position;
            Vector3 b = nodes[j].position;
            Vector3 ab = b - a;

            // --- 2) 차량 위치를 세그먼트 위로 사영 → 최근접점 P ---
            Vector3 x = transform.position;
            float t = (ab.sqrMagnitude > 1e-6f) ? Mathf.Clamp01(Vector3.Dot(x - a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 P = a + t * ab;

            // --- 3) 경로 접선(진행 방향) ---
            Vector3 tangent = ab.sqrMagnitude > 1e-6f ? ab.normalized : (currentWaypoint.position - transform.position).normalized;

            // --- 4) Stanley 조향: 헤딩오차 + 횡오차 보정 ---
            float headingErr = Mathf.Deg2Rad * Vector3.SignedAngle(transform.forward, tangent, Vector3.up);

            // 부호 있는 횡오차(경로 좌측=+, 우측=-) : 경로 법선(left)으로 투영
            Vector3 left = Vector3.Cross(Vector3.up, tangent).normalized;
            float crossTrackErr = Vector3.Dot(P - x, left);

            float speed = (_rb != null) ? _rb.velocity.magnitude : 0f;
            float stanley = Mathf.Atan2(stanleyK * crossTrackErr, speed + 0.1f);

            float steerRad = Mathf.Clamp(headingErr + stanley, -maxSteerRad, maxSteerRad);

            // -1..1로 정규화해 horizontal 로 사용
            float target = Mathf.Clamp(steerRad / maxSteerRad, -1f, 1f);
            horizontal = Mathf.Lerp(horizontal, target, steerLerp);
        }
    }
}