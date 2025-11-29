using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class AIController : MonoBehaviour
    {
        internal enum driveType
        {
            frontWheelDrive,
            rearWheelDrive,
            allWheelDrive
        }
        [SerializeField] private driveType drive;
        [SerializeField] private bool useAiInputProvider = true;
        [SerializeField] private AiInputProvider aiInputProvider; // 같은 오브젝트에 달린 Provider 


        //other classes ->

        private CarVisualEffects CarEffects;

        [Header("DEBUG")]
        public bool test;

        [Header("Variables")]
        public float handBrakeFrictionMultiplier = 2f;
        public float maxRPM, minRPM;
        public float[] gears;
        public float[] gearChangeSpeed;
        public int gearNum = 0;
        public AnimationCurve enginePower;


        [HideInInspector] public bool playPauseSmoke = false;
        [HideInInspector] public float KPH;
        [HideInInspector] public float engineRPM;
        [HideInInspector] public bool reverse = false;
        [HideInInspector] public float nitrusValue;
        [HideInInspector] public bool nitrusFlag = false;
        public float totalPower;
        private float driftFactor;
        private float wheelsRPM;
        private GameObject wheelMeshes, wheelColliders;
        [SerializeField] private WheelCollider[] wheels = new WheelCollider[4];
        [SerializeField] private GameObject[] wheelMesh = new GameObject[4];
        [SerializeField] private Transform centerOfMass;
        [SerializeField] private float brakeTorqueMax = 3000f;
        private Rigidbody rigidbody;

        //car Shop Values
        public int carPrice;
        public string carName;
        public float smoothTime = 0.08f;

        [HideInInspector] public float horizontal, vertical;
        private WheelFrictionCurve forwardFriction, sidewaysFriction;
        private float radius = 6, brakPower = 0, DownForceValue = 100f, lastValue;
        private bool flag = false;

        private void Awake()
        {

            //if (SceneManager.GetActiveScene().name == "awakeScene") return;

            if (useAiInputProvider)
            {
                aiInputProvider = GetComponent<AiInputProvider>() ?? gameObject.AddComponent<AiInputProvider>();
                if (waypoints != null) aiInputProvider.Bind(waypoints);
            }

            getObjects();
            StartCoroutine(timedLoop());
        }

        private void FixedUpdate()
        {

            // 1) 입력 결정
            if (useAiInputProvider && aiInputProvider != null)
            {
                // Provider가 계산해둔 값을 그대로 채택
                horizontal = aiInputProvider.horizontal;
                vertical = aiInputProvider.vertical;
            }
            else
            {
                // Provider를 안 쓰는 경우: 기존 내부 AIDrive 로직 사용
                AIDrive(); // (여기에 CalculateDistanceOfWaypoints/Steering/vertical=acceleration 가 들어있죠)
            }

            lastValue = engineRPM;

            WheelRPM();


            AddDownForce();
            AnimateWheels();
            SteerVehicle();
            CalculateEnginePower();
            MoveVehicle();
        }
        // AI 

        public TrackWaypoints waypoints;
        public Transform currentWaypoint;
        public List<Transform> nodes = new List<Transform>();
        [Range(0, 10)] public int distanceOffset = 5;
        [Range(0, 5)] public float sterrForce = 1;
        [Range(0, 1)] public float acceleration = 0.5f;





        private void AIDrive()
        {
            if (gameObject.tag == "AI")
            {
                CalculateDistanceOfWaypoints();
                AISteer();

                vertical = acceleration;
                //if (Input.GetKey(KeyCode.LeftShift)) boosting = true; else boosting = false;
                // handbrake = (Input.GetAxis("Jump") != 0) ? true : false;
            }
        }

        public void Bind(AiInputProvider input)       // ← RSM에서 주입할 때 호출
        {
            aiInputProvider = input;
        }

        private void WheelRPM()
        {
            float sum = 0f; int count = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                sum += wheels[i].rpm;
                count++;
            }
            wheelsRPM = (count > 0) ? (sum / count) : 0f;
        }


        private void CalculateDistanceOfWaypoints()
        {
            Vector3 position = gameObject.transform.position;
            float distance = Mathf.Infinity;

            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 difference = nodes[i].transform.position - position;
                float currentDistance = difference.magnitude;
                if (currentDistance < distance)
                {
                    if ((i + distanceOffset) >= nodes.Count)
                    {
                        currentWaypoint = nodes[1];
                        distance = currentDistance;
                    }
                    else
                    {
                        currentWaypoint = nodes[i + distanceOffset];
                        distance = currentDistance;
                    }
                }


            }
        }


        private void AISteer()
        {

            Vector3 relative = transform.InverseTransformPoint(currentWaypoint.transform.position);
            relative /= relative.magnitude;

            horizontal = (relative.x / relative.magnitude) * sterrForce;

        }


        private void CalculateEnginePower()
        {
            // 드래그
            rigidbody.drag = (Mathf.Abs(vertical) > 0.001f) ? 0.005f : 0.1f;

            // 엔진 커브 방어
            if (enginePower == null || enginePower.length == 0)
            {
                enginePower = new AnimationCurve(
                    new Keyframe(1000, 50),
                    new Keyframe(3000, 120),
                    new Keyframe(5000, 100),
                    new Keyframe(6500, 60)
                );
            }

            // 안전한 기어 인덱스/기어비
            int gi = (gears != null && gears.Length > 0)
                ? Mathf.Clamp(gearNum, 0, gears.Length - 1)
                : 0;
            float gearRatio = (gears != null && gears.Length > 0) ? gears[gi] : 1f;

            // 엔진 RPM 목표치
            float velocity = 0.0f;
            float targetRpm = 1000f + (Mathf.Abs(wheelsRPM) * 3.6f * gearRatio);

            if (engineRPM >= maxRPM || flag)
            {
                engineRPM = Mathf.SmoothDamp(engineRPM, maxRPM - 500f, ref velocity, 0.05f);
                flag = (engineRPM >= maxRPM - 450f);
                test = (lastValue > engineRPM);
            }
            else
            {
                engineRPM = Mathf.SmoothDamp(engineRPM, targetRpm, ref velocity, smoothTime);
                test = false;
            }
            if (engineRPM >= maxRPM + 1000f) engineRPM = maxRPM + 1000f;

            // ★ 토크 계산을 조금 키우고(기어비 반영), 실제로 굴러가게
            //    필요시 finalDrive 같은 계수 추가해보세요 (예: 4.0f)
            float finalDrive = 4.0f;
            totalPower = enginePower.Evaluate(engineRPM) * gearRatio * finalDrive * Mathf.Clamp01(vertical);

            MoveVehicle();
            Shifter();
        }


        private bool CheckGears()
        {
            if (gearChangeSpeed == null || gearChangeSpeed.Length == 0) return true;
            int gearIndex = Mathf.Clamp(gearNum, 0, gearChangeSpeed.Length - 1);
            return KPH >= gearChangeSpeed[gearIndex];
        }

        private void Shifter()
        {
            if (!IsGrounded()) return;

            int maxGearIndex = (gears != null && gears.Length > 0) ? (gears.Length - 1) : 0;

            if (engineRPM > maxRPM && gearNum < maxGearIndex && !reverse && CheckGears())
            {
                gearNum++;
            }
            else if (engineRPM < minRPM && gearNum > 0)
            {
                gearNum--;
            }

            // 혹시라도 범위 이탈 방지
            gearNum = Mathf.Clamp(gearNum, 0, maxGearIndex);
        }

        private bool IsGrounded()
        {
            if (wheels[0].isGrounded && wheels[1].isGrounded && wheels[2].isGrounded && wheels[3].isGrounded)
                return true;
            else
                return false;
        }

        private void MoveVehicle()
        {

            brakeVehicle();

            if (drive == driveType.allWheelDrive)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    wheels[i].motorTorque = totalPower / 4;
                    wheels[i].brakeTorque = brakPower;
                }
            }
            else if (drive == driveType.rearWheelDrive)
            {
                wheels[2].motorTorque = totalPower / 2;
                wheels[3].motorTorque = totalPower / 2;

                for (int i = 0; i < wheels.Length; i++)
                {
                    wheels[i].brakeTorque = brakPower;
                }
            }
            else
            {
                wheels[0].motorTorque = totalPower / 2;
                wheels[1].motorTorque = totalPower / 2;

                for (int i = 0; i < wheels.Length; i++)
                {
                    wheels[i].brakeTorque = brakPower;
                }
            }

            KPH = rigidbody.velocity.magnitude * 3.6f;


        }

        private void brakeVehicle()
        {
            // vertical < 0 일 때만 제동 강도 산출 (0~1)
            float brakeInput = Mathf.Clamp01(-vertical);

            // 속도가 높을수록 조금 더 강하게 (옵션)
            float speedT = Mathf.Clamp01(KPH / 200f);

            float torque = brakeTorqueMax * Mathf.Lerp(0.7f, 1.0f, speedT) * brakeInput;

            // 아주 저속에선 살짝만 잡기
            if (KPH < 8f) torque *= 0.25f;

            brakPower = torque;
        }

        private void SteerVehicle()
        {


            //acerman steering formula
            //steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius + (1.5f / 2))) * horizontalInput;

            if (horizontal > 0)
            {
                //rear tracks size is set to 1.5f       wheel base has been set to 2.55f
                wheels[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius + (1.5f / 2))) * horizontal;
                wheels[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius - (1.5f / 2))) * horizontal;
            }
            else if (horizontal < 0)
            {
                wheels[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius - (1.5f / 2))) * horizontal;
                wheels[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius + (1.5f / 2))) * horizontal;
                //transform.Rotate(Vector3.up * steerHelping);

            }
            else
            {
                wheels[0].steerAngle = 0;
                wheels[1].steerAngle = 0;
            }

        }

        private void AnimateWheels()
        {
            Vector3 wheelPosition = Vector3.zero;
            Quaternion wheelRotation = Quaternion.identity;

            for (int i = 0; i < 4; i++)
            {
                if (i >= wheels.Length || wheels[i] == null) continue;
                wheels[i].GetWorldPose(out wheelPosition, out wheelRotation);

                if (i < wheelMesh.Length && wheelMesh[i] != null)
                {
                    wheelMesh[i].transform.position = wheelPosition;
                    wheelMesh[i].transform.rotation = wheelRotation;
                }
            }
        }
        
         // RSM에서 웨이포인트 주입할 때 같이 호출하면 편함
        public void BindWaypoints(TrackWaypoints tw)
        {
            waypoints = tw;
            nodes = (tw != null) ? tw.nodes : new List<Transform>();
            if (useAiInputProvider && aiInputProvider != null) aiInputProvider.Bind(tw);
        }

        private void getObjects()
        {
            rigidbody = GetComponent<Rigidbody>();

            // 1) 내 자식에서 탐색 (프리팹 구조: wheelColliders/0..3, wheelMeshes/0..3, mass)
            var wcRoot = transform.Find("wheelColliders");
            var wmRoot = transform.Find("wheelMeshes");
            var massTr = transform.Find("mass");

            if (wcRoot != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var child = wcRoot.Find(i.ToString());
                    if (child) { wheels[i] = child.GetComponent<WheelCollider>(); }
                }
            }
            if (wmRoot != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var child = wmRoot.Find(i.ToString());
                    if (child) { wheelMesh[i] = child.gameObject; }
                }
            }
            if (massTr != null) { centerOfMass = massTr; }

            if (centerOfMass != null && rigidbody != null)
            {
                rigidbody.centerOfMass = centerOfMass.localPosition;
            }

            // 2) 웨이포인트 (미지정 시 자동)
            if (waypoints == null)
            {
                waypoints = FindObjectOfType<TrackWaypoints>();
            }
            if (waypoints != null && (nodes == null || nodes.Count == 0))
            {
                nodes = waypoints.nodes;
            }

            CarEffects = GetComponent<CarVisualEffects>();
        }

        private void AddDownForce()
        {

            rigidbody.AddForce(-transform.up * DownForceValue * rigidbody.velocity.magnitude);

        }



        private IEnumerator timedLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(.7f);
                radius = 6 + KPH / 20;

            }
        }
        /*
        public void activateNitrus()
        {
            if (!IM.boosting && nitrusValue <= 10)
            {
                nitrusValue += Time.deltaTime / 2;
            }
            else
            {
                nitrusValue -= (nitrusValue <= 0) ? 0 : Time.deltaTime;
            }

            if (IM.boosting)
            {
                if (nitrusValue > 0)
                {
                    CarEffects.startNitrusEmitter();
                    rigidbody.AddForce(transform.forward * 5000);
                }
                else CarEffects.stopNitrusEmitter();
            }
            else CarEffects.stopNitrusEmitter();

        }
        */
    }
}
