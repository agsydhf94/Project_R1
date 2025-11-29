using UnityEngine;

namespace R1
{
    public class RealisticGearbox : MonoBehaviour
    {
        [Header("Mode")]
        public bool isAutomatic = true;

        [Header("Gears")]
        public int currentGear = 1;
        public int maxGear = 6;
        public float[] gearRatios = { 0f, 3.2f, 2.1f, 1.4f, 1.0f, 0.8f, 0.6f }; // N, 1~6단
        public float finalDriveRatio = 3.5f;

        [Header("RPM")]
        public float idleRpm = 900f;
        public float maxRpm = 7000f;
        public float rpmResponseSpeed = 5f;

        [Header("Auto Shift Thresholds")]
        public float autoShiftUpRpm = 6500f;
        public float autoShiftDownRpm = 2500f;

        [Header("Shift Settings")]
        public float shiftDelay = 0.6f;
        public AnimationCurve rpmDropCurve;

        [Header("Runtime")]
        public Rigidbody rb;

        private float currentSpeed = 0f;
        private float currentRpm = 1000f;

        private bool isShifting = false;
        private float shiftTimer = 0f;
        private int targetGear;

        void Update()
        {
            currentSpeed = rb.velocity.magnitude * 3.6f;

            if (!isShifting)
            {
                if (isAutomatic)
                    AutoShiftLogic();
                else
                    ManualShiftInput();
            }

            float wheelRpm = currentSpeed * 60f / (2f * Mathf.PI * 0.34f);
            float targetRpm = wheelRpm * gearRatios[currentGear] * finalDriveRatio;
            targetRpm = Mathf.Clamp(targetRpm, idleRpm, maxRpm);

            if (isShifting)
            {
                shiftTimer += Time.deltaTime;
                float t = shiftTimer / shiftDelay;
                float drop = rpmDropCurve.Evaluate(t);
                currentRpm = Mathf.Lerp(currentRpm, targetRpm, Time.deltaTime * rpmResponseSpeed);
                currentRpm -= drop * 200f;

                if (t >= 1f)
                {
                    isShifting = false;
                    currentGear = targetGear;
                }
            }
            else
            {
                currentRpm = Mathf.Lerp(currentRpm, targetRpm, Time.deltaTime * rpmResponseSpeed);
            }
        }

        void ManualShiftInput()
        {
            if (Input.GetKeyDown(KeyCode.A) && currentGear < maxGear)
            {
                StartShift(currentGear + 1);
            }
            else if (Input.GetKeyDown(KeyCode.S) && currentGear > 1)
            {
                StartShift(currentGear - 1);
            }
        }

        void AutoShiftLogic()
        {
            if (currentRpm >= autoShiftUpRpm && currentGear < maxGear)
            {
                StartShift(currentGear + 1);
            }
            else if (currentRpm <= autoShiftDownRpm && currentGear > 1)
            {
                StartShift(currentGear - 1);
            }
        }

        void StartShift(int newGear)
        {
            isShifting = true;
            shiftTimer = 0f;
            targetGear = newGear;
        }

        public float GetCurrentRPM() => currentRpm;
        public int GetCurrentGear() => currentGear;
        public float GetCurrentSpeed() => currentSpeed;
    }
}
