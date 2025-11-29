using UnityEngine;

namespace R1
{
    public class RealisticCarPhysics : MonoBehaviour
    {
        public Rigidbody rb;

        [Header("Friction")]
        public float B = 10f;
        public float C = 1.9f;
        public float D = 1.0f;

        [Header("Steering")]
        public float maxSteeringAngle = 30f;
        public float steeringResponseTime = 0.3f;

        [Header("Mass")]
        public float vehicleMass = 1400f;
        public Vector3 baseCOM = new Vector3(0, -0.5f, 0.2f);
        public float COMShiftFactor = 0.01f;

        private float currentSteeringAngle = 0f;
        private float velocity;

        void Start()
        {
            rb.mass = vehicleMass;
            rb.centerOfMass = baseCOM;
        }

        void FixedUpdate()
        {
            velocity = rb.velocity.magnitude;

            // 1. Steering: exponential smoothing (first-order delay)
            float desiredSteering = Input.GetAxis("Horizontal") * maxSteeringAngle;
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, desiredSteering, Time.fixedDeltaTime / steeringResponseTime);

            // 2. Tire lateral force using simplified Pacejka (slip angle approx.)
            float slipAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;
            float lateralForce = D * Mathf.Sin(C * Mathf.Atan(B * slipAngle * Mathf.Deg2Rad));

            // 3. Apply steering torque
            float steeringTorque = lateralForce * 0.5f; // simplified lever arm
            rb.AddTorque(transform.up * steeringTorque, ForceMode.Acceleration);

            // 4. Brake force (logarithmic scale)
            if (Input.GetKey(KeyCode.DownArrow))
            {
                float brakeForce = 3000f * Mathf.Log(velocity + 1f);
                rb.AddForce(-rb.velocity.normalized * brakeForce, ForceMode.Force);
            }

            // 5. Center of mass shifting during acceleration
            Vector3 acc = rb.velocity - previousVelocity;
            rb.centerOfMass = baseCOM + new Vector3(0, 0, -acc.z * COMShiftFactor);

            previousVelocity = rb.velocity;
        }

        private Vector3 previousVelocity;
    }
}
