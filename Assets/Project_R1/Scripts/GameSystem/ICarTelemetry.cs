
namespace R1
{
    /// <summary>
    /// Standardized interface exposing vehicle telemetry data.
    /// Provides speed, RPM, gear, reverse flag, and nitro values so that
    /// HUD, replay, and telemetry modules can consume a consistent data contract.
    /// </summary>
    public interface ICarTelemetry
    {
        /// <summary>Current vehicle speed in kilometers per hour.</summary>
        float SpeedKmh { get; }

        /// <summary>Current engine RPM value.</summary>
        float EngineRpm { get; }

        /// <summary>Current gear index (0 = neutral, positive = forward).</summary>
        int GearIndex { get; }

        /// <summary>True if the vehicle is in reverse gear.</summary>
        bool IsReverse { get; }

        /// <summary>Remaining nitro charge as a normalized value (0..1).</summary>
        float Nitro { get; }
    }
}
