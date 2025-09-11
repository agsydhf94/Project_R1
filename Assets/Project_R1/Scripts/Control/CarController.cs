using System;
using System.Collections;
using UnityEngine;

namespace R1
{
    public class CarController : MonoBehaviour
    {
        /// <summary>Drive configuration: FWD, RWD, or AWD.</summary>
        internal enum driveType
        {
            frontWheelDrive,
            rearWheelDrive,
            allWheelDrive
        }
        [SerializeField] private driveType drive;

        //other classes ->
        private GameManager manager;
        private InputManager inputManager;
        private CarVisualEffects carVisualEffects;

        /// <summary>Engine sound helper flag (legacy/test use).</summary>
        [HideInInspector] public bool test;

        [Header("Variables")]
        /// <summary>Multiplier applied to friction when handbrake is engaged.</summary>
        public float handBrakeFrictionMultiplier = 2f;

        /// <summary>Maximum engine RPM clamp.</summary>
        public float maxRPM;

        /// <summary>Minimum engine RPM threshold for downshift.</summary>
        public float minRPM;

        /// <summary>Per-gear ratios array; index matches gearNum.</summary>
        public float[] gears;

        /// <summary>Speed thresholds per gear for shifting checks.</summary>
        public float[] gearChangeSpeed;

        /// <summary>Final drive ratio applied with gear ratio.</summary>
        public float finalDrive = 4.3f;

        /// <summary>Engine power curve evaluated against current RPM.</summary>
        public AnimationCurve enginePower;


        /// <summary>True for automatic shifting; false for manual (Q/E).</summary>
        public bool isAutomatic = true;

        /// <summary>Minimum time between manual shifts to avoid rapid toggling.</summary>
        [SerializeField] private float gearShiftCooldown = 0.2f; // 연속 시프트 방지 간격

        /// <summary>Target RPM threshold to trigger upshift in automatic mode.</summary>
        public float shiftRpm;

        private float lastShiftTime;


        /// <summary>Event fired after gear changes. Passes the new gear index.</summary>
        public event Action<int> OnGearChanged;


        /// <summary>Current gear index (0-based), public for legacy consumers.</summary>
        [HideInInspector] public int gearNum = 1;

        /// <summary>True when rear wheel slip should spawn drift smoke VFX.</summary>
        [HideInInspector] public bool playPauseSmoke = false;

        /// <summary>True when the vehicle has finished a race/event.</summary>
        [HideInInspector] public bool hasFinished;

        /// <summary>Current forward speed in km/h.</summary>
        [HideInInspector] public float currentSpeed;

        /// <summary>Current engine RPM value.</summary>
        [HideInInspector] public float currentEngineRPM;

        /// <summary>True when the car is in reverse state.</summary>
        [HideInInspector] public bool reverse = false;

        /// <summary>Current nitrous charge value.</summary>
        [HideInInspector] public float nitrusValue;

        /// <summary>True while nitrous is actively being used.</summary>
        [HideInInspector] public bool nitrusFlag = false;

        // wobble 관련
        // RPM Wobble 상태 제어용

        /// <summary>True while the short RPM wobble effect is active after shifting.</summary>
        private bool rpmWobbleActive = false;

        /// <summary>Elapsed time of the current RPM wobble.</summary>
        private float rpmWobbleTimer = 0f;

        /// <summary>Total duration of the RPM wobble effect (seconds).</summary>
        [SerializeField] private float rpmWobbleDuration = 0.35f;     // 전체 지속 시간

        /// <summary>Amplitude of the RPM wobble (±RPM).</summary>
        [SerializeField] private float rpmWobbleStrength = 250f;       // 진폭 (±RPM)

        /// <summary>Number of sine oscillations during the wobble window.</summary>
        [SerializeField] private int rpmWobbleOscillations = 3;        // 진동 횟수 (sin 파형 수)

        /// <summary>Smoothing time used in RPM/engine damping.</summary>
        private float smoothTime = 0.09f;


        /// <summary>WheelColliders array (FL, FR, RL, RR expected).</summary>
        public WheelCollider[] wheels = new WheelCollider[4];

        /// <summary>Visual wheel meshes aligned to WheelColliders.</summary>
        public GameObject[] wheelMesh = new GameObject[4];

        /// <summary>Transform used to set the rigidbody center of mass.</summary>
        public GameObject centerOfMass;

        /// <summary>Cached rigidbody for physics calculations.</summary>
        private Rigidbody rigidbody;


        //car Shop Values
        /// <summary>Car price in shop/career modes.</summary>
        public int carPrice;

        /// <summary>Display name of the car.</summary>
        public string carName;



        // 롤 억제 강도
        /// <summary>Front axle anti-roll strength.</summary>
        public float __antiRollFront = 10000f;

        /// <summary>Rear axle anti-roll strength.</summary>
        public float __antiRollRear = 10000f;

        // 과도한 롤 각속도 감쇠
        /// <summary>Additional roll damping factor (angular damping helper).</summary>
        public float __rollDamping = 2.0f; // 1.5~3.0 사이 시도

        [Header("G-Force")]

        /// <summary>Instantaneous G-force estimation (in g).</summary>
        public float gforce;

        /// <summary>Velocity from last fixed step, used to compute acceleration.</summary>
        private Vector3 lastVelocity;

        /// <summary>Gravity constant used for g-force normalization.</summary>
        private float gravity = 9.81f;


        /// <summary>Maximum steering angle at low speed (degrees).</summary>
        [SerializeField] private float maxSteerAngle = 35f;   // 저속에서 최대 조향각

        /// <summary>Minimum steering angle at high speed (degrees).</summary>
        [SerializeField] private float minSteerAngle = 8f;    // 고속에서 최소 조향각

        /// <summary>Speed in km/h at which steering reaches min angle.</summary>
        [SerializeField] private float maxSteerSpeed = 200f;  // km/h 기준, 이 속도에서 minSteerAngle로 수렴

        // --- Braking stability ---
        /// <summary>Maximum base brake torque (low-speed baseline).</summary>
        [SerializeField] private float brakeTorqueMax = 3000f; // 최고 제동 토크(저속 기준)

        /// <summary>Front brake distribution (0..1).</summary>
        [SerializeField, Range(0f, 1f)] private float frontBrakeBias = 0.65f; // 앞바퀴 비중

        /// <summary>Enables ABS logic if true.</summary>
        [SerializeField] private bool absEnabled = true;

        /// <summary>Forward slip threshold beyond which ABS reduces torque.</summary>
        [SerializeField] private float absSlip = 0.35f;         // 이 이상이면 잠김으로 판단

        /// <summary>ABS release rate (N·m/s) when wheel is locking.</summary>
        [SerializeField] private float absReleaseRate = 8000f;  // 잠길 때 줄이는 속도 (N·m/s)

        /// <summary>ABS apply rate (N·m/s) when traction is regained.</summary>
        [SerializeField] private float absApplyRate = 6000f;    // 풀렸을 때 다시 올리는 속도

        // 내부 상태 (휠별 현재 브레이크 토크 저장)
        /// <summary>Per-wheel brake torque state used by ABS (internal).</summary>
        private float[] __brakeTorque = new float[4];

        // 다운 포스
        /// <summary>Downforce coefficient applied along -up direction.</summary>
        public float downForceValue = 10f;

        private WheelFrictionCurve forwardFriction, sidewaysFriction;
        private float radius = 6, brakPower = 0, wheelsRPM, driftFactor, lastValue, horizontal, vertical, totalPower;
        private bool flag = false;

        // 내부 준비 상태 플래그 (필드명/시그니처 영향 없음)
        private bool __ready;


        /// <summary>
        /// Initializes core references, starts periodic tasks, hooks gear-change wobble effect,
        /// and validates required components (engine curve, wheels, rigidbody).
        /// </summary>
        private void Awake()
        {
            GetObjects();
            StartCoroutine(TimedLoop());

            OnGearChanged += StartRpmWobble;

            __ready = true;

            if (enginePower == null)
            {
                enginePower = new AnimationCurve(
                    new Keyframe(1000, 50),
                    new Keyframe(3000, 120),
                    new Keyframe(5000, 100),
                    new Keyframe(6500, 60)
                );
                Debug.LogWarning("[CarController] enginePower is null. Applied a temporary default curve.", this);
            }

            if (wheels == null || wheels.Length < 4 || wheels[0] == null || wheels[1] == null || wheels[2] == null || wheels[3] == null)
            {
                Debug.LogError("[CarController] WheelCollider references missing. Check Inspector.", this);
                __ready = false;
            }

            if (rigidbody == null)
            {
                rigidbody = GetComponent<Rigidbody>();
                if (rigidbody == null)
                {
                    Debug.LogError("[CarController] Rigidbody missing.", this);
                    __ready = false;
                }
            }

            lastVelocity = rigidbody.velocity;
        }


        /// <summary>
        /// Polls input values (if available), caches previous RPM, and runs
        /// automatic/manual shifters each frame. Physics-affecting work is deferred to FixedUpdate().
        /// </summary>
        private void Update()
        {
            if (inputManager != null)
            {
                horizontal = inputManager.horizontal;
                vertical = inputManager.vertical;
            }
            else
            {
                horizontal = 0f;
                vertical = 0f;
            }

            lastValue = currentEngineRPM;

            AutomaticShifter();
            ManualShifter();
        }


        /// <summary>
        /// Runs physics-related updates at fixed timestep: downforce, wheel animation,
        /// steering, engine power, G-force computation, anti-roll, traction, and nitrous.
        /// Skips traction/nitrous for AI-tagged cars.
        /// </summary>
        private void FixedUpdate()
        {
            if (!__ready) return;

            AddDownForce();
            AnimateWheels();
            SteerVehicle();
            CalculateEnginePower();
            CalculateGForce();
            ApplyAntiRoll();

            if (gameObject.tag == "AI") return;
            AdjustTraction();
            ActivateNitrus();
        }

        
        /// <summary>
        /// Unsubscribes wobble handler from the gear-changed event when disabled.
        /// </summary>
        void OnDisable()
        {
            OnGearChanged -= StartRpmWobble;
        }


        /// <summary>
        /// Calculates wheels' average RPM, updates engine RPM with smoothing and clamping,
        /// applies gear ratios and final drive, and advances the vehicle via MoveVehicle().
        /// Also applies a short-lived RPM wobble effect right after gear changes.
        /// </summary>
        private void CalculateEnginePower()
        {
            WheelRPM();

            if (Mathf.Abs(vertical) > 0.001f)
            {
                rigidbody.drag = 0.005f;
            }
            else
            {
                rigidbody.drag = 0.1f;
            }

            int gi = Mathf.Clamp(gearNum, 0, (gears != null && gears.Length > 0) ? gears.Length - 1 : 0);
            float gearRatio = (gears != null && gears.Length > 0) ? gears[gi] : 1f;

            totalPower = gearRatio * finalDrive * enginePower.Evaluate(currentEngineRPM) * vertical;

            float velocity = 0.0f;

            if (currentEngineRPM >= maxRPM || flag)
            {
                currentEngineRPM = Mathf.SmoothDamp(currentEngineRPM, maxRPM - 500, ref velocity, 0.05f);
                flag = (currentEngineRPM >= maxRPM - 450);
                test = (lastValue > currentEngineRPM);
            }
            else
            {
                float targetRPM = 1000 + (Mathf.Abs(wheelsRPM) * 3.6f * gearRatio);
                currentEngineRPM = Mathf.SmoothDamp(currentEngineRPM, targetRPM, ref velocity, smoothTime);
                test = false;
            }

            if (currentEngineRPM >= maxRPM + 1000)
                currentEngineRPM = maxRPM + 1000;

            if (rpmWobbleActive)
            {
                rpmWobbleTimer += Time.deltaTime;
                float t = rpmWobbleTimer / rpmWobbleDuration;

                if (t >= 1f)
                {
                    rpmWobbleActive = false;
                }
                else
                {
                    float wobble = Mathf.Sin(Mathf.PI * rpmWobbleOscillations * t) * (1f - t) * rpmWobbleStrength;
                    currentEngineRPM += wobble;
                }
            }

            MoveVehicle();
        }


        /// <summary>
        /// Computes the average RPM of all grounded wheel colliders and updates reverse state
        /// transitions, notifying the GameManager if needed.
        /// </summary>
        private void WheelRPM()
        {
            float sum = 0;
            int R = 0;
            for (int i = 0; i < 4; i++)
            {
                if (wheels[i] == null) continue;
                sum += wheels[i].rpm;
                R++;
            }
            wheelsRPM = (R != 0) ? sum / R : 0;

            if (wheelsRPM < 0 && !reverse)
            {
                reverse = true;
                if (gameObject.tag != "AI" && manager != null) manager.ChangeGear();
            }
            else if (wheelsRPM > 0 && reverse)
            {
                reverse = false;
                if (gameObject.tag != "AI" && manager != null) manager.ChangeGear();
            }
        }


        /// <summary>
        /// Validates whether current speed satisfies the threshold for the current gear
        /// (when gearChangeSpeed is provided).
        /// </summary>
        /// <returns>True if shifting is allowed with respect to speed thresholds; otherwise false.</returns>
        private bool CheckGears()
        {
            int gi = Mathf.Clamp(
                gearNum,
                0,
                (gearChangeSpeed != null && gearChangeSpeed.Length > 0)
                    ? gearChangeSpeed.Length - 1
                    : 0
            );
            if (gearChangeSpeed == null || gearChangeSpeed.Length == 0) return true;
            return currentSpeed >= gearChangeSpeed[gi];
        }


        /// <summary>
        /// Handles automatic up/down shifting when grounded, using shift/min RPM thresholds
        /// and speed checks. Aborts when manual mode is active.
        /// </summary>
        private void AutomaticShifter()
        {
            // 땅에 닿지 않은 경우 시프트 불가
            if (!IsGrounded()) return;

            if (!isAutomatic) return;


            if (currentEngineRPM > shiftRpm && gearNum < ((gears != null) ? gears.Length : 1) - 1 && !reverse && CheckGears())
            {
                GearUp();
                return;
            }

            if (currentEngineRPM < minRPM && gearNum > 0)
            {
                GearDown();
            }
        }


        /// <summary>
        /// Handles manual shifting when grounded with debounce (cooldown) protection:
        /// E = gear up, Q = gear down. Aborts when automatic mode is active.
        /// </summary>
        private void ManualShifter()
        {
            if (isAutomatic) return;

            if (!IsGrounded()) return;

            if (Time.time - lastShiftTime < gearShiftCooldown) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (gears != null && gearNum < gears.Length - 1)
                {
                    GearUp();
                }
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                if (gearNum > 0)
                {
                    GearDown();
                }
            }
        }


        /// <summary>
        /// Increments the gear index, registers the shift time, invokes wobble and
        /// notifies GameManager for UI/FX updates.
        /// </summary>
        private void GearUp()
        {
            gearNum++;
            lastShiftTime = Time.time;

            OnGearChanged?.Invoke(gearNum);

            if (gameObject.tag != "AI" && manager != null)
                manager.ChangeGear();
        }


        /// <summary>
        /// Decrements the gear index, registers the shift time, invokes wobble and
        /// notifies GameManager for UI/FX updates.
        /// </summary>
        private void GearDown()
        {
            gearNum--;
            lastShiftTime = Time.time;

            OnGearChanged?.Invoke(gearNum);

            if (gameObject.tag != "AI" && manager != null)
                manager.ChangeGear();
        }


        /// <summary>
        /// Starts a short RPM wobble effect used to accent gear shift transitions.
        /// </summary>
        /// <param name="newGear">The gear index after shifting.</param>
        public void StartRpmWobble(int newGear)
        {
            rpmWobbleActive = true;
            rpmWobbleTimer = 0f;
        }


        /// <summary>
        /// Returns whether all four wheels are grounded (defensive null checks included).
        /// </summary>
        /// <returns>True if every wheel is grounded; otherwise false.</returns>
        private bool IsGrounded()
        {
            if (wheels == null || wheels.Length < 4 || wheels[0] == null || wheels[1] == null || wheels[2] == null || wheels[3] == null)
                return false;
            if (wheels[0].isGrounded && wheels[1].isGrounded && wheels[2].isGrounded && wheels[3].isGrounded)
                return true;
            else
                return false;
        }


        /// <summary>
        /// Moves the vehicle by applying motor torque and brake torque
        /// to wheels according to drivetrain configuration (FWD/RWD/AWD).
        /// </summary>
        private void MoveVehicle()
        {

            BrakeVehicle();

            if (drive == driveType.allWheelDrive)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] == null) continue;
                    wheels[i].motorTorque = totalPower / 4f;
                }
                ApplyBrakeTorquesToWheels();
            }
            else if (drive == driveType.rearWheelDrive)
            {
                if (wheels[2] != null) wheels[2].motorTorque = totalPower / 2f;
                if (wheels[3] != null) wheels[3].motorTorque = totalPower / 2f;

                ApplyBrakeTorquesToWheels(); 
            }
            else // FWD
            {
                if (wheels[0] != null) wheels[0].motorTorque = totalPower / 2f;
                if (wheels[1] != null) wheels[1].motorTorque = totalPower / 2f;

                ApplyBrakeTorquesToWheels(); 
            }

            if (rigidbody != null)
                currentSpeed = rigidbody.velocity.magnitude * 3.6f;
        }


        /// <summary>
        /// Applies the computed brake torque values (per wheel) to the WheelColliders.
        /// This replaces the previous uniform brake assignment for more responsive braking.
        /// </summary>
        void ApplyBrakeTorquesToWheels()
        {
            if (wheels == null || wheels.Length < 4) return;
            if (wheels[0] != null) wheels[0].brakeTorque = __brakeTorque[0];
            if (wheels[1] != null) wheels[1].brakeTorque = __brakeTorque[1];
            if (wheels[2] != null) wheels[2].brakeTorque = __brakeTorque[2];
            if (wheels[3] != null) wheels[3].brakeTorque = __brakeTorque[3];
        }


        /// <summary>
        /// Computes brake demand from input, applies speed-based torque capping,
        /// distributes front/rear brake bias, and updates per-wheel brake torques via ABS.
        /// </summary>
        private void BrakeVehicle()
        {
            float brakeInput = Mathf.Clamp01(-vertical); // 0~1

            float speedT = Mathf.Clamp01(currentSpeed / 200f);
            float maxTorqueNow = Mathf.Lerp(brakeTorqueMax, brakeTorqueMax * 0.6f, speedT);

            if (brakeInput > 0f) brakPower = (currentSpeed >= 10f) ? 500f : 50f;
            else if (Mathf.Abs(vertical) <= 0.001f && Mathf.Abs(currentSpeed) <= 10f) brakPower = 10f;
            else brakPower = 0f;

            float frontTarget = brakeInput * maxTorqueNow * frontBrakeBias;
            float rearTarget = brakeInput * maxTorqueNow * (1f - frontBrakeBias);

            if (wheels.Length >= 4)
            {
                if (wheels[0] != null) __brakeTorque[0] = ApplyABS(0, frontTarget);
                if (wheels[1] != null) __brakeTorque[1] = ApplyABS(1, frontTarget);
                if (wheels[2] != null) __brakeTorque[2] = ApplyABS(2, rearTarget);
                if (wheels[3] != null) __brakeTorque[3] = ApplyABS(3, rearTarget);
            }
        }


        /// <summary>
        /// Applies steering angles to the front wheels using a speed-sensitive model.
        /// Uses a runtime-estimated wheelbase and track width to approximate Ackermann geometry, ensuring the inner wheel steers at a greater angle than the outer wheel during turns.
        /// Includes safeguards against division by zero and incorrect sign handling.
        /// </summary>
        private void SteerVehicle()
        {
            if (wheels == null || wheels.Length < 2 || wheels[0] == null || wheels[1] == null) return;

            float steer = GetCurrentSteerAngle();
            if (Mathf.Abs(steer) < 0.0001f)
            {
                wheels[0].steerAngle = 0f;
                wheels[1].steerAngle = 0f;
                return;
            }

            float wb;
            if (wheels.Length >= 4 && wheels[2] != null && wheels[3] != null)
            {
                Vector3 front = 0.5f * (wheels[0].transform.position + wheels[1].transform.position);
                Vector3 rear  = 0.5f * (wheels[2].transform.position + wheels[3].transform.position);
                wb = Vector3.Distance(front, rear);
                if (wb < 0.1f) wb = 2.55f;
            }
            else wb = 2.55f;

            float tw = 1.5f;
            float sign = Mathf.Sign(steer);
            float scale = steer / maxSteerAngle;

            float denomL = radius + sign * (tw * 0.5f);
            float denomR = radius - sign * (tw * 0.5f);

            float angleL = Mathf.Rad2Deg * Mathf.Atan(wb / Mathf.Max(0.001f, denomL)) * scale;
            float angleR = Mathf.Rad2Deg * Mathf.Atan(wb / Mathf.Max(0.001f, denomR)) * scale;

            wheels[0].steerAngle = angleL;
            wheels[1].steerAngle = angleR;
        }


        /// <summary>
        /// Updates the visual wheel meshes' positions and rotations from their WheelColliders.
        /// </summary>
        private void AnimateWheels()
        {
            if (wheels == null || wheelMesh == null) return;

            Vector3 wheelPosition = Vector3.zero;
            Quaternion wheelRotation = Quaternion.identity;

            for (int i = 0; i < 4; i++)
            {
                if (wheels.Length <= i || wheels[i] == null) continue;
                wheels[i].GetWorldPose(out wheelPosition, out wheelRotation);
                if (wheelMesh.Length > i && wheelMesh[i] != null)
                {
                    wheelMesh[i].transform.position = wheelPosition;
                    wheelMesh[i].transform.rotation = wheelRotation;
                }
            }
        }


        /// <summary>
        /// Locates and caches component references (InputManager, GameManager, CarVisualEffects, Rigidbody)
        /// and applies the configured center of mass to the rigidbody.
        /// </summary>
        private void GetObjects()
        {
            inputManager = GetComponent<InputManager>();

            var gameManager = GameObject.FindGameObjectWithTag("GameManager");
            if (gameManager != null)
            {
                manager = gameManager.GetComponent<GameManager>();
            } 

            carVisualEffects = GetComponent<CarVisualEffects>();
            if (rigidbody == null)
            {
                rigidbody = GetComponent<Rigidbody>();
            }

            rigidbody.centerOfMass = centerOfMass.transform.localPosition;
        }


        /// <summary>
        /// Applies aerodynamic downforce proportional to speed.
        /// </summary>
        private void AddDownForce()
        {
            if (rigidbody == null) return;
            rigidbody.AddForce(-transform.up * downForceValue * rigidbody.velocity.magnitude, ForceMode.Force);
        }


        /// <summary>
        /// Adjusts wheel friction curves for drifting when handbrake is active,
        /// otherwise restores grip based on speed. Emits smoke when slip exceeds thresholds.
        /// </summary>
        private void AdjustTraction()
        {
            if (wheels == null || wheels.Length < 4 || wheels[0] == null) return;

            //time it takes to go from normal drive to drift 
            float driftSmothFactor = .7f * Time.fixedDeltaTime;

            if (inputManager != null && inputManager.handbrake)
            {
                sidewaysFriction = wheels[0].sidewaysFriction;
                forwardFriction = wheels[0].forwardFriction;

                float velocity = 0;
                float target = driftFactor * handBrakeFrictionMultiplier;
                float newVal = Mathf.SmoothDamp(forwardFriction.asymptoteValue, target, ref velocity, driftSmothFactor);

                sidewaysFriction.extremumValue = sidewaysFriction.asymptoteValue =
                forwardFriction.extremumValue = forwardFriction.asymptoteValue = newVal;

                for (int i = 0; i < 4; i++)
                {
                    if (wheels[i] == null) continue;
                    wheels[i].sidewaysFriction = sidewaysFriction;
                    wheels[i].forwardFriction = forwardFriction;
                }

                sidewaysFriction.extremumValue = sidewaysFriction.asymptoteValue =
                forwardFriction.extremumValue = forwardFriction.asymptoteValue = 1.1f;
                //extra grip for the front wheels
                for (int i = 0; i < 2; i++)
                {
                    if (wheels[i] == null) continue;
                    wheels[i].sidewaysFriction = sidewaysFriction;
                    wheels[i].forwardFriction = forwardFriction;
                }
                if (rigidbody != null)
                    rigidbody.AddForce(transform.forward * (currentSpeed / 400f) * 10000f, ForceMode.Force);
            }
            //executed when handbrake is NOT being held
            else
            {

                forwardFriction = wheels[0].forwardFriction;
                sidewaysFriction = wheels[0].sidewaysFriction;

                float grip = ((currentSpeed * handBrakeFrictionMultiplier) / 300f) + 1f;

                forwardFriction.extremumValue = forwardFriction.asymptoteValue =
                sidewaysFriction.extremumValue = sidewaysFriction.asymptoteValue = grip;

                for (int i = 0; i < 4; i++)
                {
                    if (wheels[i] == null) continue;
                    wheels[i].forwardFriction = forwardFriction;
                    wheels[i].sidewaysFriction = sidewaysFriction;
                }
            }

            //checks the amount of slip to control the drift (rear wheels)
            for (int i = 2; i < 4; i++)
            {
                if (wheels[i] == null) continue;

                WheelHit wheelHit;
                bool gotHit = wheels[i].GetGroundHit(out wheelHit);
                if (!gotHit) { playPauseSmoke = false; continue; }

                //smoke
                if (Mathf.Abs(wheelHit.sidewaysSlip) >= 0.3f || Mathf.Abs(wheelHit.forwardSlip) >= .3f)
                    playPauseSmoke = true;
                else
                    playPauseSmoke = false;

                if (wheelHit.sidewaysSlip < 0) driftFactor = (1 + -horizontal) * Mathf.Abs(wheelHit.sidewaysSlip);
                if (wheelHit.sidewaysSlip > 0) driftFactor = (1 + horizontal) * Mathf.Abs(wheelHit.sidewaysSlip);
            }
        }


        /// <summary>
        /// Periodically updates the turning radius helper based on current speed.
        /// </summary>
        /// <returns>Coroutine enumerator.</returns>
        private IEnumerator TimedLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(.7f);
                radius = 6 + currentSpeed / 20;
            }
        }


        /// <summary>
        /// Applies anti-roll forces per axle by comparing suspension compression on left/right wheels,
        /// reducing body roll during cornering.
        /// </summary>
        private void ApplyAntiRoll()
        {
            if (wheels == null || wheels.Length < 4 || wheels[0] == null || wheels[1] == null || wheels[2] == null || wheels[3] == null) return;

            void ComputeAxis(int leftIndex, int rightIndex, float antiRollStrength)
            {
                var wL = wheels[leftIndex];
                var wR = wheels[rightIndex];

                float travelL = 1.0f;
                float travelR = 1.0f;
                bool groundedL = false, groundedR = false;

                WheelHit hit;

                if (wL.GetGroundHit(out hit))
                {
                    groundedL = true;

                    travelL = (-wL.transform.InverseTransformPoint(hit.point).y - wL.radius) / wL.suspensionDistance;
                }
                if (wR.GetGroundHit(out hit))
                {
                    groundedR = true;
                    travelR = (-wR.transform.InverseTransformPoint(hit.point).y - wR.radius) / wR.suspensionDistance;
                }


                float antiRollForce = (travelL - travelR) * antiRollStrength;

                if (groundedL)
                    rigidbody.AddForceAtPosition(wL.transform.up * -antiRollForce, wL.transform.position, ForceMode.Force);
                if (groundedR)
                    rigidbody.AddForceAtPosition(wR.transform.up * antiRollForce, wR.transform.position, ForceMode.Force);
            }


            ComputeAxis(0, 1, __antiRollFront); // front: wheels[0]=FL, [1]=FR
            ComputeAxis(2, 3, __antiRollRear);  // rear: wheels[2]=RL, [3]=RR
        }


        /// <summary>
        /// Computes the current steering angle considering speed sensitivity and braking input
        /// (reduced steering under heavy braking).
        /// </summary>
        /// <returns>Steering angle in degrees to be applied to front wheels.</returns>
        private float GetCurrentSteerAngle()
        {
            float t = Mathf.Clamp01(currentSpeed / maxSteerSpeed);
            float currentMax = Mathf.Lerp(maxSteerAngle, minSteerAngle, t);

            // 브레이크 인풋에 따라 스티어 줄이기
            float brakeInput = Mathf.Clamp01(-vertical);
            float brakeScale = Mathf.Lerp(1f, 0.7f, brakeInput);

            return currentMax * brakeScale * horizontal;
        }


        /// <summary>
        /// Simple ABS controller per wheel: lowers brake torque quickly on lock (high slip),
        /// then restores toward target when traction returns.
        /// </summary>
        /// <param name="i">Wheel index (0..3).</param>
        /// <param name="targetTorque">Desired brake torque for this wheel.</param>
        /// <returns>Adjusted brake torque after ABS logic.</returns>
        private float ApplyABS(int i, float targetTorque)
        {
            if (!absEnabled || wheels[i] == null) return targetTorque;

            WheelHit hit;
            bool grounded = wheels[i].GetGroundHit(out hit);
            float current = __brakeTorque[i];

            if (grounded && Mathf.Abs(hit.forwardSlip) > absSlip)
            {
                current = Mathf.MoveTowards(current, 0f, absReleaseRate * Time.fixedDeltaTime);
            }
            else
            {
                current = Mathf.MoveTowards(current, targetTorque, absApplyRate * Time.fixedDeltaTime);
            }

            __brakeTorque[i] = Mathf.Max(0f, current);
            return __brakeTorque[i];
        }


        /// <summary>
        /// Charges nitrous when not boosting; consumes it while boosting to add forward force
        /// and trigger nitrous VFX if available.
        /// </summary>
        public void ActivateNitrus()
        {
            if (inputManager == null || rigidbody == null) return;

            if (!inputManager.boosting && nitrusValue <= 10)
            {
                nitrusValue += Time.fixedDeltaTime / 2f;
            }
            else
            {
                nitrusValue -= (nitrusValue <= 0) ? 0 : Time.fixedDeltaTime;
            }

            if (inputManager.boosting)
            {
                if (nitrusValue > 0)
                {
                    if (carVisualEffects != null) carVisualEffects.startNitrusEmitter();
                    rigidbody.AddForce(transform.forward * 5000, ForceMode.Force);
                }
                else if (carVisualEffects != null) carVisualEffects.stopNitrusEmitter();
            }
            else if (carVisualEffects != null) carVisualEffects.stopNitrusEmitter();
        }


        /// <summary>
        /// Calculates current G-force from rigidbody acceleration magnitude.
        /// </summary>
        private void CalculateGForce()
        {
            Vector3 deltaV = rigidbody.velocity - lastVelocity;
            float acceleration = deltaV.magnitude / Time.fixedDeltaTime;

            gforce = acceleration / gravity;

            lastVelocity = rigidbody.velocity;
        }
        

        /// <summary>
        /// Returns the raw steering input from the InputManager, or 0 if unavailable.
        /// </summary>
        /// <returns>Steering input in range [-1, +1].</returns>
        public float GetSteerInput()
        {
            return inputManager != null ? inputManager.horizontal : 0f;  // -1(Left) ~ +1(Right)
        }
    }
}
