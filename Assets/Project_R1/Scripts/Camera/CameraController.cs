using UnityEngine;

namespace R1
{
    /// <summary>
    /// Controls the third-person chase camera for the player's vehicle.
    /// Provides smooth following, yaw slide (turn reveal), and G-force tilt effects
    /// for a more dynamic and cinematic feel.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        private GameObject player;
        private CarController carController;
        private Rigidbody rb;
        private Camera mainCamera;

        /// <summary>
        /// Reference point attached to the player used as the base for camera positioning.
        /// Typically placed behind the vehicle to act as a follow target.
        /// </summary>
        private Transform cameraConstraint;

        /// <summary>
        /// Reference point attached to the player that the camera should look at.
        /// Usually placed slightly above the vehicle for a natural look direction.
        /// </summary>
        private Transform cameraLookTarget;

        [Header("Position Smoothing")]
        /// <summary>
        /// Position smoothing factor for camera movement.
        /// </summary>
        public float followSmooth = 0.1f;
        private Vector3 smoothedTarget;
        private Vector3 rigVelocity;

        [Header("Yaw Slide (Turn Reveal)")]
        /// <summary>
        /// Gain applied to yaw delta for slide offset.
        /// </summary>
        public float yawSlideGain = 0.06f;

        /// <summary>
        /// Maximum sideways slide offset when turning.
        /// </summary>
        public float yawSlideMax = 0.8f;

        /// <summary>
        /// Smoothing factor for yaw slide transitions.
        /// </summary>
        public float slideSmooth = 8f;
        private float sideOffset, lookOffset;
        private float prevYaw;
        private float smoothedDeltaYaw;

        [Header("G-Force Tilt")]
        /// <summary>
        /// Strength multiplier for G-force tilt effect.
        /// </summary>
        public float gForceTiltStrength = 4f;

        /// <summary>
        /// Maximum pitch angle from longitudinal G-force.
        /// </summary>
        public float maxPitch = 6f;

        /// <summary>
        /// Maximum roll angle from lateral G-force.
        /// </summary>
        public float maxRoll = 5f;

        /// <summary>
        /// Smoothing factor for tilt transitions.
        /// </summary>
        public float tiltSmooth = 5f;
        private float pitch, roll;
        private Vector3 prevVelocity;

        [Header("Fixed Distance Settings")]
        /// <summary>
        /// Base distance behind the vehicle to place the camera.
        /// </summary>
        public float baseDistance = 6f;

        /// <summary>
        /// Height offset above the camera constraint point.
        /// </summary>
        public float cameraHeight = 2.5f;
        private float currentDistance;


        /// <summary>
        /// Initializes references and sets up camera positioning.
        /// </summary>
        void Start()
        {
            mainCamera = Camera.main;
            player = GameObject.FindGameObjectWithTag("Player");

            if (!player)
            {
                Debug.LogError("[CameraController] Player tag not found.");
                enabled = false;
                return;
            }

            carController = player.GetComponent<CarController>();
            rb = player.GetComponent<Rigidbody>();
            cameraConstraint = player.transform.Find("CameraConstraint");
            cameraLookTarget = player.transform.Find("CameraLookAt");

            if (!cameraConstraint || !cameraLookTarget || !mainCamera)
            {
                Debug.LogError("[CameraController] CameraConstraint or CameraLookAt missing.");
                enabled = false;
                return;
            }

            prevYaw = player.transform.eulerAngles.y;
            smoothedTarget = cameraConstraint.position;
            prevVelocity = rb.velocity;
            currentDistance = baseDistance;

            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.transform.localRotation = Quaternion.identity;
        }


        /// <summary>
        /// Updates camera position and orientation each frame,
        /// applying yaw slide, G-force tilt, and distance constraints.
        /// </summary>
        void LateUpdate()
        {
            if (!rb || !mainCamera || !carController) return;

            ApplyYawSlide();
            ApplyGForceTilt();

            currentDistance = baseDistance;

            Vector3 flatForward = player.transform.forward;
            flatForward.y = 0f;
            flatForward.Normalize();

            Vector3 backOffset = -flatForward * currentDistance;
            Vector3 heightOffset = Vector3.up * cameraHeight;
            Vector3 targetPos = cameraConstraint.position + player.transform.right * sideOffset + backOffset + heightOffset;

            // Smooth follow
            smoothedTarget = Vector3.SmoothDamp(smoothedTarget, targetPos, ref rigVelocity, followSmooth);
            smoothedTarget.y = cameraConstraint.position.y + cameraHeight;

            // Clamp distance
            float distanceToTarget = Vector3.Distance(smoothedTarget, cameraConstraint.position);
            float maxAllowedDistance = baseDistance + 1.0f;

            if (distanceToTarget > maxAllowedDistance)
            {
                Vector3 dir = (smoothedTarget - cameraConstraint.position).normalized;
                smoothedTarget = cameraConstraint.position + dir * maxAllowedDistance;
            }

            transform.position = smoothedTarget;

            // Look direction
            Vector3 lookTarget = cameraLookTarget.position - player.transform.right * lookOffset;
            Vector3 forward = (lookTarget - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            Quaternion targetTilt = Quaternion.Euler(pitch, 0f, roll);
            mainCamera.transform.localRotation = Quaternion.Lerp(
                mainCamera.transform.localRotation,
                targetTilt,
                Time.deltaTime * tiltSmooth
            );
        }


        /// <summary>
        /// Applies yaw-based sliding effect to offset the camera during turns.
        /// </summary>
        void ApplyYawSlide()
        {
            float yaw = player.transform.eulerAngles.y;
            float rawDeltaYaw = Mathf.DeltaAngle(prevYaw, yaw);
            prevYaw = yaw;

            if (carController.KPH < 50)
                rawDeltaYaw = 0f;

            smoothedDeltaYaw = Mathf.Lerp(smoothedDeltaYaw, rawDeltaYaw, Time.deltaTime * 10f);

            float spdFactor = Mathf.Clamp01(carController.KPH / 50f);
            float targetSide = Mathf.Clamp(-smoothedDeltaYaw * yawSlideGain * spdFactor, -yawSlideMax, yawSlideMax);

            sideOffset = Mathf.Lerp(sideOffset, targetSide, Time.deltaTime * slideSmooth);
            lookOffset = Mathf.Lerp(lookOffset, targetSide * 1.2f, Time.deltaTime * slideSmooth);
        }


        /// <summary>
        /// Applies G-force tilt based on vehicle acceleration.
        /// </summary>
        void ApplyGForceTilt()
        {
            Vector3 vel = rb.velocity;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            Vector3 accel = (vel - prevVelocity) / dt;
            Vector3 localAccel = player.transform.InverseTransformDirection(accel);
            prevVelocity = vel;

            float longG = localAccel.z / 9.81f;
            float latG = localAccel.x / 9.81f;

            if (carController.KPH < 50 || accel.magnitude < 2f)
            {
                pitch = Mathf.Lerp(pitch, 0f, dt * tiltSmooth);
                roll = Mathf.Lerp(roll, 0f, dt * tiltSmooth);
                return;
            }

            float targetPitch = Mathf.Clamp(-longG * gForceTiltStrength, -maxPitch, maxPitch);
            float targetRoll = Mathf.Clamp(-latG * gForceTiltStrength, -maxRoll, maxRoll);

            pitch = Mathf.Lerp(pitch, targetPitch, dt * tiltSmooth);
            roll = Mathf.Lerp(roll, targetRoll, dt * tiltSmooth);
        }
    }
}
