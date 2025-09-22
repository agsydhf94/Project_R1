using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace R1
{
    /// <summary>
    /// Updates HUD widgets (texts, gauges, needle) from vehicle telemetry.
    /// Can consume data directly from <see cref="CarController"/> or any
    /// implementation of <see cref="ICarTelemetry"/>.
    /// </summary>
    public class HudUpdater
    {
        /// <summary>Image used as the RPM fill gauge.</summary>
        private readonly Image rpmGauge;

        /// <summary>Text displaying engine RPM.</summary>
        private readonly TMP_Text rpmText;

        /// <summary>Text displaying vehicle speed (km/h).</summary>
        private readonly TMP_Text speedText;

        /// <summary>Text displaying current gear (or "R" if reversing).</summary>
        private readonly TMP_Text gearText;

        /// <summary>Image used as the nitro fill gauge.</summary>
        private readonly Image nitroGauge;

        /// <summary>Transform of the RPM needle to rotate.</summary>
        private readonly Transform needle;

        /// <summary>Gauge fill value at 0 RPM.</summary>
        private readonly float fill0;

        /// <summary>Gauge fill value at max RPM.</summary>
        private readonly float fill1;

        /// <summary>Smoothing time (seconds) for RPM gauge fill.</summary>
        private readonly float fillSmooth;

        /// <summary>Needle angle (degrees) at 0 RPM.</summary>
        private readonly float angle0;

        /// <summary>Needle angle (degrees) at max RPM.</summary>
        private readonly float angle1;

        /// <summary>Smoothing time (seconds) for needle rotation.</summary>
        private readonly float angleSmooth;

        /// <summary>Internal velocity used by SmoothDamp for gauge fill.</summary>
        private float fillVel;

        /// <summary>Current smoothed gauge fill value.</summary>
        private float fillCurr;

        /// <summary>Internal angular velocity used by SmoothDampAngle for needle.</summary>
        private float needleVel;



        /// <summary>
        /// Creates a HUD updater bound to the provided UI references and motion ranges.
        /// </summary>
        /// <param name="rpmGauge">Image to display RPM fill.</param>
        /// <param name="rpmText">Text to display RPM numbers.</param>
        /// <param name="speedText">Text to display speed (km/h).</param>
        /// <param name="gearText">Text to display current gear or "R".</param>
        /// <param name="nitroGauge">Image to display nitro fill.</param>
        /// <param name="needle">Transform of the RPM needle.</param>
        /// <param name="fill0">Gauge fill at 0 RPM.</param>
        /// <param name="fill1">Gauge fill at max RPM.</param>
        /// <param name="fillSmooth">Smoothing time for gauge fill.</param>
        /// <param name="angle0">Needle angle at 0 RPM (degrees).</param>
        /// <param name="angle1">Needle angle at max RPM (degrees).</param>
        /// <param name="angleSmooth">Smoothing time for needle rotation.</param>
        public HudUpdater(Image rpmGauge, TMP_Text rpmText, TMP_Text speedText, TMP_Text gearText,
                            Image nitroGauge, Transform needle,
                            float fill0, float fill1, float fillSmooth,
                            float angle0, float angle1, float angleSmooth)
        {
            this.rpmGauge = rpmGauge; this.rpmText = rpmText; this.speedText = speedText; this.gearText = gearText;
            this.nitroGauge = nitroGauge; this.needle = needle;
            this.fill0 = fill0; this.fill1 = fill1; this.fillSmooth = fillSmooth;
            this.angle0 = angle0; this.angle1 = angle1; this.angleSmooth = angleSmooth;
            fillCurr = fill0;
        }


        // --- Legacy path: update directly from CarController ---
        /// <summary>
        /// Updates the HUD from a <see cref="CarController"/> instance.
        /// No-op if <paramref name="car"/> is null.
        /// </summary>
        /// <param name="car">Vehicle controller providing speed, RPM, gear, reverse state, nitro, and max RPM.</param>
        public void UpdateFrom(CarController car)
        {
            if (!car) return;
            ApplyToHud(car.currentSpeed, car.currentEngineRPM, car.gearNum, car.reverse, car.nitrusValue, Mathf.Max(1f, car.maxRPM));
        }


        // --- New path: update from ICarTelemetry (pass exact max RPM if known) ---
        /// <summary>
        /// Updates the HUD from a generic <see cref="ICarTelemetry"/> source.
        /// Uses <paramref name="maxRpmHint"/> to normalize RPM if the source does not expose a max RPM.
        /// </summary>
        /// <param name="t">Telemetry provider (speed, RPM, gear, reverse, nitro).</param>
        /// <param name="maxRpmHint">Estimated or known max RPM used to normalize gauge/needle (defaults to 8000).</param>
        public void UpdateFrom(ICarTelemetry t, float maxRpmHint = 8000f)
        {
            if (t == null) return;
            ApplyToHud(t.SpeedKmh, t.EngineRpm, t.GearIndex, t.IsReverse, t.Nitro, Mathf.Max(1f, maxRpmHint));
        }


        /// <summary>
        /// Applies numeric values to the HUD texts, gauges and needle with smoothing.
        /// </summary>
        /// <param name="speedKmh">Vehicle speed in km/h.</param>
        /// <param name="engineRpm">Current engine RPM.</param>
        /// <param name="gearIndex">Zero-based gear index (displayed as 1-based if not reversing).</param>
        /// <param name="isReverse">Whether the vehicle is in reverse.</param>
        /// <param name="nitro">Nitro amount (mapped to gauge fill).</param>
        /// <param name="maxRpm">Max RPM used to normalize RPM-based visuals.</param>
        private void ApplyToHud(float speedKmh, float engineRpm, int gearIndex, bool isReverse, float nitro, float maxRpm)
        {
            if (speedText) speedText.text = $"{speedKmh:0} km/h";
            if (rpmText) rpmText.text = $"{engineRpm:0} RPM";
            if (gearText) gearText.text = (!isReverse) ? (gearIndex + 1).ToString() : "R";
            if (nitroGauge) nitroGauge.fillAmount = nitro / 45f;


            float rpm01 = Mathf.Clamp01(engineRpm / Mathf.Max(1f, maxRpm));
            if (rpmGauge)
            {
                float target = Mathf.Lerp(fill0, fill1, rpm01);
                fillCurr = Mathf.SmoothDamp(fillCurr, target, ref fillVel, fillSmooth);
                rpmGauge.fillAmount = fillCurr;
            }
            if (needle)
            {
                float targetAngle = Mathf.Lerp(angle0, angle1, rpm01);
                float newZ = Mathf.SmoothDampAngle(needle.localEulerAngles.z, targetAngle, ref needleVel, angleSmooth);
                needle.localEulerAngles = new Vector3(0, 0, newZ);
            }
        }
    }
}
