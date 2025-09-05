using UnityEngine;
using UnityEngine.UI;

namespace R1
{
    /// <summary>
    /// Applies HUD shaking effects based on vehicle G-force values.
    /// Can bias shaking direction based on steering or acceleration/braking input,
    /// and optionally pulse a vignette overlay.
    /// </summary>
    public class GForceHudShaker : MonoBehaviour
    {
        /// <summary>
        /// Reference to the car controller used to read G-force values.
        /// </summary>
        public CarController carController;

        /// <summary>
        /// The root RectTransform of the HUD that will be shaken.
        /// </summary>                 
        public RectTransform hudRoot;             

        /// <summary>
        /// Mapping curve from absolute G-force to normalized shake strength (0–1).
        /// </summary>
        public AnimationCurve gToStrength = AnimationCurve.Linear(0, 0, 3f, 1f);

        /// <summary>
        /// G-force values below this threshold are ignored.
        /// </summary>
        public float deadZoneG = 0.1f;

        /// <summary>
        /// Maximum shake offset in pixels (X/Y).
        /// </summary>
        public Vector2 maxShakePx = new Vector2(18f, 12f);

        public bool rollActivate;

        /// <summary>
        /// Maximum roll angle (around the Z-axis).
        /// </summary>
        public float maxRollDeg = 3.5f;

        /// <summary>
        /// Noise frequency used to drive shaking randomness.
        /// </summary>
        public float frequency = 12f;

        /// <summary>
        /// Damping factor. Higher values settle the shake faster.
        /// </summary>
        [Range(0.1f, 20f)] public float damping = 7f;


        /// <summary>
        /// Attack factor. Higher values allow the shake to increase faster.
        /// </summary>
        [Range(0.1f, 10f)] public float attack = 10f;


        /// <summary>
        /// Whether to use unscaled time (ignoring Time.timeScale).
        /// </summary>
        public bool useUnscaledTime = true;

        /// <summary>
        /// Steering bias. Tilts shake distribution left/right depending on steering input.
        /// </summary>
        [Range(0f, 1f)] public float steerBias = 0.3f;

        /// <summary>
        /// Acceleration/brake bias. Tilts shake distribution up/down depending on vertical input.
        /// </summary>
        [Range(0f, 1f)] public float accelBrakeBias = 0.25f;

        /// <summary>
        /// Optional vignette image to pulse alpha along with shake strength.
        /// </summary>
        public Image vignette;    

        /// <summary>
        /// Mapping curve from shake strength to vignette alpha.
        /// </summary>
        public AnimationCurve strengthToVignette = AnimationCurve.Linear(0, 0, 1, 0.35f);

        // Internal state
        private Vector2 _baseAnchoredPos;
        private float _baseZRot;
        private float _strength;            // 0..1
        private float _timeSeedX, _timeSeedY;


        /// <summary>
        /// Resets animation curves to defaults.
        /// </summary>
        void Reset()
        {
            gToStrength = AnimationCurve.Linear(0, 0, 3f, 1f);
            strengthToVignette = AnimationCurve.Linear(0, 0, 1, 0.35f);
        }


        /// <summary>
        /// Initializes base HUD position/rotation and randomizes noise seeds.
        /// </summary>
        void Awake()
        {
            if (!hudRoot) hudRoot = GetComponent<RectTransform>();
            if (hudRoot)
            {
                _baseAnchoredPos = hudRoot.anchoredPosition;
                _baseZRot = hudRoot.localEulerAngles.z;
            }

            // Randomize noise phase
            _timeSeedX = Random.value * 1000f;
            _timeSeedY = Random.value * 2000f;
        }


        /// <summary>
        /// Updates shake strength and applies HUD shake each frame.
        /// </summary>
        void Update()
        {
            if (!carController)
                carController = GameObject.FindGameObjectWithTag("Player").GetComponent<CarController>();

            if (!hudRoot || carController == null) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = useUnscaledTime ? Time.unscaledTime : Time.time;

            // Convert G-force to target shake strength
            float gAbs = Mathf.Max(0f, Mathf.Abs(carController.gforce) - deadZoneG);
            float target = Mathf.Clamp01(gToStrength.Evaluate(gAbs));

            // Apply directional bias (steering/accel)
            float steer = carController.GetSteerInput();             // -1..1
            float vert = Mathf.Clamp(carController.currentSpeed > 1f ? (carController.nitrusFlag ? 1f : 0f) : 0f, -1f, 1f); // 간단 예시

            // Smooth strength (fast attack, slower release)
            float speedUp = attack;          // 키울 때 속도
            float slowDown = damping;         // 줄일 때 속도
            float rate = (target > _strength ? speedUp : slowDown);
            _strength = Mathf.MoveTowards(_strength, target, rate * dt);
            if (_strength < 0.0001f) _strength = 0f;

            // Generate Perlin noise offsets
            float nx = Mathf.PerlinNoise(_timeSeedX, t * frequency) * 2f - 1f;
            float ny = Mathf.PerlinNoise(_timeSeedY, t * frequency) * 2f - 1f;

            // Apply directional bias
            nx += steer * steerBias;                  // 코너링 때 좌우로 더 기움
            ny += Mathf.Sign(vert) * accelBrakeBias; // 가속/브레이크 시 상하 기울기

            // Compute final offsets
            Vector2 offset = new Vector2(
                nx * maxShakePx.x * _strength,
                ny * maxShakePx.y * _strength
            );

            // Apply roll if activated
            if (rollActivate)
            {
                float roll = maxRollDeg * Mathf.Clamp(nx * 0.6f + ny * 0.4f, -1f, 1f) * _strength;
                var e = hudRoot.localEulerAngles;
                e.z = _baseZRot + roll;
                hudRoot.localEulerAngles = e;
            }

            // Apply to HUD
            hudRoot.anchoredPosition = _baseAnchoredPos + offset;
            

            // Apply vignette if present
            if (vignette)
            {
                float alpha = Mathf.Clamp01(strengthToVignette.Evaluate(_strength));
                var color = vignette.color;
                color.a = alpha;
                vignette.color = color;
            }
        }

        /// <summary>
        /// Instantly stops shaking and resets HUD/vignette to base state.
        /// </summary>
        public void ResetShakeInstant()
        {
            _strength = 0f;
            if (hudRoot)
            {
                hudRoot.anchoredPosition = _baseAnchoredPos;
                var localEulerAngle = hudRoot.localEulerAngles;
                localEulerAngle.z = _baseZRot;
                hudRoot.localEulerAngles = localEulerAngle;
            }
            if (vignette)
            {
                var color = vignette.color;
                color.a = 0f;
                vignette.color = color;
            }
        }
    }
}
